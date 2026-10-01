using CS2AreaSystem.Core.Finance;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaFinanceLedgerTests {
 private static AreaFinanceEntry E(ulong area, AreaFinanceKind kind, long amount) => new AreaFinanceEntry(area, kind, amount);
 [Test] public void ExpensesAggregatePerAreaAndKindWithoutLosingAuditDetail() {
  var ledger = new AreaFinanceLedger();
  Assert.That(ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 10), Is.True);
  ledger.EnqueueExpense(2, AreaFinanceKind.PolicyUpkeep, 7);
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 5);
  ledger.EnqueueExpense(1, AreaFinanceKind.ParkManagement, 3);
  var batch = ledger.TakeBatch();
  Assert.That(batch.Expenses, Is.EqualTo(new[] { E(1, AreaFinanceKind.PolicyUpkeep, 15), E(2, AreaFinanceKind.PolicyUpkeep, 7), E(1, AreaFinanceKind.ParkManagement, 3) }));
  Assert.That(batch.TotalExpense, Is.EqualTo(25));
  Assert.That(batch.Total(AreaFinanceKind.PolicyUpkeep), Is.EqualTo(22));
  Assert.That(batch.Total(AreaFinanceKind.ParkManagement), Is.EqualTo(3));
  Assert.That(batch.Incomes, Is.Empty);
  Assert.That(batch.TotalIncome, Is.Zero);
 }
 [Test] public void IncomeIsKeptApartFromExpense() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueIncome(4, AreaFinanceKind.ParkAdmission, 20);
  ledger.EnqueueExpense(4, AreaFinanceKind.ParkManagement, 6);
  var batch = ledger.TakeBatch();
  Assert.That(batch.Incomes, Is.EqualTo(new[] { E(4, AreaFinanceKind.ParkAdmission, 20) }));
  Assert.That(batch.TotalIncome, Is.EqualTo(20));
  Assert.That(batch.TotalExpense, Is.EqualTo(6));
 }
 [Test] public void AKindIsOnlyAcceptedInItsOwnDirection() {
  var ledger = new AreaFinanceLedger();
  Assert.That(ledger.EnqueueExpense(1, AreaFinanceKind.ParkAdmission, 10), Is.False, "admission is never an expense");
  Assert.That(ledger.EnqueueIncome(1, AreaFinanceKind.PolicyUpkeep, 10), Is.False);
  Assert.That(ledger.EnqueueIncome(1, AreaFinanceKind.ParkManagement, 10), Is.False);
  Assert.That(ledger.EnqueueExpense(1, (AreaFinanceKind)99, 10), Is.False);
  Assert.That(ledger.HasPending, Is.False);
 }
 [TestCase(0L)] [TestCase(-5L)]
 public void NonpositiveAmountsAreIgnored(long amount) {
  var ledger = new AreaFinanceLedger();
  Assert.That(ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, amount), Is.False);
  Assert.That(ledger.EnqueueIncome(1, AreaFinanceKind.ParkAdmission, amount), Is.False);
  Assert.That(ledger.HasPending, Is.False);
 }
 [Test] public void TakingABatchEmptiesThePendingSetSoNothingIsAggregatedTwice() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 10);
  Assert.That(ledger.HasPending, Is.True);
  Assert.That(ledger.TakeBatch().TotalExpense, Is.EqualTo(10));
  Assert.That(ledger.HasPending, Is.False);
  var second = ledger.TakeBatch();
  Assert.That(second.IsEmpty, Is.True);
  Assert.That(second.TotalExpense, Is.Zero);
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 4);
  Assert.That(ledger.TakeBatch().Expenses, Is.EqualTo(new[] { E(1, AreaFinanceKind.PolicyUpkeep, 4) }), "a new batch starts from zero");
 }
 [Test] public void ClearDropsPendingRequests() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 10);
  ledger.Clear();
  Assert.That(ledger.HasPending, Is.False);
  Assert.That(ledger.TakeBatch().IsEmpty, Is.True);
 }
 [Test] public void TotalsSaturateInsteadOfWrapping() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, long.MaxValue - 1);
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 5);
  ledger.EnqueueExpense(2, AreaFinanceKind.ParkManagement, 5);
  var batch = ledger.TakeBatch();
  Assert.That(batch.Expenses[0].Amount, Is.EqualTo(long.MaxValue));
  Assert.That(batch.TotalExpense, Is.EqualTo(long.MaxValue));
 }
 [Test] public void FullyAppliedBatchResolvesToTheRequestedEntries() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 10);
  ledger.EnqueueIncome(2, AreaFinanceKind.ParkAdmission, 4);
  var batch = ledger.TakeBatch();
  var settled = batch.Resolve(10, 4);
  Assert.That(settled.Expenses, Is.EqualTo(batch.Expenses));
  Assert.That(settled.Incomes, Is.EqualTo(batch.Incomes));
  Assert.That(settled.TotalExpense, Is.EqualTo(10));
  Assert.That(settled.TotalIncome, Is.EqualTo(4));
 }
 [Test] public void SaturatedApplicationIsAllocatedOnceInEntryOrder() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(7, AreaFinanceKind.PolicyUpkeep, 60);
  ledger.EnqueueExpense(8, AreaFinanceKind.PolicyUpkeep, 60);
  ledger.EnqueueExpense(9, AreaFinanceKind.ParkManagement, 60);
  var settled = ledger.TakeBatch().Resolve(appliedExpense: 70, appliedIncome: 0);
  Assert.That(settled.Expenses, Is.EqualTo(new[] { E(7, AreaFinanceKind.PolicyUpkeep, 60), E(8, AreaFinanceKind.PolicyUpkeep, 10) }));
  Assert.That(settled.TotalExpense, Is.EqualTo(70));
  Assert.That(settled.Total(AreaFinanceKind.PolicyUpkeep), Is.EqualTo(70));
  Assert.That(settled.Total(AreaFinanceKind.ParkManagement), Is.Zero);
 }
 [Test] public void ResolveNeverRecordsMoreThanWasRequestedOrLessThanZero() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 10);
  var batch = ledger.TakeBatch();
  Assert.That(batch.Resolve(999, 999).TotalExpense, Is.EqualTo(10));
  Assert.That(batch.Resolve(-5, -5).Expenses, Is.Empty);
 }
}
public class AreaFinanceAccountsTests {
 private const uint Day = AreaFinanceModel.FramesPerDay;
 [Test] public void AreaManagementIsPolicyUpkeepPlusParkManagementOverTheTrailingDay() {
  var accounts = new AreaFinanceAccounts();
  accounts.Record(1000, AreaFinanceKind.PolicyUpkeep, 30);
  accounts.Record(2000, AreaFinanceKind.ParkManagement, 12);
  accounts.Record(2000, AreaFinanceKind.ParkAdmission, 50);
  Assert.That(accounts.AreaManagement(3000), Is.EqualTo(42));
  Assert.That(accounts.AreaServiceFees(3000), Is.EqualTo(50));
  Assert.That(accounts.Settled(AreaFinanceKind.PolicyUpkeep, 3000), Is.EqualTo(30));
 }
 [Test] public void AmountsOlderThanOneDayLeaveTheTrailingTotal() {
  var accounts = new AreaFinanceAccounts();
  accounts.Record(1000, AreaFinanceKind.PolicyUpkeep, 30);
  accounts.Record(1000 + Day / 2, AreaFinanceKind.PolicyUpkeep, 5);
  Assert.That(accounts.AreaManagement(1000 + Day - 1), Is.EqualTo(35));
  Assert.That(accounts.AreaManagement(1000 + Day), Is.EqualTo(5));
  Assert.That(accounts.AreaManagement(1000 + Day + Day / 2), Is.Zero);
 }
 [Test] public void TheTrailingWindowSurvivesFrameCounterWrap() {
  var accounts = new AreaFinanceAccounts();
  accounts.Record(uint.MaxValue - 10, AreaFinanceKind.ParkAdmission, 9);
  Assert.That(accounts.AreaServiceFees(100), Is.EqualTo(9));
 }
 [Test] public void CoveredFramesReportsHowMuchOfTheDayWasObserved() {
  var accounts = new AreaFinanceAccounts();
  Assert.That(accounts.CoveredFrames(500), Is.Zero, "nothing observed yet");
  accounts.Start(500);
  Assert.That(accounts.CoveredFrames(1500), Is.EqualTo(1000u));
  Assert.That(accounts.CoveredFrames(500 + 3 * Day), Is.EqualTo(Day));
 }
 [Test] public void NonpositiveAndUnknownAmountsAreNotRecorded() {
  var accounts = new AreaFinanceAccounts();
  accounts.Record(10, AreaFinanceKind.PolicyUpkeep, 0);
  accounts.Record(10, AreaFinanceKind.PolicyUpkeep, -3);
  accounts.Record(10, (AreaFinanceKind)77, 5);
  Assert.That(accounts.AreaManagement(20) + accounts.AreaServiceFees(20), Is.Zero);
 }
 [Test] public void AmountsOfOneFrameShareARecordSoTheAccountStaysBoundedByFramesNotVisits() {
  var accounts = new AreaFinanceAccounts();
  for (int visit = 0; visit < 10_000; visit++) {
   accounts.Record(500, AreaFinanceKind.ParkAdmission, 3);
   accounts.Record(500, visit % 2 == 0 ? AreaFinanceKind.PolicyUpkeep : AreaFinanceKind.ParkManagement, 1);
  }
  accounts.Record(756, AreaFinanceKind.ParkAdmission, 7);
  Assert.That(accounts.RecordCount, Is.EqualTo(4), "one record per frame and kind");
  Assert.That(accounts.AreaServiceFees(800), Is.EqualTo(30_007));
  Assert.That(accounts.AreaManagement(800), Is.EqualTo(10_000));
  Assert.That(accounts.Settled(AreaFinanceKind.PolicyUpkeep, 800), Is.EqualTo(5_000));
  Assert.That(accounts.AreaServiceFees(500 + Day), Is.EqualTo(7), "the merged frame leaves the window as a whole");
  Assert.That(accounts.RecordCount, Is.EqualTo(1));
 }
 [Test] public void MergedAmountsSaturateInsteadOfWrapping() {
  var accounts = new AreaFinanceAccounts();
  accounts.Record(1, AreaFinanceKind.ParkAdmission, long.MaxValue - 1);
  accounts.Record(1, AreaFinanceKind.ParkAdmission, 5);
  Assert.That(accounts.AreaServiceFees(2), Is.EqualTo(long.MaxValue));
 }
 [Test] public void ClearForgetsEverything() {
  var accounts = new AreaFinanceAccounts();
  accounts.Start(0);
  accounts.Record(10, AreaFinanceKind.PolicyUpkeep, 5);
  accounts.Clear();
  Assert.That(accounts.AreaManagement(20), Is.Zero);
  Assert.That(accounts.CoveredFrames(20), Is.Zero);
 }
}
public class AreaFinanceRuntimeTests {
 private sealed class Listener : IAreaFinanceListener {
  public readonly List<(AreaFinanceBatch Settled, uint Frame)> Calls = new();
  public bool Throw;
  public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame) { if (Throw) throw new InvalidOperationException("listener"); Calls.Add((settled, frame)); }
 }
 private static AreaFinanceBatch ApplyOnce(AreaFinanceRuntime runtime, ref PlayerMoney money, uint frame) {
  var batch = runtime.BeginApply(frame);
  var applied = AreaFinanceVanillaAdapter.Apply(ref money, batch.TotalExpense, batch.TotalIncome);
  return runtime.CompleteApply(applied.Expense, applied.Income);
 }
 [Test] public void AppliedAmountsAreRecordedAndReportedOnce() {
  var runtime = new AreaFinanceRuntime(_ => { }); var listener = new Listener(); runtime.AddListener(listener);
  runtime.Start(0);
  runtime.EnqueueExpense(5, AreaFinanceKind.ParkManagement, 40);
  var money = new PlayerMoney(-1_000);
  var settled = ApplyOnce(runtime, ref money, 256);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(-1_040));
  Assert.That(settled.Expenses, Is.EqualTo(new[] { new AreaFinanceEntry(5, AreaFinanceKind.ParkManagement, 40) }));
  Assert.That(listener.Calls, Has.Count.EqualTo(1)); Assert.That(listener.Calls[0].Frame, Is.EqualTo(256u));
  Assert.That(runtime.AreaManagement(300), Is.EqualTo(40));
  Assert.That(runtime.HasInFlight, Is.False);
  Assert.That(runtime.CompleteApply(40, 0), Is.Null, "nothing in flight: nothing recorded twice");
  Assert.That(listener.Calls, Has.Count.EqualTo(1));
 }
 [Test] public void SettledHouseholdIncomeIsRecordedWithoutQueueingAnotherCredit() {
  var runtime = new AreaFinanceRuntime(_ => { });
  Assert.That(runtime.RecordSettledIncome(10, 5, AreaFinanceKind.ParkAdmission, 25), Is.True);
  Assert.That(runtime.HasQueued, Is.False, "the household transfer already credited the city");
  Assert.That(runtime.BeginApply(10), Is.Null);
  Assert.That(runtime.AreaServiceFees(20), Is.EqualTo(25));
  Assert.That(runtime.RecordSettledIncome(10, 5, AreaFinanceKind.ParkManagement, 25), Is.False);
  Assert.That(runtime.RecordSettledIncome(10, 5, AreaFinanceKind.ParkAdmission, 0), Is.False);
 }
 [Test] public void AFailingListenerIsIsolatedAndReported() {
  var errors = new List<Exception>();
  var runtime = new AreaFinanceRuntime(errors.Add); var bad = new Listener { Throw = true }; var good = new Listener();
  runtime.AddListener(bad); runtime.AddListener(good);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 3);
  var money = new PlayerMoney(0);
  ApplyOnce(runtime, ref money, 1);
  Assert.That(errors, Has.Count.EqualTo(1));
  Assert.That(good.Calls, Has.Count.EqualTo(1));
  Assert.That(runtime.AreaManagement(2), Is.EqualTo(3), "accounts are written before listeners run");
 }
 [Test] public void AbortDropsTheBatchWithoutRecordingIt() {
  var runtime = new AreaFinanceRuntime(_ => { });
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 3);
  Assert.That(runtime.BeginApply(1), Is.Not.Null);
  runtime.AbortApply();
  Assert.That(runtime.HasInFlight, Is.False); Assert.That(runtime.HasQueued, Is.False);
  Assert.That(runtime.AreaManagement(2), Is.Zero, "nothing was applied, so nothing is recorded");
 }
 [Test] public void BeginApplyWhileABatchIsInFlightTakesNothing() {
  var runtime = new AreaFinanceRuntime(_ => { });
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 3);
  runtime.BeginApply(1);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 4);
  Assert.That(runtime.BeginApply(2), Is.Null, "results of the first batch must be recorded first");
  Assert.That(runtime.HasQueued, Is.True);
 }
 [Test] public void ResetClearsQueuedInFlightAndSettledState() {
  var runtime = new AreaFinanceRuntime(_ => { }); var listener = new Listener(); runtime.AddListener(listener);
  runtime.Start(0);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 3); runtime.BeginApply(1);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 4); runtime.RecordSettledIncome(1, 1, AreaFinanceKind.ParkAdmission, 9);
  runtime.Reset();
  Assert.That(runtime.HasQueued || runtime.HasInFlight, Is.False);
  Assert.That(runtime.AreaManagement(2) + runtime.AreaServiceFees(2), Is.Zero);
  Assert.That(runtime.CoveredFrames(2), Is.Zero);
  Assert.That(runtime.CompleteApply(3, 0), Is.Null); Assert.That(listener.Calls, Is.Empty);
 }
}
