using Colossal.Mathematics;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
// #57: Park landscape candidates are enumerated tile by tile within the per-frame budget instead of all at scan start.
public class ParkLandscapeCollectionTests {
 private static Bounds2 Box(float x0, float z0, float x1, float z1) => new(new float2(x0, z0), new float2(x1, z1));
 // Synthetic static objects: (id, bounds) spread over the search area, some spanning several tiles.
 private static List<(int Id, Bounds2 Bounds)> Objects(int count, Bounds2 area, int seed = 7) {
  var random = new System.Random(seed);
  var list = new List<(int, Bounds2)>(count);
  for (int i = 0; i < count; i++) {
   float x = area.min.x - 5 + (float)random.NextDouble() * (area.max.x - area.min.x + 10);
   float z = area.min.y - 5 + (float)random.NextDouble() * (area.max.y - area.min.y + 10);
   float size = i % 50 == 0 ? 300 : (float)random.NextDouble() * 6; // a few objects larger than a tile
   list.Add((i, Box(x, z, x + size, z + size)));
  }
  return list;
 }
 private static bool Overlaps(Bounds2 a, Bounds2 b) => math.all(a.min <= b.max) && math.all(a.max >= b.min);
 // What the system's tile iterator does over the static search tree.
 private static int Enumerate(ParkLandscapeTiling tiling, ParkLandscapeTile tile, List<(int Id, Bounds2 Bounds)> objects, List<int> into) {
  int visited = 0;
  foreach (var (id, bounds) in objects) {
   if (!Overlaps(tile.Query, bounds)) continue;
   visited++;
   if (tiling.Owns(tile, bounds)) into.Add(id);
  }
  return visited;
 }
 [Test] public void TilesCoverTheSearchBoundsAndOwnEveryOverlappingObjectExactlyOnce() {
  var search = Box(-1000, -700, 1130, 905);
  var tiling = new ParkLandscapeTiling(search, ParkLandscapeTiling.DefaultTileSize);
  var objects = Objects(20_000, search);
  var seen = new Dictionary<int, int>();
  int tiles = 0;
  while (!tiling.Done) {
   var tile = tiling.Next(); tiles++;
   var items = new List<int>();
   Enumerate(tiling, tile, objects, items);
   foreach (var id in items) seen[id] = seen.TryGetValue(id, out var n) ? n + 1 : 1;
  }
  Assert.That(tiles, Is.EqualTo(tiling.TileCount));
  var expected = objects.Where(o => Overlaps(search, o.Bounds)).Select(o => o.Id).ToHashSet();
  Assert.That(seen.Keys, Is.EquivalentTo(expected), "no object inside the search bounds is lost");
  Assert.That(seen.Values.All(n => n == 1), Is.True, "an object spanning tiles is collected once");
 }
 [Test] public void ATinySearchIsOneTile() {
  var tiling = new ParkLandscapeTiling(Box(0, 0, 10, 10), ParkLandscapeTiling.DefaultTileSize);
  Assert.That(tiling.TileCount, Is.EqualTo(1));
  var degenerate = new ParkLandscapeTiling(Box(5, 5, 5, 5), ParkLandscapeTiling.DefaultTileSize);
  Assert.That(degenerate.TileCount, Is.EqualTo(1));
 }
 [TestCase(10_000)]
 [TestCase(50_000)]
 public void DenseParksAreCollectedOverSeveralFramesWithinTheBudget(int count) {
  var search = Box(0, 0, 1200, 1200);
  var objects = Objects(count, search);
  var tiling = new ParkLandscapeTiling(search, ParkLandscapeTiling.DefaultTileSize);
  var collector = new ParkLandscapeCollector<int>(tiling, budget: 4096, maxTilesPerFrame: 64);
  var classified = new List<int>();
  int frames = 0, maxTile = 0;
  while (!collector.Done) {
   collector.Step((tile, into) => { int visited = Enumerate(tiling, tile, objects, into); maxTile = Math.Max(maxTile, visited); return visited; }, classified.Add);
   frames++;
   Assert.That(frames, Is.LessThan(10_000), "terminates");
  }
  var expected = objects.Where(o => Overlaps(search, o.Bounds)).Select(o => o.Id).OrderBy(i => i).ToArray();
  Assert.That(classified.OrderBy(i => i).ToArray(), Is.EqualTo(expected));
  Assert.That(frames, Is.GreaterThan(1), "never the whole Park in the first frame");
  Assert.That(collector.MaxFrameWork, Is.LessThanOrEqualTo(4096 + maxTile), "a frame overshoots by at most one tile");
  Assert.That(collector.Enumerated, Is.GreaterThanOrEqualTo(expected.Length));
  Assert.That(collector.Classified, Is.EqualTo(expected.Length));
  Assert.That(collector.TilesEnumerated, Is.EqualTo(tiling.TileCount));
 }
 [Test] public void EmptyTilesDoNotStallTheScan() {
  var search = Box(0, 0, 5000, 5000);
  var tiling = new ParkLandscapeTiling(search, ParkLandscapeTiling.DefaultTileSize);
  var collector = new ParkLandscapeCollector<int>(tiling, budget: 4096, maxTilesPerFrame: 64);
  int frames = 0;
  while (!collector.Done) { collector.Step((_, _) => 0, _ => { }); frames++; }
  Assert.That(frames, Is.EqualTo((tiling.TileCount + 63) / 64), "at most maxTilesPerFrame tree queries per frame");
 }
 [Test] public void SystemEnumeratesThroughTheCollector() {
  var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Modules/Park/ParkLandscapeSystem.cs"));
  Assert.That(source, Does.Contain("new ParkLandscapeCollector<Entity>("));
  Assert.That(source, Does.Contain("struct TileIterator"));
  Assert.That(source, Does.Not.Contain("new ParkLandscapeScan<Entity>(candidates)"), "no up-front collection of every candidate");
 }
}
