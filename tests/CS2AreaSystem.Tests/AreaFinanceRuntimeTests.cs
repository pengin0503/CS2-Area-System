using CS2AreaSystem.Core.Finance;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 2 C4 Task 0: every request batch reports exactly one completion, including zero settlement and aborts, so module
// pending state (support rosters, event requests) can never stay stuck waiting for a positive settlement callback.
public class AreaFinanceCompletionTests {
 private sealed class Completion {
  public readonly List<(AreaFinanceBatch Requested, AreaFinanceBatch Settled, uint Frame)> Calls = new();
 }
 private sealed class RequestListener : IAreaFinanceRequestListener {
  public readonly Completion Log = new();
  public Action OnCall;
  public void OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame) { OnCall?.Invoke(); Log.Calls.Add((requested, settled, frame)); }
 }
 private sealed class SettledListener : IAreaFinanceListener {
  public readonly List<AreaFinanceBatch> Batches = new();
  public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame) => Batches.Add(settled);
 }
 private static AreaFinanceRuntime Runtime(RequestListener listener, List<Exception> errors = null) {
  var runtime = new AreaFinanceRuntime(e => errors?.Add(e));
  runtime.AddRequestListener(listener);
  runtime.Start(0);
  return runtime;
 }
 [Test] public void ZeroSettlementStillReportsTheRequestedEntriesOnce() {
  var listener = new RequestListener();
  var runtime = Runtime(listener);
  runtime.EnqueueExpense(7, AreaFinanceKind.PolicyUpkeep, 100);
  runtime.BeginApply(40);
  runtime.CompleteApply(0, 0);
  Assert.That(listener.Log.Calls, Has.Count.EqualTo(1));
  var call = listener.Log.Calls[0];
  Assert.That(call.Requested.Expenses, Is.EqualTo(new[] { new AreaFinanceEntry(7, AreaFinanceKind.PolicyUpkeep, 100) }));
  Assert.That(call.Settled.IsEmpty, Is.True, "nothing reached city money");
  Assert.That(call.Frame, Is.EqualTo(40u));
 }
 [Test] public void AbortedSchedulingReportsTheRequestWithAnEmptySettlement() {
  var listener = new RequestListener();
  var settled = new SettledListener();
  var runtime = Runtime(listener);
  runtime.AddListener(settled);
  runtime.EnqueueExpense(7, AreaFinanceKind.ParkManagement, 30);
  runtime.BeginApply(12);
  runtime.AbortApply();
  Assert.That(listener.Log.Calls, Has.Count.EqualTo(1));
  Assert.That(listener.Log.Calls[0].Requested.Total(AreaFinanceKind.ParkManagement), Is.EqualTo(30));
  Assert.That(listener.Log.Calls[0].Settled.IsEmpty, Is.True);
  Assert.That(settled.Batches, Is.Empty, "the positive settlement callback keeps its applied-only contract");
  Assert.That(runtime.Settled(AreaFinanceKind.ParkManagement, 12), Is.Zero);
 }
 [Test] public void MixedKindPartialBatchReportsEveryRequestedPairEvenWhenSettlementOmitsIt() {
  var listener = new RequestListener();
  var runtime = Runtime(listener);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 50);
  runtime.EnqueueExpense(2, AreaFinanceKind.ParkManagement, 40);
  runtime.EnqueueExpense(3, AreaFinanceKind.PolicyUpkeep, 10);
  runtime.BeginApply(5);
  runtime.CompleteApply(60, 0);
  var call = listener.Log.Calls.Single();
  Assert.That(call.Requested.Expenses, Has.Count.EqualTo(3));
  Assert.That(call.Settled.Expenses, Is.EqualTo(new[] { new AreaFinanceEntry(1, AreaFinanceKind.PolicyUpkeep, 50), new AreaFinanceEntry(2, AreaFinanceKind.ParkManagement, 10) }));
  Assert.That(call.Settled.Expenses.Any(e => e.AreaStableId == 3), Is.False, "area 3 is identified only by the requested batch");
 }
 [Test] public void ACompletedBatchIsNeverReportedTwice() {
  var listener = new RequestListener();
  var runtime = Runtime(listener);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 50);
  runtime.BeginApply(5);
  runtime.CompleteApply(50, 0);
  Assert.That(runtime.CompleteApply(50, 0), Is.Null);
  runtime.AbortApply();
  runtime.Reset();
  Assert.That(listener.Log.Calls, Has.Count.EqualTo(1));
 }
 [Test] public void AFailingRequestListenerIsIsolatedFromOtherListenersAndCityAccounts() {
  var errors = new List<Exception>();
  var failing = new RequestListener { OnCall = () => throw new InvalidOperationException("listener") };
  var runtime = Runtime(failing, errors);
  var second = new RequestListener();
  var settled = new SettledListener();
  runtime.AddRequestListener(second);
  runtime.AddListener(settled);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 50);
  runtime.BeginApply(5);
  Assert.DoesNotThrow(() => runtime.CompleteApply(50, 0));
  Assert.That(errors.Select(e => e.Message), Is.EqualTo(new[] { "listener" }));
  Assert.That(second.Log.Calls, Has.Count.EqualTo(1));
  Assert.That(settled.Batches, Has.Count.EqualTo(1));
  Assert.That(runtime.Settled(AreaFinanceKind.PolicyUpkeep, 5), Is.EqualTo(50));
 }
 [Test] public void RemovedRequestListenersAreNotCalled() {
  var listener = new RequestListener();
  var runtime = Runtime(listener);
  Assert.That(runtime.RemoveRequestListener(listener), Is.True);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 50);
  runtime.BeginApply(5);
  runtime.CompleteApply(50, 0);
  Assert.That(listener.Log.Calls, Is.Empty);
 }
 [Test] public void ResetCancelsQueuedRequestsWithAnEmptyCompletionAndNoCharge() {
  var listener = new RequestListener();
  var runtime = Runtime(listener);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 50);
  runtime.Reset();
  var call = listener.Log.Calls.Single();
  Assert.That(call.Requested.Total(AreaFinanceKind.PolicyUpkeep), Is.EqualTo(50));
  Assert.That(call.Settled.IsEmpty, Is.True);
  Assert.That(runtime.HasQueued, Is.False);
  runtime.Start(0);
  Assert.That(runtime.BeginApply(9), Is.Null, "a cancelled request is never applied later");
 }
 // Preload order (AreaFinanceSystem.OnGamePreload): complete the old-city job, record what it applied, then reset.
 [Test] public void OldCityAppliedDebitIsReportedBeforeResetAndQueuedWorkIsCancelled() {
  var listener = new RequestListener();
  var runtime = Runtime(listener);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 30);
  runtime.BeginApply(10);
  runtime.EnqueueExpense(2, AreaFinanceKind.ParkManagement, 5);
  runtime.CompleteApply(30, 0);
  runtime.Reset();
  Assert.That(listener.Log.Calls, Has.Count.EqualTo(2));
  Assert.That(listener.Log.Calls[0].Settled.Total(AreaFinanceKind.PolicyUpkeep), Is.EqualTo(30), "applied old-city debit is accounted");
  Assert.That(listener.Log.Calls[1].Requested.Total(AreaFinanceKind.ParkManagement), Is.EqualTo(5));
  Assert.That(listener.Log.Calls[1].Settled.IsEmpty, Is.True, "unapplied work is cancelled without charge");
  Assert.That(runtime.Settled(AreaFinanceKind.PolicyUpkeep, 10), Is.Zero, "a new city starts with empty accounts");
 }
 [Test] public void FinanceSystemRecordsTheAppliedOldCityBatchBeforeResettingOnPreload() {
  var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Core/Finance/AreaFinanceSystem.cs"));
  int preload = source.IndexOf("protected override void OnGamePreload", StringComparison.Ordinal);
  Assert.That(preload, Is.GreaterThan(0));
  var body = source.Substring(preload, source.IndexOf("[Preserve]", preload, StringComparison.Ordinal) - preload);
  int complete = body.IndexOf("pending.Complete()", StringComparison.Ordinal), record = body.IndexOf("RecordResults()", StringComparison.Ordinal),
   reset = body.IndexOf("runtime.Reset()", StringComparison.Ordinal), generation = body.IndexOf("loadGeneration++", StringComparison.Ordinal);
  Assert.That(complete, Is.GreaterThanOrEqualTo(0));
  Assert.That(record, Is.GreaterThan(complete), "the completed job's applied amounts are recorded");
  Assert.That(reset, Is.GreaterThan(record), "only then is the runtime reset");
  Assert.That(generation, Is.GreaterThan(reset), "listeners see the completion under the old generation");
 }
}
