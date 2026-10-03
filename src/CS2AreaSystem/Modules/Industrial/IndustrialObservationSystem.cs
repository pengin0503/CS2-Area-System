using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Token and member source for observation cycles (membership index in game and tests).
    public interface IIndustrialCycleSource
    {
        ulong MembershipToken(ulong areaId);
        ulong StructureToken(ulong areaId);
        // Primary and manually owned members, plus optional candidate-only overlap reads in the shared budget.
        IReadOnlyList<Entity> PrimaryMembers(ulong areaId, out int nonPrimary);
    }
    // Primary-member lists are cached per Area and reused while the membership index reports the same (Epoch, revision)
    // for it (#72): the revision advances whenever the Area's members or their primary flags change. A cached list is
    // never mutated (a running cycle may hold it); a change builds a new one.
    public sealed class IndustrialMembershipCycleSource : IIndustrialCycleSource
    {
        private readonly AreaMembershipIndex index;
        private readonly IndustrialStructureTracker structure;
        private readonly IndustrialCompanyAssignmentIndex assignments;
        private readonly bool includeOwnershipCandidates;
        private readonly Dictionary<ulong, PrimaryList> lists = new Dictionary<ulong, PrimaryList>();
        private readonly List<Entity> buffer = new List<Entity>();
        private readonly HashSet<Entity> included = new HashSet<Entity>();
        private readonly HashSet<ulong> ids = new HashSet<ulong>();
        private readonly List<ulong> gone = new List<ulong>();
        private int epoch;
        public long Rebuilds { get; private set; }
        public int CachedAreas => lists.Count;
        public IndustrialMembershipCycleSource(AreaMembershipIndex index, IndustrialStructureTracker structure, IndustrialCompanyAssignmentIndex assignments = null, bool includeOwnershipCandidates = false)
        { this.index = index; this.structure = structure; this.assignments = assignments; this.includeOwnershipCandidates = includeOwnershipCandidates; epoch = index.Epoch; }
        public ulong MembershipToken(ulong areaId) => index.GetAreaRevision(areaId);
        public ulong StructureToken(ulong areaId) => assignments == null ? structure.Get(areaId) : IndustrialGenerations.Combine(structure.Get(areaId), assignments.GetAreaRevision(areaId));
        public IReadOnlyList<Entity> PrimaryMembers(ulong areaId, out int nonPrimary)
        {
            if (epoch != index.Epoch) { lists.Clear(); epoch = index.Epoch; }
            ulong revision = assignments == null ? index.GetAreaRevision(areaId) : IndustrialGenerations.Combine(index.GetAreaRevision(areaId), assignments.GetAreaRevision(areaId));
            if (lists.TryGetValue(areaId, out var cached) && cached.Revision == revision) { nonPrimary = cached.NonPrimary; return cached.Members; }
            buffer.Clear(); nonPrimary = 0;
            foreach (var member in index.GetMembers(areaId))
            {
                bool owned = false;
                foreach (var relation in index.GetAreas(member))
                    if (relation.AreaStableId == areaId) { owned = relation.IsPrimaryForType; break; }
                if (owned) buffer.Add(member); else nonPrimary++;
            }
            if (assignments != null)
            {
                included.Clear(); foreach (var member in buffer) included.Add(member);
                foreach (var member in assignments.GetBuildings(areaId))
                    if (IndustrialCompanyOwnership.Related(areaId, index.GetAreas(member)) && included.Add(member)) { buffer.Add(member); nonPrimary--; }
            }
            if (includeOwnershipCandidates)
            {
                included.Clear(); foreach (var member in buffer) included.Add(member);
                // Existing indexed members only: candidate reads earn no metrics unless company ownership resolves here.
                foreach (var member in index.GetMembers(areaId)) if (included.Add(member)) buffer.Add(member);
            }
            buffer.Sort((a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Version.CompareTo(b.Version));
            var members = System.Array.AsReadOnly(buffer.ToArray());
            lists[areaId] = new PrimaryList(revision, members, nonPrimary);
            Rebuilds++;
            return members;
        }
        // Drops the lists of Areas that are gone (called with the runtime's Area list when it changes).
        public void RetainOnly(IEnumerable<IndustrialAreaRef> alive)
        {
            if (lists.Count == 0) return;
            IndustrialAreaIds.Fill(alive, ids);
            gone.Clear();
            foreach (var id in lists.Keys) if (!ids.Contains(id)) gone.Add(id);
            foreach (var id in gone) lists.Remove(id);
        }
        public void Clear() => lists.Clear();
        private readonly struct PrimaryList
        {
            public readonly ulong Revision;
            public readonly IReadOnlyList<Entity> Members;
            public readonly int NonPrimary;
            public PrimaryList(ulong revision, IReadOnlyList<Entity> members, int nonPrimary) { Revision = revision; Members = members; NonPrimary = nonPrimary; }
        }
    }
    // Industrial-local structure generation: advanced from native change evidence only (a member building's
    // Updated tag, vanilla RentersUpdated tenant events), never from ordinary economic value changes.
    public sealed class IndustrialStructureTracker
    {
        private readonly Dictionary<ulong, ulong> tokens = new Dictionary<ulong, ulong>();
        public ulong Get(ulong areaId) => tokens.TryGetValue(areaId, out var token) ? token : 0UL;
        public void Bump(ulong areaId) => tokens[areaId] = AreaMembershipIndex.NextRevision(Get(areaId));
        public void MemberChanged(Entity member, AreaMembershipIndex index)
        {
            foreach (var relation in index.GetAreas(member))
                if (relation.TypeId == (int)AreaTypeId.Industrial) Bump(relation.AreaStableId);
        }
        public void Remove(ulong areaId) => tokens.Remove(areaId);
        public int Count => tokens.Count;
        public void RetainOnly(IEnumerable<IndustrialAreaRef> alive)
        {
            IndustrialAreaIds.Fill(alive, ids);
            gone.Clear();
            foreach (var id in tokens.Keys) if (!ids.Contains(id)) gone.Add(id);
            foreach (var id in gone) tokens.Remove(id);
        }
        private readonly HashSet<ulong> ids = new HashSet<ulong>();
        private readonly List<ulong> gone = new List<ulong>();
        public void Clear() => tokens.Clear();
    }
    public sealed class IndustrialCycle
    {
        public ulong AreaId;
        public long CycleId, HourBucket;
        public uint StartFrame;
        public double StartHours;
        public ulong MembershipToken, StructureToken;
        public IReadOnlyList<Entity> Members = new Entity[0];
        public int NonPrimaryMembers, NextMember, Outstanding;
        public IndustrialCycleAccumulator Accumulator = new IndustrialCycleAccumulator();
        public bool Done => NextMember >= Members.Count && Outstanding == 0;
    }
    // Per-Area hourly cycles with a shared, fair read budget (design §18.2). Pure: no ECS access.
    public sealed class IndustrialObservationScheduler
    {
        public const int DefaultBudget = 128;
        public int Budget { get; }
        private readonly Dictionary<ulong, IndustrialCycle> cycles = new Dictionary<ulong, IndustrialCycle>();
        private readonly Dictionary<ulong, long> lastBucket = new Dictionary<ulong, long>();
        private readonly Dictionary<ulong, (ulong Membership, ulong Structure)> lastTokens = new Dictionary<ulong, (ulong, ulong)>();
        private readonly List<ulong> order = new List<ulong>();
        private long nextCycleId = 1;
        private int cursor;
        public int RestartCount { get; private set; }
        public IndustrialObservationScheduler(int budget = DefaultBudget) { Budget = budget > 0 ? budget : DefaultBudget; }
        public IndustrialCycle Cycle(ulong areaId) => cycles.TryGetValue(areaId, out var cycle) ? cycle : null;
        public int ActiveCycles => cycles.Count;
        public void Clear() { cycles.Clear(); lastBucket.Clear(); lastTokens.Clear(); order.Clear(); cursor = 0; }
        public void Sync(IReadOnlyList<IndustrialAreaRef> areas, long bucket, uint frame, double hours, IIndustrialCycleSource source)
        {
            var alive = new HashSet<ulong>();
            foreach (var area in areas)
            {
                alive.Add(area.StableId);
                if (area.State != AreaLifecycleState.Active)
                {
                    // Suspended/unavailable Areas keep authoritative state but are not observed.
                    cycles.Remove(area.StableId); lastBucket.Remove(area.StableId); lastTokens.Remove(area.StableId);
                    continue;
                }
                if (cycles.TryGetValue(area.StableId, out var cycle))
                {
                    if (cycle.MembershipToken != source.MembershipToken(area.StableId) || cycle.StructureToken != source.StructureToken(area.StableId))
                    { Start(area.StableId, bucket, frame, hours, source); RestartCount++; }
                }
                else if (!lastBucket.TryGetValue(area.StableId, out var last) || last != bucket || !lastTokens.TryGetValue(area.StableId, out var tokens) ||
                    tokens.Membership != source.MembershipToken(area.StableId) || tokens.Structure != source.StructureToken(area.StableId)) Start(area.StableId, bucket, frame, hours, source);
            }
            var gone = new List<ulong>();
            foreach (var id in cycles.Keys) if (!alive.Contains(id)) gone.Add(id);
            foreach (var id in lastBucket.Keys) if (!alive.Contains(id) && !gone.Contains(id)) gone.Add(id);
            foreach (var id in gone) { cycles.Remove(id); lastBucket.Remove(id); lastTokens.Remove(id); }
            order.Clear(); order.AddRange(cycles.Keys); order.Sort();
        }
        private void Start(ulong areaId, long bucket, uint frame, double hours, IIndustrialCycleSource source)
        {
            var members = source.PrimaryMembers(areaId, out int nonPrimary);
            cycles[areaId] = new IndustrialCycle { AreaId = areaId, CycleId = nextCycleId++, HourBucket = bucket, StartFrame = frame, StartHours = hours,
                MembershipToken = source.MembershipToken(areaId), StructureToken = source.StructureToken(areaId), Members = members, NonPrimaryMembers = nonPrimary };
            lastBucket[areaId] = bucket;
            lastTokens[areaId] = (source.MembershipToken(areaId), source.StructureToken(areaId));
        }
        // Round-robin, one member at a time across Areas, up to the total budget.
        public void NextReads(List<IndustrialReadRequest> into)
        {
            into.Clear();
            if (order.Count == 0) return;
            int idle = 0;
            while (into.Count < Budget && idle < order.Count)
            {
                if (cursor >= order.Count) cursor = 0;
                var cycle = cycles[order[cursor++]];
                if (cycle.NextMember >= cycle.Members.Count) { idle++; continue; }
                idle = 0;
                into.Add(new IndustrialReadRequest(cycle.Members[cycle.NextMember++], cycle.AreaId, cycle.CycleId));
                cycle.Outstanding++;
            }
        }
        // Reads that were handed out but will never be completed (dropped job results): rewind those cycles so they
        // are read again instead of waiting forever. Their partial totals are discarded.
        public void Abandon()
        {
            foreach (var cycle in cycles.Values)
                if (cycle.Outstanding > 0) { cycle.Outstanding = 0; cycle.NextMember = 0; cycle.Accumulator = new IndustrialCycleAccumulator(); }
        }
        // Results of restarted/dropped cycles are ignored, so generations never mix.
        public bool Complete(in IndustrialReadRequest request, in IndustrialEntityObservation observation, bool failed = false)
        {
            if (!cycles.TryGetValue(request.AreaId, out var cycle) || cycle.CycleId != request.CycleId) return false;
            cycle.Outstanding--;
            if (failed) cycle.Accumulator.FailedReads++;
            IndustrialObservationAggregator.Apply(cycle.Accumulator, observation);
            return true;
        }
        // Completed cycles whose tokens are still equal; a changed generation restarts instead of publishing.
        public List<IndustrialCycle> TakeFinished(IIndustrialCycleSource source, long bucket, uint frame, double hours)
        {
            var finished = new List<IndustrialCycle>();
            foreach (var id in new List<ulong>(cycles.Keys))
            {
                var cycle = cycles[id];
                if (!cycle.Done) continue;
                if (cycle.MembershipToken != source.MembershipToken(id) || cycle.StructureToken != source.StructureToken(id))
                { Start(id, bucket, frame, hours, source); RestartCount++; continue; }
                cycles.Remove(id);
                finished.Add(cycle);
            }
            if (finished.Count > 0) { order.Clear(); order.AddRange(cycles.Keys); order.Sort(); }
            return finished;
        }
    }
    // Low-frequency Industrial observation (design §18): hourly cycles per Active Area over Core's primary-owned
    // building members, at most 128 building reads per update in total. Reads run in a scheduled job over
    // read-only lookups (no main-thread completion of vanilla jobs); results are consumed next update.
    public partial class IndustrialObservationSystem : GameSystemBase
    {
        public const int UpdateFrames = 16;
        private IndustrialRuntimeSystem runtime;
        private Game.Simulation.SimulationSystem simulation;
        private ResourceSystem resourceSystem;
        private EntityQuery economyQuery;
        private readonly IndustrialObservationScheduler scheduler = new IndustrialObservationScheduler();
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private readonly List<IndustrialReadRequest> requests = new List<IndustrialReadRequest>();
        private NativeArray<IndustrialReadRequest> pendingRequests;
        private NativeList<IndustrialBuildingRead> buildingReads;
        private NativeList<IndustrialCompanyRead> companyReads;
        private JobHandle pending;
        private bool hasPending;
        private int loadGeneration = -1;
        private CS2AreaSystem.Core.Diagnostics.AreaFailureLog failures;
        public IndustrialObservationScheduler Scheduler => scheduler;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>();
            simulation = World.GetOrCreateSystemManaged<Game.Simulation.SimulationSystem>();
            resourceSystem = World.GetOrCreateSystemManaged<ResourceSystem>();
            failures = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Areas.AreaCoreServicesSystem>().Failures;
            economyQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
            buildingReads = new NativeList<IndustrialBuildingRead>(IndustrialObservationScheduler.DefaultBudget, Allocator.Persistent);
            companyReads = new NativeList<IndustrialCompanyRead>(IndustrialObservationScheduler.DefaultBudget, Allocator.Persistent);
        }
        [Preserve]
        protected override void OnDestroy()
        {
            pending.Complete();
            if (pendingRequests.IsCreated) pendingRequests.Dispose();
            if (buildingReads.IsCreated) buildingReads.Dispose();
            if (companyReads.IsCreated) companyReads.Dispose();
            base.OnDestroy();
        }
        [Preserve]
        protected override void OnUpdate()
        {
            // Our own job from the previous update; completing it does not wait on vanilla producers.
            pending.Complete();
            if (!runtime.Ready || !runtime.TryGetClock(out var clock)) { DropPending(); return; }
            if (loadGeneration != runtime.LoadGeneration)
            {
                // Load: no pre-load cycle, read or snapshot survives.
                loadGeneration = runtime.LoadGeneration;
                DropPending(); scheduler.Clear();
            }
            try
            {
                uint frame = simulation.frameIndex;
                double hours = clock.GameHours(frame);
                long bucket = clock.HourBucket(frame);
                var source = runtime.CycleSource;
                // Validate explicit references before consuming any result or capturing a generation.
                runtime.RefreshAssignments();
                if (!runtime.AssignmentsReady) { DropPending(); return; }
                if (hasPending) Consume(bucket);
                runtime.CollectAreas(areas);
                scheduler.Sync(areas, bucket, frame, hours, source);
                foreach (var cycle in scheduler.TakeFinished(source, bucket, frame, hours)) Publish(cycle, frame, hours);
                MarkPaused();
                // Reads are handed out only when they can be scheduled, so no cycle waits on a read that never runs.
                if (economyQuery.IsEmptyIgnoreFilter) return;
                scheduler.NextReads(requests);
                if (requests.Count > 0) Schedule();
                failures.Recovered(Entity.Null, "Industrial.Observation");
            }
            catch (System.Exception error)
            {
                DropPending();
                failures.Report(Entity.Null, "Industrial.Observation", error, "Industrial observation update failed; Area Core and other modules continue");
            }
        }
        private void DropPending()
        {
            // Any read handed out but not consumed (dropped results or a failed schedule) is read again later.
            scheduler.Abandon();
            hasPending = false;
            if (pendingRequests.IsCreated) pendingRequests.Dispose();
            buildingReads.Clear(); companyReads.Clear();
        }
        private void Consume(long currentHourBucket)
        {
            hasPending = false;
            var companies = new List<IndustrialCompanyRead>();
            int c = 0;
            for (int i = 0; i < buildingReads.Length; i++)
            {
                var read = buildingReads[i];
                companies.Clear();
                while (c < companyReads.Length && IndustrialObservationMapper.Matches(companyReads[c], read)) companies.Add(companyReads[c++]);
                var request = new IndustrialReadRequest(read.Building, read.AreaId, read.CycleId);
                if (read.Failed) failures.Warn(read.Building, "Industrial.Read", "Unreadable", "Industrial observation skipped an entity it could not read: " + read.Building);
                else failures.Recovered(read.Building, "Industrial.Read");
                var semantics = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Areas.AreaCoreServicesSystem>().Semantics;
                var cycle = scheduler.Cycle(read.AreaId);
                if (cycle != null && cycle.CycleId == read.CycleId && cycle.StructureToken == runtime.CycleSource.StructureToken(read.AreaId))
                    foreach (var company in companies) runtime.ObserveCompanyCandidate(read, company, currentHourBucket);
                scheduler.Complete(request, IndustrialObservationMapper.ToOwnedObservation(read, companies, runtime.Membership, runtime.Assignments, company => {
                    var facts = IndustrialCompanyOwnership.ReadLive(company.Company, EntityManager, semantics, out var liveBuilding, read.Traits);
                    if (liveBuilding != company.Building) facts.Live = false;
                    return facts;
                }, semantics), read.Failed);
            }
            if (pendingRequests.IsCreated) pendingRequests.Dispose();
            buildingReads.Clear(); companyReads.Clear();
        }
        private void Publish(IndustrialCycle cycle, uint frame, double hours)
        {
            IndustrialAreaRef area = default; bool found = false;
            foreach (var a in areas) if (a.StableId == cycle.AreaId) { area = a; found = true; break; }
            if (!found) return;
            var snapshot = runtime.Store.GetOrCreate(cycle.AreaId, area.Area);
            snapshot.Paused = false;
            IndustrialObservationAggregator.Finalize(cycle.Accumulator, snapshot, new IndustrialCycleInfo {
                CycleId = cycle.CycleId, HourBucket = cycle.HourBucket, StartFrame = cycle.StartFrame, EndFrame = frame, StartHours = cycle.StartHours, EndHours = hours,
                MembershipToken = cycle.MembershipToken, StructureToken = cycle.StructureToken, Active = area.State == AreaLifecycleState.Active,
                NonPrimaryMembers = cycle.NonPrimaryMembers });
        }
        // Suspended Areas keep their last snapshot only as paused/stale and lose interpolation endpoints.
        private void MarkPaused()
        {
            foreach (var area in areas)
                if (area.State != AreaLifecycleState.Active && runtime.Store.TryGet(area.StableId, out var snapshot) && !snapshot.Paused)
                { snapshot.Paused = true; snapshot.ResetBaselines(); }
        }
        private void Schedule()
        {
            pendingRequests = new NativeArray<IndustrialReadRequest>(requests.Count, Allocator.Persistent);
            for (int i = 0; i < requests.Count; i++) pendingRequests[i] = requests[i];
            var job = new ReadJob { Requests = pendingRequests, Buildings = buildingReads, Companies = companyReads, Reader = CreateReader() };
            var dependency = Dependency;
            var outcome = Core.Jobs.ScheduledJob.Run(() => job.Schedule(dependency), handle => { Dependency = handle; resourceSystem.AddPrefabsReader(handle); },
                handle => handle.Complete(), out pending, out var error);
            // Not scheduled: the caller's catch drops the reads (they are read again later). Scheduled, or completed after a
            // failed registration: the reads exist and are consumed next update, so no cycle stays outstanding.
            if (outcome == Core.Jobs.ScheduledJobOutcome.NotScheduled) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            hasPending = true;
            if (outcome == Core.Jobs.ScheduledJobOutcome.CompletedAfterRegistrationFailure)
                failures.Report(Entity.Null, "Industrial.ReadRegistration", error, "Industrial observation could not register its resource-prefab read; this batch was completed immediately");
            else failures.Recovered(Entity.Null, "Industrial.ReadRegistration");
        }
        private IndustrialObservationReader CreateReader() => new IndustrialObservationReader {
            Buildings = GetComponentLookup<Building>(true), Prefabs = GetComponentLookup<PrefabRef>(true),
            Abandoned = GetComponentLookup<Abandoned>(true), Destroyed = GetComponentLookup<Destroyed>(true),
            ExtractorProperties = GetComponentLookup<ExtractorProperty>(true), IndustrialProperties = GetComponentLookup<IndustrialProperty>(true),
            OfficeProperties = GetComponentLookup<OfficeProperty>(true), CommercialProperties = GetComponentLookup<CommercialProperty>(true), StorageProperties = GetComponentLookup<StorageProperty>(true),
            CargoStations = GetComponentLookup<Game.Buildings.CargoTransportStation>(true), ExtractorFacilities = GetComponentLookup<Game.Buildings.ExtractorFacility>(true),
            PropertyDatas = GetComponentLookup<BuildingPropertyData>(true), CargoStationDatas = GetComponentLookup<CargoTransportStationData>(true),
            ExtractorFacilityDatas = GetComponentLookup<ExtractorFacilityData>(true), SpawnableDatas = GetComponentLookup<SpawnableBuildingData>(true),
            BuildingDatas = GetComponentLookup<BuildingData>(true), WorkProviders = GetComponentLookup<WorkProvider>(true), Companies = GetComponentLookup<CompanyData>(true),
            OfficeCompanies = GetComponentLookup<OfficeCompany>(true), CommercialCompanies = GetComponentLookup<CommercialCompany>(true),
            ServiceAvailables = GetComponentLookup<ServiceAvailable>(true), ExtractorCompanies = GetComponentLookup<Game.Companies.ExtractorCompany>(true),
            ProcessingCompanies = GetComponentLookup<Game.Companies.ProcessingCompany>(true), IndustrialCompanies = GetComponentLookup<IndustrialCompany>(true),
            StorageCompanies = GetComponentLookup<Game.Companies.StorageCompany>(true), Profitabilities = GetComponentLookup<Profitability>(true),
            Statistics = GetComponentLookup<CompanyStatisticData>(true), Processes = GetComponentLookup<IndustrialProcessData>(true),
            WorkplaceDatas = GetComponentLookup<WorkplaceData>(true), StorageLimits = GetComponentLookup<StorageLimitData>(true),
            ResourceDatas = GetComponentLookup<ResourceData>(true), Attached = GetComponentLookup<Attached>(true), Citizens = GetComponentLookup<Citizen>(true),
            Renters = GetBufferLookup<Renter>(true), Employees = GetBufferLookup<Employee>(true), Efficiencies = GetBufferLookup<Efficiency>(true),
            ResourceBuffers = GetBufferLookup<Resources>(true), InstalledUpgrades = GetBufferLookup<InstalledUpgrade>(true),
            ResourcePrefabs = resourceSystem.GetPrefabs(), Economy = economyQuery.GetSingleton<EconomyParameterData>() };
        private struct ReadJob : IJob
        {
            [ReadOnly] public NativeArray<IndustrialReadRequest> Requests;
            public IndustrialObservationReader Reader;
            public NativeList<IndustrialBuildingRead> Buildings;
            public NativeList<IndustrialCompanyRead> Companies;
            public void Execute()
            {
                for (int i = 0; i < Requests.Length; i++) Reader.Read(Requests[i], ref Buildings, ref Companies);
            }
        }
    }
}
