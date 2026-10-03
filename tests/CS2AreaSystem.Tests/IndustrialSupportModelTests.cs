using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 2 C4: per-company daily support. Pure state and allocation rules (spec §C4, Phase 2 clarifications).
public class IndustrialSupportModelTests {
 [Test] public void SettingRangeIsZeroToFiveThousandInStepsOf250DefaultZero() {
  Assert.That(IndustrialSupportModel.MinPerCompany, Is.Zero);
  Assert.That(IndustrialSupportModel.MaxPerCompany, Is.EqualTo(5000));
  Assert.That(IndustrialSupportModel.Step, Is.EqualTo(250));
  Assert.That(IndustrialSupportState.CreateDefault().DailySupportPerCompany, Is.Zero);
 }
 [TestCase(-5, 0)] [TestCase(0, 0)] [TestCase(250, 250)] [TestCase(5000, 5000)] [TestCase(9000, 5000)] [TestCase(374, 250)] [TestCase(375, 500)]
 public void SettingIsClampedAndSnappedToTheStep(int requested, int expected) =>
  Assert.That(IndustrialSupportModel.NormalizeSetting(requested), Is.EqualTo(expected));
 [Test] public void DailyRequestIsReceiverCountTimesSettingAndSaturates() {
  Assert.That(IndustrialSupportModel.DailyRequest(3, 1000), Is.EqualTo(3000));
  Assert.That(IndustrialSupportModel.DailyRequest(0, 1000), Is.Zero);
  Assert.That(IndustrialSupportModel.DailyRequest(5, 0), Is.Zero);
  Assert.That(IndustrialSupportModel.DailyRequest(int.MaxValue, 5000), Is.EqualTo((long)int.MaxValue * 5000));
  Assert.That(IndustrialSupportModel.DailyRequest(-1, 5000), Is.Zero);
 }
 [Test] public void PartialSettlementIsSplitEvenlyWithRemaindersToEarlierReceivers() {
  var allocation = IndustrialSupportModel.Allocate(1001, 1000, new[] { true, true, true });
  Assert.That(allocation.Amounts, Is.EqualTo(new long[] { 334, 334, 333 }));
  Assert.That(allocation.Unallocated, Is.Zero);
 }
 [Test] public void HalfSettlementWithUnchangedReceiversCreditsExactlyTheSettledHalf() {
  var allocation = IndustrialSupportModel.Allocate(1500, 1000, new[] { true, true, true });
  Assert.That(allocation.Amounts.Sum(), Is.EqualTo(1500));
  Assert.That(allocation.Amounts, Is.EqualTo(new long[] { 500, 500, 500 }));
 }
 [Test] public void RemovedReceiversAreNotReplacedAndCapsLeaveExcessUnallocated() {
  var allocation = IndustrialSupportModel.Allocate(3000, 1000, new[] { true, false, true });
  Assert.That(allocation.Amounts, Is.EqualTo(new long[] { 1000, 0, 1000 }), "each receiver is capped at the accepted per-company amount");
  Assert.That(allocation.Unallocated, Is.EqualTo(1000));
 }
 [Test] public void ZeroReceiversLeaveTheWholeSettlementUnallocated() {
  var allocation = IndustrialSupportModel.Allocate(2000, 1000, new[] { false, false });
  Assert.That(allocation.Amounts, Is.EqualTo(new long[] { 0, 0 }));
  Assert.That(allocation.Unallocated, Is.EqualTo(2000));
  Assert.That(IndustrialSupportModel.Allocate(2000, 1000, Array.Empty<bool>()).Unallocated, Is.EqualTo(2000));
 }
 [Test] public void ZeroSettlementAllocatesNothing() {
  var allocation = IndustrialSupportModel.Allocate(0, 1000, new[] { true, true });
  Assert.That(allocation.Amounts, Is.EqualTo(new long[] { 0, 0 }));
  Assert.That(allocation.Unallocated, Is.Zero);
 }
 [Test] public void CompletionAccountingKeepsSettledEqualToCreditedPlusUnallocated() {
  var state = IndustrialSupportState.CreateDefault();
  state = IndustrialSupportModel.RecordCompletion(state, requested: 3000, settled: 1500, credited: 1200);
  Assert.That(state.LifetimeRequested, Is.EqualTo(3000));
  Assert.That(state.LifetimeSettled, Is.EqualTo(1500));
  Assert.That(state.LifetimeCredited, Is.EqualTo(1200));
  Assert.That(state.LifetimeUnallocated, Is.EqualTo(300));
  state = IndustrialSupportModel.RecordCompletion(state, requested: 100, settled: 0, credited: 0);
  Assert.That(state.LifetimeRequested, Is.EqualTo(3100));
  Assert.That(state.LifetimeSettled, Is.EqualTo(state.LifetimeCredited + state.LifetimeUnallocated));
 }
 [Test] public void CompletionNeverCreditsMoreThanWasSettled() {
  var state = IndustrialSupportModel.RecordCompletion(IndustrialSupportState.CreateDefault(), requested: 100, settled: 50, credited: 80);
  Assert.That(state.LifetimeCredited, Is.EqualTo(50));
  Assert.That(state.LifetimeUnallocated, Is.Zero);
 }
 [Test] public void LifetimeCountersSaturate() {
  var state = IndustrialSupportState.CreateDefault();
  state.LifetimeRequested = state.LifetimeSettled = state.LifetimeCredited = long.MaxValue - 1;
  state = IndustrialSupportModel.RecordCompletion(state, 10, 10, 10);
  Assert.That(state.LifetimeRequested, Is.EqualTo(long.MaxValue));
  Assert.That(state.LifetimeSettled, Is.EqualTo(long.MaxValue));
  Assert.That(state.LifetimeCredited, Is.EqualTo(long.MaxValue));
 }
 [Test] public void DayZeroAndUninitializedStateAreNeverAnAlreadyScheduledDay() {
  Assert.That(IndustrialSupportModel.AlreadyScheduled(IndustrialSupportState.CreateDefault(), 0), Is.False);
  Assert.That(IndustrialSupportModel.AlreadyScheduled(default, 0), Is.False, "an uninitialized component is not day zero");
  var state = IndustrialSupportModel.MarkScheduled(IndustrialSupportState.CreateDefault(), 0);
  Assert.That(IndustrialSupportModel.AlreadyScheduled(state, 0), Is.True);
  Assert.That(IndustrialSupportModel.AlreadyScheduled(state, 1), Is.False);
 }
 [Test] public void SchemaV1WritesExactlyTheApprovedFieldsInOrderAndRoundTrips() {
  var sample = new IndustrialSupportState { SchemaVersion = 1, DailySupportPerCompany = 750, LastScheduledDay = 41,
   LifetimeRequested = 3000, LifetimeSettled = 2000, LifetimeCredited = 1500, LifetimeUnallocated = 500 };
  var io = MemorySerializer.Create();
  sample.Serialize(io.Writer);
  Assert.That(io.Data.Tokens, Is.EqualTo(new object[] { (ushort)1, 750, 41L, 3000L, 2000L, 1500L, 500L }));
  var copy = new IndustrialSupportState(); copy.Deserialize(io.Reader);
  Assert.That(io.Data.Position, Is.EqualTo(io.Data.Tokens.Count));
  Assert.That(copy, Is.EqualTo(sample));
 }
 [Test] public void UnsupportedFutureSchemaIsRejected() {
  var io = MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(99);
  Assert.Throws<System.IO.InvalidDataException>(() => new IndustrialSupportState().Deserialize(io.Reader));
 }
 [Test] public void LoadedSettingOutsideTheRangeIsNormalized() {
  var io = MemorySerializer.Create();
  new IndustrialSupportState { SchemaVersion = 1, DailySupportPerCompany = 99999, LastScheduledDay = -1 }.Serialize(io.Writer);
  var copy = new IndustrialSupportState(); copy.Deserialize(io.Reader);
  Assert.That(copy.DailySupportPerCompany, Is.EqualTo(5000));
 }
 [Test] public void IndustrialSupportIsAnAreaManagementExpense() {
  Assert.That((byte)AreaFinanceKind.IndustrialSupport, Is.EqualTo(3));
  Assert.That(AreaFinanceKinds.IsExpense(AreaFinanceKind.IndustrialSupport), Is.True);
  Assert.That(AreaFinanceKinds.IsIncome(AreaFinanceKind.IndustrialSupport), Is.False);
 }
}
