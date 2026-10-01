using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkProgressionModelTests {
 private static ParkProgressInputs Good(uint hold = ParkProgressionModel.QualityHoldFrames) => new() {
  Quality = 99, LifetimeVisitors = 1_000_000, LifetimeTouristVisitors = 1_000_000, RecentSatisfaction = 99, Maintenance = 99, Variety = 99, Landscape = 99, HoldFrames = hold };
 [TestCase(1, 1_000, 40, 250)] [TestCase(2, 4_000, 55, 1_500)] [TestCase(3, 12_000, 70, 7_500)] [TestCase(4, 30_000, 82, 25_000)]
 public void PromotionTargetsMatchTheApprovedInitialValues(int level, long xp, float quality, long visitors) {
  var target = ParkProgressionModel.GetTarget(level);
  Assert.That(target.Experience, Is.EqualTo(xp)); Assert.That(target.Quality, Is.EqualTo(quality)); Assert.That(target.Visitors, Is.EqualTo(visitors));
 }
 [Test] public void EachThresholdIsRequired() {
  Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, Good()).CanPromote, Is.True);
  Assert.That(ParkProgressionModel.EvaluatePromotion(1, 999, Good()).CanPromote, Is.False, "XP");
  var i = Good(); i.Quality = 39.9f; Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, i).CanPromote, Is.False, "Quality");
  i = Good(); i.LifetimeVisitors = 249; Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, i).CanPromote, Is.False, "visitors");
  i = Good(); i.Variety = 44; Assert.That(ParkProgressionModel.EvaluatePromotion(2, 4_000, i).CanPromote, Is.False, "Lv3 variety");
  i = Good(); i.Landscape = 39; Assert.That(ParkProgressionModel.EvaluatePromotion(2, 4_000, i).CanPromote, Is.False, "Lv3 landscape");
  i = Good(); i.RecentSatisfaction = 64; Assert.That(ParkProgressionModel.EvaluatePromotion(3, 12_000, i).CanPromote, Is.False, "Lv4 satisfaction");
  i = Good(); i.Maintenance = 59; Assert.That(ParkProgressionModel.EvaluatePromotion(3, 12_000, i).CanPromote, Is.False, "Lv4 maintenance");
  Assert.That(ParkProgressionModel.EvaluatePromotion(5, long.MaxValue, Good()).CanPromote, Is.False, "Landmark is the top level");
 }
 [Test] public void LandmarkNeedsAnyTwoPrestigeObjectives() {
  var i = Good(); i.Landscape = 81; i.Variety = 76; i.LifetimeTouristVisitors = 0;
  Assert.That(ParkProgressionModel.EvaluatePromotion(4, 30_000, i).CanPromote, Is.True, "designed local park route");
  i = Good(); i.Landscape = 10; i.Variety = 76; i.LifetimeTouristVisitors = 5_000;
  Assert.That(ParkProgressionModel.EvaluatePromotion(4, 30_000, i).CanPromote, Is.True, "tourism route");
  i = Good(); i.Landscape = 10; i.Variety = 10; i.LifetimeTouristVisitors = 5_000;
  var result = ParkProgressionModel.EvaluatePromotion(4, 30_000, i);
  Assert.That(result.CanPromote, Is.False);
  Assert.That(result.Checklist.Count(c => c.Prestige), Is.EqualTo(3)); Assert.That(result.Checklist.Count(c => c.Prestige && c.Met), Is.EqualTo(1));
 }
 [Test] public void QualitySpikesDoNotPromoteUntilHeldForAboutADay() {
  Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, Good(hold: 0)).CanPromote, Is.False);
  Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, Good(hold: 0)).RequirementsMet, Is.True);
  uint hold = 0;
  hold = ParkProgressionModel.UpdateHold(hold, true, 100_000); hold = ParkProgressionModel.UpdateHold(hold, true, 100_000);
  Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, Good(hold)).CanPromote, Is.False);
  hold = ParkProgressionModel.UpdateHold(hold, false, 1);
  Assert.That(hold, Is.Zero, "a drop restarts the hold");
  hold = ParkProgressionModel.UpdateHold(hold, true, ParkProgressionModel.QualityHoldFrames);
  Assert.That(ParkProgressionModel.EvaluatePromotion(1, 1_000, Good(hold)).CanPromote, Is.True);
  Assert.That(ParkProgressionModel.UpdateHold(uint.MaxValue - 1, true, 100), Is.EqualTo(uint.MaxValue), "saturates");
 }
 [Test] public void EarnedLevelNeverDemotes() {
  var bad = new ParkProgressInputs();
  for (int level = 1; level <= 5; level++) Assert.That(ParkProgressionModel.EvaluatePromotion(level, 0, bad).NextLevel, Is.EqualTo(level));
  Assert.That(ParkProgressionModel.ApplyPromotion(3, false), Is.EqualTo(3));
  Assert.That(ParkProgressionModel.ApplyPromotion(3, true), Is.EqualTo(4));
  Assert.That(ParkProgressionModel.ApplyPromotion(5, true), Is.EqualTo(5));
  Assert.That(ParkProgressionModel.NormalizeLevel(0), Is.EqualTo(1));
 }
 [Test] public void VisitXpDependsOnSatisfactionAndIsNeverNegative() {
  Assert.That(ParkProgressionModel.ComputeVisitXp(100), Is.GreaterThan(ParkProgressionModel.ComputeVisitXp(50)));
  Assert.That(ParkProgressionModel.ComputeVisitXp(50), Is.GreaterThan(ParkProgressionModel.ComputeVisitXp(0)));
  Assert.That(ParkProgressionModel.ComputeVisitXp(0), Is.GreaterThan(0), "low satisfaction still teaches something");
  Assert.That(ParkProgressionModel.ComputeVisitXp(-500), Is.GreaterThanOrEqualTo(0));
  Assert.That(ParkProgressionModel.ComputeVisitXp(double.NaN), Is.GreaterThanOrEqualTo(0));
 }
 [Test] public void PassiveQualityXpNeedsActivityAndIsCappedPerDay() {
  Assert.That(ParkProgressionModel.ComputePassiveXp(90, recentVisitors: 0, active: true, grantedToday: 0, 1.0), Is.Zero);
  Assert.That(ParkProgressionModel.ComputePassiveXp(90, 10, active: false, grantedToday: 0, 1.0), Is.Zero, "suspended parks earn nothing");
  Assert.That(ParkProgressionModel.ComputePassiveXp(40, 10, true, 0, 1.0), Is.Zero, "only strong quality");
  Assert.That(ParkProgressionModel.ComputePassiveXp(90, 10, true, 0, 1.0), Is.GreaterThan(0));
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, ParkProgressionModel.PassiveXpDailyCap, 1.0), Is.Zero, "daily cap reached");
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, ParkProgressionModel.PassiveXpDailyCap - 1, 1.0), Is.EqualTo(1));
 }
}
public class ParkProgressionStepTests {
 private static ParkQualitySnapshot Quality(float value) {
  var q = new ParkQualitySnapshot { Satisfaction = ParkMetric.Of(value), Attractiveness = ParkMetric.Of(value), Accessibility = ParkMetric.Of(value),
   Variety = ParkMetric.Of(value), Maintenance = ParkMetric.Of(value), Comfort = ParkMetric.Of(value), Landscape = ParkMetric.Of(value) };
  q.Result = new ParkQualityResult { Quality = value, Confidence = 1 };
  return q;
 }
 private static ParkAreaState Visited(long visitors) { var s = ParkAreaState.CreateDefault(); s.LifetimeVisitors = visitors; s.Satisfaction = new ParkSatisfactionState { Mean = 80, Weight = 50 }; return s; }
 private static ParkProgressStep Step(int level, long xp, ParkAreaState state, bool active, float quality, uint frame, uint elapsed = ParkProgressionSystem.UpdateFrames, long recent = 10) =>
  ParkProgressionModel.Step(new ParkProgressStep { Level = level, Experience = xp, State = state }, ParkProgressionSystem.BuildInputs(Quality(quality), state), active, recent, frame, elapsed, 1.0);
 [Test] public void PromotesOnlyAfterRequirementsHoldForADay() {
  var step = Step(1, 1_000, Visited(300), true, 60, 10_000);
  Assert.That(step.Level, Is.EqualTo(1)); Assert.That(step.State.PromotionHoldFrames, Is.EqualTo(ParkProgressionSystem.UpdateFrames));
  for (uint f = 1; f <= 40 && step.Level == 1; f++) step = ParkProgressionModel.Step(step, ParkProgressionSystem.BuildInputs(Quality(60), step.State), true, 10, 10_000 + f * ParkProgressionSystem.UpdateFrames, ParkProgressionSystem.UpdateFrames, 1.0);
  Assert.That(step.Level, Is.EqualTo(2)); Assert.That(step.Promoted, Is.True); Assert.That(step.State.PromotionHoldFrames, Is.Zero, "hold restarts for the next level");
 }
 [Test] public void SuspendedParksNeitherEarnNorPromoteButKeepProgress() {
  var state = Visited(300); state.PromotionHoldFrames = ParkProgressionModel.QualityHoldFrames;
  var step = Step(1, 5_000, state, active: false, quality: 95, frame: 10_000);
  Assert.That(step.Level, Is.EqualTo(1)); Assert.That(step.Experience, Is.EqualTo(5_000));
  Assert.That(step.State.PromotionHoldFrames, Is.Zero); Assert.That(step.State.LifetimeVisitors, Is.EqualTo(300));
 }
 [Test] public void QualityCollapseNeverDemotes() {
  var step = Step(4, 20_000, Visited(10_000), true, 0, 10_000);
  Assert.That(step.Level, Is.EqualTo(4)); Assert.That(step.Experience, Is.EqualTo(20_000));
 }
 [Test] public void PassiveXpIsCappedPerInGameDayAndResetsNextDay() {
  var state = Visited(300); long xp = 0; var step = new ParkProgressStep { Level = 1, Experience = 0, State = state };
  uint frame = 5;
  for (int i = 0; i < 32; i++) { frame += ParkProgressionSystem.UpdateFrames; step = ParkProgressionModel.Step(step, ParkProgressionSystem.BuildInputs(Quality(100), step.State), true, 10, frame, ParkProgressionSystem.UpdateFrames, 1.0); }
  xp = step.Experience;
  Assert.That(xp, Is.LessThanOrEqualTo(ParkProgressionModel.PassiveXpDailyCap * 2), "at most one cap per in-game day crossed");
  long before = step.Experience;
  step = ParkProgressionModel.Step(step, ParkProgressionSystem.BuildInputs(Quality(100), step.State), true, 0, frame + ParkProgressionSystem.UpdateFrames, ParkProgressionSystem.UpdateFrames, 1.0);
  Assert.That(step.Experience, Is.EqualTo(before), "no recent visitors, no passive XP");
  Assert.That(step.State.PassiveXpToday, Is.LessThanOrEqualTo(ParkProgressionModel.PassiveXpDailyCap));
 }
 [Test] public void VisitXpGoesToActiveParksOnly() {
  Assert.That(ParkProgressionModel.AwardVisitXp(100, 80, true, 1.0), Is.EqualTo(100 + ParkProgressionModel.ComputeVisitXp(80)));
  Assert.That(ParkProgressionModel.AwardVisitXp(100, 80, false, 1.0), Is.EqualTo(100));
  Assert.That(ParkProgressionModel.AwardVisitXp(long.MaxValue, 100, true, 1.0), Is.EqualTo(long.MaxValue), "saturates");
 }
}
public class ParkGameplayFacadeTests {
 [Test] public void BudgetActionAcceptsPercentWithinTheServiceRange() {
  var settings = ParkStateDefaults.Settings(); var state = ParkAreaState.CreateDefault();
  Assert.That(ParkGameplayFacade.TryApplyAction("setBudget", "120", 1, ref settings, ref state), Is.True);
  Assert.That(settings.BudgetMultiplier, Is.EqualTo(1.2f).Within(1e-5));
  Assert.That(ParkGameplayFacade.TryApplyAction("setBudget", "400", 1, ref settings, ref state), Is.True);
  Assert.That(settings.BudgetMultiplier, Is.EqualTo(1.5f));
  Assert.That(ParkGameplayFacade.TryApplyAction("setBudget", "lots", 1, ref settings, ref state), Is.False);
  Assert.That(settings.BudgetMultiplier, Is.EqualTo(1.5f));
 }
 [Test] public void FeeActionRequiresLevelTwo() {
  var settings = ParkStateDefaults.Settings(); var state = ParkAreaState.CreateDefault();
  Assert.That(ParkGameplayFacade.TryApplyAction("setFee", "20", 1, ref settings, ref state), Is.False);
  Assert.That(state.AdmissionFee, Is.Zero);
  Assert.That(ParkGameplayFacade.TryApplyAction("setFee", "20", 2, ref settings, ref state), Is.True);
  Assert.That(state.AdmissionFee, Is.EqualTo(20));
  Assert.That(ParkGameplayFacade.TryApplyAction("setFee", "9999", 2, ref settings, ref state), Is.True);
  Assert.That(state.AdmissionFee, Is.EqualTo(ParkFinanceModel.MaxAdmissionFee));
 }
 [Test] public void UnknownOrPolicyActionsAreRejected() {
  var settings = ParkStateDefaults.Settings(); var state = ParkAreaState.CreateDefault();
  foreach (var action in new[] { "", null, "setPolicy", "startEvent", "addXp", "setLevel" })
   Assert.That(ParkGameplayFacade.TryApplyAction(action, "1", 5, ref settings, ref state), Is.False);
 }
}
public class ParkFocusedManagementXpTests {
 private static ParkQualitySnapshot Q(float value) {
  var q = new ParkQualitySnapshot { Satisfaction = ParkMetric.Of(value), Attractiveness = ParkMetric.Of(value), Accessibility = ParkMetric.Of(value),
   Variety = ParkMetric.Of(value), Maintenance = ParkMetric.Of(value), Comfort = ParkMetric.Of(value), Landscape = ParkMetric.Of(value) };
  q.Result = new ParkQualityResult { Quality = value, Confidence = 1 };
  return q;
 }
 [Test] public void VisitXpIsRaisedByTenPercent() {
  Assert.That(ParkProgressionModel.ComputeVisitXp(100), Is.EqualTo(10));
  Assert.That(ParkProgressionModel.AwardVisitXp(100, 100, true, 1.10), Is.EqualTo(111));
  Assert.That(ParkProgressionModel.AwardVisitXp(100, 100, false, 1.10), Is.EqualTo(100), "a suspended Park earns nothing");
  Assert.That(ParkProgressionModel.AwardVisitXp(long.MaxValue - 3, 100, true, 1.10), Is.EqualTo(long.MaxValue));
 }
 [Test] public void PassiveXpIsMultipliedBeforeTheDailyCap() {
  int baseAward = ParkProgressionModel.ComputePassiveXp(100, 10, true, 0, 1.0);
  Assert.That(baseAward, Is.EqualTo(5));
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, 0, 1.10), Is.EqualTo(6));
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, ParkProgressionModel.PassiveXpDailyCap - 2, 1.10), Is.EqualTo(2), "the cap still holds");
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, ParkProgressionModel.PassiveXpDailyCap, 1.10), Is.Zero);
  Assert.That(ParkProgressionModel.ComputePassiveXp(40, 10, true, 0, 1.10), Is.Zero, "no base award: no bonus");
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, false, 0, 1.10), Is.Zero);
 }
 [Test] public void ADayOfFocusedManagementNeverExceedsTheFortyXpCap() {
  var step = new ParkProgressStep { Level = 1, State = ParkAreaState.CreateDefault() };
  // Every tick of one in-game day (32 ticks of 1/32 day).
  for (uint i = 0; i < 32; i++) {
   uint frame = 5 * ParkProgressionModel.FramesPerDay + i * ParkProgressionSystem.UpdateFrames;
   step = ParkProgressionModel.Step(step, ParkProgressionSystem.BuildInputs(Q(100), step.State), true, 10, frame, ParkProgressionSystem.UpdateFrames, 1.10);
  }
  Assert.That(step.Experience, Is.EqualTo(ParkProgressionModel.PassiveXpDailyCap));
 }
 [Test] public void StepAppliesTheMultiplierToPassiveXp() {
  var state = ParkAreaState.CreateDefault();
  var inputs = ParkProgressionSystem.BuildInputs(Q(100), state);
  var plain = ParkProgressionModel.Step(new ParkProgressStep { Level = 1, State = state }, inputs, true, 10, 1000, ParkProgressionSystem.UpdateFrames, 1.0);
  var focused = ParkProgressionModel.Step(new ParkProgressStep { Level = 1, State = state }, inputs, true, 10, 1000, ParkProgressionSystem.UpdateFrames, 1.10);
  Assert.That(plain.Experience, Is.EqualTo(5)); Assert.That(focused.Experience, Is.EqualTo(6));
  Assert.That(focused.State.PassiveXpToday, Is.EqualTo(6), "the cap counts the multiplied award");
  var suspended = ParkProgressionModel.Step(new ParkProgressStep { Level = 1, State = state }, inputs, false, 10, 1000, ParkProgressionSystem.UpdateFrames, 1.10);
  Assert.That(suspended.Experience, Is.Zero);
 }
}

