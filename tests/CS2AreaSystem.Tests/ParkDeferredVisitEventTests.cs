using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkDeferredVisitEventTests {
 static Entity E(int i) => new() { Index = i, Version = 1 };
 static uint EventFrame(ParkVisitEvent visit) {
  var field = typeof(ParkVisitEvent).GetField("EventFrame");
  Assert.That(field, Is.Not.Null, "runtime events must retain their observation/completion frame");
  return (uint)field.GetValue(visit);
 }
 static ParkEventEffects Historical(ParkEventHistoryEntry entry, uint frame, bool active = true) {
  var method = typeof(ParkEventModel).GetMethods().SingleOrDefault(m => m.Name == "HistoricalEffects" && m.GetParameters()[0].ParameterType == typeof(ParkEventHistoryEntry).MakeByRefType());
  Assert.That(method, Is.Not.Null, "expired Festival history must still resolve deferred visit effects");
  return (ParkEventEffects)method.Invoke(null, new object[] { entry, frame, active });
 }
 [TestCase(false, 90u, 110u, 100f, 10L)]
 [TestCase(true, 262_240u, 262_250u, 100f, 13L)]
 public void DeferredVisitUsesCompletionFrameForSatisfactionAndXp(bool expiredBeforeDispatch, uint observed, uint dispatched, float expectedSatisfaction, long expectedXp) {
  var state = ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 100, 500);
  var history = ParkEventModel.HistoryEntry(state, 500);
  var tracker = new ParkVisitTracker(0); var events = new List<ParkVisitEvent>();
  tracker.Observe(E(50), 7, false, 100, observed - 2, events);
  tracker.Expire(observed, events);
  var visit = events.Single(e => e.Kind == ParkVisitEventKind.Completed);
  Assert.That(EventFrame(visit), Is.EqualTo(observed));
  if (expiredBeforeDispatch) {
   state = ParkEventModel.Expire(state, dispatched);
   Assert.That(state.ActiveEventId, Is.Zero, "normal expiry may run before deferred dispatch");
  }
  var effects = Historical(history, EventFrame(visit));
  Assert.That(effects.VisitXpMultiplier, Is.EqualTo(expiredBeforeDispatch ? 1.25 : 1.0));
  var park = new ParkAreaRef(E(7), 7, AreaLifecycleState.Active);
  var listener = new XpListener(effects); var recorded = new List<ParkVisitEvent>();
  ParkVisitDispatcher.Dispatch(new[] { visit }, new Dictionary<ulong, ParkAreaRef> { [7] = park }, new[] { park }, new ParkRuntimeStore(), tracker,
   dispatched, (p, e) => recorded.Add(e), new IParkVisitListener[] { listener }, error => throw error,
   (p, e) => { e.Satisfaction = effects.AdjustSatisfaction(e.Satisfaction, e.HasSatisfaction); return e; });
  Assert.That(recorded.Single().Satisfaction, Is.EqualTo(expectedSatisfaction));
  Assert.That(listener.Xp, Is.EqualTo(expectedXp));
 }
 [TestCase(false, 90u, 110u, 60f)]
 [TestCase(true, 262_240u, 262_250u, 65f)]
 public void DeferredKnownSatisfactionDoesNotGainOrLoseFestivalBonusAtDispatch(bool expired, uint observed, uint dispatched, float expected) {
  var history = ParkEventModel.HistoryEntry(ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 100, 500), 500);
  var tracker = new ParkVisitTracker(0); var events = new List<ParkVisitEvent>();
  tracker.Observe(E(50), 7, false, 60, observed - 2, events); tracker.Expire(observed, events);
  var visit = events.Single(e => e.Kind == ParkVisitEventKind.Completed);
  Assert.That(Historical(history, EventFrame(visit)).AdjustSatisfaction(visit.Satisfaction, visit.HasSatisfaction), Is.EqualTo(expected));
 }
 [Test] public void TrackerStampsStartParkSwitchAndExpiryWhenEachEventOccurs() {
  var tracker = new ParkVisitTracker(0); var events = new List<ParkVisitEvent>();
  tracker.Observe(E(50), 7, false, 60, 100, events); tracker.Observe(E(50), 9, false, 60, 200, events); tracker.Expire(300, events);
  Assert.That(events.Select(EventFrame), Is.EqualTo(new uint[] { 100, 200, 200, 300 }));
 }
 [Test] public void HistoricalEffectsRespectIntervalAndParkActivity() {
  var entry = ParkEventModel.HistoryEntry(ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 100, 1), 1);
  Assert.That(Historical(entry, 99).IsNeutral, Is.True); Assert.That(Historical(entry, 100).VisitXpMultiplier, Is.EqualTo(1.25));
  Assert.That(Historical(entry, 262_244).IsNeutral, Is.True); Assert.That(Historical(entry, 110, false).IsNeutral, Is.True);
 }
 [Test] public void BothNativeEffectReadersUseTheSameEventFrameAndCanReadHistory() {
  var visits = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkVisitSystem.cs");
  var xp = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkProgressionSystem.cs");
  Assert.That(visits.Contains("ParkEventSystem.ReadVisitEffects(World, EntityManager, park.Area, park.State == AreaLifecycleState.Active, e.EventFrame)"), Is.True);
  Assert.That(xp.Contains("uint frame = visit.EventFrame;"), Is.True);
  Assert.That(xp.Contains("ParkEventSystem.ReadVisitEffects(World, EntityManager, area, true, frame)"), Is.True);
 }
 sealed class XpListener(ParkEventEffects effects) : IParkVisitListener {
  public long Xp;
  public void OnVisitStarted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, ParkVisitTracker tracker) { }
  public void OnVisitCompleted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, bool parkActive) =>
   Xp = ParkProgressionModel.AwardVisitXp(Xp, visit.Satisfaction, parkActive, effects.VisitXpMultiplier);
 }
}
