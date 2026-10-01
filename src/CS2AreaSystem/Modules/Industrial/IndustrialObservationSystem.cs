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
        // Members for which this Area is Core's primary Industrial owner; the rest are counted as overlap only.
        IReadOnlyList<Entity> PrimaryMembers(ulong areaId, out int nonPrimary);
    }
    public sealed class IndustrialMembershipCycleSource : IIndustrialCycleSource
    {
        private readonly AreaMembershipIndex index;
        private readonly IndustrialStructureTracker structure;
        public IndustrialMembershipCycleSource(AreaMembershipIndex index, IndustrialStructureTracker structure) { this.index = index; this.structure = structure; }
        public ulong MembershipToken(ulong areaId) => index.GetAreaRevision(areaId);
        public ulong StructureToken(ulong areaId) => structure.Get(areaId);
        public IReadOnlyList<Entity> PrimaryMembers(ulong areaId, out int nonPrimary)
        {
            var primary = new List<Entity>(); nonPrimary = 0;
            foreach (var member in index.GetMembers(areaId))
            {
                bool owned = false;
                foreach (var relation in index.GetAreas(member))
                    if (relation.AreaStableId == areaId) { owned = relation.IsPrimaryForType; break; }
                if (owned) primary.Add(member); else nonPrimary++;
            }
            primary.Sort((a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Version.CompareTo(b.Version));
            return primary;
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
            var ids = new HashSet<ulong>();
            foreach (var area in alive) ids.Add(area.StableId);
            var gone = new List<ulong>();
            foreach (var id in tokens.Keys) if (!ids.Contains(id)) gone.Add(id);
            foreach (var id in gone) tokens.Remove(id);
        }
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
        private readonly List<ulong> order = new List<ulong>();
        private long nextCycleId = 1;
        private int cursor;
        public int RestartCount { get; private set; }
        public IndustrialObservationScheduler(int budget = DefaultBudget) { Budget = budget > 0 ? budget : DefaultBudget; }
        public IndustrialCycle Cycle(ulong areaId) => cycles.TryGetValue(areaId, out var cycle) ? cycle : null;
        public int ActiveCycles => cycles.Count;
        public void Clear() { cycles.Clear(); lastBucket.Clear(); order.Clear(); cursor = 0; }
        public void Sync(IReadOnlyList<IndustrialAreaRef> areas, long bucket, uint frame, double hours, IIndustrialCycleSource source)
        {
            var alive = new HashSet<ulong>();
            foreach (var area in areas)
            {
                alive.Add(area.StableId);
                if (area.State != AreaLifecycleState.Active)
                {
                    // Suspended/unavailable Areas keep authoritative state but are not observed.
                    cycles.Remove(area.StableId); lastBucket.Remove(area.StableId);
                    continue;
                }
                if (cycles.TryGetValue(area.StableId, out var cycle))
                {
                    if (cycle.MembershipToken != source.MembershipToken(area.StableId) || cycle.StructureToken != source.StructureToken(area.StableId))
                    { Start(area.StableId, bucket, frame, hours, source); RestartCount++; }
                }
                else if (!lastBucket.TryGetValue(area.StableId, out var last) || last != bucket) Start(area.StableId, bucket, frame, hours, source);
            }
            var gone = new List<ulong>();
            foreach (var id in cycles.Keys) if (!alive.Contains(id)) gone.Add(id);
            foreach (var id in lastBucket.Keys) if (!alive.Contains(id) && !gone.Contains(id)) gone.Add(id);
            foreach (var id in gone) { cycles.Remove(id); lastBucket.Remove(id); }
            order.Clear(); order.AddRange(cycles.Keys); order.Sort();
        }
        private void Start(ulong areaId, long bucket, uint frame, double hours, IIndustrialCycleSource source)
        {
            var members = source.PrimaryMembers(areaId, out int nonPrimary);
            cycles[areaId] = new IndustrialCycle { AreaId = areaId, CycleId = nextCycleId++, HourBucket = bucket, StartFrame = frame, StartHours = hours,
                MembershipToken = source.MembershipToken(areaId), StructureToken = source.StructureToken(areaId), Members = members, NonPrimaryMembers = nonPrimary };
            lastBucket[areaId] = bucket;
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
        private int loadGeneration = -1, failuresLogged;
        public IndustrialObservationScheduler Scheduler => scheduler;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>();
            simulation = World.GetOrCreateSystemManaged<Game.Simulation.SimulationSystem>();
            resourceSystem = World.GetOrCreateSystemManaged<ResourceSystem>();
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
                if (hasPending) Consume();
                runtime.CollectAreas(areas);
                scheduler.Sync(areas, bucket, frame, hours, source);
                foreach (var cycle in scheduler.TakeFinished(source, bucket, frame, hours)) Publish(cycle, frame, hours);
                MarkPaused();
                // Reads are handed out only when they can be scheduled, so no cycle waits on a read that never runs.
                if (economyQuery.IsEmptyIgnoreFilter) return;
                scheduler.NextReads(requests);
                if (requests.Count > 0) Schedule();
            }
            catch (System.Exception error)
            {
                DropPending();
                if (failuresLogged++ < 3) Mod.Log.Error(error, "Industrial observation update failed; Area Core and other modules continue");
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
        private void Consume()
        {
            hasPending = false;
            var companies = new List<IndustrialCompanyRead>();
            int c = 0;
            for (int i = 0; i < buildingReads.Length; i++)
            {
                var read = buildingReads[i];
                companies.Clear();
                while (c < companyReads.Length && companyReads[c].Building == read.Building) companies.Add(companyReads[c++]);
                var request = new IndustrialReadRequest(read.Building, read.AreaId, read.CycleId);
                if (read.Failed && failuresLogged++ < 3) Mod.Log.Warn("Industrial observation skipped an entity it could not read: " + read.Building);
                scheduler.Complete(request, IndustrialObservationMapper.ToObservation(read, companies), read.Failed);
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
            if (outcome == Core.Jobs.ScheduledJobOutcome.CompletedAfterRegistrationFailure && failuresLogged++ < 3)
                Mod.Log.Error(error, "Industrial observation could not register its resource-prefab read; this batch was completed immediately");
        }
        private IndustrialObservationReader CreateReader() => new IndustrialObservationReader {
            Buildings = GetComponentLookup<Building>(true), Prefabs = GetComponentLookup<PrefabRef>(true),
            Abandoned = GetComponentLookup<Abandoned>(true), Destroyed = GetComponentLookup<Destroyed>(true),
            ExtractorProperties = GetComponentLookup<ExtractorProperty>(true), IndustrialProperties = GetComponentLookup<IndustrialProperty>(true),
            OfficeProperties = GetComponentLookup<OfficeProperty>(true), StorageProperties = GetComponentLookup<StorageProperty>(true),
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
