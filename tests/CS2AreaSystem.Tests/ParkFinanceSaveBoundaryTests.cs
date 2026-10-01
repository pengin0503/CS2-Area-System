using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkFinanceSaveBoundaryTests {
 // Native jobs cannot execute outside Unity. Exercise the production save boundary with the real transaction
 // ledger, PlayerMoney arithmetic, counter recording and component serializer, in the same lifecycle order.
 private sealed class Settlement {
  public readonly ParkTransactionLedger Ledger = new();
  public PlayerMoney Money = new(1000);
  public ParkAreaState State = ParkAreaState.CreateDefault();
  public long Household = 100;
  public bool LaterCityWriterPending;
  private ParkTransactionBatch batch;
  private List<long> collected;
  private long managementPaid;
  public void Schedule() {
   if (LaterCityWriterPending) throw new InvalidOperationException("A later economy writer still owns the city balance");
   batch = Ledger.TakeBatch(); collected = null;
  }
  public void CompleteDependencies() {
   if (!LaterCityWriterPending) return;
   Money = new PlayerMoney(2_000_000_000); LaterCityWriterPending = false;
  }
  public void ExecuteJob() {
   if (batch == null || collected != null) return;
   collected = new();
   foreach (var request in batch.Admissions) {
    long amount = ParkFinanceModel.CollectAdmission(ref Money, request.Fee, Household);
    Household -= amount; collected.Add(amount);
   }
   managementPaid = ParkFinanceModel.PayManagement(ref Money, -batch.CityDelta);
  }
  public void CompleteAndRecord() {
   ExecuteJob(); if (batch == null) return;
   var result = ParkTransactionLedger.Resolve(batch, collected, managementPaid); batch = null;
   foreach (var admission in result.Admissions) State = ParkFinanceModel.RecordAdmission(State, admission.Collected);
   foreach (var charge in result.Management) State = ParkFinanceModel.RecordManagementExpense(State, charge.Paid);
  }
  public void Flush(bool ready = true, int generation = 2, int currentGeneration = 2) =>
   ParkFinanceSaveBoundary.Flush(ready, generation, currentGeneration, CompleteAndRecord, () => Ledger.HasPending, CompleteDependencies, Schedule);
  public void Admission(long fee) => Ledger.EnqueueAdmission(new ParkAdmissionRequest { ParkId = 7, Fee = fee });
 }
 [Test] public void SaveAfterTransferBeforeNextUpdatePersistsMatchingFinancialCounters() {
  var settlement = new Settlement(); settlement.Admission(20); settlement.Ledger.EnqueueManagement(default, 7, 30);
  settlement.Schedule(); settlement.ExecuteJob();
  Assert.That(ParkFinanceModel.StoredCityMoney(settlement.Money), Is.EqualTo(990));
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.Zero, "scheduled results have not yet been recorded");
  settlement.Flush();
  var restored = MemorySerializer.RoundTrip(settlement.State);
  Assert.That(restored.LifetimeAdmissionRevenue, Is.EqualTo(20));
  Assert.That(restored.LifetimeManagementExpense, Is.EqualTo(30));
  Assert.That(ParkFinanceModel.StoredCityMoney(settlement.Money), Is.EqualTo(1000 + restored.LifetimeAdmissionRevenue - restored.LifetimeManagementExpense));
  settlement.Flush(); settlement.CompleteAndRecord();
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.EqualTo(20));
  Assert.That(settlement.State.LifetimeManagementExpense, Is.EqualTo(30), "repeated save/update cannot duplicate records");
 }
 [Test] public void SaveDrainsBothAnExistingBatchAndRequestsQueuedBehindIt() {
  var settlement = new Settlement(); settlement.Admission(20); settlement.Schedule();
  settlement.Admission(15); settlement.Ledger.EnqueueManagement(default, 7, 10);
  settlement.Flush();
  Assert.That(settlement.Ledger.HasPending, Is.False);
  var restored = MemorySerializer.RoundTrip(settlement.State);
  Assert.That(restored.LifetimeAdmissionRevenue, Is.EqualTo(35)); Assert.That(restored.LifetimeManagementExpense, Is.EqualTo(10));
  Assert.That(settlement.Household, Is.EqualTo(65)); Assert.That(ParkFinanceModel.StoredCityMoney(settlement.Money), Is.EqualTo(1025));
 }
 [Test] public void SavePersistsPartialAdmissionAndSaturatedManagementWithoutDoubleBooking() {
  var settlement = new Settlement { Household = 7, Money = new PlayerMoney(-1_999_999_990) };
  settlement.Admission(60); settlement.Ledger.EnqueueManagement(default, 7, 60);
  settlement.Schedule(); settlement.ExecuteJob(); settlement.Flush(); settlement.Flush();
  var restored = MemorySerializer.RoundTrip(settlement.State);
  Assert.That(restored.LifetimeAdmissionRevenue, Is.EqualTo(7)); Assert.That(restored.LifetimeManagementExpense, Is.EqualTo(17));
  Assert.That(settlement.Household, Is.Zero); Assert.That(ParkFinanceModel.StoredCityMoney(settlement.Money), Is.EqualTo(-2_000_000_000));
 }
 [TestCase(false, 2, 2)] [TestCase(true, 1, 2)]
 public void SaveNeverSettlesTransactionsFromAnUnreadyOrDifferentLoad(bool ready, int generation, int current) {
  var settlement = new Settlement(); settlement.Admission(20); settlement.Schedule();
  settlement.Flush(ready, generation, current);
  Assert.That(settlement.Household, Is.EqualTo(100)); Assert.That(ParkFinanceModel.StoredCityMoney(settlement.Money), Is.EqualTo(1000));
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.Zero);
 }
 [Test] public void QueuedSaveTransfersWaitForLaterEconomyWritersAndUseTheirBalance() {
  var settlement = new Settlement { LaterCityWriterPending = true }; settlement.Admission(60);
  Assert.DoesNotThrow(() => settlement.Flush());
  Assert.That(settlement.LaterCityWriterPending, Is.False);
  Assert.That(ParkFinanceModel.StoredCityMoney(settlement.Money), Is.EqualTo(2_000_000_000));
  Assert.That(settlement.Household, Is.EqualTo(100), "the completed writer filled the city; admission must not consume household money");
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.Zero);
 }
}
