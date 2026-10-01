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
}
