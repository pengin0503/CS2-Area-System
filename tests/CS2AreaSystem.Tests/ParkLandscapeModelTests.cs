using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkLandscapeModelTests {
 [Test] public void SingletonVariationsDoNotResetDominantPrefabDiminishingReturns() {
  var dominant = new ParkLandscapeTally(); var balanced = new ParkLandscapeTally();
  for (int prefab = 0; prefab < 20; prefab++) {
   int a = prefab == 0 ? 100 : 1, b = prefab == 19 ? 5 : 6;
   for (int i = 0; i < a; i++) dominant.Add(ParkLandscapeCategory.Vegetation, prefab);
   for (int i = 0; i < b; i++) balanced.Add(ParkLandscapeCategory.Vegetation, prefab);
  }
  var aInputs = dominant.ToInputs(10_000, 0); var bInputs = balanced.ToInputs(10_000, 0);
  Assert.That(aInputs.Vegetation.Count, Is.EqualTo(bInputs.Vegetation.Count));
  Assert.That(aInputs.Vegetation.DistinctPrefabs, Is.EqualTo(bInputs.Vegetation.DistinctPrefabs));
  Assert.That(ParkLandscapeModel.EffectiveCount(aInputs.Vegetation), Is.EqualTo(24 + Math.Sqrt(95)).Within(0.001));
  Assert.That(ParkLandscapeModel.EffectiveCount(bInputs.Vegetation), Is.EqualTo(119).Within(0.001));
  Assert.That(ParkLandscapeModel.ComputeLandscape(aInputs).Vegetation, Is.LessThan(ParkLandscapeModel.ComputeLandscape(bInputs).Vegetation));
 }
 private const float Hectare = 10_000f;
 private static ParkLandscapeInputs Inputs(float area = Hectare, int trees = 0, int treeKinds = 1, int benches = 0, int benchKinds = 1, int decor = 0, int lights = 0, float path = 0, int unknown = 0, int decorKinds = 1, int lightKinds = 1) => new() {
  AreaSquareMeters = area,
  Vegetation = Support.LandscapeInputs.Balanced(trees, treeKinds),
  Amenities = Support.LandscapeInputs.Balanced(benches, benchKinds),
  Decoration = Support.LandscapeInputs.Balanced(decor, decorKinds),
  Lighting = Support.LandscapeInputs.Balanced(lights, lightKinds),
  PathLength = path, UnknownCount = unknown };
 private static float Score(ParkLandscapeInputs i) => ParkLandscapeModel.ComputeLandscape(i).Score;
 [Test] public void EmptyParkScoresZeroAndScoreIsBounded() {
  Assert.That(Score(Inputs()), Is.Zero);
  var lavish = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 400, treeKinds: 30, benches: 80, benchKinds: 8, decor: 60, lights: 80, path: 3000));
  Assert.That(lavish.Score, Is.InRange(0f, 100f));
  Assert.That(lavish.Vegetation, Is.LessThanOrEqualTo(ParkLandscapeModel.VegetationWeight + 0.001f));
 }
 [Test] public void DensityHasDiminishingReturns() {
  float a = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 20, treeKinds: 10)).Vegetation;
  float b = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 40, treeKinds: 20)).Vegetation;
  float c = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 80, treeKinds: 40)).Vegetation;
  Assert.That(b, Is.GreaterThan(a)); Assert.That(c, Is.GreaterThan(b));
  Assert.That(c - b, Is.LessThan(b - a), "each doubling is worth less");
 }
 [Test] public void RepeatingOnePrefabSaturatesQuickly() {
  float ten = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 10, treeKinds: 1)).Vegetation;
  float hundredSame = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 100, treeKinds: 1)).Vegetation;
  float hundredVaried = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 100, treeKinds: 20)).Vegetation;
  Assert.That(hundredSame, Is.LessThan(hundredVaried));
  Assert.That(hundredSame - ten, Is.LessThan(ParkLandscapeModel.VegetationWeight * 0.35f), "the 100th copy of one tree adds little");
 }
 [Test] public void NewCategoryIsWorthMoreThanMoreOfADominantCategory() {
  var heavy = Inputs(trees: 60, treeKinds: 12);
  float moreTrees = Score(Inputs(trees: 90, treeKinds: 18));
  float benches = Score(Inputs(trees: 60, treeKinds: 12, benches: 6, benchKinds: 2));
  Assert.That(benches - Score(heavy), Is.GreaterThan(moreTrees - Score(heavy)));
  Assert.That(ParkLandscapeModel.ComputeLandscape(Inputs(trees: 60, treeKinds: 12, benches: 6, benchKinds: 2, decor: 4, lights: 6, path: 300)).Diversity,
   Is.GreaterThan(ParkLandscapeModel.ComputeLandscape(heavy).Diversity));
 }
 [Test] public void ClutterPenaltyAppliesOnlyBeyondTheUpperDensity() {
  var dense = ParkLandscapeModel.ComputeLandscape(Inputs(area: 2_000, trees: 80, treeKinds: 15, benches: 20, benchKinds: 4));
  Assert.That(dense.ClutterPenalty, Is.Zero, "a dense garden is not punished");
  Assert.That(dense.Cluttered, Is.False);
  var spam = ParkLandscapeModel.ComputeLandscape(Inputs(area: 2_000, trees: 300, treeKinds: 15, benches: 20, benchKinds: 4, unknown: 200));
  Assert.That(spam.ClutterPenalty, Is.GreaterThan(0)); Assert.That(spam.Cluttered, Is.True);
  Assert.That(spam.ClutterPenalty, Is.LessThanOrEqualTo(ParkLandscapeModel.MaxClutterPenalty), "modest, bounded penalty");
 }
 [Test] public void SmallAndLargeParksAreBothViableWithSubLinearTargets() {
  float Scaled(float hectares, float objectsPerUnit) {
   float unit = ParkLandscapeModel.SizeScale(hectares * Hectare);
   int n(float perUnit) => (int)Math.Round(perUnit * objectsPerUnit * unit);
   return Score(Inputs(area: hectares * Hectare, trees: n(40), treeKinds: Math.Max(1, n(8)), benches: n(8), benchKinds: Math.Max(1, n(2)), decor: n(6), lights: n(8), path: 250 * objectsPerUnit * unit, decorKinds: Math.Max(1, n(1.5f)), lightKinds: Math.Max(1, n(2))));
  }
  float small = Scaled(0.25f, 1f), large = Scaled(16f, 1f);
  Assert.That(small, Is.GreaterThan(55f)); Assert.That(large, Is.GreaterThan(55f));
  Assert.That(Math.Abs(small - large), Is.LessThan(12f));
  Assert.That(ParkLandscapeModel.SizeScale(16 * Hectare) / ParkLandscapeModel.SizeScale(0.25f * Hectare), Is.LessThan(64f), "targets grow sub-linearly with area");
  Assert.That(ParkLandscapeModel.SizeScale(1f), Is.GreaterThan(0f), "tiny parks keep a floor");
 }
 [Test] public void PathsCountAndPathlessParksLoseThePathContribution() {
  var pathless = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 40, treeKinds: 8));
  var walked = ParkLandscapeModel.ComputeLandscape(Inputs(trees: 40, treeKinds: 8, path: 250));
  Assert.That(pathless.Paths, Is.Zero); Assert.That(walked.Paths, Is.GreaterThan(ParkLandscapeModel.PathWeight * 0.5f));
 }
}
