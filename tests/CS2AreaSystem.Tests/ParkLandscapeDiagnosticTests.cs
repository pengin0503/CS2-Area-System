using Colossal.Mathematics;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
// Wave A5: advisory Park landscape diagnostics from the existing sliced scan, with bounded (4x4 grid) spatial evidence.
public class ParkLandscapeDiagnosticTests {
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private const float W = ParkLandscapeModel.VegetationWeight;
 // A result with every category at full weight (no shortage, no clutter).
 private static ParkLandscapeResult Full() => new() { Vegetation = ParkLandscapeModel.VegetationWeight, Amenities = ParkLandscapeModel.AmenitiesWeight,
  Decoration = ParkLandscapeModel.DecorationWeight, Lighting = ParkLandscapeModel.LightingWeight, Paths = ParkLandscapeModel.PathWeight };
 private static ParkLandscapeDiagnosticCollector Grid() => new(0, 0, 400, 400);
 // Cell (cx, cz) of the 4x4 grid over a 400 m square is 100 m wide.
 private static void Put(ParkLandscapeDiagnosticCollector grid, int cx, int cz, ParkLandscapeCategory category, int entity) =>
  grid.Add(category, cx * 100 + 50, cz * 100 + 50, E(entity));
 private static List<ParkLandscapeDiagnostic> Evaluate(ParkLandscapeResult result, ParkLandscapeDiagnosticCollector? grid = null) =>
  ParkLandscapeDiagnostics.Evaluate(result, grid ?? Grid()).ToList();
 // ---- pure evaluator
 [Test] public void CategoryBelowHalfItsWeightIsAShortage() {
  var r = Full(); r.Vegetation = W * 0.25f;
  var d = Evaluate(r).Single();
  Assert.That(d.Key, Is.EqualTo("LowVegetation"));
  Assert.That(d.Severity, Is.EqualTo(0.75).Within(1e-6), "normalized missing share");
  Assert.That(d.Target, Is.EqualTo(Entity.Null), "no positional evidence for where vegetation is missing");
 }
 [Test] public void ExactlyHalfIsNotAShortage() {
  var r = Full(); r.Vegetation = W * 0.5f; r.Paths = ParkLandscapeModel.PathWeight * 0.5f;
  Assert.That(Evaluate(r), Is.Empty);
 }
 [Test] public void EveryCategoryHasItsShortageKey() {
  var r = new ParkLandscapeResult();
  var keys = Evaluate(r).Select(d => d.Key).ToList();
  Assert.That(keys, Has.Count.EqualTo(ParkLandscapeDiagnostics.MaxDiagnostics), "capped");
  Assert.That(ParkLandscapeDiagnostics.Keys, Is.EquivalentTo(new[] { "LowVegetation", "LowAmenities", "LowDecoration", "LowLighting", "LowPaths", "Cluttered", "SparseAmenities", "SparseLighting" }));
 }
 [Test] public void ClutterSeverityUsesTheExistingMaxPenalty() {
  var r = Full(); r.Cluttered = true; r.ClutterPenalty = ParkLandscapeModel.MaxClutterPenalty * 0.4f;
  var grid = Grid();
  Put(grid, 0, 0, ParkLandscapeCategory.Unknown, 1); Put(grid, 3, 3, ParkLandscapeCategory.Unknown, 2); Put(grid, 3, 3, ParkLandscapeCategory.Unknown, 3);
  var d = Evaluate(r, grid).Single();
  Assert.That(d.Key, Is.EqualTo("Cluttered"));
  Assert.That(d.Severity, Is.EqualTo(0.4).Within(1e-6));
  Assert.That(d.Target, Is.EqualTo(E(2)), "first object of the densest cell");
  Assert.That(Evaluate(Full(), grid), Is.Empty, "no clutter diagnostic unless the model flags clutter");
 }
 [Test] public void SparseCoverageIsSuppressedBelowEightEvidencedCells() {
  var grid = Grid();
  for (int i = 0; i < 7; i++) Put(grid, i % 4, i / 4, ParkLandscapeCategory.Vegetation, 10 + i);
  Assert.That(grid.EvidencedCells, Is.EqualTo(7));
  Assert.That(Evaluate(Full(), grid), Is.Empty);
 }
 [Test] public void EightCellsWithLowAmenityOrLightingCoverageAreSparse() {
  var grid = Grid();
  for (int i = 0; i < 8; i++) Put(grid, i % 4, i / 4, ParkLandscapeCategory.Vegetation, 10 + i);
  Put(grid, 0, 0, ParkLandscapeCategory.Amenity, 50);
  Put(grid, 1, 0, ParkLandscapeCategory.Lighting, 60); Put(grid, 2, 0, ParkLandscapeCategory.Lighting, 61);
  for (int i = 0; i < 3; i++) Put(grid, 3, 1, ParkLandscapeCategory.Vegetation, 70 + i);
  var d = Evaluate(Full(), grid);
  Assert.That(d.Select(x => x.Key), Is.EqualTo(new[] { "SparseAmenities" }), "1/8 amenity cells is sparse; 2/8 lighting cells is exactly 25%, not sparse");
  Assert.That(d[0].Severity, Is.EqualTo(0.5).Within(1e-6), "1/8 coverage against a 25% target");
  Assert.That(d[0].Target, Is.EqualTo(E(17)), "a representative object in the densest evidenced cell without amenities");
  Assert.That(grid.EvidencedCells, Is.EqualTo(8));
 }
 [Test] public void SparseWarningIsNotRepeatedWhenTheCategoryIsAlreadyShort() {
  var grid = Grid();
  for (int i = 0; i < 9; i++) Put(grid, i % 4, i / 4, ParkLandscapeCategory.Vegetation, 10 + i);
  var r = Full(); r.Amenities = 0; r.Lighting = 0;
  var keys = Evaluate(r, grid).Select(x => x.Key).ToList();
  Assert.That(keys, Does.Contain("LowAmenities").And.Contain("LowLighting"));
  Assert.That(keys, Does.Not.Contain("SparseAmenities").And.Not.Contain("SparseLighting"));
 }
 [Test] public void ResultCapsAtFourInDeterministicOrder() {
  var r = new ParkLandscapeResult { Vegetation = W * 0.1f, Amenities = 0, Decoration = ParkLandscapeModel.DecorationWeight * 0.1f, Lighting = 0,
   Paths = ParkLandscapeModel.PathWeight * 0.3f, Cluttered = true, ClutterPenalty = ParkLandscapeModel.MaxClutterPenalty };
  var d = Evaluate(r);
  Assert.That(d.Select(x => x.Key), Is.EqualTo(new[] { "Cluttered", "LowAmenities", "LowLighting", "LowDecoration" }), "severity desc, then key");
  Assert.That(Evaluate(r).Select(x => x.Key), Is.EqualTo(d.Select(x => x.Key)));
 }
 // ---- bounded collector
 [Test] public void ThousandsOfObjectsKeepSixteenCellsAndNoPerObjectState() {
  var grid = Grid();
  var random = new System.Random(3);
  var categories = new[] { ParkLandscapeCategory.Vegetation, ParkLandscapeCategory.Amenity, ParkLandscapeCategory.Lighting, ParkLandscapeCategory.Decoration, ParkLandscapeCategory.Unknown };
  Put(grid, 0, 0, ParkLandscapeCategory.Vegetation, 1); // warm-up
  long before = GC.GetAllocatedBytesForCurrentThread();
  for (int i = 0; i < 20_000; i++)
   grid.Add(categories[i % categories.Length], (float)random.NextDouble() * 400, (float)random.NextDouble() * 400, E(100 + i));
  long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
  Assert.That(allocated, Is.LessThan(1024), "adding objects allocates nothing per object");
  Assert.That(ParkLandscapeDiagnosticCollector.Cells, Is.EqualTo(16));
  Assert.That(grid.EvidencedCells, Is.EqualTo(16));
  Assert.That(grid.Objects, Is.EqualTo(20_001));
 }
 [Test] public void RepresentativeTargetIsDeterministic() {
  ParkLandscapeDiagnosticCollector Fill() {
   var grid = Grid();
   for (int i = 0; i < 500; i++) grid.Add(ParkLandscapeCategory.Unknown, 350 + i % 40, 350 + i % 30, E(1000 + i));
   for (int i = 0; i < 10; i++) grid.Add(ParkLandscapeCategory.Unknown, 10, 10, E(5000 + i));
   return grid;
  }
  var r = Full(); r.Cluttered = true; r.ClutterPenalty = 3;
  Assert.That(Evaluate(r, Fill()).Single().Target, Is.EqualTo(E(1000)));
  Assert.That(Evaluate(r, Fill()).Single().Target, Is.EqualTo(Evaluate(r, Fill()).Single().Target));
 }
 [Test] public void ObjectsOutsideTheBoundsClampToEdgeCellsAndNoneIsIgnored() {
  var grid = Grid();
  grid.Add(ParkLandscapeCategory.None, 10, 10, E(1));
  Assert.That(grid.Objects, Is.Zero);
  grid.Add(ParkLandscapeCategory.Vegetation, -5, 900, E(2));
  Assert.That(grid.EvidencedCells, Is.EqualTo(1));
  var degenerate = new ParkLandscapeDiagnosticCollector(5, 5, 5, 5);
  degenerate.Add(ParkLandscapeCategory.Vegetation, 5, 5, E(3));
  Assert.That(degenerate.EvidencedCells, Is.EqualTo(1));
 }
 [Test] public void ScanBudgetIsUnchanged() {
  Assert.That(ParkLandscapeSystem.ObjectsPerFrame, Is.EqualTo(4096));
  Assert.That(ParkLandscapeSystem.TilesPerFrame, Is.EqualTo(64));
 }
 // ---- sliced scan invalidation
 [Test] public void BoundaryEditDiscardsTheTallyGridAndDiagnosticsTogether() {
  var scheduler = new ParkLandscapeScheduler(1000);
  scheduler.Track(7); scheduler.Take(100, 1);
  var scan = new ParkLandscapeScanState(7, scheduler.RevisionOf(7), 0, 0, 400, 400);
  for (int i = 0; i < 9; i++) scan.Add(ParkLandscapeCategory.Unknown, 1, i % 4 * 100 + 50, i / 4 * 100 + 50, E(10 + i));
  Assert.That(scan.Diagnostics.EvidencedCells, Is.EqualTo(9));
  scheduler.BoundaryChanged(7);
  Assert.That(scan.TryComplete(scheduler, 10_000, 0, 1, out var stale), Is.False);
  Assert.That(stale, Is.Null, "nothing from the pre-edit scan reaches the snapshot");
  var fresh = new ParkLandscapeScanState(7, scheduler.RevisionOf(7), 0, 0, 400, 400);
  for (int i = 0; i < 9; i++) fresh.Add(ParkLandscapeCategory.Vegetation, 1, i % 4 * 100 + 50, i / 4 * 100 + 50, E(10 + i));
  Assert.That(fresh.TryComplete(scheduler, 160_000, 0, 1, out var landscape), Is.True);
  Assert.That(landscape!.Available, Is.True);
  Assert.That(landscape.Inputs.Vegetation.Count, Is.EqualTo(9));
  Assert.That(landscape.Diagnostics.Select(d => d.Key), Does.Contain("SparseAmenities").Or.Contain("LowAmenities"));
  Assert.That(landscape.Diagnostics, Has.Count.LessThanOrEqualTo(ParkLandscapeDiagnostics.MaxDiagnostics));
 }
 // ---- focus action
 private static List<Triangle2> Square(float size) => new() {
  new Triangle2(new float2(0, 0), new float2(size, 0), new float2(size, size)), new Triangle2(new float2(0, 0), new float2(size, size), new float2(0, size)) };
 private static ParkLandscapeSnapshot Landscape(params Entity[] targets) => new() { Available = true,
  Diagnostics = targets.Select((t, i) => new ParkLandscapeDiagnostic("Cluttered" + i, 0.5, t)).ToList() };
 private sealed class Objects {
  public readonly Dictionary<Entity, (float X, float Z)> Standalone = new();
  public (bool Valid, float X, float Z) Position(Entity e) => Standalone.TryGetValue(e, out var p) ? (true, p.X, p.Z) : (false, 0, 0);
 }
 [Test] public void FocusResolvesAValidCurrentTarget() {
  var objects = new Objects(); objects.Standalone[E(5)] = (50, 50);
  var focused = new List<Entity>();
  Assert.That(ParkGameplayFacade.TryFocus(Landscape(E(5)), AreaDiagnosticTargetKey.Format(E(5)), objects.Position, new ParkPolygon(Square(100)), focused.Add), Is.True);
  Assert.That(focused, Is.EqualTo(new[] { E(5) }));
 }
 [Test] public void FocusRejectsADeletedTarget() {
  var objects = new Objects();
  Assert.That(ParkGameplayFacade.TryFocus(Landscape(E(5)), AreaDiagnosticTargetKey.Format(E(5)), objects.Position, new ParkPolygon(Square(100)), _ => Assert.Fail()), Is.False);
 }
 [Test] public void FocusRejectsATargetMovedOutsideThePark() {
  var objects = new Objects(); objects.Standalone[E(5)] = (150, 50);
  Assert.That(ParkGameplayFacade.TryFocus(Landscape(E(5)), AreaDiagnosticTargetKey.Format(E(5)), objects.Position, new ParkPolygon(Square(100)), _ => Assert.Fail()), Is.False);
 }
 [Test] public void FocusRejectsAKeyFromAnOldSnapshot() {
  var objects = new Objects(); objects.Standalone[E(5)] = (50, 50); objects.Standalone[E(6)] = (60, 60);
  Assert.That(ParkGameplayFacade.TryFocus(Landscape(E(6)), AreaDiagnosticTargetKey.Format(E(5)), objects.Position, new ParkPolygon(Square(100)), _ => Assert.Fail()), Is.False);
  Assert.That(ParkGameplayFacade.TryFocus(new ParkLandscapeSnapshot(), AreaDiagnosticTargetKey.Format(E(5)), objects.Position, new ParkPolygon(Square(100)), _ => Assert.Fail()), Is.False,
   "no scan yet");
  Assert.That(ParkGameplayFacade.TryFocus(null!, AreaDiagnosticTargetKey.Format(E(5)), objects.Position, new ParkPolygon(Square(100)), _ => Assert.Fail()), Is.False);
 }
 [Test] public void FocusIsNotABudgetOrFeeChange() {
  var settings = new CS2AreaSystem.Core.Areas.ManagedAreaSettings { BudgetMultiplier = 1 }; var state = ParkAreaState.CreateDefault();
  Assert.That(ParkGameplayFacade.TryApplyAction(ParkGameplayFacade.FocusDiagnosticTarget, "5.1", 3, ref settings, ref state), Is.False);
  Assert.That(settings.BudgetMultiplier, Is.EqualTo(1));
  Assert.That(ParkGameplayFacade.FocusDiagnosticTarget, Is.EqualTo("focusDiagnosticTarget"));
 }
}
