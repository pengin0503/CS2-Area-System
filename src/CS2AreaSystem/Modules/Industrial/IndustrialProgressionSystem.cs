using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Consumes each coherent published observation once (design §14): settles its game-hour XP bucket and daily
    // activity interval, updates the peak workforce, completes calendar days (also while Suspended) and applies at
    // most one promotion. Level/XP live in ManagedAreaProgress and never demote.
    public partial class IndustrialProgressionSystem : GameSystemBase
    {
        private IndustrialRuntimeSystem runtime;
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private int failuresLogged;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => IndustrialObservationSystem.UpdateFrames;
        public static IndustrialProgressInputs BuildInputs(IndustrialObservationResult result, in IndustrialAreaState state)
        {
            if (result == null) return new IndustrialProgressInputs { StreakDays = state.HighThroughputStreakDays };
            return new IndustrialProgressInputs {
                Production = result.Production, Logistics = result.Logistics, Employment = result.Employment, Efficiency = result.Efficiency, Performance = result.Performance,
                ActiveProductiveFacilities = result.OperatingProducers, EmployedWorkers = result.Employees, FunctionalCargo = result.CargoOperational > 0,
                MeaningfulOutputs = result.RollingMeaningfulOutputs, StreakDays = state.HighThroughputStreakDays, Fresh = result.Active && !result.Delayed };
        }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>();
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready || !runtime.TryGetClock(out var clock)) return;
            uint day = clock.Day(runtime.SimulationFrame);
            runtime.CollectAreas(areas);
            foreach (var area in areas)
            {
                try { Tick(area, day); }
                catch (System.Exception error)
                {
                    if (failuresLogged++ < 3) Mod.Log.Error(error, "Industrial progression update failed for one Area; others continue");
                }
            }
        }
        private void Tick(IndustrialAreaRef area, uint day)
        {
            if (!EntityManager.Exists(area.Area) || !EntityManager.HasComponent<IndustrialAreaState>(area.Area) || !EntityManager.HasComponent<ManagedAreaProgress>(area.Area)) return;
            var state = EntityManager.GetComponentData<IndustrialAreaState>(area.Area);
            var progress = EntityManager.GetComponentData<ManagedAreaProgress>(area.Area);
            bool changed = false;
            if (runtime.Store.TryGet(area.StableId, out var snapshot) && snapshot.Current != null && snapshot.Current.CycleId != snapshot.LastConsumedCycle)
            {
                var result = snapshot.Current;
                snapshot.LastConsumedCycle = result.CycleId;
                bool active = area.State == AreaLifecycleState.Active && result.Active;
                var inputs = BuildInputs(result, state);
                // Interval coverage first, so a cycle straddling midnight credits the completed day before it closes.
                var completedDay = IndustrialProgressionModel.AccumulateDailyActivity(new IndustrialDailyActivityObservation {
                    Hours = result.StartHours, HourBucket = result.HourBucket, Activity = result.Activity.HasValue ? result.Activity.Value : double.NaN,
                    Generation = result.Generation, Valid = active && inputs.Fresh && result.Activity.HasValue }, ref state, ref snapshot.ActivityBaseline);
                if (completedDay.HasValue) snapshot.LastCompletedDay = completedDay;
                inputs.StreakDays = state.HighThroughputStreakDays;
                var level = new IndustrialLevelProgress { Level = progress.Level, Experience = progress.Experience };
                var step = IndustrialProgressionModel.Step(ref level, ref state, inputs,
                    new IndustrialObservationStamp { HourBucket = result.HourBucket, Active = active, Fresh = inputs.Fresh });
                snapshot.LastXp = step.Xp; snapshot.HasXpResult = true;
                // Level is permanent: a stored higher level is never lowered.
                progress.Level = System.Math.Max(IndustrialProgressionModel.NormalizeLevel(progress.Level), level.Level);
                progress.Experience = level.Experience;
                if (progress.SchemaVersion == 0) progress.SchemaVersion = ManagedAreaProgress.CurrentSchemaVersion;
                changed = true;
            }
            if (day > state.ActivityDay)
            {
                var completed = IndustrialProgressionModel.AdvanceToDay(day, ref state);
                if (completed.HasValue && runtime.Store.TryGet(area.StableId, out var display)) display.LastCompletedDay = completed;
                changed = true;
            }
            if (!changed) return;
            EntityManager.SetComponentData(area.Area, state);
            EntityManager.SetComponentData(area.Area, progress);
        }
    }
}
