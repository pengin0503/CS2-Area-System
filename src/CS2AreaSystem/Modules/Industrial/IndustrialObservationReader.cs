using System;
using System.Collections.Generic;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public readonly struct IndustrialReadRequest
    {
        public Entity Building { get; }
        public ulong AreaId { get; }
        public long CycleId { get; }
        public IndustrialReadRequest(Entity building, ulong areaId, long cycleId) { Building = building; AreaId = areaId; CycleId = cycleId; }
    }
    // Blittable per-building read produced inside the observation job; the only place raw ECS values are copied.
    public struct IndustrialBuildingRead
    {
        public Entity Building;
        public ulong AreaId;
        public long CycleId;
        public bool Exists, Available, Failed;
        public IndustrialFacilityTraits Traits;
        public bool HasEfficiency, LackResourcesZero;
        public float Efficiency;
        // Workplaces of the building itself (cargo/storage service buildings).
        public bool HasBuildingWorkplace;
        public int BuildingJobs, BuildingEmployees;
        public int Vacancy0, Vacancy1, Vacancy2, Vacancy3, Vacancy4;
        public bool HasStorage;
        public double Stored, Capacity;
    }
    // Blittable per-company read (only attributable industrial tenants; offices/commercial are never emitted).
    public struct IndustrialCompanyRead
    {
        public Entity Company;
        public Entity Building;
        public ulong AreaId;
        public long CycleId;
        public bool HasStorage;
        public double Stored, Capacity;
        public bool Producer, Extractor, Storage;
        public bool HasJobs;
        public int Jobs, Employees;
        public int Vacancy0, Vacancy1, Vacancy2, Vacancy3, Vacancy4;
        public bool HasProfit;
        public int ProfitIndex;
        public bool HasActivity;
        public ulong OutputResource;
        public double ActualPerDay, CapacityPerDay;
        public IndustrialFlowState Input, Output;
        // The tenant's vanilla recipe (IndustrialProcessData), copied for producers with a valid output (Wave A4).
        public bool HasProcess;
        public ulong ProcessInput1, ProcessInput2, ProcessOutput;
        public int ProcessInput1Amount, ProcessInput2Amount, ProcessOutputAmount;
    }
    // Symptom rules reconstructed from the 1.6.2f1 production systems (docs/verification/industrial-observation-contracts.md).
    public static class IndustrialFlowRules
    {
        // ProcessingCompanySystem: produced = min(potential, (int)(stock / (inputAmount / outputAmount))) per input.
        // Affected only when potential output is positive (workers/efficiency present) and an input cannot cover one unit.
        public static IndustrialFlowState Input(bool hasInput1, int input1Amount, int outputAmount, int stock1, bool hasInput2, int input2Amount, int stock2, int potentialPerDay)
        {
            bool uses1 = hasInput1 && input1Amount > 0, uses2 = hasInput2 && input2Amount > 0;
            if (!uses1 && !uses2) return IndustrialFlowState.NotApplicable;
            if (outputAmount <= 0) return IndustrialFlowState.Unavailable;
            // Idle production without potential (no workers, zero efficiency) does not prove input starvation.
            if (potentialPerDay <= 0) return IndustrialFlowState.Unavailable;
            bool starved = (uses1 && Units(stock1, input1Amount, outputAmount) <= 0) || (uses2 && Units(stock2, input2Amount, outputAmount) <= 0);
            return starved ? IndustrialFlowState.Affected : IndustrialFlowState.Healthy;
        }
        private static int Units(int stock, int inputAmount, int outputAmount) => (int)((float)stock / ((float)inputAmount * 1f / (float)outputAmount));
        // ExtractorCompanySystem: production is capped at free storage (limit − total stored).
        public static IndustrialFlowState ExtractorOutput(bool hasLimit, int limit, int totalStored)
        {
            if (!hasLimit || limit <= 0) return IndustrialFlowState.Unavailable;
            return limit - totalStored <= 0 ? IndustrialFlowState.Affected : IndustrialFlowState.Healthy;
        }
        // Processor: weighted storage exhausted (StorageSection "Full"; BuyingCompanySystem stops buying inputs).
        public static IndustrialFlowState ProcessorOutput(bool hasLimit, int limit, double weightedStored)
        {
            if (!hasLimit || limit <= 0) return IndustrialFlowState.Unavailable;
            return weightedStored >= limit ? IndustrialFlowState.Affected : IndustrialFlowState.Healthy;
        }
        // Worst observable state of several tenants: Affected > Healthy > Unavailable > NotApplicable.
        public static IndustrialFlowState Worst(IndustrialFlowState a, IndustrialFlowState b) => Rank(a) >= Rank(b) ? a : b;
        private static int Rank(IndustrialFlowState s) => s == IndustrialFlowState.Affected ? 3 : s == IndustrialFlowState.Healthy ? 2 : s == IndustrialFlowState.Unavailable ? 1 : 0;
    }
    // Converts raw reads of one building into the module-owned observation DTO. Pure.
    public static class IndustrialObservationMapper
    {
        public static bool Matches(in IndustrialCompanyRead company, in IndustrialBuildingRead building) =>
            company.Building == building.Building && company.AreaId == building.AreaId && company.CycleId == building.CycleId;
        // Buildings keep Core ownership. Company facts, jobs and warehouse stocks follow only the resolved Company owner.
        public static IndustrialEntityObservation ToOwnedObservation(in IndustrialBuildingRead building, IReadOnlyList<IndustrialCompanyRead> companies,
            Core.Membership.AreaMembershipIndex membership, IndustrialCompanyAssignmentIndex assignments,
            Func<IndustrialCompanyRead, IndustrialCompanyEligibility> eligibility, Core.Semantics.AreaAssetSemanticRegistry registry = null)
        {
            var read = building;
            bool primary = false;
            foreach (var r in membership.GetAreas(read.Building))
                if (r.TypeId == (int)Core.Areas.AreaTypeId.Industrial && r.AreaStableId == read.AreaId) { primary = r.IsPrimaryForType; break; }
            var selected = new List<IndustrialCompanyRead>();
            var seen = new HashSet<Entity>();
            bool companyStorage = false;
            foreach (var company in companies)
            {
                companyStorage |= company.HasStorage;
                if (company.Building != read.Building || !seen.Add(company.Company)) continue;
                var owner = IndustrialCompanyOwnership.Resolve(company.Company, company.Building, membership.GetAreas(company.Building), assignments, eligibility(company));
                if (owner.Valid && owner.AreaStableId == read.AreaId) selected.Add(company);
            }
            read.Traits.HasOfficeCompany = false;
            read.Traits.HasExtractorCompany = read.Traits.HasProcessorCompany = read.Traits.HasStorageCompany = false;
            if (!primary || (companies.Count > 0 && selected.Count == 0))
            {
                read.Traits.HasExtractorProperty = read.Traits.HasExtractorFacility = read.Traits.ManufacturesGoods = false;
            }
            if (!primary)
            {
                read.HasBuildingWorkplace = false;
                read.HasEfficiency = false; read.Efficiency = 0; read.LackResourcesZero = false;
                read.Traits.HasCargoStation = read.Traits.HasStorageProperty = read.Traits.HasWorkplaces = false;
                read.HasStorage = false;
            }
            if (companyStorage) { read.HasStorage = false; read.Stored = read.Capacity = 0; }
            foreach (var company in selected)
            {
                read.Traits.HasExtractorCompany |= company.Extractor;
                read.Traits.HasProcessorCompany |= company.Producer && !company.Extractor;
                read.Traits.HasStorageCompany |= company.Storage;
                read.Traits.HasWorkplaces |= company.HasJobs;
                if (company.HasStorage) { read.HasStorage = true; read.Stored += company.Stored; read.Capacity += company.Capacity; }
            }
            // No non-primary facility facts are credited unless one of its companies resolves here.
            if (!primary && selected.Count == 0) return new IndustrialEntityObservation { Facility = read.Building, CandidateOnly = true, OwnedCompanies = Array.Empty<Entity>() };
            var observation = ToObservation(read, selected, registry);
            var owned = new Entity[selected.Count];
            for (int i = 0; i < owned.Length; i++) owned[i] = selected[i].Company;
            observation.OwnedCompanies = owned;
            return observation;
        }
        public static IndustrialEntityObservation ToObservation(in IndustrialBuildingRead building, IReadOnlyList<IndustrialCompanyRead> companies,Core.Semantics.AreaAssetSemanticRegistry registry=null)
        {
            var obs = new IndustrialEntityObservation { Facility = building.Building, Input = IndustrialFlowState.NotApplicable, Output = IndustrialFlowState.NotApplicable };
            if (!building.Exists || building.Failed) { obs.Input = obs.Output = IndustrialFlowState.Unavailable; return obs; }
            obs.Roles = IndustrialFacilityClassifier.Classify(building.Traits,registry);
            obs.Available = building.Available;
            obs.IsProducer = (obs.Roles & IndustrialFacilityClassifier.ProducerRoles) != 0;
            if (obs.Roles == IndustrialFacilityRole.None) return obs;
            int? jobs = null, employees = null;
            var vacancies = new int[5];
            if (building.HasBuildingWorkplace)
            {
                jobs = building.BuildingJobs; employees = building.BuildingEmployees;
                vacancies[0] += building.Vacancy0; vacancies[1] += building.Vacancy1; vacancies[2] += building.Vacancy2; vacancies[3] += building.Vacancy3; vacancies[4] += building.Vacancy4;
            }
            double actual = 0, capacity = 0, profitSum = 0, profitWeight = 0;
            bool anyActivity = false, operating = false;
            var resources = new List<IndustrialResourceActivity>();
            List<IndustrialProcessActivity> processes = null;
            foreach (var c in companies)
            {
                if (c.HasJobs)
                {
                    jobs = (jobs ?? 0) + c.Jobs; employees = (employees ?? 0) + c.Employees;
                    vacancies[0] += c.Vacancy0; vacancies[1] += c.Vacancy1; vacancies[2] += c.Vacancy2; vacancies[3] += c.Vacancy3; vacancies[4] += c.Vacancy4;
                }
                if (c.HasProfit)
                {
                    double w = IndustrialWeighting.Weight(c.HasJobs ? c.Jobs : (int?)null);
                    profitSum += IndustrialSourceNormalization.ProfitViability(c.ProfitIndex) * w; profitWeight += w;
                }
                if (!c.Producer) continue;
                obs.ProducerTenants++;
                obs.Input = IndustrialFlowRules.Worst(obs.Input, c.Input);
                obs.Output = IndustrialFlowRules.Worst(obs.Output, c.Output);
                if (c.HasProcess) (processes ??= new List<IndustrialProcessActivity>()).Add(new IndustrialProcessActivity(IndustrialProcessDiagnostics.KeyOf(c), c.HasActivity, c.ActualPerDay));
                if (!c.HasActivity) continue;
                anyActivity = true; actual += c.ActualPerDay; capacity += c.CapacityPerDay;
                operating |= c.ActualPerDay > 0;
                resources.Add(new IndustrialResourceActivity { Resource = c.OutputResource, ActualPerDay = c.ActualPerDay, CapacityPerDay = c.CapacityPerDay });
            }
            obs.Jobs = jobs; obs.Employees = employees;
            obs.VacanciesByLevel = vacancies;
            obs.Resources = resources.ToArray();
            obs.Processes = processes != null ? processes.ToArray() : Array.Empty<IndustrialProcessActivity>();
            obs.ProductiveActivity = anyActivity ? IndustrialSourceNormalization.ActivityPercent(actual, capacity) : null;
            obs.IsOperatingProducer = obs.IsProducer && obs.Available && operating && obs.ProductiveActivity.HasValue;
            obs.Efficiency = building.HasEfficiency ? IndustrialSourceNormalization.EfficiencyPercent(building.Efficiency) : null;
            obs.Profitability = profitWeight > 0 ? profitSum / profitWeight : (double?)null;
            obs.LackResourcesFactor = building.LackResourcesZero;
            if ((obs.Roles & IndustrialFacilityRole.Cargo) != 0) obs.CargoOperational = building.Available && (!building.HasEfficiency || building.Efficiency > 0);
            if (building.HasStorage) { obs.StoredAmount = building.Stored; obs.StorageCapacity = building.Capacity; }
            // A vacant producer building has no process, so its channels stay NotApplicable (vacancy shows in active share).
            return obs;
        }
    }
    // Job-side reader over read-only lookups: start from Core's building member, resolve only that building's own
    // components, prefab and Renter companies (never broadens Core membership). See the observation-contract ledger.
    public struct IndustrialObservationReader
    {
        [ReadOnly] public ComponentLookup<Building> Buildings;
        [ReadOnly] public ComponentLookup<PrefabRef> Prefabs;
        [ReadOnly] public ComponentLookup<Abandoned> Abandoned;
        [ReadOnly] public ComponentLookup<Destroyed> Destroyed;
        [ReadOnly] public ComponentLookup<ExtractorProperty> ExtractorProperties;
        [ReadOnly] public ComponentLookup<IndustrialProperty> IndustrialProperties;
        [ReadOnly] public ComponentLookup<OfficeProperty> OfficeProperties;
        [ReadOnly] public ComponentLookup<CommercialProperty> CommercialProperties;
        [ReadOnly] public ComponentLookup<StorageProperty> StorageProperties;
        [ReadOnly] public ComponentLookup<Game.Buildings.CargoTransportStation> CargoStations;
        [ReadOnly] public ComponentLookup<Game.Buildings.ExtractorFacility> ExtractorFacilities;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> PropertyDatas;
        [ReadOnly] public ComponentLookup<CargoTransportStationData> CargoStationDatas;
        [ReadOnly] public ComponentLookup<ExtractorFacilityData> ExtractorFacilityDatas;
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> SpawnableDatas;
        [ReadOnly] public ComponentLookup<BuildingData> BuildingDatas;
        [ReadOnly] public ComponentLookup<WorkProvider> WorkProviders;
        [ReadOnly] public ComponentLookup<CompanyData> Companies;
        [ReadOnly] public ComponentLookup<OfficeCompany> OfficeCompanies;
        [ReadOnly] public ComponentLookup<CommercialCompany> CommercialCompanies;
        [ReadOnly] public ComponentLookup<ServiceAvailable> ServiceAvailables;
        [ReadOnly] public ComponentLookup<Game.Companies.ExtractorCompany> ExtractorCompanies;
        [ReadOnly] public ComponentLookup<Game.Companies.ProcessingCompany> ProcessingCompanies;
        [ReadOnly] public ComponentLookup<IndustrialCompany> IndustrialCompanies;
        [ReadOnly] public ComponentLookup<Game.Companies.StorageCompany> StorageCompanies;
        [ReadOnly] public ComponentLookup<Profitability> Profitabilities;
        [ReadOnly] public ComponentLookup<CompanyStatisticData> Statistics;
        [ReadOnly] public ComponentLookup<IndustrialProcessData> Processes;
        [ReadOnly] public ComponentLookup<WorkplaceData> WorkplaceDatas;
        [ReadOnly] public ComponentLookup<StorageLimitData> StorageLimits;
        [ReadOnly] public ComponentLookup<ResourceData> ResourceDatas;
        [ReadOnly] public ComponentLookup<Attached> Attached;
        [ReadOnly] public ComponentLookup<Citizen> Citizens;
        [ReadOnly] public BufferLookup<Renter> Renters;
        [ReadOnly] public BufferLookup<Employee> Employees;
        [ReadOnly] public BufferLookup<Efficiency> Efficiencies;
        [ReadOnly] public BufferLookup<Resources> ResourceBuffers;
        [ReadOnly] public BufferLookup<InstalledUpgrade> InstalledUpgrades;
        [ReadOnly] public ResourcePrefabs ResourcePrefabs;
        public EconomyParameterData Economy;
        public void Read(in IndustrialReadRequest request, ref NativeList<IndustrialBuildingRead> buildings, ref NativeList<IndustrialCompanyRead> companies)
        {
            var read = new IndustrialBuildingRead { Building = request.Building, AreaId = request.AreaId, CycleId = request.CycleId };
            int companyStart = companies.Length;
            try
            {
                ReadBuilding(request.Building, ref read, ref companies);
            }
            catch (Exception)
            {
                // One failing entity is skipped (reported as Failed, logged with bounded repetition on the main thread).
                companies.Length = companyStart;
                read = new IndustrialBuildingRead { Building = request.Building, AreaId = request.AreaId, CycleId = request.CycleId, Exists = true, Failed = true };
            }
            for (int i = companyStart; i < companies.Length; i++)
            { var company = companies[i]; company.AreaId = request.AreaId; company.CycleId = request.CycleId; companies[i] = company; }
            buildings.Add(read);
        }
        private void ReadBuilding(Entity entity, ref IndustrialBuildingRead read, ref NativeList<IndustrialCompanyRead> companies)
        {
            if (!Buildings.HasComponent(entity) || !Prefabs.HasComponent(entity)) return;
            read.Exists = true;
            var building = Buildings[entity];
            read.Available = !Abandoned.HasComponent(entity) && !Destroyed.HasComponent(entity) && !BuildingUtils.CheckOption(building, BuildingOption.Inactive);
            var prefab = Prefabs[entity].m_Prefab;
            ref var t = ref read.Traits;
            t.IsBuilding = true;t.Prefab=prefab;t.HasCommercialProperty=CommercialProperties.HasComponent(entity);
            t.HasExtractorProperty = ExtractorProperties.HasComponent(entity);
            t.HasStorageProperty = StorageProperties.HasComponent(entity);
            t.HasOfficeProperty = OfficeProperties.HasComponent(entity);
            t.HasCargoStation = CargoStations.HasComponent(entity) || CargoStationDatas.HasComponent(prefab);
            t.HasExtractorFacility = ExtractorFacilities.HasComponent(entity) || ExtractorFacilityDatas.HasComponent(prefab);
            bool allowedStored = false;
            if (PropertyDatas.HasComponent(prefab))
            {
                var property = PropertyDatas[prefab];
                bool industrial = IndustrialProperties.HasComponent(entity);
                t.ManufacturesGoods = industrial && IndustrialFacilityReader.ManufacturesGoods(property.m_AllowedManufactured);
                allowedStored = industrial && property.m_AllowedStored != Resource.NoResource;
                t.HasStorageProperty |= allowedStored;
            }
            int level = SpawnableDatas.HasComponent(prefab) ? Math.Max(1, (int)SpawnableDatas[prefab].m_Level) : 1;
            float efficiency = 1f, efficiencyWithoutLack = 1f;
            if (Efficiencies.HasBuffer(entity))
            {
                var buffer = Efficiencies[entity];
                read.HasEfficiency = true;
                // Same product as GetEfficiency(DynamicBuffer); this overload also binds under the net8 API-check profile.
                efficiency = BuildingUtils.GetEfficiency(entity, ref Efficiencies);
                efficiencyWithoutLack = BuildingUtils.GetEfficiencyExcludingFactor(buffer, EfficiencyFactor.LackResources);
                read.LackResourcesZero = BuildingUtils.GetEfficiencyFactor(buffer, EfficiencyFactor.LackResources) <= 0f;
                read.Efficiency = efficiency;
            }
            // The building's own workplaces (city-service cargo/storage stations).
            if (WorkProviders.HasComponent(entity))
            {
                t.HasWorkplaces = true;
                ReadJobs(entity, prefab, level, out read.BuildingJobs, out read.BuildingEmployees, out read.Vacancy0, out read.Vacancy1, out read.Vacancy2, out read.Vacancy3, out read.Vacancy4, out read.HasBuildingWorkplace);
            }
            bool storageFromCompany = false;
            if (Renters.HasBuffer(entity))
            {
                var renters = Renters[entity];
                for (int i = 0; i < renters.Length; i++)
                {
                    var company = renters[i].m_Renter;
                    if (!Companies.HasComponent(company) || !Prefabs.HasComponent(company)) continue;
                    bool office = OfficeCompanies.HasComponent(company);
                    t.HasOfficeCompany |= office;
                    // Offices and commercial tenants are never attributable industrial activity.
                    if (office || CommercialCompanies.HasComponent(company) || ServiceAvailables.HasComponent(company)) continue;
                    var companyPrefab = Prefabs[company].m_Prefab;
                    var c = new IndustrialCompanyRead { Company = company, Building = entity };
                    c.Extractor = ExtractorCompanies.HasComponent(company);
                    bool processor = !c.Extractor && ProcessingCompanies.HasComponent(company) && IndustrialCompanies.HasComponent(company);
                    c.Storage = StorageCompanies.HasComponent(company);
                    c.Producer = c.Extractor || processor;
                    if (!c.Producer && !c.Storage) continue;
                    t.HasExtractorCompany |= c.Extractor; t.HasProcessorCompany |= processor; t.HasStorageCompany |= c.Storage;
                    if (WorkProviders.HasComponent(company))
                    {
                        t.HasWorkplaces = true;
                        ReadJobs(company, companyPrefab, level, out c.Jobs, out c.Employees, out c.Vacancy0, out c.Vacancy1, out c.Vacancy2, out c.Vacancy3, out c.Vacancy4, out c.HasJobs);
                    }
                    if (Profitabilities.HasComponent(company)) { c.HasProfit = true; c.ProfitIndex = IndustrialSourceNormalization.ProfitIndex(Profitabilities[company].m_Profitability); }
                    if (c.Producer) ReadProduction(entity, company, companyPrefab, level, efficiency, efficiencyWithoutLack, ref c);
                    if (c.Storage && ReadWarehouse(company, companyPrefab, prefab, allowedStored, out var stored, out var capacity))
                    {
                        c.HasStorage = true; c.Stored = stored; c.Capacity = capacity;
                        if (!storageFromCompany) { read.HasStorage = true; read.Stored = stored; read.Capacity = capacity; }
                        storageFromCompany = true;
                    }
                    companies.Add(c);
                }
            }
            // Cargo/service storage on the building itself (StorageSection: prefab StorageLimitData with upgrades).
            if (!storageFromCompany && (t.HasCargoStation || t.HasStorageProperty) && UpgradeUtils.TryGetCombinedComponent(entity, out StorageLimitData limit, ref Prefabs, ref StorageLimits, ref InstalledUpgrades) && limit.m_Limit > 0 && ResourceBuffers.HasBuffer(entity))
            { read.HasStorage = true; read.Capacity = limit.m_Limit; read.Stored = Math.Min(limit.m_Limit, Math.Max(0, WeightedStored(ResourceBuffers[entity]))); }
        }
        private void ReadJobs(Entity worker, Entity prefab, int level, out int jobs, out int employees, out int v0, out int v1, out int v2, out int v3, out int v4, out bool known)
        {
            jobs = employees = v0 = v1 = v2 = v3 = v4 = 0; known = false;
            int max = WorkProviders[worker].m_MaxWorkers;
            if (max < 0 || !WorkplaceDatas.HasComponent(prefab)) return;
            var places = EmploymentData.GetWorkplacesData(max, level, WorkplaceDatas[prefab].m_Complexity);
            jobs = places.total; known = true;
            int e0 = 0, e1 = 0, e2 = 0, e3 = 0, e4 = 0;
            if (Employees.HasBuffer(worker))
            {
                var list = Employees[worker];
                employees = list.Length;
                for (int i = 0; i < list.Length; i++)
                    switch (list[i].m_Level) { case 0: e0++; break; case 1: e1++; break; case 2: e2++; break; case 3: e3++; break; default: e4++; break; }
            }
            v0 = Math.Max(0, places.uneducated - e0); v1 = Math.Max(0, places.poorlyEducated - e1); v2 = Math.Max(0, places.educated - e2);
            v3 = Math.Max(0, places.wellEducated - e3); v4 = Math.Max(0, places.highlyEducated - e4);
        }
        private void ReadProduction(Entity property, Entity company, Entity companyPrefab, int level, float efficiency, float efficiencyWithoutLack, ref IndustrialCompanyRead c)
        {
            c.Input = c.Output = IndustrialFlowState.Unavailable;
            if (!Processes.HasComponent(companyPrefab)) return;
            var process = Processes[companyPrefab];
            var output = process.m_Output.m_Resource;
            if (output == Resource.NoResource || !ResourceDatas.HasComponent(ResourcePrefabs[output])) return;
            // The recipe is prefab data: known even when this cycle cannot measure the company's production rate.
            IndustrialProcessDiagnostics.TryCopy(process, ref c);
            if (!WorkProviders.HasComponent(company) || !WorkplaceDatas.HasComponent(companyPrefab) || !ResourceBuffers.HasBuffer(company)) return;
            c.OutputResource = (ulong)output;
            var resources = ResourceBuffers[company];
            var workplace = WorkplaceDatas[companyPrefab];
            int capacity = EconomyUtils.GetCompanyProductionPerDay(1f, WorkProviders[company].m_MaxWorkers, level, true, workplace, process, ResourcePrefabs, ref ResourceDatas, ref Economy);
            bool hasEmployees = Employees.HasBuffer(company);
            int actual = 0, potential = 0;
            if (c.Extractor)
            {
                if (!Statistics.HasComponent(company)) return;
                actual = Statistics[company].m_LastUpdateProduce;
            }
            else if (hasEmployees)
            {
                var employees = Employees[company];
                actual = EconomyUtils.GetCompanyProductionPerDay(efficiency, true, employees, process, ResourcePrefabs, ref ResourceDatas, ref Citizens, ref Economy);
                potential = EconomyUtils.GetCompanyProductionPerDay(efficiencyWithoutLack, true, employees, process, ResourcePrefabs, ref ResourceDatas, ref Citizens, ref Economy);
            }
            else return;
            c.HasActivity = true; c.ActualPerDay = actual; c.CapacityPerDay = capacity;
            bool hasLimit = StorageLimits.HasComponent(companyPrefab);
            int limit = hasLimit ? StorageLimits[companyPrefab].m_Limit : 0;
            if (c.Extractor)
            {
                c.Input = IndustrialFlowState.NotApplicable;
                if (hasLimit && Attached.HasComponent(property) && InstalledUpgrades.HasBuffer(Attached[property].m_Parent) &&
                    UpgradeUtils.TryGetCombinedComponent(Attached[property].m_Parent, out StorageLimitData extra, ref Prefabs, ref StorageLimits, ref InstalledUpgrades))
                    limit += extra.m_Limit;
                c.Output = IndustrialFlowRules.ExtractorOutput(hasLimit, limit, EconomyUtils.GetTotalStorageUsed(resources));
            }
            else
            {
                var in1 = process.m_Input1; var in2 = process.m_Input2;
                c.Input = IndustrialFlowRules.Input(in1.m_Resource != Resource.NoResource, in1.m_Amount, process.m_Output.m_Amount,
                    in1.m_Resource != Resource.NoResource ? EconomyUtils.GetResources(in1.m_Resource, resources) : 0,
                    in2.m_Resource != Resource.NoResource, in2.m_Amount, in2.m_Resource != Resource.NoResource ? EconomyUtils.GetResources(in2.m_Resource, resources) : 0, potential);
                c.Output = IndustrialFlowRules.ProcessorOutput(hasLimit, limit, WeightedStored(resources));
            }
        }
        private bool ReadWarehouse(Entity company, Entity companyPrefab, Entity buildingPrefab, bool allowedStored, out double stored, out double capacity)
        {
            stored = capacity = 0;
            if (!StorageLimits.HasComponent(companyPrefab) || !ResourceBuffers.HasBuffer(company)) return false;
            var limit = StorageLimits[companyPrefab];
            capacity = allowedStored && SpawnableDatas.HasComponent(buildingPrefab) && BuildingDatas.HasComponent(buildingPrefab)
                ? limit.GetAdjustedLimitForWarehouse(SpawnableDatas[buildingPrefab], BuildingDatas[buildingPrefab]) : limit.m_Limit;
            if (capacity <= 0) return false;
            stored = Math.Min(capacity, Math.Max(0, WeightedStored(ResourceBuffers[company])));
            return true;
        }
        private double WeightedStored(DynamicBuffer<Resources> resources)
        {
            double sum = 0;
            for (int i = 0; i < resources.Length; i++)
            {
                var r = resources[i].m_Resource;
                if (r == Resource.NoResource || r == Resource.Money) continue;
                if (EconomyUtils.GetWeight(r, ResourcePrefabs, ref ResourceDatas) > 0f) sum += resources[i].m_Amount;
            }
            return sum;
        }
    }
}
