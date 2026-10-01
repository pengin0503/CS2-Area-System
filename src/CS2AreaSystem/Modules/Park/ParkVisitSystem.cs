using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Citizens;
using Game.Buildings;
using Game.Companies;
using Game.Common;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Observes real vanilla leisure (design §8): never replaces or patches LeisureSystem and never reads its private
    // event queue. Hot path: citizen -> CurrentBuilding -> relevant building map (from AreaMembershipIndex) -> Park.
    // Citizen data is read by a scheduled chunk job (no main-thread sync); its matches are consumed next update.
    public partial class ParkVisitSystem : GameSystemBase
    {
        private ParkRuntimeSystem runtime;
        private SimulationSystem simulation;
        private EntityQuery visitors;
        private readonly ParkVisitTracker tracker = new ParkVisitTracker(ParkVisitCadence.GraceFrames);
        private readonly List<ParkVisitEvent> events = new List<ParkVisitEvent>();
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly Dictionary<ulong, ParkAreaRef> activeParks = new Dictionary<ulong, ParkAreaRef>();
        private readonly CS2AreaSystem.Core.ListenerSet<IParkVisitListener> listeners = new CS2AreaSystem.Core.ListenerSet<IParkVisitListener>();
        private NativeParallelHashMap<Entity, ParkVisitTarget> targets;
        private NativeList<ParkVisitMatch> matches;
        private JobHandle pending;
        private bool hasPending, targetsDirty = true;
        private uint pendingFrame;
        private string activeSignature = "";
        private int loadGeneration = -1;
        private bool failureLogged;
        public ParkVisitTracker Tracker => tracker;
        public int ObservedLastUpdate { get; private set; }
        public bool AddListener(IParkVisitListener listener) => listeners.Add(listener);
        // Owners remove themselves in OnDestroy; a destroyed system is never called back.
        public bool RemoveListener(IParkVisitListener listener) => listeners.Remove(listener);
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)ParkVisitCadence.UpdateInterval;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            simulation = World.GetOrCreateSystemManaged<SimulationSystem>();
            visitors = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Citizen>(), ComponentType.ReadOnly<Leisure>(), ComponentType.ReadOnly<CurrentBuilding>(),
                    ComponentType.ReadOnly<HouseholdMember>(), ComponentType.ReadOnly<UpdateFrame>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() } });
            targets = new NativeParallelHashMap<Entity, ParkVisitTarget>(256, Allocator.Persistent);
            matches = new NativeList<ParkVisitMatch>(256, Allocator.Persistent);
        }
        [Preserve]
        protected override void OnDestroy()
        {
            pending.Complete();
            if (targets.IsCreated) targets.Dispose();
            if (matches.IsCreated) matches.Dispose();
            base.OnDestroy();
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint frame = simulation.frameIndex;
            // Our own previous job ran during the last 64 frames; completing it here does not wait on vanilla work.
            pending.Complete();
            if (loadGeneration != runtime.LoadGeneration)
            {
                // Sessions are runtime-only: restored in-Park visitors are re-observed as already paid.
                loadGeneration = runtime.LoadGeneration;
                hasPending = false; matches.Clear(); targetsDirty = true;
                tracker.Clear();
                tracker.SeedPreExistingUntil(frame + ParkVisitCadence.SeedFrames);
            }
            try
            {
                uint group = ParkVisitCadence.GroupFor(frame);
                RefreshParks();
                if (hasPending) ConsumeMatches();
                if (group == 0) { tracker.Expire(frame, events); targetsDirty = true; }
                Dispatch(frame);
                if (targetsDirty) RebuildTargets();
                if (!targets.IsEmpty) Schedule(group, frame);
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                events.Clear(); hasPending = false;
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Park visit observation failed this update"); }
            }
        }
        private void RefreshParks()
        {
            runtime.CollectParks(parks);
            activeParks.Clear();
            var signature = new System.Text.StringBuilder();
            foreach (var park in parks)
                if (park.State == AreaLifecycleState.Active) { activeParks[park.StableId] = park; signature.Append(park.StableId).Append(','); }
            var current = signature.ToString();
            if (current != activeSignature) { activeSignature = current; targetsDirty = true; }
        }
        // Relevant buildings of Active Parks; rebuilt once per observation cycle or when the Active set changes.
        private void RebuildTargets()
        {
            targetsDirty = false;
            targets.Clear();
            var index = runtime.Membership;
            foreach (var park in activeParks.Values)
                foreach (var member in index.GetMembers(park.StableId))
                {
                    if (targets.ContainsKey(member)) continue;
                    var traits = ParkFacilityReader.ReadCore(member, EntityManager, false);
                    if (ParkVisitTargets.TryResolve(index.GetAreas(member), (int)AreaTypeId.Park, id => activeParks.ContainsKey(id), traits, out var target))
                        targets.TryAdd(member, target);
                }
        }
        private void Schedule(uint group, uint frame)
        {
            matches.Clear();
            visitors.ResetFilter();
            visitors.SetSharedComponentFilter(new UpdateFrame(group));
            var job = new ObserveJob {
                EntityType = GetEntityTypeHandle(),
                CurrentBuildingType = GetComponentTypeHandle<CurrentBuilding>(true),
                LeisureType = GetComponentTypeHandle<Leisure>(true),
                HouseholdMemberType = GetComponentTypeHandle<HouseholdMember>(true),
                Renters = GetComponentLookup<PropertyRenter>(true), Prefabs = GetComponentLookup<PrefabRef>(true),
                Providers = GetComponentLookup<LeisureProviderData>(true), Buildings = GetComponentLookup<Building>(true),
                Services = GetComponentLookup<ServiceAvailable>(true), Processes = GetComponentLookup<IndustrialProcessData>(true),
                Resources = GetBufferLookup<Resources>(true),
                Tourists = GetComponentLookup<TouristHousehold>(true),
                Targets = targets.AsReadOnly(),
                Matches = matches };
            // The filter is copied into the scheduled job, so it can be reset immediately.
            pending = job.Schedule(visitors, Dependency);
            visitors.ResetFilter();
            Dependency = pending;
            hasPending = true;
            pendingFrame = frame;
        }
        private void ConsumeMatches()
        {
            hasPending = false;
            ObservedLastUpdate = matches.Length;
            for (int i = 0; i < matches.Length; i++)
            {
                var match = matches[i];
                if (!activeParks.ContainsKey(match.ParkId)) continue;
                runtime.Store.TryGet(match.ParkId, out var snapshot);
                ParkVisitModel.TrySample(snapshot, match.Facility, out float experience);
                tracker.Observe(match.Citizen, match.ParkId, match.Tourist, experience, pendingFrame, events);
            }
            matches.Clear();
        }
        private void Dispatch(uint frame)
        {
            ParkVisitDispatcher.Dispatch(events, activeParks, parks, runtime.Store, tracker, frame, (park, e) =>
            {
                if (EntityManager.Exists(park.Area) && EntityManager.HasComponent<ParkAreaState>(park.Area))
                    EntityManager.SetComponentData(park.Area, ParkVisitModel.RecordCompletedVisit(EntityManager.GetComponentData<ParkAreaState>(park.Area), e.Tourist, e.Satisfaction, e.HasSatisfaction));
            }, listeners.Items, error => Mod.Log.Error(error, "Park visit listener failed"));
            events.Clear();
            foreach (var park in parks)
                if (runtime.Store.TryGet(park.StableId, out var snapshot)) snapshot.ActiveVisitors = tracker.ActiveCount(park.StableId);
        }
        public struct ParkVisitMatch
        {
            public Entity Citizen;
            public ulong ParkId;
            public float Facility;
            public bool Tourist;
        }
        private struct ObserveJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CurrentBuilding> CurrentBuildingType;
            [ReadOnly] public ComponentTypeHandle<Leisure> LeisureType;
            [ReadOnly] public ComponentTypeHandle<HouseholdMember> HouseholdMemberType;
            [ReadOnly] public ComponentLookup<PropertyRenter> Renters;
            [ReadOnly] public ComponentLookup<PrefabRef> Prefabs;
            [ReadOnly] public ComponentLookup<LeisureProviderData> Providers;
            [ReadOnly] public ComponentLookup<Building> Buildings;
            [ReadOnly] public ComponentLookup<ServiceAvailable> Services;
            [ReadOnly] public ComponentLookup<IndustrialProcessData> Processes;
            [ReadOnly] public BufferLookup<Resources> Resources;
            [ReadOnly] public ComponentLookup<TouristHousehold> Tourists;
            [ReadOnly] public NativeParallelHashMap<Entity, ParkVisitTarget>.ReadOnly Targets;
            public NativeList<ParkVisitMatch> Matches;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var buildings = chunk.GetNativeArray(ref CurrentBuildingType);
                var leisure = chunk.GetNativeArray(ref LeisureType);
                var households = chunk.GetNativeArray(ref HouseholdMemberType);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!Targets.TryGetValue(buildings[i].m_CurrentBuilding, out var target)) continue;
                    var agent = leisure[i].m_TargetAgent;
                    if (agent == Entity.Null) continue;
                    var building = buildings[i].m_CurrentBuilding;
                    var property = Renters.HasComponent(agent) ? Renters[agent].m_Property : Entity.Null;
                    if (!ParkLeisureObservation.TryResolveProvider(agent, building, property, ReadProvider(agent), ReadProvider(building), out var provider)) continue;
                    float facility = ParkVisitModel.FacilityExperience(true, Providers[Prefabs[provider].m_Prefab].m_Efficiency);
                    var household = households[i].m_Household;
                    Matches.Add(new ParkVisitMatch { Citizen = entities[i], ParkId = target.ParkId, Facility = facility,
                        Tourist = household != Entity.Null && Tourists.HasComponent(household) });
                }
            }
            private ParkLeisureProviderState ReadProvider(Entity entity)
            {
                var state = new ParkLeisureProviderState { Entity = entity };
                if (entity == Entity.Null || !Prefabs.HasComponent(entity)) return state;
                var prefab = Prefabs[entity].m_Prefab;
                state.HasPrefab = true;
                state.HasLeisureData = Providers.HasComponent(prefab);
                if (!state.HasLeisureData) return state;
                state.Inactive = Buildings.HasComponent(entity) && BuildingUtils.CheckOption(Buildings[entity], BuildingOption.Inactive);
                state.ServiceUnavailable = Services.HasComponent(entity) && Services[entity].m_ServiceAvailable <= 0;
                if (Processes.HasComponent(prefab))
                {
                    var output = Processes[prefab].m_Output.m_Resource;
                    state.OutOfStock = output != Resource.NoResource && Resources.HasBuffer(entity) && EconomyUtils.GetResources(output, Resources[entity]) <= 0;
                }
                return state;
            }
        }
    }
    // Routes visit events to Park statistics and listeners (finance, progression). Started events only for Active
    // Parks; a completion whose Park became Suspended is still counted but listeners are told it is not active;
    // events of deleted Parks are dropped. A failing listener never stops the others.
    public static class ParkVisitDispatcher
    {
        public static void Dispatch(IReadOnlyList<ParkVisitEvent> events, IReadOnlyDictionary<ulong, ParkAreaRef> active, IReadOnlyList<ParkAreaRef> all,
            ParkRuntimeStore store, ParkVisitTracker tracker, uint frame, System.Action<ParkAreaRef, ParkVisitEvent> recordCompleted,
            IReadOnlyList<IParkVisitListener> listeners, System.Action<System.Exception> report)
        {
            foreach (var e in events)
            {
                if (!active.TryGetValue(e.ParkId, out var park))
                {
                    if (e.Kind != ParkVisitEventKind.Completed) continue;
                    bool found = false;
                    foreach (var p in all) if (p.StableId == e.ParkId) { park = p; found = true; break; }
                    if (!found || park.Area == Entity.Null) continue;
                }
                var snapshot = store.GetOrCreate(e.ParkId, park.Area);
                if (e.Kind == ParkVisitEventKind.Started) ParkVisitRecorder.RecordOccupancy(snapshot, tracker.ActiveCount(e.ParkId));
                else { ParkVisitRecorder.RecordRecent(snapshot, e.Tourist, frame); recordCompleted(park, e); }
                bool isActive = park.State == AreaLifecycleState.Active;
                foreach (var listener in listeners)
                {
                    try
                    {
                        if (e.Kind == ParkVisitEventKind.Started) listener.OnVisitStarted(e, park.Area, snapshot, tracker);
                        else listener.OnVisitCompleted(e, park.Area, snapshot, isActive);
                    }
                    catch (System.Exception error) { report(error); }
                }
            }
        }
    }
    public interface IParkVisitListener
    {
        void OnVisitStarted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, ParkVisitTracker tracker);
        void OnVisitCompleted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, bool parkActive);
    }
}
