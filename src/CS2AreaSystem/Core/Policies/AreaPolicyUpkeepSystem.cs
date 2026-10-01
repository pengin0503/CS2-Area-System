using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.Common;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Policies
{
    // Bills effective policies as Area Management expense (design §13–15). Runs in GameSimulation before
    // AreaFinanceSystem takes the batch, on the same 256-frame cadence; elapsed frames are capped so a time jump or a
    // load never back-charges, and the first update after a load only sets the baseline.
    public partial class AreaPolicyUpkeepSystem : GameSystemBase
    {
        public const uint UpdateFrames = AreaFinanceModel.FramesPerDay / 1024;
        private readonly List<AreaPolicyUpkeepCharge> charges = new List<AreaPolicyUpkeepCharge>();
        private AreaPolicySystem policies;
        private AreaFinanceSystem finance;
        private SimulationSystem simulation;
        private AreaLoadCoordinatorSystem load;
        private EntityQuery areas;
        private uint lastFrame;
        private int loadGeneration, startedGeneration = -1;
        private CS2AreaSystem.Core.Diagnostics.AreaFailureLog failures;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            policies = World.GetOrCreateSystemManaged<AreaPolicySystem>();
            finance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            simulation = World.GetOrCreateSystemManaged<SimulationSystem>();
            load = World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>();
            failures = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>().Failures;
            areas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>(), ComponentType.ReadOnly<ManagedAreaPolicyEntry>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            loadGeneration++;
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (GameManager.instance.isGameLoading || !load.Ready) return;
            uint frame = simulation.frameIndex;
            if (startedGeneration != loadGeneration)
            {
                startedGeneration = loadGeneration;
                lastFrame = frame;
                policies.ClearUpkeep();
                return;
            }
            uint elapsed = AreaFinanceModel.ElapsedFrames(lastFrame, frame, UpdateFrames * 2);
            lastFrame = frame;
            if (elapsed == 0 || areas.IsEmptyIgnoreFilter) return;
            using (var entities = areas.ToEntityArray(Allocator.Temp))
            {
                foreach (var area in entities)
                {
                    charges.Clear();
                    try { policies.SettleUpkeep(area, elapsed, charges); failures.Recovered(area, "Policy.Upkeep"); }
                    catch (System.Exception error)
                    {
                        // One Area's failure leaves the other Areas billed and running.
                        failures.Report(area, "Policy.Upkeep", error, "Area policy upkeep could not be settled for managed area " + area + "; others continue");
                        continue;
                    }
                    foreach (var charge in charges) finance.EnqueueExpense(charge.AreaStableId, AreaFinanceKind.PolicyUpkeep, charge.Amount);
                }
            }
        }
    }
}
