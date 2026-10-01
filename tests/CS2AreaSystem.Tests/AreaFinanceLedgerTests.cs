using CS2AreaSystem.Core.Finance;
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
 [Test] public void ClearForgetsEverything() {
  var accounts = new AreaFinanceAccounts();
  accounts.Start(0);
  accounts.Record(10, AreaFinanceKind.PolicyUpkeep, 5);
  accounts.Clear();
  Assert.That(accounts.AreaManagement(20), Is.Zero);
  Assert.That(accounts.CoveredFrames(20), Is.Zero);
 }
}
