using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 2 C4 Task 3: only the amount Area Finance settled reaches company money, once, after revalidation.
public class IndustrialSupportSettlementTests {
 static Entity E(int id) => new() { Index = id, Version = 1 };
 internal sealed class Harness {
  public readonly AreaFinanceRuntime Finance = new(_ => { });
  public readonly IndustrialSupportLedger Ledger = new();
  public readonly Dictionary<Entity, int> Money = new();
  public readonly HashSet<Entity> Owned = new();
  public readonly List<IndustrialSupportCompletion> Completed = new();
  public int Generation = 1;
  public Harness() { Finance.Start(0); Finance.AddRequestListener(new Listener(this)); }
  private sealed class Listener : IAreaFinanceRequestListener {
   private readonly Harness h; public Listener(Harness h) => this.h = h;
   public void OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame) => IndustrialSupportSettlement.OnCompleted(h.Ledger, h.Generation, requested, settled);
  }
  public void Request(ulong area, int perCompany, params Entity[] receivers) {
   foreach (var r in receivers) { Owned.Add(r); Money.TryAdd(r, 0); }
   long requested = IndustrialSupportModel.DailyRequest(receivers.Length, perCompany);
   Assert.That(Finance.EnqueueExpense(area, AreaFinanceKind.IndustrialSupport, requested), Is.True);
   Assert.That(Ledger.TryBegin(Generation, area, E((int)area), perCompany, receivers, requested), Is.True);
  }
  public void Settle(long applied) { Finance.BeginApply(10); Finance.CompleteApply(applied, 0); }
  // The credit job surrogate: the same per-receiver arithmetic as IndustrialSupportSystem.CreditJob.
  public IndustrialSupportCreditBatch Prepare() {
   var batch = IndustrialSupportSettlement.Prepare(Ledger, (company, area) => Owned.Contains(company), Completed);
   return batch;
  }
  public long[] Execute(IndustrialSupportCreditBatch batch) {
   var credited = new long[batch.Receivers.Count];
   for (int i = 0; i < credited.Length; i++) {
    var receiver = batch.Receivers[i]; int amount = batch.Amounts[i];
    if (amount <= 0 || !Money.TryGetValue(receiver, out var before)) continue;
    int after = (int)Math.Clamp((long)before + amount, int.MinValue, int.MaxValue);
    Money[receiver] = after;
    credited[i] = IndustrialSupportCredit.Credited(before, after, amount);
   }
   return credited;
  }
  public void Finish(IndustrialSupportCreditBatch batch, long[] credited) => Completed.AddRange(IndustrialSupportSettlement.Finish(Ledger, batch, credited));
  public void Run() { var batch = Prepare(); Finish(batch, Execute(batch)); }
 }
 [Test] public void HalfSettlementWithUnchangedReceiversCreditsExactlyTheSettledHalf() {
  var h = new Harness();
  h.Request(9, 1000, E(1), E(2), E(3));
  h.Settle(1500);
  h.Run();
  Assert.That(h.Money.Values, Is.EqualTo(new[] { 500, 500, 500 }));
  var done = h.Completed.Single();
  Assert.That((done.Requested, done.Settled, done.Credited), Is.EqualTo((3000L, 1500L, 1500L)));
  Assert.That(h.Ledger.HasOutstanding(9), Is.False);
 }
 [Test] public void ZeroOrAbortedCompletionFinishesWithoutCreditOrRetry() {
  var h = new Harness();
  h.Request(9, 1000, E(1));
  h.Finance.BeginApply(10); h.Finance.AbortApply();
  h.Run();
  Assert.That(h.Money[E(1)], Is.Zero);
  Assert.That((h.Completed.Single().Requested, h.Completed.Single().Settled), Is.EqualTo((1000L, 0L)));
  Assert.That(h.Ledger.HasOutstanding(9), Is.False);
  h.Request(10, 1000, E(2));
  h.Settle(0);
  h.Run();
  Assert.That(h.Completed.Last().Settled, Is.Zero);
  Assert.That(h.Money[E(2)], Is.Zero);
 }
 [Test] public void CompanyDeletedBetweenRequestAndSettlementLeavesItsShareUnallocated() {
  var h = new Harness();
  h.Request(9, 1000, E(1), E(2), E(3));
  h.Settle(3000);
  h.Owned.Remove(E(2)); h.Money.Remove(E(2));
  Assert.DoesNotThrow(() => h.Run());
  var done = h.Completed.Single();
  Assert.That(done.Credited, Is.EqualTo(2000));
  Assert.That(done.Settled - done.Credited, Is.EqualTo(1000), "settled = credited + unallocated; never given to a newcomer");
 }
 [Test] public void ReceiverMoneyHeadroomIsAccountedFromTheActualIncrease() {
  var h = new Harness();
  h.Request(9, 1000, E(1), E(2));
  h.Money[E(1)] = int.MaxValue - 100;
  h.Settle(2000);
  h.Run();
  Assert.That(h.Money[E(1)], Is.EqualTo(int.MaxValue));
  Assert.That(h.Completed.Single().Credited, Is.EqualTo(1100));
 }
 [Test] public void ReceiverMovedOrReassignedBeforeCreditGetsNothing() {
  var h = new Harness();
  h.Request(9, 1000, E(1), E(2));
  h.Settle(2000);
  h.Owned.Remove(E(1));
  h.Run();
  Assert.That(h.Money[E(1)], Is.Zero);
  Assert.That(h.Money[E(2)], Is.EqualTo(1000), "capped at the accepted per-company amount");
  Assert.That(h.Completed.Single().Credited, Is.EqualTo(1000));
 }
 [Test] public void MoreThan64ReceiversAreAllCredited() {
  var h = new Harness();
  var receivers = Enumerable.Range(1, 100).Select(E).ToArray();
  h.Request(9, 250, receivers);
  h.Settle(25_000);
  h.Run();
  Assert.That(h.Money.Values.All(v => v == 250), Is.True);
  Assert.That(h.Completed.Single().Credited, Is.EqualTo(25_000));
 }
 [Test] public void ACompletionIsConsumedOnceAndTheAreaStaysOutstandingUntilCredited() {
  var h = new Harness();
  h.Request(9, 1000, E(1));
  h.Settle(1000);
  var batch = h.Prepare();
  Assert.That(h.Ledger.HasOutstanding(9), Is.True, "no new request while its credit is in flight");
  IndustrialSupportSettlement.OnCompleted(h.Ledger, h.Generation, Batch(9, 1000), Batch(9, 1000));
  Assert.That(h.Prepare().Receivers, Is.Empty, "a duplicate completion never re-credits");
  h.Finish(batch, h.Execute(batch));
  h.Finish(batch, new long[] { 1000 });
  Assert.That(h.Money[E(1)], Is.EqualTo(1000));
  Assert.That(h.Completed, Has.Count.EqualTo(1));
 }
 [Test] public void OtherGenerationsAndKindsAreIgnored() {
  var h = new Harness();
  h.Request(9, 1000, E(1));
  IndustrialSupportSettlement.OnCompleted(h.Ledger, h.Generation + 1, Batch(9, 1000), Batch(9, 1000));
  Assert.That(h.Ledger.Get(9).Stage, Is.EqualTo(IndustrialSupportStage.Requested));
  var parkOnly = Batch(9, 1000, AreaFinanceKind.ParkManagement);
  IndustrialSupportSettlement.OnCompleted(h.Ledger, h.Generation, parkOnly, parkOnly);
  Assert.That(h.Ledger.Get(9).Stage, Is.EqualTo(IndustrialSupportStage.Requested));
 }
 [Test] public void AJobThatCouldNotBeScheduledIsAccountedUnallocatedAndNeverQueuedAgain() {
  var h = new Harness();
  h.Request(9, 1000, E(1));
  h.Settle(1000);
  var batch = h.Prepare();
  h.Finish(batch, new long[batch.Receivers.Count]);
  Assert.That(h.Completed.Single().Credited, Is.Zero);
  Assert.That(h.Prepare().Receivers, Is.Empty);
  Assert.That(h.Money[E(1)], Is.Zero);
 }
 [TestCase(0, 100, 100, 100)] [TestCase(int.MaxValue - 10, int.MaxValue, 100, 10)] [TestCase(5, 5, 100, 0)] [TestCase(-50, 50, 100, 100)]
 public void CreditedIsTheActualIncreaseNeverAboveTheAllocation(int before, int after, int amount, long expected) =>
  Assert.That(IndustrialSupportCredit.Credited(before, after, amount), Is.EqualTo(expected));
 static AreaFinanceBatch Batch(ulong area, long amount, AreaFinanceKind kind = AreaFinanceKind.IndustrialSupport) {
  var runtime = new AreaFinanceRuntime(_ => { }); runtime.EnqueueExpense(area, kind, amount); return runtime.BeginApply(1);
 }
}
// Save after the city debit but before the receiver credit: the save flush credits and records exactly once.
public class IndustrialSupportSaveBoundaryTests {
 static Entity E(int id) => new() { Index = id, Version = 1 };
 [Test] public void SaveFlushCreditsSettledSupportOnceBeforeSerialization() {
  var h = new IndustrialSupportSettlementTests.Harness();
  h.Request(9, 1000, E(1), E(2));
  h.Settle(2000);
  void Flush(bool ready = true, int generation = 1) => AreaFinanceSaveBoundary.Flush(ready, generation, h.Generation, () => { },
   () => h.Ledger.All.Any(p => p.Stage == IndustrialSupportStage.Settled), () => { }, () => h.Run());
  Flush(ready: false); Flush(generation: 0);
  Assert.That(h.Money[E(1)], Is.Zero, "an unready or other-generation save credits nothing");
  Flush(); Flush();
  Assert.That(h.Money[E(1)] + h.Money[E(2)], Is.EqualTo(2000));
  Assert.That(h.Completed, Has.Count.EqualTo(1), "repeated saves never credit twice");
  Assert.That(h.Ledger.Count, Is.Zero, "nothing pending is serialized or replayed after load");
 }
 [Test] public void ARequestSettledAtTheFinanceFlushIsCreditedByTheFollowingSupportFlush() {
  var h = new IndustrialSupportSettlementTests.Harness();
  h.Request(9, 500, E(1));
  // AreaFinanceSaveSystem runs first and applies the queued request; its completion reaches the ledger.
  h.Settle(500);
  Assert.That(h.Ledger.Get(9).Stage, Is.EqualTo(IndustrialSupportStage.Settled));
  h.Run();
  Assert.That(h.Money[E(1)], Is.EqualTo(500));
 }
 [Test] public void SameDayLoadDoesNotScheduleAgain() {
  var state = IndustrialSupportModel.MarkScheduled(IndustrialSupportState.CreateDefault(), 12);
  var io = Support.MemorySerializer.Create(); state.Serialize(io.Writer);
  var loaded = new IndustrialSupportState(); loaded.Deserialize(io.Reader);
  Assert.That(IndustrialSupportModel.AlreadyScheduled(loaded, 12), Is.True);
 }
 [Test] public void SupportSaveFlushRunsAfterAreaFinanceAndBeforeTheSerializer() {
  var system = IndustrialSupportSourceGuardTests.Source("Modules/Industrial/IndustrialSupportSystem.cs");
  Assert.That(system, Does.Contain("AreaFinanceSaveBoundary.Flush("));
  Assert.That(system, Does.Contain("IAreaFinanceRequestListener"));
 }
}
