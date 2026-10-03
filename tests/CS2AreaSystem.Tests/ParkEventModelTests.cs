using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 2 C5 Task 1: the Park Festival catalog, its full-day interval, cost and bounded internal effects.
public class ParkEventModelTests {
 static ParkEventState Started(uint frame, long paid = 100) => ParkEventModel.Start(ParkEventState.CreateDefault(), ParkEventCatalog.ParkFestival, frame, paid);
 [Test] public void CatalogHasOnlyTheParkFestival() {
  Assert.That(ParkEventCatalog.ParkFestival, Is.EqualTo(1));
  Assert.That(ParkEventCatalog.Ids, Is.EqualTo(new[] { 1 }));
  Assert.That(ParkEventCatalog.IsKnown(1), Is.True);
  Assert.That(ParkEventCatalog.IsKnown(0), Is.False); Assert.That(ParkEventCatalog.IsKnown(2), Is.False);
  Assert.That(ParkEventCatalog.DurationFrames, Is.EqualTo(262_144u));
 }
 [TestCase(1000.0, 500L)] [TestCase(1001.0, 500L)] [TestCase(1003.0, 502L)] [TestCase(1.0, 1L)] [TestCase(0.0, 1L)] [TestCase(2.9, 1L)] [TestCase(3.0, 2L)]
 public void CostIsHalfTheDailyManagementRoundedToEvenAndAtLeastOne(double daily, long expected) {
  Assert.That(ParkEventModel.TryCost(daily, out var cost), Is.True);
  Assert.That(cost, Is.EqualTo(expected));
 }
 [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1.0)]
 public void UnavailableDailyCostDisablesTheStart(double daily) => Assert.That(ParkEventModel.TryCost(daily, out _), Is.False);
 [Test] public void HugeDailyCostSaturates() {
  Assert.That(ParkEventModel.TryCost(double.MaxValue, out var cost), Is.True);
  Assert.That(cost, Is.EqualTo(long.MaxValue));
 }
 [Test] public void StartPersistsAFullDayIntervalAndThePaidCost() {
  var state = Started(1000, 250);
  Assert.That((state.ActiveEventId, state.StartFrame, state.EndFrameExclusive), Is.EqualTo((1, 1000u, 1000u + 262_144u)));
  Assert.That((state.LifetimeEvents, state.LifetimeEventExpense), Is.EqualTo((1L, 250L)));
  Assert.That(ParkEventModel.IsActive(state, 1000), Is.True);
  Assert.That(ParkEventModel.IsActive(state, 1000 + 262_143), Is.True);
  Assert.That(ParkEventModel.IsActive(state, 1000 + 262_144), Is.False, "end is exclusive");
  Assert.That(ParkEventModel.IsActive(state, 999), Is.False, "before the start (unsigned elapsed wraps high)");
 }
 [Test] public void NearMidnightStartStillLastsAFullDay() {
  uint lateEvening = 262_144u * 5 - 10;
  var state = Started(lateEvening);
  Assert.That(ParkEventModel.IsActive(state, 262_144u * 5 + 5), Is.True, "the next midnight does not end it");
  Assert.That(ParkEventModel.IsActive(state, lateEvening + 262_144u), Is.False);
 }
 [Test] public void FrameCounterWrapKeepsTheInterval() {
  uint start = uint.MaxValue - 100;
  var state = Started(start);
  Assert.That(state.EndFrameExclusive, Is.EqualTo(unchecked(start + 262_144u)));
  Assert.That(ParkEventModel.IsActive(state, 50), Is.True, "after wrap, still inside");
  Assert.That(ParkEventModel.IsActive(state, unchecked(start + 262_144u)), Is.False);
 }
 [Test] public void EffectsApplyOnlyWhileActiveAndTheParkIsActive() {
  var state = Started(1000);
  var on = ParkEventModel.Effects(state, 2000, parkActive: true);
  Assert.That((on.CapacityShare, on.SatisfactionBonus, on.VisitXpMultiplier), Is.EqualTo((0.85f, 5f, 1.25)));
  Assert.That(ParkEventModel.Effects(state, 2000, parkActive: false), Is.EqualTo(ParkEventEffects.Neutral), "suspended Park: neutral, time continues");
  Assert.That(ParkEventModel.Effects(state, 1000 + 262_144, parkActive: true), Is.EqualTo(ParkEventEffects.Neutral), "read-time expiry");
  Assert.That(ParkEventModel.Effects(ParkEventState.CreateDefault(), 2000, true), Is.EqualTo(ParkEventEffects.Neutral));
 }
 [Test] public void UnknownEventIdsAreNeutral() {
  var state = Started(1000); state.ActiveEventId = 7;
  Assert.That(ParkEventModel.IsActive(state, 2000), Is.False);
  Assert.That(ParkEventModel.Effects(state, 2000, true), Is.EqualTo(ParkEventEffects.Neutral));
 }
 [Test] public void ExpireClearsOnlyAnEndedOrUnknownEvent() {
  var state = Started(1000);
  Assert.That(ParkEventModel.Expire(state, 2000).ActiveEventId, Is.EqualTo(1));
  var ended = ParkEventModel.Expire(state, 1000 + 262_144);
  Assert.That(ended.ActiveEventId, Is.Zero);
  Assert.That((ended.StartFrame, ended.EndFrameExclusive, ended.LifetimeEvents), Is.EqualTo((1000u, 1000u + 262_144u, 1L)), "the record of the last event is kept");
 }
 [Test] public void SatisfactionBonusIsAddedOnceToKnownSamplesAndClamped() {
  var on = ParkEventModel.Effects(Started(1000), 2000, true);
  Assert.That(on.AdjustSatisfaction(60f, true), Is.EqualTo(65f));
  Assert.That(on.AdjustSatisfaction(98f, true), Is.EqualTo(100f));
  Assert.That(on.AdjustSatisfaction(-3f, true), Is.EqualTo(5f).Within(1e-6), "clamped into 0..100 before the bonus");
  Assert.That(float.IsNaN(on.AdjustSatisfaction(float.NaN, false)), Is.True, "unknown satisfaction stays unknown");
  Assert.That(on.AdjustSatisfaction(40f, false), Is.EqualTo(40f));
  Assert.That(ParkEventEffects.Neutral.AdjustSatisfaction(60f, true), Is.EqualTo(60f));
 }
 [Test] public void CapacityAndXpComposeOnceWithPolicies() {
  var on = ParkEventModel.Effects(Started(1000), 2000, true);
  Assert.That(on.ComposeCapacityShare(0.85f), Is.EqualTo(0.85f * 0.85f).Within(1e-6));
  Assert.That(on.ComposeXpMultiplier(1.10), Is.EqualTo(1.10 * 1.25).Within(1e-12));
  Assert.That(ParkEventEffects.Neutral.ComposeXpMultiplier(1.10), Is.EqualTo(1.10));
  // Existing saturating, uncapped visit XP is preserved: 10 base × 1.375 → 14.
  Assert.That(ParkProgressionModel.AwardVisitXp(0, 100, true, on.ComposeXpMultiplier(1.10)), Is.EqualTo(14));
  Assert.That(ParkProgressionModel.AwardVisitXp(long.MaxValue - 1, 100, true, on.ComposeXpMultiplier(1.0)), Is.EqualTo(long.MaxValue));
 }
 [Test] public void HistoryKeepsTheNewestSixteen() {
  var history = new List<ParkEventHistoryEntry>();
  for (uint i = 0; i < 20; i++) ParkEventModel.AppendHistory(history, ParkEventModel.HistoryEntry(Started(i * 10), i + 1));
  Assert.That(history, Has.Count.EqualTo(ParkEventModel.HistoryCapacity));
  Assert.That(ParkEventModel.HistoryCapacity, Is.EqualTo(16));
  Assert.That(history.Select(h => h.StartFrame), Is.EqualTo(Enumerable.Range(4, 16).Select(i => (uint)(i * 10))), "oldest dropped, order kept");
  Assert.That(history[^1].SettledExpense, Is.EqualTo(20));
 }
 [Test] public void UnstartedPaidExpenseIsAccountedWithoutStarting() {
  var state = ParkEventModel.RecordUnstartedExpense(ParkEventState.CreateDefault(), 300);
  Assert.That((state.ActiveEventId, state.LifetimeEvents, state.LifetimeEventExpense), Is.EqualTo((0, 0L, 300L)));
  state.LifetimeEventExpense = long.MaxValue - 1;
  Assert.That(ParkEventModel.RecordUnstartedExpense(state, 300).LifetimeEventExpense, Is.EqualTo(long.MaxValue));
 }
}
public class ParkEventSerializationTests {
 [Test] public void EventStateRoundTripsEveryAuthoritativeField() {
  var state = ParkEventModel.Start(ParkEventState.CreateDefault(), ParkEventCatalog.ParkFestival, uint.MaxValue - 5, 9_000_000_123);
  var io = MemorySerializer.Create(); state.Serialize(io.Writer);
  Assert.That(io.Data.Tokens[0], Is.EqualTo(ParkEventState.CurrentSchemaVersion));
  var copy = new ParkEventState(); copy.Deserialize(io.Reader);
  Assert.That(io.Data.Position, Is.EqualTo(io.Data.Tokens.Count));
  Assert.That((copy.ActiveEventId, copy.StartFrame, copy.EndFrameExclusive, copy.LifetimeEvents, copy.LifetimeEventExpense),
   Is.EqualTo((state.ActiveEventId, state.StartFrame, state.EndFrameExclusive, 1L, 9_000_000_123L)));
 }
 [Test] public void HistoryEntryRoundTrips() {
  var entry = new ParkEventHistoryEntry { EventId = 1, StartFrame = 7, EndFrameExclusive = 7 + 262_144, SettledExpense = 450 };
  var io = MemorySerializer.Create(); entry.Serialize(io.Writer);
  var copy = new ParkEventHistoryEntry(); copy.Deserialize(io.Reader);
  Assert.That(io.Data.Position, Is.EqualTo(io.Data.Tokens.Count));
  Assert.That((copy.SchemaVersion, copy.EventId, copy.StartFrame, copy.EndFrameExclusive, copy.SettledExpense),
   Is.EqualTo((ParkEventHistoryEntry.CurrentSchemaVersion, 1, 7u, 7u + 262_144u, 450L)));
 }
 [Test] public void UnsupportedFutureSchemasAreRejected() {
  var io = MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(1);
  Assert.Throws<System.IO.InvalidDataException>(() => new ParkEventState().Deserialize(io.Reader));
  io = MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(1);
  Assert.Throws<System.IO.InvalidDataException>(() => new ParkEventHistoryEntry().Deserialize(io.Reader));
 }
 [Test] public void OldSavesWithoutEventDataAreNeutral() {
  var state = ParkEventState.CreateDefault();
  Assert.That((state.SchemaVersion, state.ActiveEventId, state.LifetimeEvents), Is.EqualTo((ParkEventState.CurrentSchemaVersion, 0, 0L)));
  Assert.That(ParkEventModel.Effects(default, 123, true), Is.EqualTo(ParkEventEffects.Neutral), "a missing component reads as default");
 }
 [Test] public void SavedStateValidationRejectsImpossibleEventData() {
  static bool Invalid(ParkEventState s) { var r = new CS2AreaSystem.Core.Persistence.AreaSavedStateReport(); ParkSavedStateValidator.ValidateEvents(s, r); return r.IsInvalid; }
  Assert.That(Invalid(ParkEventState.CreateDefault()), Is.False);
  Assert.That(Invalid(ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 5, 10)), Is.False);
  var s = ParkEventState.CreateDefault(); s.LifetimeEventExpense = -1; Assert.That(Invalid(s), Is.True);
  s = ParkEventState.CreateDefault(); s.LifetimeEvents = -1; Assert.That(Invalid(s), Is.True);
  s = ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 5, 10); s.EndFrameExclusive = s.StartFrame + 10; Assert.That(Invalid(s), Is.True, "interval must be exactly one event duration");
 }
}
