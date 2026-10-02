using System.Reflection;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using Game;
using Game.SceneFlow;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;

public class ParkLoadLifecycleTests {
    private sealed class Runtime : ParkRuntimeSystem {
        public void Preload() => OnGamePreload(default, default);
    }
    [Test] public void NativeSystemCreationSubscribesToPreloadWithoutUpdateRegistration() {
        var create = typeof(GameSystemBase).GetMethod("OnCreate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var calls = IlCallScanner.Find(create, _ => true);
        Assert.That(calls.Count(c => c.Target.DeclaringType == typeof(GameManager) && c.Target.Name == "add_onGamePreload"), Is.EqualTo(1));
        Assert.That(calls.Any(c => c.Target.DeclaringType == typeof(UpdateSystem)), Is.False);
    }
    [Test] public void RuntimeClearsSnapshotsAndAdvancesOnlyAtEachPreloadBoundary() {
        var runtime = new Runtime();
        for (int load = 1; load <= 3; load++) {
            runtime.Store.GetOrCreate(7, new Entity { Index = load, Version = 1 });
            Assert.That(runtime.Store.All.Count(), Is.EqualTo(1));
            runtime.Preload();
            Assert.That(runtime.LoadGeneration, Is.EqualTo(load), "A → B → new city use the same single owner");
            Assert.That(runtime.Store.All, Is.Empty);
            Assert.That(runtime.LoadGeneration, Is.EqualTo(load), "reading runtime state does not advance generation");
        }
    }
}
