using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Citizens;
using Game.Common;
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
        private readonly List<IParkVisitListener> listeners = new List<IParkVisitListener>();
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
        public void AddListener(IParkVisitListener listener) { if (!listeners.Contains(listener)) listeners.Add(listener); }
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
                HouseholdMemberType = GetComponentTypeHandle<HouseholdMember>(true),
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
                float experience = float.NaN;
                if (runtime.Store.TryGet(match.ParkId, out var snapshot))
                {
                    var visit = snapshot.Experience; visit.Facility = match.Facility;
                    experience = ParkVisitModel.ComputeVisitSample(visit);
                }
                tracker.Observe(match.Citizen, match.ParkId, match.Tourist, experience, pendingFrame, events);
            }
            matches.Clear();
        }
        private void Dispatch(uint frame)
        {
            foreach (var e in events)
            {
                if (!activeParks.TryGetValue(e.ParkId, out var park))
                {
                    // Completed after its Park was suspended/deleted: the visit is closed without new gameplay rewards.
                    if (e.Kind != ParkVisitEventKind.Completed) continue;
                    foreach (var p in parks) if (p.StableId == e.ParkId) { park = p; break; }
                    if (park.Area == Entity.Null) continue;
                }
                var snapshot = runtime.Store.GetOrCreate(e.ParkId, park.Area);
                if (e.Kind == ParkVisitEventKind.Started) ParkVisitRecorder.RecordOccupancy(snapshot, tracker.ActiveCount(e.ParkId));
                else
                {
                    ParkVisitRecorder.RecordRecent(snapshot, e.Tourist, frame);
                    if (EntityManager.Exists(park.Area) && EntityManager.HasComponent<ParkAreaState>(park.Area))
                        EntityManager.SetComponentData(park.Area, ParkVisitModel.RecordCompletedVisit(EntityManager.GetComponentData<ParkAreaState>(park.Area), e.Tourist, e.Satisfaction));
                }
                bool active = park.State == AreaLifecycleState.Active;
                foreach (var listener in listeners)
                {
                    try
                    {
                        if (e.Kind == ParkVisitEventKind.Started) listener.OnVisitStarted(e, park.Area, snapshot, tracker);
                        else listener.OnVisitCompleted(e, park.Area, snapshot, active);
                    }
                    catch (System.Exception error) { Mod.Log.Error(error, "Park visit listener failed"); }
                }
            }
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
            [ReadOnly] public ComponentTypeHandle<HouseholdMember> HouseholdMemberType;
            [ReadOnly] public ComponentLookup<TouristHousehold> Tourists;
            [ReadOnly] public NativeParallelHashMap<Entity, ParkVisitTarget>.ReadOnly Targets;
            public NativeList<ParkVisitMatch> Matches;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var buildings = chunk.GetNativeArray(ref CurrentBuildingType);
                var households = chunk.GetNativeArray(ref HouseholdMemberType);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!Targets.TryGetValue(buildings[i].m_CurrentBuilding, out var target)) continue;
                    var household = households[i].m_Household;
                    Matches.Add(new ParkVisitMatch { Citizen = entities[i], ParkId = target.ParkId, Facility = target.Facility,
                        Tourist = household != Entity.Null && Tourists.HasComponent(household) });
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
