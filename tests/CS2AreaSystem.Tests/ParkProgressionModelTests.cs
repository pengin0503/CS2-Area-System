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
  Assert.That(ParkProgressionModel.ComputePassiveXp(90, recentVisitors: 0, active: true, grantedToday: 0), Is.Zero);
  Assert.That(ParkProgressionModel.ComputePassiveXp(90, 10, active: false, grantedToday: 0), Is.Zero, "suspended parks earn nothing");
  Assert.That(ParkProgressionModel.ComputePassiveXp(40, 10, true, 0), Is.Zero, "only strong quality");
  Assert.That(ParkProgressionModel.ComputePassiveXp(90, 10, true, 0), Is.GreaterThan(0));
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, ParkProgressionModel.PassiveXpDailyCap), Is.Zero, "daily cap reached");
  Assert.That(ParkProgressionModel.ComputePassiveXp(100, 10, true, ParkProgressionModel.PassiveXpDailyCap - 1), Is.EqualTo(1));
 }
}
