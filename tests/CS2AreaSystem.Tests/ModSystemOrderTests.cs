using CS2AreaSystem.Core.Prefabs;
using CS2AreaSystem.Tests.Support;
using Game;
using Game.Common;
using Game.UI;
using Game.UI.InGame;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// AreaToolbarUnlockSystem's Unlock event is only useful if ToolbarUISystem runs after it in the same frame: Events are
// destroyed by the Cleanup phase at the end of that frame. These tests read the compiled schedules (the supplied
// 1.6.2f1 Game.SystemOrder and Mod.OnLoad) instead of trusting comments; they cannot run the game loop itself.
public class ModSystemOrderTests {
    [Test] public void ParkEntrancesRunBeforeActivationIndependentlyOfFinance() {
        var order=Schedule(SystemUpdatePhase.ModificationEnd);
        Assert.That(order.IndexOf(typeof(CS2AreaSystem.Modules.Park.ParkEntranceSystem)),Is.GreaterThan(order.IndexOf(typeof(CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem))));
        Assert.That(order.IndexOf(typeof(CS2AreaSystem.Modules.Park.ParkEntranceSystem)),Is.LessThan(order.IndexOf(typeof(CS2AreaSystem.Core.Anchors.AreaActivationSystem))));
        Assert.That(ModCalls.Count(c=>c.System==typeof(CS2AreaSystem.Modules.Park.ParkEntranceSystem)),Is.EqualTo(1));
    }
    private static readonly List<UpdateSystemCall> Vanilla = UpdateSystemCallReader.Read(typeof(SystemOrder));
    private static readonly List<UpdateSystemCall> ModCalls = UpdateSystemCallReader.Read(typeof(Mod));
    private static UpdateSystemCall VanillaRegistration<T>() => Vanilla.Single(c => c.System == typeof(T));
    private static readonly List<UpdateSystemCall> ModExecution = UpdateSystemCallReader.ReadExecution(typeof(Mod).GetMethod(nameof(Mod.OnLoad))!);
    private static readonly List<UpdateSystemCall> VanillaExecution = UpdateSystemCallReader.ReadExecution(typeof(SystemOrder).GetMethod(nameof(SystemOrder.Initialize))!);
    internal static List<Type> Schedule(SystemUpdatePhase phase) => UpdateSchedule.Resolve(phase, VanillaExecution, ModExecution);
    [Test] public void VanillaToolbarRebindRunsInsideMainLoopBeforeEventsAreDestroyed() {
        Assert.That(Vanilla, Has.Count.GreaterThan(500), "the whole vanilla schedule was read");
        Assert.That(VanillaRegistration<ToolbarUISystem>(), Is.EqualTo(new UpdateSystemCall("UpdateAt", typeof(ToolbarUISystem), null, SystemUpdatePhase.UIUpdate)));
        Assert.That(UpdateSystemCallReader.Read(typeof(UIUpdateSystem)), Is.EqualTo(new[] { new UpdateSystemCall("Update", null, null, SystemUpdatePhase.UIUpdate) }),
            "UIUpdateSystem drives the UIUpdate phase");
        Assert.That(VanillaRegistration<UIUpdateSystem>(), Is.EqualTo(new UpdateSystemCall("UpdateAt", typeof(UIUpdateSystem), null, SystemUpdatePhase.MainLoop)));
        // UpdateAfter sorts after every UpdateAt of the phase, mod registrations included: Events are collected last
        // in MainLoop and destroyed in Cleanup, so nothing later in the frame can observe them.
        Assert.That(VanillaRegistration<PrepareCleanUpSystem>(), Is.EqualTo(new UpdateSystemCall("UpdateAfter", typeof(PrepareCleanUpSystem), null, SystemUpdatePhase.MainLoop)));
        Assert.That(VanillaRegistration<CleanUpSystem>(), Is.EqualTo(new UpdateSystemCall("UpdateAt", typeof(CleanUpSystem), null, SystemUpdatePhase.Cleanup)));
    }
    [Test] public void ToolbarUnlockIsPublishedImmediatelyBeforeToolbarUISystem() {
        var registrations = ModCalls.Where(c => c.System == typeof(AreaToolbarUnlockSystem)).ToList();
        // A MainLoop UpdateAt sorts after UIUpdateSystem (registered first by the game), so ToolbarUISystem never saw the event.
        Assert.That(registrations, Is.EqualTo(new[] { new UpdateSystemCall("UpdateBefore", typeof(AreaToolbarUnlockSystem), typeof(ToolbarUISystem), VanillaRegistration<ToolbarUISystem>().Phase) }),
            "UpdateBefore only takes effect in the phase where the anchor itself is registered");
    }
    [Test] public void ExecutionReaderFollowsModRegistrationHelpersInCallOrder() {
        Assert.That(ModExecution.Select(c => c.System), Is.EquivalentTo(ModCalls.Select(c => c.System)), "every compiled registration is replayed once");
        int lifecycle = ModExecution.FindIndex(c => c.System == typeof(CS2AreaSystem.Core.Areas.ManagedAreaLifecycleSystem));
        int park = ModExecution.FindIndex(c => c.System == typeof(CS2AreaSystem.Modules.Park.ParkVisitSystem));
        int ui = ModExecution.FindIndex(c => c.System == typeof(CS2AreaSystem.Core.UI.AreaManagementUISystem));
        Assert.That(lifecycle, Is.LessThan(park)); Assert.That(park, Is.LessThan(ui));
    }
    [Test] public void ScheduleReplayMatchesKnownVanillaAnchors() {
        var ui = Schedule(SystemUpdatePhase.UIUpdate);
        Assert.That(ui.IndexOf(typeof(AreaToolbarUnlockSystem)), Is.EqualTo(ui.IndexOf(typeof(ToolbarUISystem)) - 1));
    }
    [Test] public void PolicyBootstrapRunsOnceAfterLoadCoordinatorInModificationEnd() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Policies.AreaPolicyBootstrapSystem)).Select(c => c.Phase),
            Is.EqualTo(new[] { SystemUpdatePhase.ModificationEnd }));
        var order = Schedule(SystemUpdatePhase.ModificationEnd);
        int coordinator = order.IndexOf(typeof(CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem));
        int lifecycle = order.IndexOf(typeof(CS2AreaSystem.Core.Areas.ManagedAreaLifecycleSystem));
        int bootstrap = order.IndexOf(typeof(CS2AreaSystem.Core.Policies.AreaPolicyBootstrapSystem));
        Assert.That(coordinator, Is.GreaterThanOrEqualTo(0)); Assert.That(lifecycle, Is.GreaterThanOrEqualTo(0));
        Assert.That(bootstrap, Is.GreaterThan(coordinator), "load readiness is known before buffers are created");
        Assert.That(bootstrap, Is.GreaterThan(lifecycle), "ManagedArea exists before its policy buffer is added");
    }
    [Test] public void PolicyRuntimeRunsAfterBootstrapAndLifecycleActivationInModificationEnd() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Policies.AreaPolicySystem)).Select(c => c.Phase),
            Is.EqualTo(new[] { SystemUpdatePhase.ModificationEnd }));
        var order = Schedule(SystemUpdatePhase.ModificationEnd);
        int runtime = order.IndexOf(typeof(CS2AreaSystem.Core.Policies.AreaPolicySystem));
        Assert.That(runtime, Is.GreaterThan(order.IndexOf(typeof(CS2AreaSystem.Core.Policies.AreaPolicyBootstrapSystem))));
        Assert.That(runtime, Is.GreaterThan(order.IndexOf(typeof(CS2AreaSystem.Core.Anchors.AreaActivationSystem))), "current lifecycle state is resolved first");
        Assert.That(order.IndexOf(typeof(CS2AreaSystem.Core.Anchors.AreaActivationSystem)), Is.GreaterThanOrEqualTo(0));
    }
    [Test] public void AreaFinanceAppliesAfterEveryGameSimulationProducer() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Finance.AreaFinanceSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateAfter", typeof(CS2AreaSystem.Core.Finance.AreaFinanceSystem), null, SystemUpdatePhase.GameSimulation) }));
        var order = Schedule(SystemUpdatePhase.GameSimulation);
        int finance = order.IndexOf(typeof(CS2AreaSystem.Core.Finance.AreaFinanceSystem));
        Assert.That(finance, Is.GreaterThan(order.IndexOf(typeof(CS2AreaSystem.Modules.Park.ParkFinanceSystem))), "Park charges are queued first");
        Assert.That(order.IndexOf(typeof(CS2AreaSystem.Modules.Park.ParkFinanceSystem)), Is.GreaterThanOrEqualTo(0));
        var producers = ModCalls.Where(c => c.Phase == SystemUpdatePhase.GameSimulation && c.Method == "UpdateAt").Select(c => c.System!).ToList();
        Assert.That(producers, Is.Not.Empty);
        foreach (var producer in producers) Assert.That(finance, Is.GreaterThan(order.IndexOf(producer)), producer.Name);
        Assert.That(finance, Is.GreaterThan(order.IndexOf(typeof(Game.Simulation.BudgetApplySystem))), "same frame as the vanilla budget apply, after it");
    }
    [Test] public void AreaFinanceSaveFlushRunsBeforeTheSerializer() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateBefore", typeof(CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem), typeof(Game.Serialization.SerializerSystem), SystemUpdatePhase.Serialize) }));
        var order = Schedule(SystemUpdatePhase.Serialize);
        int flush = order.IndexOf(typeof(CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem));
        Assert.That(flush, Is.GreaterThanOrEqualTo(0));
        Assert.That(flush, Is.LessThan(order.IndexOf(typeof(Game.Serialization.SerializerSystem))));
    }
    [Test] public void ParkFinanceRecordsAdmissionsBeforeAreaFinanceFlushesBeforeTheSerializer() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Modules.Park.ParkFinanceSaveSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateBefore", typeof(CS2AreaSystem.Modules.Park.ParkFinanceSaveSystem), typeof(CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem), SystemUpdatePhase.Serialize) }));
        var order = Schedule(SystemUpdatePhase.Serialize);
        int park = order.IndexOf(typeof(CS2AreaSystem.Modules.Park.ParkFinanceSaveSystem));
        int area = order.IndexOf(typeof(CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem));
        Assert.That(park, Is.GreaterThanOrEqualTo(0));
        Assert.That(park, Is.LessThan(area), "admission income is recorded and Park counters exist before Area finance settles");
        Assert.That(area, Is.LessThan(order.IndexOf(typeof(Game.Serialization.SerializerSystem))));
    }
    [Test] public void PolicyUpkeepIsQueuedBeforeAreaFinanceAppliesTheBatch() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Policies.AreaPolicyUpkeepSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateAt", typeof(CS2AreaSystem.Core.Policies.AreaPolicyUpkeepSystem), null, SystemUpdatePhase.GameSimulation) }));
        var order = Schedule(SystemUpdatePhase.GameSimulation);
        int upkeep = order.IndexOf(typeof(CS2AreaSystem.Core.Policies.AreaPolicyUpkeepSystem));
        Assert.That(upkeep, Is.GreaterThanOrEqualTo(0));
        Assert.That(upkeep, Is.LessThan(order.IndexOf(typeof(CS2AreaSystem.Core.Finance.AreaFinanceSystem))));
        // Policy state is resolved on demand from the saved buffer; AreaPolicySystem itself runs earlier, in ModificationEnd.
        Assert.That(Schedule(SystemUpdatePhase.ModificationEnd), Does.Contain(typeof(CS2AreaSystem.Core.Policies.AreaPolicySystem)));
    }
    [Test] public void IndustrialPolicyEffectsAreSyncedBeforeIndustrialObservation() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Modules.Industrial.IndustrialPolicyEffectSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateBefore", typeof(CS2AreaSystem.Modules.Industrial.IndustrialPolicyEffectSystem),
                typeof(CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem), SystemUpdatePhase.GameSimulation) }));
        var order = Schedule(SystemUpdatePhase.GameSimulation);
        int effects = order.IndexOf(typeof(CS2AreaSystem.Modules.Industrial.IndustrialPolicyEffectSystem));
        Assert.That(effects, Is.GreaterThanOrEqualTo(0));
        Assert.That(effects, Is.LessThan(order.IndexOf(typeof(CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem))),
            "an observation cycle started in the same frame sees the applied vanilla state");
    }
    [Test] public void AreaBudgetIsPublishedInTheUIUpdatePhaseOnly() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Finance.AreaBudgetBindingSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateAfter", typeof(CS2AreaSystem.Core.Finance.AreaBudgetBindingSystem), null, SystemUpdatePhase.UIUpdate) }));
        Assert.That(Schedule(SystemUpdatePhase.UIUpdate), Does.Contain(typeof(Game.UI.InGame.BudgetUISystem)));
    }
    // Regression snapshot of the relative order of every mod system per phase (captured before the lifecycle split; the
    // only intended change is that PreTool now runs ManagedAreaToolGuardSystem instead of ManagedAreaLifecycleSystem).
    [TestCase(SystemUpdatePhase.PrefabUpdate, "AreaPrefabRegistrationSystem")]
    [TestCase(SystemUpdatePhase.PreTool, "ManagedAreaToolGuardSystem")]
    [TestCase(SystemUpdatePhase.ToolUpdate, "AreaLabelSystem")]
    [TestCase(SystemUpdatePhase.ModificationEnd, "ManagedAreaOverlapSystem,ManagedAreaLifecycleSystem,AreaPlacementSystem,AreaLoadCoordinatorSystem,AreaPolicyBootstrapSystem,AreaMembershipSystem,ParkEntranceSystem,ParkConnectivitySystem,AreaActivationSystem,AreaPolicySystem,ParkRuntimeSystem,ParkLandscapeSystem,IndustrialRuntimeSystem")]
    [TestCase(SystemUpdatePhase.GameSimulation, "AreaPolicyUpkeepSystem,ParkVisitSystem,ParkQualitySystem,ParkFinanceSystem,ParkProgressionSystem,IndustrialPolicyEffectSystem,IndustrialObservationSystem,IndustrialProgressionSystem,AreaFinanceSystem")]
    [TestCase(SystemUpdatePhase.Serialize, "ParkFinanceSaveSystem,AreaFinanceSaveSystem")]
    [TestCase(SystemUpdatePhase.UIUpdate, "AreaToolbarUnlockSystem,AreaManagementUISystem,AreaBudgetBindingSystem")]
    public void ModSystemOrderPerPhaseIsUnchanged(SystemUpdatePhase phase, string expected) {
        var ours = Schedule(phase).Where(t => t.Assembly == typeof(Mod).Assembly).Select(t => t.Name);
        Assert.That(string.Join(",", ours), Is.EqualTo(expected));
    }
    // A new Area is named and selected in the same ModificationEnd that gives it its identity.
    [Test] public void PlacementRunsAfterLifecycleInModificationEnd() {
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Labels.AreaPlacementSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateAfter", typeof(CS2AreaSystem.Core.Labels.AreaPlacementSystem),
                typeof(CS2AreaSystem.Core.Areas.ManagedAreaLifecycleSystem), SystemUpdatePhase.ModificationEnd) }));
    }
    // The label click overrides the selection DefaultToolSystem made from the same press, so it must run after it.
    [Test] public void LabelsRunAfterTheDefaultToolInToolUpdate() {
        Assert.That(VanillaRegistration<Game.Tools.DefaultToolSystem>(), Is.EqualTo(new UpdateSystemCall("UpdateAt", typeof(Game.Tools.DefaultToolSystem), null, SystemUpdatePhase.ToolUpdate)));
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Labels.AreaLabelSystem)),
            Is.EqualTo(new[] { new UpdateSystemCall("UpdateAfter", typeof(CS2AreaSystem.Core.Labels.AreaLabelSystem), typeof(Game.Tools.DefaultToolSystem), SystemUpdatePhase.ToolUpdate) }));
        var order = Schedule(SystemUpdatePhase.ToolUpdate);
        Assert.That(order.IndexOf(typeof(CS2AreaSystem.Core.Labels.AreaLabelSystem)), Is.GreaterThan(order.IndexOf(typeof(Game.Tools.DefaultToolSystem))));
    }
    [Test] public void EveryModSystemIsRegisteredInExactlyOnePhase() {
        var phases = new HashSet<SystemUpdatePhase> { SystemUpdatePhase.PrefabUpdate, SystemUpdatePhase.PreTool, SystemUpdatePhase.ModificationEnd,
            SystemUpdatePhase.GameSimulation, SystemUpdatePhase.Serialize, SystemUpdatePhase.UIUpdate, SystemUpdatePhase.ToolUpdate };
        foreach (var group in ModCalls.GroupBy(c => c.System))
            Assert.That(group.Select(c => c.Phase).Distinct().Count(), Is.EqualTo(1), group.Key!.Name);
        Assert.That(ModCalls.Select(c => c.Phase).Distinct(), Is.SubsetOf(phases));
    }
    [Test] public void ParkLoadGenerationOwnerIsRegisteredOnceBeforeLandscape() {
        var runtime = typeof(CS2AreaSystem.Modules.Park.ParkRuntimeSystem);
        Assert.That(ModCalls.Where(c => c.System == runtime).Select(c => c.Phase), Is.EqualTo(new[] { SystemUpdatePhase.ModificationEnd }));
        var order = Schedule(SystemUpdatePhase.ModificationEnd);
        Assert.That(order.IndexOf(runtime), Is.GreaterThan(order.IndexOf(typeof(CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem))));
        Assert.That(order.IndexOf(runtime), Is.LessThan(order.IndexOf(typeof(CS2AreaSystem.Modules.Park.ParkLandscapeSystem))));
    }
    [Test] public void NativeAreaGeometryAndSearchRunBeforeLoadRecovery() {
        Assert.That(VanillaRegistration<Game.Areas.GeometrySystem>().Phase, Is.EqualTo(SystemUpdatePhase.Modification2B));
        Assert.That(VanillaRegistration<Game.Areas.SearchSystem>().Phase, Is.EqualTo(SystemUpdatePhase.Modification5));
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem)).Select(c => c.Phase),
            Is.EqualTo(new[] { SystemUpdatePhase.ModificationEnd }));
    }
    // #52: PreTool runs before the tool writes its definitions; structural Area identity work belongs to ModificationEnd.
    [Test] public void ToolGuardIsTheOnlyPreToolSystemAndLifecycleRunsInModificationEndOnly() {
        Assert.That(ModCalls.Where(c => c.Phase == SystemUpdatePhase.PreTool).Select(c => c.System),
            Is.EqualTo(new[] { typeof(CS2AreaSystem.Core.Areas.ManagedAreaToolGuardSystem) }));
        Assert.That(ModCalls.Where(c => c.System == typeof(CS2AreaSystem.Core.Areas.ManagedAreaLifecycleSystem)).Select(c => c.Phase),
            Is.EqualTo(new[] { SystemUpdatePhase.ModificationEnd }));
    }
}
