using System.Collections.Generic;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Areas;
using Game.Objects;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Low-frequency Quality recomputation (design §21.5): a few Parks per update, round-robin; never UI-driven.
    public partial class ParkQualitySystem : GameSystemBase
    {
        public const int UpdateFrames = 256, ParksPerUpdate = 4;
        private ParkRuntimeSystem runtime;
        private AreaCoreServicesSystem services;
        private ParkPolicyEffectReader policyEffects;
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly List<(float X, float Z)> positions = new List<(float X, float Z)>();
        private readonly List<ParkFacilityTraits> members = new List<ParkFacilityTraits>();
        private int cursor;
        private bool failureLogged;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            var policies = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Policies.AreaPolicySystem>();
            policyEffects = new ParkPolicyEffectReader((area, id) => policies.IsEffective(area, id),
                error => Mod.Log.Error(error, "Area policy effects unavailable; Park Quality continues without them"));
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            runtime.CollectParks(parks);
            if (parks.Count == 0) return;
            var ids = new List<ulong>(parks.Count);
            var byId = new Dictionary<ulong, ParkAreaRef>();
            foreach (var park in parks) { ids.Add(park.StableId); byId[park.StableId] = park; }
            foreach (var id in ParkQualitySchedule.Pick(ids, id => runtime.Store.TryGet(id, out var s) && s.Quality != null, cursor, ParksPerUpdate, out cursor))
                Recompute(byId[id]);
        }
        public void Recompute(ParkAreaRef park)
        {
            try
            {
                if (!EntityManager.Exists(park.Area) || !EntityManager.HasComponent<ParkAreaState>(park.Area)) return;
                var snapshot = runtime.Store.GetOrCreate(park.StableId, park.Area);
                var state = EntityManager.GetComponentData<ParkAreaState>(park.Area);
                float area = EntityManager.HasComponent<Geometry>(park.Area) ? EntityManager.GetComponentData<Geometry>(park.Area).m_SurfaceArea : snapshot.AreaSquareMeters;
                snapshot.AreaSquareMeters = area;
                var entrances = services.Anchors.GetValidAnchors(park.Area, AreaAnchorRole.Entrance);
                positions.Clear();
                foreach (var entrance in entrances)
                    if (EntityManager.HasComponent<Transform>(entrance)) { var p = EntityManager.GetComponentData<Transform>(entrance).m_Position; positions.Add((p.x, p.z)); }
                members.Clear();
                bool primaryForAll = true;
                var index = runtime.Membership;
                foreach (var member in index.GetMembers(park.StableId))
                {
                    members.Add(ParkFacilityReader.Read(member, EntityManager));
                    foreach (var relation in index.GetAreas(member))
                        if (relation.AreaStableId == park.StableId && !relation.IsPrimaryForType && ParkFacilityClassifier.IsVisitRelevant(members[members.Count - 1])) primaryForAll = false;
                }
                snapshot.Facilities = ParkFacilitySnapshot.Build(members);
                snapshot.EntranceCount = entrances.Count;
                int level = EntityManager.HasComponent<ManagedAreaProgress>(park.Area) ? ParkProgressionModel.NormalizeLevel(EntityManager.GetComponentData<ManagedAreaProgress>(park.Area).Level) : 1;
                float budget = EntityManager.HasComponent<ManagedAreaSettings>(park.Area) ? EntityManager.GetComponentData<ManagedAreaSettings>(park.Area).BudgetMultiplier : ParkFinanceModel.DefaultBudget;
                uint frame = runtime.SimulationFrame;
                snapshot.Quality = ParkQualityEvaluator.Evaluate(new ParkQualityContext {
                    AreaSquareMeters = area, Entrances = entrances.Count, EntranceSpread = ParkQualityModel.ComputeEntranceSpread(positions, area),
                    EntrancePositions = positions.ToArray(),
                    Landscape = snapshot.Landscape, Facilities = snapshot.Facilities, Satisfaction = state.Satisfaction, TouristSatisfaction = state.TouristSatisfaction,
                    ActiveVisitors = snapshot.ActiveVisitors, Budget = budget, Level = level, AdmissionFee = state.AdmissionFee,
                    RecentTouristVisits = snapshot.RecentTouristVisitors.Sum(frame), IsPrimaryForAllMembers = primaryForAll,
                    Active = park.State == AreaLifecycleState.Active, Policies = policyEffects.Read(park.Area) });
                snapshot.Experience = snapshot.Quality.Experience;
                snapshot.QualityFrame = frame;
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                // The previous snapshot (if any) is retained; Core lifecycle and other Parks continue.
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Park quality evaluation failed; last result kept"); }
            }
        }
    }
}
