using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkFinanceSaveBoundaryTests {
 // Native jobs cannot execute outside Unity. Exercise both production save boundaries with the real ledgers, household
 // transfer, PlayerMoney arithmetic, counter recording and component serializer, in the registered order:
 // Park flush (admission debit recorded, Area Service Fees recorded) → Area flush (management charged) → serializer.
 private sealed class Settlement : IAreaFinanceListener {
  public readonly ParkTransactionLedger Ledger = new();
  public readonly AreaFinanceRuntime Finance = new(_ => { });
  public PlayerMoney Money = new(1000);
  public ParkAreaState State = ParkAreaState.CreateDefault();
  public long Household = 100;
  public bool LaterCityWriterPending;
  private ParkTransactionBatch batch;
  private List<long> collected;
  private AreaFinanceApplied? areaApplied;
  public Settlement() { Finance.AddListener(this); }
  public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame) {
   foreach (var charge in ParkAreaFinance.AppliedManagement(settled)) State = ParkFinanceModel.RecordManagementExpense(State, charge.Amount);
  }
  public void CompleteDependencies() {
   if (!LaterCityWriterPending) return;
   Money = new PlayerMoney(2_000_000_000); LaterCityWriterPending = false;
  }
  // ParkFinanceSystem: admissions only.
  public void Schedule() {
   if (LaterCityWriterPending) throw new InvalidOperationException("A later economy writer still owns the city balance");
   batch = Ledger.TakeBatch(); collected = null;
  }
  public void ExecuteJob() {
   if (batch == null || collected != null) return;
   collected = new();
   foreach (var request in batch.Admissions) {
    long amount = AreaFinanceHouseholdTransfer.Transfer(ref Money, Household, request.Fee);
    Household -= amount; collected.Add(amount);
   }
  }
  public void CompleteAndRecord() {
   ExecuteJob(); if (batch == null) return;
   var result = ParkTransactionLedger.Resolve(batch, collected); batch = null;
   foreach (var admission in result.Admissions) {
    State = ParkFinanceModel.RecordAdmission(State, admission.Collected);
    Finance.RecordSettledIncome(1, admission.Request.ParkId, AreaFinanceKind.ParkAdmission, admission.Collected);
   }
  }
  // AreaFinanceSystem.
  public void ScheduleArea() {
   if (LaterCityWriterPending) throw new InvalidOperationException("A later economy writer still owns the city balance");
   if (Finance.BeginApply(1) != null) areaApplied = null;
  }
  public void CompleteAndRecordArea() {
   if (!Finance.HasInFlight) return;
   if (areaApplied == null) {
    var pending = Finance.InFlight!;
    areaApplied = AreaFinanceVanillaAdapter.Apply(ref Money, pending.TotalExpense, pending.TotalIncome);
   }
   Finance.CompleteApply(areaApplied.Value.Expense, areaApplied.Value.Income); areaApplied = null;
  }
  public void Save(bool ready = true, int generation = 2, int currentGeneration = 2) {
   AreaFinanceSaveBoundary.Flush(ready, generation, currentGeneration, CompleteAndRecord, () => Ledger.HasPending, CompleteDependencies, Schedule);
   AreaFinanceSaveBoundary.Flush(ready, 2, 2, CompleteAndRecordArea, () => Finance.HasQueued, CompleteDependencies, ScheduleArea);
  }
  public void Admission(long fee) => Ledger.EnqueueAdmission(new ParkAdmissionRequest { ParkId = 7, Fee = fee });
  public void Management(long amount) => Finance.EnqueueExpense(7, AreaFinanceKind.ParkManagement, amount);
 }
 private static long Stored(PlayerMoney money) => AreaFinanceVanillaAdapter.StoredCityMoney(money);
 [Test] public void SaveAfterTransferBeforeNextUpdatePersistsMatchingFinancialCounters() {
  var settlement = new Settlement(); settlement.Admission(20); settlement.Management(30);
  settlement.Schedule(); settlement.ExecuteJob();
  Assert.That(Stored(settlement.Money), Is.EqualTo(1020));
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.Zero, "scheduled results have not yet been recorded");
  settlement.Save();
  var restored = MemorySerializer.RoundTrip(settlement.State);
  Assert.That(restored.LifetimeAdmissionRevenue, Is.EqualTo(20));
  Assert.That(restored.LifetimeManagementExpense, Is.EqualTo(30));
  Assert.That(Stored(settlement.Money), Is.EqualTo(1000 + restored.LifetimeAdmissionRevenue - restored.LifetimeManagementExpense));
  Assert.That(settlement.Finance.AreaServiceFees(2), Is.EqualTo(20)); Assert.That(settlement.Finance.AreaManagement(2), Is.EqualTo(30));
  settlement.Save(); settlement.CompleteAndRecord(); settlement.CompleteAndRecordArea();
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.EqualTo(20));
  Assert.That(settlement.State.LifetimeManagementExpense, Is.EqualTo(30), "repeated save/update cannot duplicate records");
  Assert.That(settlement.Finance.AreaServiceFees(2), Is.EqualTo(20));
 }
 [Test] public void SaveDrainsBothAnExistingBatchAndRequestsQueuedBehindIt() {
  var settlement = new Settlement(); settlement.Admission(20); settlement.Schedule();
  settlement.Admission(15); settlement.Management(10); settlement.ScheduleArea(); settlement.Management(4);
  settlement.Save();
  Assert.That(settlement.Ledger.HasPending, Is.False); Assert.That(settlement.Finance.HasQueued || settlement.Finance.HasInFlight, Is.False);
  var restored = MemorySerializer.RoundTrip(settlement.State);
  Assert.That(restored.LifetimeAdmissionRevenue, Is.EqualTo(35)); Assert.That(restored.LifetimeManagementExpense, Is.EqualTo(14));
  Assert.That(settlement.Household, Is.EqualTo(65)); Assert.That(Stored(settlement.Money), Is.EqualTo(1021));
 }
 [Test] public void SavePersistsPartialAdmissionAndFullManagementBelowZero() {
  var settlement = new Settlement { Household = 7, Money = new PlayerMoney(-1_000_000) };
  settlement.Admission(60); settlement.Management(60);
  settlement.Schedule(); settlement.ExecuteJob(); settlement.Save(); settlement.Save();
  var restored = MemorySerializer.RoundTrip(settlement.State);
  Assert.That(restored.LifetimeAdmissionRevenue, Is.EqualTo(7)); Assert.That(restored.LifetimeManagementExpense, Is.EqualTo(60));
  Assert.That(settlement.Household, Is.Zero); Assert.That(Stored(settlement.Money), Is.EqualTo(-1_000_053));
 }
 [TestCase(false, 2, 2)] [TestCase(true, 1, 2)]
 public void SaveNeverSettlesTransactionsFromAnUnreadyOrDifferentLoad(bool ready, int generation, int current) {
  var settlement = new Settlement(); settlement.Admission(20); settlement.Schedule();
  AreaFinanceSaveBoundary.Flush(ready, generation, current, settlement.CompleteAndRecord, () => settlement.Ledger.HasPending, settlement.CompleteDependencies, settlement.Schedule);
  Assert.That(settlement.Household, Is.EqualTo(100)); Assert.That(Stored(settlement.Money), Is.EqualTo(1000));
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.Zero);
 }
 [Test] public void QueuedSaveTransfersWaitForLaterEconomyWritersAndUseTheirBalance() {
  var settlement = new Settlement { LaterCityWriterPending = true }; settlement.Admission(60);
  Assert.DoesNotThrow(() => settlement.Save());
  Assert.That(settlement.LaterCityWriterPending, Is.False);
  Assert.That(Stored(settlement.Money), Is.EqualTo(2_000_000_000));
  Assert.That(settlement.Household, Is.EqualTo(100), "the completed writer filled the city; admission must not consume household money");
  Assert.That(settlement.State.LifetimeAdmissionRevenue, Is.Zero);
  Assert.That(settlement.Finance.AreaServiceFees(2), Is.Zero);
 }
}
