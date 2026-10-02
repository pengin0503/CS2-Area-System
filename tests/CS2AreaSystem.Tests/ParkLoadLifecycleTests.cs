using System.Reflection;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using Game;
using Game.SceneFlow;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;

public class ParkLoadLifecycleTests {
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MethodInfo OwnMethod<T>(string name) => typeof(T).GetMethod(name, Fields | BindingFlags.DeclaredOnly)!;
    private static FieldInfo Field<T>(string name) => typeof(T).GetField(name, Fields)!;
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
    [Test] public void FinancePreloadCompletesThenRecordsBeforeReleasingLocalState() {
        var preload = OwnMethod<ParkFinanceSystem>("OnGamePreload");
        Assert.That(preload, Is.Not.Null, "load must drain before deserialization even while runtime is unready");
        var calls = IlCallScanner.Find(preload, _ => true);
        Assert.That(calls.FindIndex(c => c.Target.Name == "Complete"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "RecordResults")));
        Assert.That(calls.FindIndex(c => c.Target.Name == "RecordResults"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "DropPending")));
    }
    [Test] public void FinanceCompletesPendingWorkBeforeAnyReadinessReturn() {
        var calls = IlCallScanner.Find(OwnMethod<ParkFinanceSystem>("OnUpdate"), _ => true);
        Assert.That(calls.FindIndex(c => c.Target.Name == "Complete"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "get_Ready")));
        Assert.That(calls.Any(c => c.Target.Name == "DropPending"), Is.True, "the unready path must discard local state");
    }
    [Test] public void FinanceDiscardDropsQueuedAndInFlightAdmissionsWithoutRemovingListeners() {
        var finance = new ParkFinanceSystem();
        var ledger = (ParkTransactionLedger)Field<ParkFinanceSystem>("ledger").GetValue(finance)!;
        ledger.EnqueueAdmission(new ParkAdmissionRequest { ParkId = 7, Fee = 20 });
        Field<ParkFinanceSystem>("inFlight").SetValue(finance, new ParkTransactionBatch());
        Field<ParkFinanceSystem>("loadGeneration").SetValue(finance, 4);
        var drop = OwnMethod<ParkFinanceSystem>("DropPending");
        Assert.That(drop, Is.Not.Null);
        // Unity's JobHandle.Complete cannot execute in .NET. The compiled preload test above pins completion before
        // this actual managed cleanup; allocated native-buffer release still requires the in-game job-safety scenario.
        drop.Invoke(finance, null);
        Assert.That(ledger.HasPending, Is.False);
        Assert.That(Field<ParkFinanceSystem>("inFlight").GetValue(finance), Is.Null);
        foreach (var name in new[] { "requests", "collected" }) {
            var buffer = Field<ParkFinanceSystem>(name).GetValue(finance)!;
            Assert.That(buffer.GetType().GetProperty("IsCreated")!.GetValue(buffer), Is.False);
        }
        Assert.That(IlCallScanner.Find(OwnMethod<ParkFinanceSystem>("OnGamePreload"), c => c.Name == "RemoveListener"), Is.Empty,
            "listeners survive a new city; owners remove them only at destruction");
        Assert.That(IlCallScanner.Find(drop, c => c.Name == "DisposeBuffers"), Has.Count.EqualTo(1));
    }
    [Test] public void VisitPreloadCompletesBeforeResetAndDisposesBothContainers() {
        var preload = OwnMethod<ParkVisitSystem>("OnGamePreload");
        Assert.That(preload, Is.Not.Null);
        var calls = IlCallScanner.Find(preload, _ => true);
        Assert.That(calls.FindIndex(c => c.Target.Name == "Complete"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "ResetVisits")));
        Assert.That(calls.Count(c => c.Target.Name == "Dispose"), Is.EqualTo(2), "persistent matches and target entity keys belong to the old city");
        Assert.That(calls.Any(c => c.Target.Name == "SeedPreExistingUntil"), Is.False, "seed uses the new city's first ready frame");
    }
    [Test] public void VisitCompletesAndDiscardsBeforeAnUnreadyReturn() {
        var calls = IlCallScanner.Find(OwnMethod<ParkVisitSystem>("OnUpdate"), _ => true);
        Assert.That(calls.FindIndex(c => c.Target.Name == "Complete"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "get_Ready")));
        Assert.That(calls.FindIndex(c => c.Target.Name == "DropPending"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "ConsumeMatches")));
        Assert.That(calls.Count(c => c.Target.Name == "SeedPreExistingUntil"), Is.EqualTo(1));
    }
    [Test] public void VisitResetDropsOldSessionsEventsTargetsAndCachesBeforeRestoredSeed() {
        var visits = new ParkVisitSystem();
        var citizen = new Entity { Index = 12, Version = 1 };
        var events = (List<ParkVisitEvent>)Field<ParkVisitSystem>("events").GetValue(visits)!;
        visits.Tracker.Observe(citizen, 7, false, 50, 100, events);
        Field<ParkVisitSystem>("hasPending").SetValue(visits, true);
        Field<ParkVisitSystem>("activeSignature").SetValue(visits, "7,");
        var reset = OwnMethod<ParkVisitSystem>("ResetVisits");
        Assert.That(reset, Is.Not.Null);
        reset.Invoke(visits, null);
        Assert.That(visits.Tracker.ActiveCount(7), Is.Zero); Assert.That(events, Is.Empty);
        Assert.That(Field<ParkVisitSystem>("hasPending").GetValue(visits), Is.False);
        Assert.That(Field<ParkVisitSystem>("activeSignature").GetValue(visits), Is.EqualTo(""));
        Assert.That(Field<ParkVisitSystem>("targetsDirty").GetValue(visits), Is.True);
        visits.Tracker.SeedPreExistingUntil(500);
        visits.Tracker.Observe(citizen, 7, false, 50, 200, events);
        visits.Tracker.Observe(citizen, 7, false, 50, 300, events);
        Assert.That(events.Count(e => e.Kind == ParkVisitEventKind.Started && e.FeeSatisfied), Is.EqualTo(1));
        visits.Tracker.Observe(new Entity { Index = 13, Version = 1 }, 7, false, 50, 600, events);
        Assert.That(events.Count(e => e.Kind == ParkVisitEventKind.Started && !e.FeeSatisfied), Is.EqualTo(1));
        Assert.That(IlCallScanner.Find(reset, c => c.Name == "RemoveListener"), Is.Empty);
    }
}
