using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Park policy effects change real Park model inputs (design §9.1, §11): management funding, the landscape model's credit
// for scenic objects, comfortable capacity and the fair admission fee. Quality is always the plain weighted model of
// its six metrics; no policy adds points after it.
public class ParkPolicyEffectTests {
 private static ParkPolicyEffects Only(params int[] ids) => ParkPolicyEffects.From(id => ids.Contains(id));
 private static ParkLandscapeInputs Landscape() => new() {
  AreaSquareMeters = 10_000, PathLength = 300, UnknownCount = 4,
  Vegetation = Support.LandscapeInputs.Balanced(30, 6), Amenities = Support.LandscapeInputs.Balanced(5, 2),
  Decoration = Support.LandscapeInputs.Balanced(4, 2), Lighting = Support.LandscapeInputs.Balanced(5, 1) };
 private static ParkQualityContext Context(ParkPolicyEffects policies, int fee = 20, int visitors = 30) {
  var inputs = Landscape();
  return new ParkQualityContext {
   AreaSquareMeters = 10_000, Entrances = 2, EntranceSpread = 0.5f, Budget = 1f, Level = 4, AdmissionFee = fee, ActiveVisitors = visitors,
   Landscape = new ParkLandscapeSnapshot { Available = true, Inputs = inputs, Result = ParkLandscapeModel.ComputeLandscape(inputs), TransitPoints = 1, TransitTypes = 1 },
   Facilities = ParkFacilitySnapshot.Build(new[] { new ParkFacilityTraits { IsBuilding = true, HasPark = true, HasLeisureData = true,
    LeisureType = Game.Agents.LeisureType.CityPark, LeisureEfficiency = 20, Attractiveness = 30, HasAttractivenessProvider = true } }),
   Satisfaction = new ParkSatisfactionState { Mean = 70, Weight = 40 }, IsPrimaryForAllMembers = true, Active = true, Policies = policies };
 }
 private static void AssertQualityIsThePlainModel(ParkQualitySnapshot q) {
  var plain = ParkQualityModel.ComputeQuality(new ParkQualityInputs { Satisfaction = q.Satisfaction, Attractiveness = q.Attractiveness,
   Accessibility = q.Accessibility, Variety = q.Variety, Maintenance = q.Maintenance, Comfort = q.Comfort });
  Assert.That(q.Quality, Is.EqualTo(plain.Quality), "no policy adds Quality after the six-metric model");
 }

 [Test] public void TuningValuesAreTheDocumentedNamedConstants() {
  Assert.That(ParkPolicyEffects.EnhancedMaintenanceFundingMultiplier, Is.EqualTo(1.25));
  Assert.That(ParkPolicyEffects.LandscapeProtectionScenicCredit, Is.EqualTo(1.25f));
  Assert.That(ParkPolicyEffects.LandscapeProtectionCapacityShare, Is.EqualTo(0.85f));
  Assert.That(ParkPolicyEffects.PremiumServicesFairFeeMultiplier, Is.EqualTo(1.3));
 }
 [Test] public void NoPolicyIsTheNeutralBaseline() {
  foreach (var none in new[] { ParkPolicyEffects.None, default, ParkPolicyEffects.From(_ => false) }) {
   Assert.That(none.ManagementFundingMultiplier, Is.EqualTo(1.0));
   Assert.That(none.ScenicCredit, Is.EqualTo(1f));
   Assert.That(none.CapacityShare, Is.EqualTo(1f));
   Assert.That(none.FairFeeMultiplier, Is.EqualTo(1.0));
  }
 }
 [Test] public void EachImplementedPolicyDrivesOnlyItsOwnInputs() {
  var maintenance = Only(AreaPolicyIds.EnhancedMaintenance);
  Assert.That(maintenance.ManagementFundingMultiplier, Is.EqualTo(1.25)); Assert.That(maintenance.ScenicCredit, Is.EqualTo(1f));
  Assert.That(maintenance.CapacityShare, Is.EqualTo(1f)); Assert.That(maintenance.FairFeeMultiplier, Is.EqualTo(1.0));
  var landscape = Only(AreaPolicyIds.LandscapeProtection);
  Assert.That(landscape.ScenicCredit, Is.EqualTo(1.25f)); Assert.That(landscape.CapacityShare, Is.EqualTo(0.85f));
  Assert.That(landscape.ManagementFundingMultiplier, Is.EqualTo(1.0)); Assert.That(landscape.FairFeeMultiplier, Is.EqualTo(1.0));
  var premium = Only(AreaPolicyIds.PremiumServices);
  Assert.That(premium.FairFeeMultiplier, Is.EqualTo(1.3)); Assert.That(premium.ManagementFundingMultiplier, Is.EqualTo(1.0));
  Assert.That(premium.ScenicCredit, Is.EqualTo(1f)); Assert.That(premium.CapacityShare, Is.EqualTo(1f));
 }
 [Test] public void PoliciesWithoutAVerifiedEffectNeverChangeAnInput() {
  var unverified = Only(AreaPolicyIds.NightEvents, AreaPolicyIds.QuietOperations, AreaPolicyIds.MajorEventAttraction, AreaPolicyIds.FocusedManagement);
  Assert.That(unverified, Is.EqualTo(ParkPolicyEffects.None));
  var asked = new List<int>();
  ParkPolicyEffects.From(id => { asked.Add(id); return false; });
  Assert.That(asked, Is.EquivalentTo(new[] { AreaPolicyIds.EnhancedMaintenance, AreaPolicyIds.LandscapeProtection, AreaPolicyIds.PremiumServices }));
 }
 [Test] public void AFailingPolicyReadFallsBackToTheBaselineAndReportsOnce() {
  var errors = new List<Exception>();
  var reader = new ParkPolicyEffectReader((area, id) => throw new InvalidOperationException("policy store"), errors.Add);
  Assert.That(reader.Read(Entity.Null), Is.EqualTo(ParkPolicyEffects.None));
  Assert.That(reader.Read(Entity.Null), Is.EqualTo(ParkPolicyEffects.None));
  Assert.That(errors, Has.Count.EqualTo(1));
  var area = new Entity { Index = 3, Version = 1 };
  var working = new ParkPolicyEffectReader((a, id) => a == area && id == AreaPolicyIds.PremiumServices, errors.Add);
  Assert.That(working.Read(area), Is.EqualTo(Only(AreaPolicyIds.PremiumServices)));
  Assert.That(working.Read(Entity.Null), Is.EqualTo(ParkPolicyEffects.None), "effects are read per Area");
 }

 // 2001 Enhanced Maintenance: more real management money, and coverage is that funding relative to need.
 [Test] public void EnhancedMaintenanceRaisesRealManagementSpendAndCoverageTogether() {
  const float need = 800f;
  Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(need, 1f, 1.0), Is.EqualTo(800f).Within(0.001));
  Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(need, 1f, 1.25), Is.EqualTo(1000f).Within(0.001));
  Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(need, 1.5f, 1.25), Is.EqualTo(1500f).Within(0.001));
  Assert.That(ParkFinanceModel.ComputeManagementCoverage(1f, 1.0), Is.EqualTo(1f));
  Assert.That(ParkFinanceModel.ComputeManagementCoverage(1f, 1.25), Is.EqualTo(1.25f).Within(1e-6));
  Assert.That(ParkFinanceModel.ComputeManagementCoverage(3f, 1.25), Is.EqualTo(1.875f).Within(1e-6), "the slider range still applies");
  foreach (var budget in new[] { 0.5f, 1f, 1.5f })
   Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(need, budget, 1.25),
    Is.EqualTo(need * ParkFinanceModel.ComputeManagementCoverage(budget, 1.25)).Within(0.01), "spend is exactly the funded coverage");
 }
 [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(0.0)] [TestCase(-1.0)]
 public void InvalidFundingMultipliersKeepTheBaseline(double multiplier) {
  Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(800f, 1f, multiplier), Is.EqualTo(800f).Within(0.001));
  Assert.That(ParkFinanceModel.ComputeManagementCoverage(1f, multiplier), Is.EqualTo(1f));
 }
 [Test] public void EnhancedMaintenanceChangesQualityOnlyThroughTheMaintenanceInput() {
  var condition = Context(ParkPolicyEffects.None).Facilities.VanillaCondition;
  var baseline = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None));
  var enhanced = ParkQualityEvaluator.Evaluate(Context(Only(AreaPolicyIds.EnhancedMaintenance)));
  Assert.That(baseline.Maintenance.Value, Is.EqualTo(ParkQualityModel.ComputeMaintenance(condition, 1f)).Within(1e-4), "baseline coverage is the budget");
  Assert.That(enhanced.Maintenance.Value, Is.EqualTo(ParkQualityModel.ComputeMaintenance(condition, 1.25f)).Within(1e-4));
  Assert.That(enhanced.Maintenance.Value, Is.GreaterThan(baseline.Maintenance.Value));
  Assert.That(enhanced.Experience.Maintenance, Is.EqualTo(enhanced.Maintenance.Value), "visits experience the same maintenance");
  foreach (var (a, b) in new[] { (enhanced.Satisfaction, baseline.Satisfaction), (enhanced.Attractiveness, baseline.Attractiveness),
   (enhanced.Accessibility, baseline.Accessibility), (enhanced.Variety, baseline.Variety), (enhanced.Comfort, baseline.Comfort) })
   Assert.That(a.Value, Is.EqualTo(b.Value));
  Assert.That(enhanced.Quality, Is.GreaterThan(baseline.Quality));
  AssertQualityIsThePlainModel(enhanced); AssertQualityIsThePlainModel(baseline);
 }

 // 2004 Landscape Protection: scenic objects (vegetation, decoration) earn more landscape credit; capacity shrinks.
 [Test] public void LandscapeProtectionCreditsOnlyScenicCategories() {
  var inputs = Landscape();
  var plain = ParkLandscapeModel.ComputeLandscape(inputs);
  var neutral = ParkLandscapeModel.ComputeLandscape(inputs, 1f);
  Assert.That(neutral.Score, Is.EqualTo(plain.Score)); Assert.That(neutral.Vegetation, Is.EqualTo(plain.Vegetation));
  var protectedResult = ParkLandscapeModel.ComputeLandscape(inputs, ParkPolicyEffects.LandscapeProtectionScenicCredit);
  Assert.That(protectedResult.Vegetation, Is.GreaterThan(plain.Vegetation)); Assert.That(protectedResult.Decoration, Is.GreaterThan(plain.Decoration));
  Assert.That(protectedResult.Amenities, Is.EqualTo(plain.Amenities)); Assert.That(protectedResult.Lighting, Is.EqualTo(plain.Lighting));
  Assert.That(protectedResult.Paths, Is.EqualTo(plain.Paths)); Assert.That(protectedResult.Diversity, Is.EqualTo(plain.Diversity));
  Assert.That(protectedResult.ClutterPenalty, Is.EqualTo(plain.ClutterPenalty)); Assert.That(protectedResult.ObjectCount, Is.EqualTo(plain.ObjectCount));
  Assert.That(protectedResult.CategoriesPresent, Is.EqualTo(plain.CategoriesPresent));
  Assert.That(protectedResult.Score, Is.GreaterThan(plain.Score));
  // 25% more effective scenic objects, through the same diminishing-returns curve and category caps.
  Assert.That(protectedResult.Vegetation, Is.LessThanOrEqualTo(ParkLandscapeModel.VegetationWeight));
  var empty = new ParkLandscapeInputs { AreaSquareMeters = 10_000 };
  Assert.That(ParkLandscapeModel.ComputeLandscape(empty, 1.25f).Score, Is.Zero, "no scenic objects, no credit");
 }
 [TestCase(float.NaN)] [TestCase(0f)] [TestCase(-2f)] [TestCase(float.PositiveInfinity)]
 public void InvalidScenicCreditKeepsTheScannedLandscape(float credit) =>
  Assert.That(ParkLandscapeModel.ComputeLandscape(Landscape(), credit).Score, Is.EqualTo(ParkLandscapeModel.ComputeLandscape(Landscape()).Score));
 [Test] public void LandscapeProtectionTakesItsShareOfComfortableCapacity() {
  var inputs = new ParkCapacityInputs { AreaSquareMeters = 20_000, PathLength = 500, LeisureEfficiencySum = 30, AmenityCount = 6 };
  float full = ParkQualityModel.ComputeComfortableCapacity(inputs, 1f);
  Assert.That(ParkQualityModel.ComputeComfortableCapacity(inputs, 0.85f), Is.EqualTo(0.85f * full).Within(1e-3));
  foreach (var invalid in new[] { float.NaN, 0f, -1f, 1.5f, float.PositiveInfinity })
   Assert.That(ParkQualityModel.ComputeComfortableCapacity(inputs, invalid), Is.EqualTo(full), "a share outside (0, 1] never adds or erases capacity");
 }
 [Test] public void LandscapeProtectionChangesLandscapeAttractivenessAndComfortOnly() {
  var baseline = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, visitors: 60));
  var protectedPark = ParkQualityEvaluator.Evaluate(Context(Only(AreaPolicyIds.LandscapeProtection), visitors: 60));
  Assert.That(baseline.OccupancyRatio, Is.GreaterThan(0.6f), "fixture: the baseline is already past the no-penalty band");
  var expected = ParkLandscapeModel.ComputeLandscape(Landscape(), 1.25f);
  Assert.That(protectedPark.Landscape.Value, Is.EqualTo(expected.Score));
  Assert.That(protectedPark.EffectiveLandscape, Is.Not.Null); Assert.That(protectedPark.EffectiveLandscape!.Value.Score, Is.EqualTo(expected.Score));
  Assert.That(protectedPark.Attractiveness.Value, Is.GreaterThan(baseline.Attractiveness.Value));
  Assert.That(protectedPark.Experience.Landscape, Is.EqualTo(expected.Score));
  Assert.That(protectedPark.ComfortableCapacity, Is.EqualTo(0.85f * baseline.ComfortableCapacity).Within(1e-3));
  Assert.That(protectedPark.OccupancyRatio, Is.GreaterThan(baseline.OccupancyRatio));
  Assert.That(protectedPark.Comfort.Value, Is.LessThan(baseline.Comfort.Value), "the same visitors in a smaller usable park are more crowded");
  Assert.That(protectedPark.Variety.Value, Is.EqualTo(baseline.Variety.Value)); Assert.That(protectedPark.Accessibility.Value, Is.EqualTo(baseline.Accessibility.Value));
  Assert.That(protectedPark.Maintenance.Value, Is.EqualTo(baseline.Maintenance.Value));
  AssertQualityIsThePlainModel(protectedPark);
  Assert.That(baseline.EffectiveLandscape!.Value.Score, Is.EqualTo(baseline.Landscape.Value), "the baseline shows the scanned landscape");
 }
 [Test] public void TheLandscapeTabShowsTheLandscapeQualityUses() {
  var model = ParkPanelBuilderTests.Model();
  var inputs = model.Runtime.Landscape.Inputs;
  model.Runtime.Quality = ParkQualityEvaluator.Evaluate(new ParkQualityContext { AreaSquareMeters = 10_000, Entrances = 2, Landscape = model.Runtime.Landscape,
   Facilities = model.Runtime.Facilities, Budget = 1, Level = 3, Policies = Only(AreaPolicyIds.LandscapeProtection) });
  var tab = ParkPanelBuilder.Build(model).Tabs.Single(t => t.Id == "landscape");
  var protectedResult = ParkLandscapeModel.ComputeLandscape(inputs, 1.25f);
  Assert.That(tab.Items.Single(i => i.Id == "landscape").NumericValue, Is.EqualTo(Math.Round(protectedResult.Score, 1)).Within(0.051));
  Assert.That(protectedResult.Score, Is.GreaterThan(model.Runtime.Landscape.Result.Score));
 }

 // 2005 Premium Services: visitors accept a higher fee as fair. 1.0 is the unchanged Area behaviour.
 [Test] public void PremiumServicesRaisesTheFairFeeAndSoftensTheHighFeePenalty() {
  Assert.That(ParkFinanceModel.ComputeValueForMoney(0, 50, 1.3), Is.EqualTo(100f), "free stays free of penalty");
  Assert.That(ParkFinanceModel.ComputeValueForMoney(20, 50, 1.0), Is.EqualTo(65.22f).Within(0.005), "pinned baseline: fair fee 16.73 at Quality 50");
  foreach (var fee in new[] { 5, 20, 40, 60 })
   foreach (var quality in new[] { 0f, 50f, 100f })
    Assert.That(ParkFinanceModel.ComputeValueForMoney(fee, quality, 1.3), Is.GreaterThanOrEqualTo(ParkFinanceModel.ComputeValueForMoney(fee, quality, 1.0)), fee + "/" + quality);
  Assert.That(ParkFinanceModel.ComputeValueForMoney(40, 50, 1.3), Is.GreaterThan(ParkFinanceModel.ComputeValueForMoney(40, 50, 1.0)));
  // The whole curve is the baseline curve with a 30% higher fair fee: 13 under Premium Services feels like 10 without it.
  Assert.That(ParkFinanceModel.ComputeValueForMoney(13, 50, 1.3), Is.EqualTo(ParkFinanceModel.ComputeValueForMoney(10, 50, 1.0)).Within(1e-3));
 }
 [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(0.0)] [TestCase(-1.3)]
 public void InvalidFairFeeMultipliersKeepTheBaseline(double multiplier) =>
  Assert.That(ParkFinanceModel.ComputeValueForMoney(30, 50, multiplier), Is.EqualTo(ParkFinanceModel.ComputeValueForMoney(30, 50, 1.0)));
 [Test] public void PremiumServicesReachesVisitsThroughValueForMoneyOnly() {
  var baseline = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None, fee: 40));
  var premium = ParkQualityEvaluator.Evaluate(Context(Only(AreaPolicyIds.PremiumServices), fee: 40));
  Assert.That(premium.ValueForMoney, Is.EqualTo(ParkFinanceModel.ComputeValueForMoney(40, premium.Quality, 1.3)));
  Assert.That(premium.ValueForMoney, Is.GreaterThan(baseline.ValueForMoney));
  Assert.That(premium.Experience.ValueForMoney, Is.EqualTo(premium.ValueForMoney));
  Assert.That(premium.Quality, Is.EqualTo(baseline.Quality), "Quality itself is untouched; satisfaction follows from real visits");
  Assert.That(premium.EffectiveFee, Is.EqualTo(baseline.EffectiveFee), "the fee charged is the player's fee");
 }
 [Test] public void TheNeutralContextReproducesTheUnpolicedEvaluation() {
  var a = ParkQualityEvaluator.Evaluate(Context(ParkPolicyEffects.None));
  var b = ParkQualityEvaluator.Evaluate(Context(default));
  Assert.That(a.Quality, Is.EqualTo(b.Quality)); Assert.That(a.ValueForMoney, Is.EqualTo(ParkFinanceModel.ComputeValueForMoney(a.EffectiveFee, a.Quality, 1.0)));
  var c = Context(ParkPolicyEffects.None);
  Assert.That(a.ComfortableCapacity, Is.EqualTo(ParkQualityModel.ComputeComfortableCapacity(new ParkCapacityInputs { AreaSquareMeters = 10_000, PathLength = 300,
   LeisureEfficiencySum = c.Facilities.LeisureEfficiencySum, AmenityCount = 5 }, 1f)));
  Assert.That(a.Landscape.Value, Is.EqualTo(ParkLandscapeModel.ComputeLandscape(Landscape()).Score));
 }

 // Availability: implemented Area-internal effects are available; unproven vanilla paths stay visible but unavailable.
 [TestCase(AreaPolicyIds.EnhancedMaintenance)] [TestCase(AreaPolicyIds.LandscapeProtection)] [TestCase(AreaPolicyIds.PremiumServices)]
 public void ImplementedAreaInternalPoliciesAreAvailable(int policyId) {
  IAreaPolicyProvider park = new ParkAreaModule(null);
  Assert.That(park.IsPolicyEffectAvailable(Entity.Null, policyId, default, out var reason), Is.True);
  Assert.That(reason, Is.Null);
  Assert.That(ParkPolicyCatalog.Definitions.Single(d => d.PolicyId == policyId).EffectClass, Is.EqualTo(AreaPolicyEffectClass.AreaInternal));
 }
 [TestCase(AreaPolicyIds.NightEvents)] [TestCase(AreaPolicyIds.QuietOperations)] [TestCase(AreaPolicyIds.MajorEventAttraction)]
 public void PoliciesWithoutAVerifiedVanillaPathStayVisibleButUnavailable(int policyId) {
  IAreaPolicyProvider park = new ParkAreaModule(null);
  Assert.That(park.IsPolicyEffectAvailable(Entity.Null, policyId, default, out var reason), Is.False);
  Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
  Assert.That(ParkPolicyCatalog.Definitions.Select(d => d.PolicyId), Does.Contain(policyId), "still listed in the policy panel");
  Assert.That(ParkPolicyCatalog.Definitions.Single(d => d.PolicyId == policyId).EffectClass, Is.EqualTo(AreaPolicyEffectClass.VanillaSimulation));
 }
 [Test] public void UnknownPolicyIdsAreUnavailable() {
  Assert.That(ParkPolicyCatalog.IsEffectAvailable(2999, out var reason), Is.False);
  Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
 }
 [TestCase(false)] [TestCase(true)]
 public void DescriptionsStateTheImplementedMagnitudes(bool ja) {
  var entries = new Dictionary<string, string>();
  ParkLocalization.Append(entries, ja);
  string Description(string name) => entries["Policy.DESCRIPTION[CS2AS.Policy." + name + "]"];
  Assert.That(Description("EnhancedMaintenance"), Does.Contain("25%"));
  Assert.That(Description("LandscapeProtection"), Does.Contain("25%").And.Contain("15%"));
  Assert.That(Description("PremiumServices"), Does.Contain("30%"));
 }
}
