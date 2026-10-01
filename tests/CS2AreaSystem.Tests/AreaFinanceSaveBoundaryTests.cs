using CS2AreaSystem.Core.Finance;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaFinanceSaveBoundaryTests {
 // Native jobs cannot execute outside Unity. Exercise the production save boundary with the real ledger, PlayerMoney
 // arithmetic and settled accounts, in the order AreaFinanceSystem uses them.
 private sealed class Settlement {
  public readonly AreaFinanceLedger Ledger = new();
  public readonly AreaFinanceAccounts Accounts = new();
  public PlayerMoney Money = new(1000);
  public bool LaterCityWriterPending;
  private AreaFinanceBatch batch;
  private AreaFinanceApplied? applied;
  public void Schedule() {
   if (LaterCityWriterPending) throw new InvalidOperationException("A later economy writer still owns the city balance");
   batch = Ledger.TakeBatch(); applied = null;
  }
  public void CompleteDependencies() {
   if (!LaterCityWriterPending) return;
   Money = new PlayerMoney(-2_000_000_000); LaterCityWriterPending = false;
  }
  public void ExecuteJob() {
   if (batch == null || applied != null) return;
   applied = AreaFinanceVanillaAdapter.Apply(ref Money, batch.TotalExpense, batch.TotalIncome);
  }
  public void CompleteAndRecord() {
   ExecuteJob(); if (batch == null) return;
   var settled = batch.Resolve(applied!.Value.Expense, applied.Value.Income); batch = null;
   foreach (var entry in settled.Expenses) Accounts.Record(10, entry.Kind, entry.Amount);
   foreach (var entry in settled.Incomes) Accounts.Record(10, entry.Kind, entry.Amount);
  }
  public void Flush(bool ready = true, int generation = 2, int currentGeneration = 2) =>
   AreaFinanceSaveBoundary.Flush(ready, generation, currentGeneration, CompleteAndRecord, () => Ledger.HasPending, CompleteDependencies, Schedule);
 }
 [Test] public void SaveAppliesQueuedExpensesToCityMoneyBeforeSerialization() {
  var settlement = new Settlement();
  settlement.Ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 30);
  settlement.Flush();
  Assert.That(settlement.Ledger.HasPending, Is.False);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(settlement.Money), Is.EqualTo(970));
  Assert.That(settlement.Accounts.AreaManagement(20), Is.EqualTo(30));
 }
 [Test] public void SaveDrainsAnInFlightBatchAndRequestsQueuedBehindItExactlyOnce() {
  var settlement = new Settlement();
  settlement.Ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 30);
  settlement.Schedule();
  settlement.Ledger.EnqueueExpense(1, AreaFinanceKind.ParkManagement, 5);
  settlement.Flush(); settlement.Flush(); settlement.CompleteAndRecord();
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(settlement.Money), Is.EqualTo(965));
  Assert.That(settlement.Accounts.AreaManagement(20), Is.EqualTo(35), "repeated save/update cannot duplicate records");
 }
 [Test] public void ANegativeCityBalanceDoesNotShortenTheFlushedExpense() {
  var settlement = new Settlement { Money = new PlayerMoney(-5_000) };
  settlement.Ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 300);
  settlement.Flush();
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(settlement.Money), Is.EqualTo(-5_300));
  Assert.That(settlement.Accounts.AreaManagement(20), Is.EqualTo(300));
 }
 [TestCase(false, 2, 2)] [TestCase(true, 1, 2)]
 public void SaveNeverAppliesRequestsFromAnUnreadyOrDifferentLoad(bool ready, int generation, int current) {
  var settlement = new Settlement();
  settlement.Ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 30);
  settlement.Flush(ready, generation, current);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(settlement.Money), Is.EqualTo(1000));
  Assert.That(settlement.Ledger.HasPending, Is.True);
 }
 [Test] public void QueuedSaveSettlementWaitsForLaterEconomyWritersAndUsesTheirBalance() {
  var settlement = new Settlement { LaterCityWriterPending = true };
  settlement.Ledger.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 60);
  Assert.DoesNotThrow(() => settlement.Flush());
  Assert.That(settlement.LaterCityWriterPending, Is.False);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(settlement.Money), Is.EqualTo(-2_000_000_000));
  Assert.That(settlement.Accounts.AreaManagement(20), Is.Zero, "only the native bound shortens an expense, and only what was applied is recorded");
 }
}
