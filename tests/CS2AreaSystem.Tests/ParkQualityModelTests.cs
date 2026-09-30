using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkQualityModelTests {
 private static ParkMetric M(float value) => ParkMetric.Of(value);
 [Test] public void QualityUsesTheApprovedWeights() {
  void Only(Func<ParkQualityInputs, ParkQualityInputs> set, float expected) {
   var zero = new ParkQualityInputs { Satisfaction = M(0), Attractiveness = M(0), Accessibility = M(0), Variety = M(0), Maintenance = M(0), Comfort = M(0) };
   Assert.That(ParkQualityModel.ComputeQuality(set(zero)).Quality, Is.EqualTo(expected).Within(0.001));
  }
  Only(i => { i.Satisfaction = M(100); return i; }, 25);
  Only(i => { i.Attractiveness = M(100); return i; }, 20);
  Only(i => { i.Accessibility = M(100); return i; }, 15);
  Only(i => { i.Variety = M(100); return i; }, 15);
  Only(i => { i.Maintenance = M(100); return i; }, 15);
  Only(i => { i.Comfort = M(100); return i; }, 10);
 }
 [Test] public void UnavailableMetricUsesNeutralContributionAndDegradesConfidence() {
  var full = new ParkQualityInputs { Satisfaction = M(90), Attractiveness = M(90), Accessibility = M(90), Variety = M(90), Maintenance = M(90), Comfort = M(90) };
  var weakLandscape = full; weakLandscape.Attractiveness = M(10);
  var missing = weakLandscape; missing.Attractiveness = ParkMetric.Unavailable;
  var a = ParkQualityModel.ComputeQuality(full); var b = ParkQualityModel.ComputeQuality(missing);
  Assert.That(a.Degraded, Is.False); Assert.That(a.Confidence, Is.EqualTo(1f));
  Assert.That(b.Degraded, Is.True); Assert.That(b.Confidence, Is.EqualTo(0.8f).Within(0.001));
  Assert.That(b.Quality, Is.EqualTo(0.8f * 90 + 0.2f * ParkQualityModel.NeutralMetric).Within(0.001), "neutral ~50, no renormalization");
  Assert.That(b.Quality, Is.LessThan(a.Quality), "losing a source never raises Quality above the fully-known strong park");
  var strongMissing = full; strongMissing.Attractiveness = ParkMetric.Unavailable;
  Assert.That(ParkQualityModel.ComputeQuality(strongMissing).Quality, Is.LessThan(a.Quality));
 }
 [Test] public void ComfortFollowsTheApprovedCrowdingCurve() {
  float C(double ratio) => ParkQualityModel.ComputeComfort(ratio * 100, 100);
  Assert.That(C(0), Is.EqualTo(100)); Assert.That(C(0.59), Is.EqualTo(100), "<60% no penalty");
  Assert.That(C(1.0), Is.InRange(80f, 95f), "60-100% mild");
  Assert.That(C(1.5), Is.InRange(35f, 55f), "100-150% meaningful");
  Assert.That(C(3.0), Is.LessThan(C(1.5))); Assert.That(C(50), Is.GreaterThanOrEqualTo(ParkQualityModel.MinComfort), "severe but bounded");
  Assert.That(ParkQualityModel.ComputeComfort(0, 0), Is.EqualTo(100));
 }
 [Test] public void ComfortableCapacityIgnoresLevelAndHasDiminishingReturns() {
  Assert.That(typeof(ParkCapacityInputs).GetFields().Select(f => f.Name), Has.None.Contains("Level"), "Level must not manufacture capacity");
  var small = new ParkCapacityInputs { AreaSquareMeters = 10_000, PathLength = 300, LeisureEfficiencySum = 40, AmenityCount = 10 };
  var big = small; big.AreaSquareMeters = 40_000;
  Assert.That(ParkQualityModel.ComputeComfortableCapacity(big), Is.GreaterThan(ParkQualityModel.ComputeComfortableCapacity(small)));
  Assert.That(ParkQualityModel.ComputeComfortableCapacity(big), Is.LessThan(4 * ParkQualityModel.ComputeComfortableCapacity(small)));
 }
 [Test] public void AccessibilityRewardsEntrancesWithDiminishingReturnsAndSpread() {
  float A(int n, float spread = 1, int stops = 0, float path = 0) => ParkQualityModel.ComputeAccessibility(n, spread, stops, path);
  Assert.That(A(0), Is.Zero);
  Assert.That(A(1), Is.EqualTo(40f).Within(0.001), "one valid entrance = baseline 40%");
  Assert.That(A(2), Is.GreaterThan(A(1))); Assert.That(A(3), Is.GreaterThan(A(2)));
  Assert.That(A(3) - A(2), Is.LessThan(A(2) - A(1)));
  Assert.That(A(3, spread: 0.1f), Is.LessThan(A(3, spread: 1f)), "stacked entrances are worth less");
  Assert.That(A(10, 1, 100, 1), Is.LessThanOrEqualTo(100f));
  Assert.That(A(1, 1, 3, 0) - A(1), Is.InRange(10f, 20f), "transit up to 20%");
  Assert.That(A(1, 1, 0, 1) - A(1), Is.EqualTo(20f).Within(0.001), "internal paths 20%");
 }
 [Test] public void EntranceSpreadIsZeroForStackedAndHighForOppositeEntrances() {
  Assert.That(ParkQualityModel.ComputeEntranceSpread(new[] { (0f, 0f) }, 10_000), Is.Zero);
  Assert.That(ParkQualityModel.ComputeEntranceSpread(new[] { (0f, 0f), (1f, 1f) }, 10_000), Is.LessThan(0.1f));
  Assert.That(ParkQualityModel.ComputeEntranceSpread(new[] { (0f, 0f), (100f, 0f) }, 10_000), Is.GreaterThan(0.9f));
 }
 [Test] public void AttractivenessIsSixtyFacilityFortyLandscapeWithDiminishingFacilities() {
  Assert.That(ParkQualityModel.ComputeAttractiveness(0, 10_000, 100), Is.EqualTo(40f).Within(0.001));
  float one = ParkQualityModel.ComputeAttractiveness(20, 10_000, 0), two = ParkQualityModel.ComputeAttractiveness(40, 10_000, 0), four = ParkQualityModel.ComputeAttractiveness(80, 10_000, 0);
  Assert.That(two - one, Is.GreaterThan(four - two)); Assert.That(ParkQualityModel.ComputeAttractiveness(1e6f, 10_000, 0), Is.LessThanOrEqualTo(60.001f));
 }
 [Test] public void VarietyRewardsBreadthSeventyThirty() {
  Assert.That(ParkQualityModel.ComputeVariety(0, 5), Is.EqualTo(30f).Within(0.001));
  Assert.That(ParkQualityModel.ComputeVariety(4, 0), Is.GreaterThan(ParkQualityModel.ComputeVariety(1, 0)));
  Assert.That(ParkQualityModel.ComputeVariety(100, 5), Is.LessThanOrEqualTo(100.001f));
  Assert.That(ParkQualityModel.FacilityCategoryWeight(Game.Agents.LeisureType.Travel), Is.LessThan(ParkQualityModel.FacilityCategoryWeight(Game.Agents.LeisureType.CityPark)));
 }
 [Test] public void MaintenanceCombinesVanillaParkConditionAndManagementCoverage() {
  Assert.That(ParkQualityModel.ComputeMaintenance(ParkMetric.Unavailable, 1f), Is.EqualTo(ParkQualityModel.CoverageScore(1f)), "no park buildings: coverage only");
  Assert.That(ParkQualityModel.ComputeMaintenance(M(20), 1f), Is.LessThan(ParkQualityModel.ComputeMaintenance(M(90), 1f)));
  Assert.That(ParkQualityModel.CoverageScore(0.5f), Is.LessThan(ParkQualityModel.CoverageScore(1f)));
  float over = ParkQualityModel.CoverageScore(1.5f) - ParkQualityModel.CoverageScore(1f), under = ParkQualityModel.CoverageScore(1f) - ParkQualityModel.CoverageScore(0.5f);
  Assert.That(over, Is.GreaterThan(0)); Assert.That(over, Is.LessThan(under), "overfunding has diminishing returns");
  Assert.That(ParkQualityModel.CoverageScore(1.5f), Is.LessThanOrEqualTo(100f));
 }
 [Test] public void TourismContributionRisesWithTouristsQualityAndLevel() {
  float T(int level, float q, float tourists, float sat) => ParkQualityModel.ComputeTourismContribution(level, q, tourists, sat, 50);
  Assert.That(T(1, 80, 0, 80), Is.Zero, "no tourists, no contribution");
  Assert.That(T(3, 80, 100, 80), Is.GreaterThan(T(1, 80, 100, 80)));
  Assert.That(T(1, 90, 100, 80), Is.GreaterThan(T(1, 40, 100, 80)));
  Assert.That(T(1, 80, 400, 80), Is.GreaterThan(T(1, 80, 20, 80)));
 }
}
public class ParkQualityEvaluatorTests {
 private static ParkLandscapeSnapshot Landscape(bool available = true, float path = 400, bool cluttered = false) {
  var inputs = new ParkLandscapeInputs { AreaSquareMeters = 10_000, PathLength = path,
   Vegetation = new() { Count = 60, DistinctPrefabs = 12 }, Amenities = new() { Count = 8, DistinctPrefabs = 3 },
   Decoration = new() { Count = 5, DistinctPrefabs = 3 }, Lighting = new() { Count = 8, DistinctPrefabs = 2 }, UnknownCount = cluttered ? 5000 : 0 };
  return new ParkLandscapeSnapshot { Available = available, Inputs = inputs, Result = ParkLandscapeModel.ComputeLandscape(inputs), TransitStops = 2 };
 }
 private static ParkFacilitySnapshot Facilities() => ParkFacilitySnapshot.Build(new[] {
  new ParkFacilityTraits { IsBuilding = true, HasPark = true, HasLeisureProvider = true, HasLeisureData = true, LeisureType = Game.Agents.LeisureType.CityPark, LeisureEfficiency = 30, ParkMaintenance = 700, ParkMaintenancePool = 800 },
  new ParkFacilityTraits { IsBuilding = true, HasAttractivenessProvider = true, Attractiveness = 30 } });
 private static ParkQualityContext Context() => new() {
  AreaSquareMeters = 10_000, Entrances = 2, EntranceSpread = 0.8f, Landscape = Landscape(), Facilities = Facilities(),
  Satisfaction = new ParkSatisfactionState { Mean = 75, Weight = 60 }, TouristSatisfaction = new ParkSatisfactionState { Mean = 80, Weight = 20 },
  ActiveVisitors = 10, Budget = 1f, Level = 1, RecentTouristVisits = 50, AdmissionFee = 0, IsPrimaryForAllMembers = true, Active = true };
 private static ParkQualitySnapshot Eval(ParkQualityContext c) => ParkQualityEvaluator.Evaluate(c);
 [Test] public void AllSixMetricsAreComputedFromRealInputs() {
  var q = Eval(Context());
  foreach (var m in new[] { q.Satisfaction, q.Attractiveness, q.Accessibility, q.Variety, q.Maintenance, q.Comfort }) {
   Assert.That(m.Available, Is.True); Assert.That(m.Value, Is.InRange(0f, 100f)); }
  Assert.That(q.Result.Degraded, Is.False);
  Assert.That(q.Quality, Is.InRange(1f, 100f));
  Assert.That(q.Landscape.Available, Is.True);
 }
 [Test] public void UnavailableLandscapeIsNeutralDegradedAndNeverBetter() {
  var good = Eval(Context());
  var c = Context(); c.Landscape = new ParkLandscapeSnapshot { Available = false };
  var missing = Eval(c);
  Assert.That(missing.Result.Degraded, Is.True); Assert.That(missing.Result.Confidence, Is.LessThan(1f));
  Assert.That(missing.Attractiveness.Available, Is.False); Assert.That(missing.Variety.Available, Is.False);
  Assert.That(missing.Warnings, Does.Contain("CS2AS.Park.Warning.LowConfidence"));
  var poor = Context(); poor.Landscape = Landscape(path: 0); poor.Landscape.Inputs = new ParkLandscapeInputs { AreaSquareMeters = 10_000 };
  poor.Landscape.Result = ParkLandscapeModel.ComputeLandscape(poor.Landscape.Inputs); poor.Landscape.TransitStops = 0;
  Assert.That(missing.Quality, Is.GreaterThan(Eval(poor).Quality), "neutral sits between a bare and a well-built park");
  Assert.That(missing.Quality, Is.LessThan(good.Quality));
 }
 [Test] public void MoreEntrancesImproveAccessibility() {
  var one = Context(); one.Entrances = 1;
  Assert.That(Eval(Context()).Accessibility.Value, Is.GreaterThan(Eval(one).Accessibility.Value));
 }
 [Test] public void LevelDoesNotChangeCapacityOrComfort() {
  var low = Context(); var high = Context(); high.Level = 5;
  Assert.That(Eval(high).ComfortableCapacity, Is.EqualTo(Eval(low).ComfortableCapacity));
  Assert.That(Eval(high).Comfort.Value, Is.EqualTo(Eval(low).Comfort.Value));
 }
 [Test] public void CrowdingLowersComfortAndWarns() {
  var c = Context(); c.ActiveVisitors = 5000;
  var q = Eval(c);
  Assert.That(q.Comfort.Value, Is.LessThan(Eval(Context()).Comfort.Value));
  Assert.That(q.OccupancyRatio, Is.GreaterThan(1f)); Assert.That(q.Warnings, Does.Contain("CS2AS.Park.Warning.Crowded"));
 }
 [Test] public void BudgetChangesOnlyMaintenanceAmongQualityInputs() {
  var low = Context(); low.Budget = 0.5f; var high = Context(); high.Budget = 1.5f;
  var a = Eval(low); var b = Eval(Context()); var h = Eval(high);
  Assert.That(a.Maintenance.Value, Is.LessThan(b.Maintenance.Value)); Assert.That(h.Maintenance.Value, Is.GreaterThan(b.Maintenance.Value));
  foreach (var (x, y) in new[] { (a.Attractiveness, b.Attractiveness), (a.Accessibility, b.Accessibility), (a.Variety, b.Variety), (a.Comfort, b.Comfort), (h.Variety, b.Variety) })
   Assert.That(x.Value, Is.EqualTo(y.Value));
 }
 [Test] public void TourismContributionIsDerivedOnly() {
  var c = Context(); c.RecentTouristVisits = 0;
  Assert.That(Eval(c).TourismContribution, Is.Zero);
  Assert.That(Eval(Context()).TourismContribution, Is.GreaterThan(0));
 }
 [Test] public void WarningsExplainOwnershipPathsClutterAndFacilities() {
  var c = Context(); c.IsPrimaryForAllMembers = false; c.Landscape = Landscape(path: 0, cluttered: true); c.Facilities = ParkFacilitySnapshot.Build(Array.Empty<ParkFacilityTraits>());
  var w = Eval(c).Warnings;
  Assert.That(w, Does.Contain("CS2AS.Park.Warning.NotPrimary"));
  Assert.That(w, Does.Contain("CS2AS.Park.Warning.NoPaths"));
  Assert.That(w, Does.Contain("CS2AS.Park.Warning.Cluttered"));
  Assert.That(w, Does.Contain("CS2AS.Park.Warning.NoFacilities"));
  var none = Context(); none.Entrances = 0; none.Active = false;
  Assert.That(Eval(none).Warnings, Does.Contain("CS2AS.Park.Warning.NoEntrance"));
  Assert.That(Eval(Context()).NextActionKey, Does.StartWith("CS2AS.Park.Action."));
 }
 [Test] public void VisitExperienceReflectsFeeValueAndCurrentState() {
  var paid = Context(); paid.AdmissionFee = 60; paid.Level = 2;
  var free = Eval(Context()); var expensive = Eval(paid);
  Assert.That(free.Experience.ValueForMoney, Is.EqualTo(100f));
  Assert.That(expensive.Experience.ValueForMoney, Is.LessThan(free.Experience.ValueForMoney));
  Assert.That(free.Experience.Landscape, Is.EqualTo(free.Landscape.Value));
  Assert.That(free.Experience.Comfort, Is.EqualTo(free.Comfort.Value));
  Assert.That(free.Experience.Maintenance, Is.EqualTo(free.Maintenance.Value));
  var levelOne = Context(); levelOne.AdmissionFee = 60; levelOne.Level = 1;
  Assert.That(Eval(levelOne).Experience.ValueForMoney, Is.EqualTo(100f), "a stored fee is inactive at Level 1");
 }
}
