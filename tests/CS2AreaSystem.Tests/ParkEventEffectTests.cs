using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 2 C5 Task 3: festival effects enter the existing Park model inputs once, compose with policies and clamps, and
// end with the event or the Park's activity.
public class ParkEventEffectTests {
 static readonly ParkEventEffects Festival = ParkEventModel.Effects(ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 0, 1), 10, true);
 static ParkQualityContext Context(ParkPolicyEffects policies, ParkEventEffects? festival, int visitors = 60) {
  var c = ParkPolicyEffectTests.Context(policies, visitors: visitors); c.Event = festival; return c;
 }
 [Test] public void ActiveFestivalTakesItsShareOfComfortableCapacityOnly() {
  var baseline = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, null));
  var festival = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, Festival));
  Assert.That(festival.ComfortableCapacity, Is.EqualTo(0.85f * baseline.ComfortableCapacity).Within(1e-3));
  Assert.That(festival.OccupancyRatio, Is.GreaterThan(baseline.OccupancyRatio));
  Assert.That(festival.Comfort.Value, Is.LessThanOrEqualTo(baseline.Comfort.Value), "crowding pressure only");
  Assert.That((festival.Attractiveness.Value, festival.Variety.Value, festival.Accessibility.Value, festival.Maintenance.Value, festival.Landscape.Value),
   Is.EqualTo((baseline.Attractiveness.Value, baseline.Variety.Value, baseline.Accessibility.Value, baseline.Maintenance.Value, baseline.Landscape.Value)),
   "no attractiveness or other metric is touched");
  Assert.That(festival.TourismContribution, Is.EqualTo(baseline.TourismContribution), "no vanilla-facing attraction/tourism change");
 }
 [Test] public void FestivalCapacityComposesOnceWithLandscapeProtection() {
  var policy = ParkPolicyEffects.From(id => id == AreaPolicyIds.LandscapeProtection);
  var baseline = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, null));
  var both = ParkQualityEvaluator.Evaluate(Context(policy, Festival));
  Assert.That(both.ComfortableCapacity, Is.EqualTo(0.85f * 0.85f * baseline.ComfortableCapacity).Within(1e-3));
  var neutral = ParkQualityEvaluator.Evaluate(Context(policy, ParkEventEffects.Neutral));
  Assert.That(neutral.ComfortableCapacity, Is.EqualTo(0.85f * baseline.ComfortableCapacity).Within(1e-3), "neutral effects are the identity");
 }
 [Test] public void SuspendedOrExpiredFestivalRecomputesToTheNeutralBaseline() {
  var state = ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 1000, 1);
  var suspended = ParkEventModel.Effects(state, 2000, parkActive: false);
  var expired = ParkEventModel.Effects(state, 1000 + ParkEventCatalog.DurationFrames, parkActive: true);
  var baseline = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, null));
  Assert.That(ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, suspended)).ComfortableCapacity, Is.EqualTo(baseline.ComfortableCapacity));
  Assert.That(ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, expired)).ComfortableCapacity, Is.EqualTo(baseline.ComfortableCapacity));
 }
 [Test] public void CachedQualityFromAnEndedFestivalIsDetectedAsStale() {
  var snapshot = new ParkRuntimeSnapshot { QualityEventEffects = Festival };
  var state = ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 1000, 1);
  Assert.That(ParkEventModel.QualityNeedsRefresh(snapshot, state, 2000, true), Is.False);
  Assert.That(ParkEventModel.QualityNeedsRefresh(snapshot, state, 1000 + ParkEventCatalog.DurationFrames, true), Is.True, "expired at read time");
  Assert.That(ParkEventModel.QualityNeedsRefresh(snapshot, state, 2000, false), Is.True, "suspension neutralizes immediately");
  Assert.That(ParkEventModel.QualityNeedsRefresh(new ParkRuntimeSnapshot(), state, 2000, true), Is.True, "start applies on the next update");
  Assert.That(ParkEventModel.QualityNeedsRefresh(new ParkRuntimeSnapshot(), ParkEventState.CreateDefault(), 2000, true), Is.False);
 }
 [Test] public void SatisfactionBonusReachesStatisticsAndListenersOnceAsTheSameAdjustedEvent() {
  var park = new ParkAreaRef(new Entity { Index = 7, Version = 1 }, 7, CS2AreaSystem.Core.Areas.AreaLifecycleState.Active);
  var events = new List<ParkVisitEvent> {
   new() { Kind = ParkVisitEventKind.Completed, Citizen = new Entity { Index = 50, Version = 1 }, ParkId = 7, HasSatisfaction = true, Satisfaction = 60 },
   new() { Kind = ParkVisitEventKind.Completed, Citizen = new Entity { Index = 51, Version = 1 }, ParkId = 7, HasSatisfaction = false, Satisfaction = float.NaN },
   new() { Kind = ParkVisitEventKind.Started, Citizen = new Entity { Index = 52, Version = 1 }, ParkId = 7 } };
  var recorded = new List<ParkVisitEvent>(); var listener = new Listener(); int adjustments = 0;
  ParkVisitDispatcher.Dispatch(events, new Dictionary<ulong, ParkAreaRef> { [7] = park }, new List<ParkAreaRef> { park }, new ParkRuntimeStore(),
   new ParkVisitTracker(ParkVisitCadence.GraceFrames), 100, (p, e) => recorded.Add(e), new IParkVisitListener[] { listener }, _ => { },
   (p, e) => { adjustments++; e.Satisfaction = Festival.AdjustSatisfaction(e.Satisfaction, e.HasSatisfaction); return e; });
  Assert.That(adjustments, Is.EqualTo(2), "completed visits only, once each");
  Assert.That(recorded[0].Satisfaction, Is.EqualTo(65f)); Assert.That(listener.Completed[0].Satisfaction, Is.EqualTo(65f));
  Assert.That(float.IsNaN(recorded[1].Satisfaction) && !recorded[1].HasSatisfaction, Is.True, "unknown stays unknown");
  var state = ParkVisitModel.RecordCompletedVisit(ParkAreaState.CreateDefault(), false, recorded[0].Satisfaction, recorded[0].HasSatisfaction);
  Assert.That(state.Satisfaction.Mean, Is.EqualTo(65f));
 }
 [Test] public void FestivalVisitXpComposesWithThePolicyMultiplierButPassiveXpIsUnchanged() {
  double policy = AreaPolicyExperience.FocusedManagementMultiplier;
  long withFestival = ParkProgressionModel.AwardVisitXp(0, 100, true, Festival.ComposeXpMultiplier(policy));
  Assert.That(withFestival, Is.EqualTo((long)Math.Round(10 * 1.10 * 1.25, MidpointRounding.AwayFromZero)));
  Assert.That(ParkProgressionModel.AwardVisitXp(0, 100, true, ParkEventEffects.Neutral.ComposeXpMultiplier(policy)), Is.EqualTo(11));
  var progression = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkProgressionSystem.cs");
  Assert.That(progression, Does.Contain("ParkProgressionModel.AwardVisitXp(progress.Experience, visit.Satisfaction, true, EventEffects(area, frame).ComposeXpMultiplier(XpMultiplier(area)))"));
  int step = progression.IndexOf("ParkProgressionModel.Step(", StringComparison.Ordinal);
  Assert.That(progression.Substring(step, progression.IndexOf(';', step) - step), Does.Not.Contain("EventEffects"), "passive XP keeps its own multiplier and daily cap");
 }
 [Test] public void ParkSystemsReadEventEffectsFromTheEnabledEventModuleOnly() {
  var events = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkEventSystem.cs");
  Assert.That(events, Does.Contain("var system = world?.GetExistingSystemManaged<ParkEventSystem>();"));
  Assert.That(events, Does.Contain("if (system == null || !system.Enabled || !entities.Exists(area) || !entities.HasComponent<ParkEventState>(area)) return ParkEventEffects.Neutral;"));
  var quality = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkQualitySystem.cs");
  Assert.That(quality, Does.Contain("snapshot.QualityEventEffects = eventEffects;"));
  Assert.That(IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkVisitSystem.cs"), Does.Contain("ParkEventSystem.ReadVisitEffects("));
 }
 private sealed class Listener : IParkVisitListener {
  public readonly List<ParkVisitEvent> Completed = new();
  public void OnVisitStarted(in ParkVisitEvent v, Entity area, ParkRuntimeSnapshot s, ParkVisitTracker t) { }
  public void OnVisitCompleted(in ParkVisitEvent v, Entity area, ParkRuntimeSnapshot s, bool active) => Completed.Add(v);
 }
}
