using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // XP and promotion (design §17). Visit XP arrives per completed visit; passive XP, hold and promotion tick
    // every 1/32 in-game day. Level is persistent in ManagedAreaProgress and never demotes.
    public partial class ParkProgressionSystem : GameSystemBase, IParkVisitListener
    {
        public const uint UpdateFrames = ParkProgressionModel.FramesPerDay / 32;
        private ParkRuntimeSystem runtime;
        private CS2AreaSystem.Core.Policies.AreaPolicySystem policies;
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private uint lastFrame;
        private int loadGeneration = -1;
        private bool failureLogged, policyFailureLogged;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        public static ParkProgressInputs BuildInputs(ParkQualitySnapshot quality, in ParkAreaState state)
        {
            // Unavailable metrics never satisfy a threshold: missing data must not promote.
            float Known(ParkMetric metric) => metric.Available ? metric.Value : 0f;
            return new ParkProgressInputs {
                Quality = quality?.Quality ?? 0f, RecentSatisfaction = state.Satisfaction.Display,
                Maintenance = quality == null ? 0 : Known(quality.Maintenance), Variety = quality == null ? 0 : Known(quality.Variety),
                Landscape = quality == null ? 0 : Known(quality.Landscape),
                LifetimeVisitors = state.LifetimeVisitors, LifetimeTouristVisitors = state.LifetimeTouristVisitors, HoldFrames = state.PromotionHoldFrames };
        }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            policies = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Policies.AreaPolicySystem>();
            World.GetOrCreateSystemManaged<ParkVisitSystem>().AddListener(this);
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint frame = runtime.SimulationFrame;
            if (loadGeneration != runtime.LoadGeneration) { loadGeneration = runtime.LoadGeneration; lastFrame = frame; return; }
            uint elapsed = ParkFinanceModel.ElapsedForSettlement(lastFrame, frame, UpdateFrames * 2);
            lastFrame = frame;
            if (elapsed == 0) return;
            runtime.CollectParks(parks);
            foreach (var park in parks) Tick(park, frame, elapsed);
        }
        private void Tick(ParkAreaRef park, uint frame, uint elapsed)
        {
            try
            {
                if (!EntityManager.HasComponent<ParkAreaState>(park.Area) || !EntityManager.HasComponent<ManagedAreaProgress>(park.Area)) return;
                // Quality not computed yet (e.g. just after load): leave progression untouched until inputs exist.
                if (!runtime.Store.TryGet(park.StableId, out var snapshot) || snapshot.Quality == null) return;
                var progress = EntityManager.GetComponentData<ManagedAreaProgress>(park.Area);
                var state = EntityManager.GetComponentData<ParkAreaState>(park.Area);
                var step = ParkProgressionModel.Step(new ParkProgressStep { Level = progress.Level, Experience = progress.Experience, State = state },
                    BuildInputs(snapshot.Quality, state), park.State == AreaLifecycleState.Active, snapshot.RecentVisitors.Sum(frame), frame, elapsed, XpMultiplier(park.Area));
                progress.Level = step.Level; progress.Experience = step.Experience;
                EntityManager.SetComponentData(park.Area, progress);
                EntityManager.SetComponentData(park.Area, step.State);
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Park progression update failed"); }
            }
        }
        // Policy bonus to XP the Park already earned; a policy failure only removes the bonus.
        private double XpMultiplier(Entity area)
        {
            try { return policies.GetExperienceMultiplier(area); }
            catch (System.Exception error)
            {
                if (!policyFailureLogged) { policyFailureLogged = true; Mod.Log.Error(error, "Area policy XP bonus unavailable; Park XP continues without it"); }
                return 1.0;
            }
        }
        public void OnVisitStarted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, ParkVisitTracker tracker) { }
        public void OnVisitCompleted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, bool parkActive)
        {
            if (!parkActive || !EntityManager.Exists(area) || !EntityManager.HasComponent<ManagedAreaProgress>(area)) return;
            var progress = EntityManager.GetComponentData<ManagedAreaProgress>(area);
            progress.Experience = ParkProgressionModel.AwardVisitXp(progress.Experience, visit.Satisfaction, true, XpMultiplier(area));
            EntityManager.SetComponentData(area, progress);
        }
    }
}
