using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Observes real vanilla leisure (design §8): never replaces or patches LeisureSystem and never reads its private
    // event queue. Hot path: citizen -> CurrentBuilding -> AreaMembershipIndex.GetAreas -> primary Park.
    public partial class ParkVisitSystem : GameSystemBase
    {
        private ParkRuntimeSystem runtime;
        private SimulationSystem simulation;
        private EntityQuery visitors;
        private readonly ParkVisitTracker tracker = new ParkVisitTracker(ParkVisitCadence.GraceFrames);
        private readonly List<ParkVisitEvent> events = new List<ParkVisitEvent>();
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly Dictionary<ulong, ParkAreaRef> activeParks = new Dictionary<ulong, ParkAreaRef>();
        private readonly Dictionary<Entity, float?> buildingExperience = new Dictionary<Entity, float?>();
        private readonly List<IParkVisitListener> listeners = new List<IParkVisitListener>();
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
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint frame = simulation.frameIndex;
            if (loadGeneration != runtime.LoadGeneration)
            {
                // Sessions are runtime-only: restored in-Park visitors are re-observed as already paid.
                loadGeneration = runtime.LoadGeneration;
                tracker.Clear();
                tracker.SeedPreExistingUntil(frame + ParkVisitCadence.SeedFrames);
            }
            try
            {
                uint group = ParkVisitCadence.GroupFor(frame);
                RefreshParks();
                if (group == 0) { buildingExperience.Clear(); tracker.Expire(frame, events); }
                if (activeParks.Count > 0 || tracker.SessionCount > 0) Observe(group, frame);
                Dispatch(frame);
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                events.Clear();
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Park visit observation failed this update"); }
            }
        }
        private void RefreshParks()
        {
            runtime.CollectParks(parks);
            activeParks.Clear();
            foreach (var park in parks) if (park.State == AreaLifecycleState.Active) activeParks[park.StableId] = park;
        }
        private void Observe(uint group, uint frame)
        {
            visitors.ResetFilter();
            visitors.SetSharedComponentFilter(new UpdateFrame(group));
            using var citizens = visitors.ToEntityArray(Allocator.Temp);
            using var buildings = visitors.ToComponentDataArray<CurrentBuilding>(Allocator.Temp);
            using var households = visitors.ToComponentDataArray<HouseholdMember>(Allocator.Temp);
            visitors.ResetFilter();
            ObservedLastUpdate = citizens.Length;
            var membership = runtime.Membership;
            for (int i = 0; i < citizens.Length; i++)
            {
                var building = buildings[i].m_CurrentBuilding;
                ulong parkId = 0;
                float experience = float.NaN;
                if (building != Entity.Null)
                {
                    var relations = membership.GetAreas(building);
                    if (relations.Count > 0)
                    {
                        parkId = ParkVisitModel.PrimaryPark(relations, (int)AreaTypeId.Park, id => activeParks.ContainsKey(id));
                        if (parkId != 0)
                        {
                            var facility = FacilityExperience(building);
                            if (!facility.HasValue) parkId = 0; // not a Park/leisure/attraction building: no visit
                            else if (runtime.Store.TryGet(parkId, out var snapshot))
                            {
                                var visit = snapshot.Experience; visit.Facility = facility.Value;
                                experience = ParkVisitModel.ComputeVisitSample(visit);
                            }
                        }
                    }
                }
                if (parkId == 0) { tracker.Observe(citizens[i], 0, false, float.NaN, frame, events); continue; }
                var household = households[i].m_Household;
                bool tourist = household != Entity.Null && EntityManager.Exists(household) && EntityManager.HasComponent<TouristHousehold>(household);
                tracker.Observe(citizens[i], parkId, tourist, experience, frame, events);
            }
        }
        // Null for buildings that are not Park/leisure/attraction facilities; cached for one observation cycle.
        private float? FacilityExperience(Entity building)
        {
            if (buildingExperience.TryGetValue(building, out var cached)) return cached;
            var traits = ParkFacilityReader.Read(building, EntityManager);
            float? value = ParkFacilityClassifier.IsVisitRelevant(traits) ? ParkVisitModel.FacilityExperience(traits.HasLeisureData, traits.LeisureEfficiency) : (float?)null;
            buildingExperience[building] = value;
            return value;
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
    }
    public interface IParkVisitListener
    {
        void OnVisitStarted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, ParkVisitTracker tracker);
        void OnVisitCompleted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, bool parkActive);
    }
}
