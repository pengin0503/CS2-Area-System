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
}
