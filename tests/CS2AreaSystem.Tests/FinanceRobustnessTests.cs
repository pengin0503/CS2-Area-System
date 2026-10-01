using CS2AreaSystem.Core;
using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Core.Jobs;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #58: native containers are only released for a job that was never scheduled or has completed.
public class ScheduledJobTests {
    private sealed class FakeJob { public bool Completed; public readonly List<string> Log = new(); }
    [Test] public void FailureBeforeScheduleLeavesNothingToComplete() {
        var log = new List<string>();
        var outcome = ScheduledJob.Run<FakeJob>(() => throw new InvalidOperationException("alloc"), _ => log.Add("register"), j => j.Completed = true, out var job, out var error);
        Assert.That(outcome, Is.EqualTo(ScheduledJobOutcome.NotScheduled));
        Assert.That(job, Is.Null); Assert.That(error!.Message, Is.EqualTo("alloc")); Assert.That(log, Is.Empty);
    }
    [Test] public void RegistrationFailureAfterScheduleCompletesTheJobBeforeReturning() {
        var scheduled = new FakeJob();
        var outcome = ScheduledJob.Run(() => scheduled, j => { j.Log.Add("register"); throw new InvalidOperationException("AddWriter"); },
            j => { j.Log.Add("complete"); j.Completed = true; }, out var job, out var error);
        Assert.That(outcome, Is.EqualTo(ScheduledJobOutcome.CompletedAfterRegistrationFailure));
        Assert.That(job, Is.SameAs(scheduled)); Assert.That(job.Completed, Is.True, "containers may be released only after this");
        Assert.That(job.Log, Is.EqualTo(new[] { "register", "complete" }));
        Assert.That(error!.Message, Is.EqualTo("AddWriter"));
    }
    [Test] public void SuccessfulScheduleIsNotCompletedOnTheMainThread() {
        var scheduled = new FakeJob();
        var outcome = ScheduledJob.Run(() => scheduled, j => j.Log.Add("register"), j => j.Completed = true, out _, out var error);
        Assert.That(outcome, Is.EqualTo(ScheduledJobOutcome.Scheduled));
        Assert.That(scheduled.Completed, Is.False); Assert.That(error, Is.Null);
    }
    [Test] public void FinanceBatchCompletedAfterRegistrationFailureIsRecordedOnceAndNotRequeued() {
        var settled = new List<AreaFinanceBatch>();
        var runtime = new AreaFinanceRuntime(_ => { });
        runtime.AddListener(new Listener(settled));
        runtime.Start(0);
        runtime.EnqueueExpense(1, AreaFinanceKind.ParkManagement, 30);
        Assert.That(runtime.BeginApply(10), Is.Not.Null);
        // The system records the completed job's result instead of AbortApply.
        runtime.CompleteApply(30, 0);
        Assert.That(runtime.HasQueued, Is.False, "the charged batch is not queued again");
        Assert.That(runtime.HasInFlight, Is.False);
        Assert.That(runtime.BeginApply(20), Is.Null);
        Assert.That(settled, Has.Count.EqualTo(1));
        Assert.That(runtime.Settled(AreaFinanceKind.ParkManagement, 10), Is.EqualTo(30));
    }
    [Test] public void SystemsUseTheGuardForEveryPostScheduleRegistration() {
        string Source(string path) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/" + path));
        var finance = Source("Core/Finance/AreaFinanceSystem.cs");
        Assert.That(finance, Does.Contain("ScheduledJob.Run(() => job.Schedule(dependency), handle => { Dependency = handle; statistics.AddWriter(handle); }"));
        Assert.That(finance, Does.Not.Contain("statistics.AddWriter(pending)"));
        var industrial = Source("Modules/Industrial/IndustrialObservationSystem.cs");
        Assert.That(industrial, Does.Contain("resourceSystem.AddPrefabsReader(handle)"));
        Assert.That(industrial, Does.Not.Contain("resourceSystem.AddPrefabsReader(pending)"));
    }
    private sealed class Listener : IAreaFinanceListener {
        private readonly List<AreaFinanceBatch> into; public Listener(List<AreaFinanceBatch> into) => this.into = into;
        public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame) => into.Add(settled);
    }
}
// #75: one failing record never drops the other recorded admissions and never repeats the transfer.
public class ParkAdmissionRecordingTests {
    private static ParkAdmissionOutcome O(ulong park, long fee, long collected) =>
        new ParkAdmissionOutcome { Request = new ParkAdmissionRequest { ParkId = park, Fee = fee, Area = new Entity { Index = (int)park, Version = 1 } }, Collected = collected };
    private sealed class Sink : IParkAdmissionRecordSink {
        public readonly List<string> Records = new();
        public ulong ThrowOnFee, ThrowOnRecent;
        public void RecordRecent(in ParkAdmissionOutcome o) { if (o.Request.ParkId == ThrowOnRecent) throw new InvalidOperationException("snapshot"); Records.Add("recent:" + o.Request.ParkId); }
        public void RecordServiceFee(in ParkAdmissionOutcome o) { if (o.Request.ParkId == ThrowOnFee) throw new InvalidOperationException("fee"); Records.Add("fee:" + o.Request.ParkId + "=" + o.Collected); }
        public void RecordLifetimeRevenue(in ParkAdmissionOutcome o) => Records.Add("lifetime:" + o.Request.ParkId);
    }
    [Test] public void MiddleOutcomeFailureStillRecordsTheOthers() {
        var sink = new Sink { ThrowOnFee = 2 };
        var reports = new List<string>();
        int failures = ParkAdmissionRecording.RecordAll(new[] { O(1, 10, 10), O(2, 10, 10), O(3, 10, 4) }, sink, (e, o, what) => reports.Add(o.Request.ParkId + ":" + what));
        Assert.That(failures, Is.EqualTo(1));
        Assert.That(reports, Is.EqualTo(new[] { "2:Area Service Fees" }), "the failed outcome is diagnosed");
        Assert.That(sink.Records, Is.EqualTo(new[] { "recent:1", "fee:1=10", "lifetime:1", "recent:2", "lifetime:2", "recent:3", "fee:3=4", "lifetime:3" }));
        Assert.That(sink.Records.Count(r => r.StartsWith("fee:")), Is.EqualTo(2), "each collected fee is recorded at most once");
    }
    [Test] public void SnapshotFailureDoesNotSkipTheServiceFeeOfTheSameOutcome() {
        var sink = new Sink { ThrowOnRecent = 1 };
        ParkAdmissionRecording.RecordAll(new[] { O(1, 10, 10) }, sink, (_, _, _) => { });
        Assert.That(sink.Records, Is.EqualTo(new[] { "fee:1=10", "lifetime:1" }));
    }
    [Test] public void UncollectedOutcomeOnlyRecordsStatistics() {
        var sink = new Sink();
        ParkAdmissionRecording.RecordAll(new[] { O(1, 10, 0) }, sink, (_, _, _) => { });
        Assert.That(sink.Records, Is.EqualTo(new[] { "recent:1" }));
    }
    [Test] public void RecordingSinkHasNoMoneyPath() {
        Assert.That(typeof(IParkAdmissionRecordSink).GetMethods().Select(m => m.Name), Is.EquivalentTo(new[] { "RecordRecent", "RecordServiceFee", "RecordLifetimeRevenue" }));
    }
}
// #76: listener ownership is explicit and failure isolation is kept.
public class ListenerOwnershipTests {
    private sealed class L : IAreaFinanceListener {
        public int Calls; public bool Throw; public Action? During;
        public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame) { Calls++; During?.Invoke(); if (Throw) throw new InvalidOperationException(); }
    }
    private static void Settle(AreaFinanceRuntime runtime) { runtime.EnqueueExpense(1, AreaFinanceKind.ParkManagement, 5); runtime.BeginApply(1); runtime.CompleteApply(5, 0); }
    [Test] public void DuplicateRegistrationIsIgnoredAndRemovedListenerGetsNoCallback() {
        var runtime = new AreaFinanceRuntime(_ => { }); runtime.Start(0);
        var a = new L(); var b = new L();
        Assert.That(runtime.AddListener(a), Is.True); Assert.That(runtime.AddListener(a), Is.False); runtime.AddListener(b);
        Settle(runtime);
        Assert.That(a.Calls, Is.EqualTo(1)); Assert.That(b.Calls, Is.EqualTo(1));
        Assert.That(runtime.RemoveListener(a), Is.True); Assert.That(runtime.RemoveListener(a), Is.False);
        Settle(runtime);
        Assert.That(a.Calls, Is.EqualTo(1)); Assert.That(b.Calls, Is.EqualTo(2));
    }
    [Test] public void FailingListenerDoesNotStopOthersAndRemovalDuringCallbackIsSafe() {
        var errors = new List<Exception>();
        var runtime = new AreaFinanceRuntime(errors.Add); runtime.Start(0);
        var failing = new L { Throw = true }; var other = new L();
        failing.During = () => runtime.RemoveListener(failing);
        runtime.AddListener(failing); runtime.AddListener(other);
        Settle(runtime);
        Assert.That(errors, Has.Count.EqualTo(1)); Assert.That(other.Calls, Is.EqualTo(1));
        Settle(runtime);
        Assert.That(failing.Calls, Is.EqualTo(1)); Assert.That(other.Calls, Is.EqualTo(2));
    }
    [Test] public void ListenerSetKeepsOrderAndSnapshotsDuringIteration() {
        var set = new ListenerSet<string>();
        set.Add("a"); set.Add("b"); set.Add("c");
        var snapshot = set.Items;
        set.Remove("b"); set.Add("d");
        Assert.That(snapshot, Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(set.Items, Is.EqualTo(new[] { "a", "c", "d" }));
        Assert.That(set.Add(null!), Is.False); Assert.That(set.Remove("x"), Is.False);
    }
    [Test] public void ListenersUnsubscribeWhenTheirSystemIsDestroyed() {
        string Source(string path) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/" + path));
        var finance = Source("Modules/Park/ParkFinanceSystem.cs");
        var onDestroy = finance.Substring(finance.IndexOf("protected override void OnDestroy()"));
        Assert.That(onDestroy, Does.Contain("RemoveListener(this)"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(onDestroy.Substring(0, onDestroy.IndexOf("base.OnDestroy()")), "RemoveListener\\(this\\)").Count, Is.EqualTo(2));
        var progression = Source("Modules/Park/ParkProgressionSystem.cs");
        Assert.That(progression, Does.Contain("RemoveListener(this)"));
    }
}
