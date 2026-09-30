using Colossal.Mathematics;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
public class ParkPolygonTests {
 // A zig-zag strip of many thin triangles, like a long, detailed Park boundary.
 private static List<Triangle2> Strip(int count) {
  var result = new List<Triangle2>();
  for (int i = 0; i < count; i++) {
   float x = i * 10f;
   result.Add(new Triangle2(new float2(x, 0), new float2(x + 10, 0), new float2(x, 10 + (i % 3) * 5)));
   result.Add(new Triangle2(new float2(x + 10, 0), new float2(x + 10, 10 + ((i + 1) % 3) * 5), new float2(x, 10 + (i % 3) * 5)));
  }
  return result;
 }
 [Test] public void BoundsPrefilterGivesTheSameContainmentAndDistanceAsBruteForce() {
  var triangles = Strip(60);
  var polygon = new ParkPolygon(triangles);
  var random = new System.Random(7);
  for (int i = 0; i < 2000; i++) {
   var p = new float2((float)random.NextDouble() * 700 - 50, (float)random.NextDouble() * 80 - 40);
   bool brute = triangles.Any(t => NativeMembershipGeometry.Contains(t, p));
   Assert.That(polygon.Contains(p), Is.EqualTo(brute), $"contains {p}");
   float bruteDistance = triangles.Min(t => MathUtils.Distance(t, p, out _));
   Assert.That(polygon.Distance(p), Is.EqualTo(bruteDistance).Within(1e-3f), $"distance {p}");
  }
 }
 [Test] public void PointsFarOutsideAreRejectedByTheOverallBounds() {
  var polygon = new ParkPolygon(Strip(60));
  Assert.That(polygon.Contains(new float2(5000, 5000)), Is.False);
  Assert.That(polygon.TrianglesTestedLastQuery, Is.Zero);
  Assert.That(polygon.Contains(new float2(15, 2)), Is.True);
  Assert.That(polygon.TrianglesTestedLastQuery, Is.LessThan(10), "only triangles whose bounds contain the point are tested");
  Assert.That(new ParkPolygon(new List<Triangle2>()).Distance(new float2(0, 0)), Is.EqualTo(float.MaxValue));
 }
}
