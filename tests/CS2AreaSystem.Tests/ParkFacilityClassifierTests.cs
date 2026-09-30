using CS2AreaSystem.Modules.Park;
using Game.Agents;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkFacilityClassifierTests {
 private static ParkFacilityTraits Building(bool road = true) => new() { IsBuilding = true, HasRoadAccess = road };
 private static ParkFacilityTraits Playground() { var t = Building(); t.HasPark = true; t.HasLeisureProvider = true; t.HasLeisureData = true; t.LeisureType = LeisureType.CityPark; t.LeisureEfficiency = 30; t.ParkMaintenance = 400; t.ParkMaintenancePool = 800; return t; }
 private static ParkFacilityTraits Monument() { var t = Building(); t.HasAttractivenessProvider = true; t.Attractiveness = 25; return t; }
 private static ParkFacilityTraits Cafe() { var t = Building(); t.CommercialLeisureMask = ParkFacilityClassifier.LeisureBit(LeisureType.Meals); return t; }
 private static ParkFacilityTraits Office() => Building();
 [Test] public void RecognizedProvidersGetParkRolesAndUnrelatedBuildingsGetNone() {
  Assert.That(ParkFacilityClassifier.Classify(Playground()), Is.EqualTo(ParkFacilityRoles.CoreParkFacility | ParkFacilityRoles.Leisure));
  Assert.That(ParkFacilityClassifier.Classify(Monument()), Is.EqualTo(ParkFacilityRoles.Attraction));
  Assert.That(ParkFacilityClassifier.Classify(Cafe()), Is.EqualTo(ParkFacilityRoles.SupportingAmenity));
  Assert.That(ParkFacilityClassifier.Classify(Office()), Is.EqualTo(ParkFacilityRoles.None));
  Assert.That(ParkFacilityClassifier.Classify(new ParkFacilityTraits()), Is.EqualTo(ParkFacilityRoles.None), "not a building");
 }
 [Test] public void OnlyParkLeisureAndAttractionBuildingsAreVisitRelevant() {
  Assert.That(ParkFacilityClassifier.IsVisitRelevant(Playground()), Is.True);
  Assert.That(ParkFacilityClassifier.IsVisitRelevant(Monument()), Is.True);
  Assert.That(ParkFacilityClassifier.IsVisitRelevant(Cafe()), Is.False, "commercial spending is not a Park visit in v1");
  Assert.That(ParkFacilityClassifier.IsVisitRelevant(Office()), Is.False, "workers/residents of unrelated buildings never count");
 }
 [Test] public void UnrelatedMembersContributeNothingToTheSnapshot() {
  var empty = ParkFacilitySnapshot.Build(new[] { Office(), Office(), Office() });
  Assert.That(empty.RelevantFacilities, Is.Zero); Assert.That(empty.AttractivenessSum, Is.Zero);
  Assert.That(empty.WeightedCategories, Is.Zero); Assert.That(empty.LeisureEfficiencySum, Is.Zero);
  Assert.That(empty.VanillaCondition.Available, Is.False);
  var withOffices = ParkFacilitySnapshot.Build(new[] { Playground(), Monument(), Office() });
  var without = ParkFacilitySnapshot.Build(new[] { Playground(), Monument() });
  Assert.That(withOffices.WeightedCategories, Is.EqualTo(without.WeightedCategories));
  Assert.That(withOffices.AttractivenessSum, Is.EqualTo(without.AttractivenessSum));
  Assert.That(withOffices.RelevantFacilities, Is.EqualTo(2));
 }
 [Test] public void SnapshotAggregatesRealProviderData() {
  var s = ParkFacilitySnapshot.Build(new[] { Playground(), Playground(), Monument(), Cafe() });
  Assert.That(s.AttractivenessSum, Is.EqualTo(25));
  Assert.That(s.LeisureEfficiencySum, Is.EqualTo(60));
  Assert.That(s.VanillaCondition.Available, Is.True); Assert.That(s.VanillaCondition.Value, Is.EqualTo(50f).Within(0.01));
  // CityPark (1) + attraction (1) + supporting Meals (0.5); a second playground adds no breadth.
  Assert.That(s.WeightedCategories, Is.EqualTo(2.5f).Within(0.001));
  var travel = Building(); travel.HasLeisureProvider = true; travel.HasLeisureData = true; travel.LeisureType = LeisureType.Travel;
  Assert.That(ParkFacilitySnapshot.Build(new[] { travel }).WeightedCategories, Is.EqualTo(0.3f).Within(0.001));
 }
}
public class ParkLandscapeClassificationTests {
 private static uint Mask(params Game.Prefabs.ActivityType[] types) => types.Aggregate(0u, (m, t) => m | new Game.Prefabs.ActivityMask(t).m_Mask);
 [Test] public void TypedSemanticsDecideTheCategory() {
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, IsTree = true }), Is.EqualTo(ParkLandscapeCategory.Vegetation));
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, IsPlant = true }), Is.EqualTo(ParkLandscapeCategory.Vegetation));
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, ActivityMask = Mask(Game.Prefabs.ActivityType.BenchSitting) }), Is.EqualTo(ParkLandscapeCategory.Amenity));
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, ActivityMask = Mask(Game.Prefabs.ActivityType.Yoga, Game.Prefabs.ActivityType.Standing) }), Is.EqualTo(ParkLandscapeCategory.Amenity));
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, IsStreetLight = true }), Is.EqualTo(ParkLandscapeCategory.Lighting));
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, IsPlaceable = true }), Is.EqualTo(ParkLandscapeCategory.Decoration));
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true }), Is.EqualTo(ParkLandscapeCategory.Unknown), "no semantics: density only");
  Assert.That(ParkLandscapeClassifier.Classify(new ParkObjectTraits { IsStaticObject = true, ActivityMask = Mask(Game.Prefabs.ActivityType.Standing, Game.Prefabs.ActivityType.Enter) }), Is.EqualTo(ParkLandscapeCategory.Unknown));
 }
 [Test] public void BuildingsSubObjectsAndInfrastructureAreNotLandscape() {
  foreach (var t in new[] {
   new ParkObjectTraits { IsStaticObject = true, IsTree = true, IsBuilding = true },
   new ParkObjectTraits { IsStaticObject = true, IsTree = true, HasOwner = true },
   new ParkObjectTraits { IsStaticObject = true, IsPlaceable = true, IsNetObject = true },
   new ParkObjectTraits { IsStaticObject = true, IsPlaceable = true, IsTransportStop = true },
   new ParkObjectTraits { IsStaticObject = true, IsPlaceable = true, IsUtility = true },
   new ParkObjectTraits { IsStaticObject = true, IsTree = true, IsTempOrDeleted = true },
   new ParkObjectTraits { IsTree = true } })
   Assert.That(ParkLandscapeClassifier.Classify(t), Is.EqualTo(ParkLandscapeCategory.None));
 }
 [Test] public void TallyCountsDistinctPrefabsPerCategory() {
  var tally = new ParkLandscapeTally();
  tally.Add(ParkLandscapeCategory.Vegetation, 1); tally.Add(ParkLandscapeCategory.Vegetation, 1); tally.Add(ParkLandscapeCategory.Vegetation, 2);
  tally.Add(ParkLandscapeCategory.Amenity, 7); tally.Add(ParkLandscapeCategory.Unknown, 9); tally.Add(ParkLandscapeCategory.None, 9);
  var inputs = tally.ToInputs(5_000, 120);
  Assert.That(inputs.Vegetation.Count, Is.EqualTo(3)); Assert.That(inputs.Vegetation.DistinctPrefabs, Is.EqualTo(2));
  Assert.That(inputs.Amenities.Count, Is.EqualTo(1)); Assert.That(inputs.UnknownCount, Is.EqualTo(1));
  Assert.That(inputs.AreaSquareMeters, Is.EqualTo(5_000)); Assert.That(inputs.PathLength, Is.EqualTo(120));
 }
}
public class ParkLandscapeSchedulerTests {
 [Test] public void DirtyParksScanFirstWithinTheWorkBudget() {
  var s = new ParkLandscapeScheduler(refreshFrames: 1000);
  s.Track(1); s.Track(2); s.Track(3);
  Assert.That(s.Take(0, 1), Has.Count.EqualTo(1), "budget of one scan");
  Assert.That(s.Take(0, 1), Has.Count.EqualTo(1));
  Assert.That(s.Take(0, 5), Has.Count.EqualTo(1), "only the remaining new Park");
  Assert.That(s.Take(10, 5), Is.Empty, "fresh parks are not rescanned every update");
  s.MarkDirty(2);
  Assert.That(s.Take(20, 5), Is.EqualTo(new ulong[] { 2 }));
  s.MarkDirty(99);
  Assert.That(s.Take(30, 5), Is.Empty, "untracked parks are ignored");
 }
 [Test] public void PeriodicFallbackRefreshesStaleParksOnly() {
  var s = new ParkLandscapeScheduler(refreshFrames: 1000);
  s.Track(1); s.Track(2);
  s.Take(0, 1); s.Take(500, 1);
  Assert.That(s.Take(1001, 5), Is.EqualTo(new ulong[] { 1 }), "only the Park scanned at frame 0 is stale");
  Assert.That(s.Take(1600, 5), Is.EqualTo(new ulong[] { 2 }));
 }
 [Test] public void ForgottenParksAreDroppedAndMarkAllDirtyRequeues() {
  var s = new ParkLandscapeScheduler(refreshFrames: 1000);
  s.Track(1); s.Track(2); s.Take(0, 5);
  s.Forget(1); s.MarkAllDirty();
  Assert.That(s.Take(1, 5), Is.EqualTo(new ulong[] { 2 }));
  Assert.That(s.TrackedCount, Is.EqualTo(1));
 }
}
public class ParkLandscapeScanSliceTests {
 [Test] public void LargeScansAreSplitIntoBoundedSlices() {
  var scan = new ParkLandscapeScan<int>(Enumerable.Range(0, 10_000).ToList());
  var seen = new List<int>(); int slices = 0;
  while (!scan.Done) { var before = seen.Count; scan.Process(4096, seen.Add); slices++; Assert.That(seen.Count - before, Is.LessThanOrEqualTo(4096)); }
  Assert.That(slices, Is.EqualTo(3)); Assert.That(seen, Is.EqualTo(Enumerable.Range(0, 10_000)));
  var empty = new ParkLandscapeScan<int>(new List<int>());
  Assert.That(empty.Done, Is.True);
 }
}
