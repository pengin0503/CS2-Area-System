using CS2AreaSystem.Core.Diagnostics;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Wave A1: fixed-capacity runtime-only trend samples (oldest -> newest, one sample per bucket).
public class RuntimeTrendSeriesTests {
 private static List<RuntimeTrendSample> Copy(RuntimeTrendSeries series) { var list = new List<RuntimeTrendSample>(); series.CopyTo(list); return list; }
 [Test] public void SameBucketReplacesWithoutGrowing() {
  var series = new RuntimeTrendSeries(3);
  series.Record(10, 1); series.Record(10, 2); series.Record(10, 3);
  Assert.That(series.Count, Is.EqualTo(1));
  Assert.That(series.TryLatest(out var latest), Is.True);
  Assert.That(latest.Bucket, Is.EqualTo(10)); Assert.That(latest.Value, Is.EqualTo(3));
  Assert.That(series.TryPrevious(out _), Is.False);
 }
 [Test] public void CapacityEvictsOldestInOrder() {
  var series = new RuntimeTrendSeries(3);
  series.Record(10, 100); series.Record(11, 110); series.Record(12, 120); series.Record(13, 130);
  Assert.That(Copy(series).Select(s => s.Bucket), Is.EqualTo(new long[] { 11, 12, 13 }));
  Assert.That(Copy(series).Select(s => s.Value), Is.EqualTo(new double[] { 110, 120, 130 }));
  series.Record(13, 131);
  Assert.That(series.Count, Is.EqualTo(3));
  Assert.That(Copy(series).Select(s => s.Value), Is.EqualTo(new double[] { 110, 120, 131 }));
 }
 [Test] public void OlderBucketAndNonFiniteValuesAreIgnored() {
  var series = new RuntimeTrendSeries(4);
  series.Record(20, 5);
  series.Record(19, 99);
  series.Record(21, double.NaN); series.Record(21, double.PositiveInfinity); series.Record(20, double.NegativeInfinity);
  Assert.That(series.Count, Is.EqualTo(1));
  Assert.That(series.TryLatest(out var latest), Is.True);
  Assert.That(latest.Bucket, Is.EqualTo(20)); Assert.That(latest.Value, Is.EqualTo(5));
 }
 [Test] public void LatestPreviousAndClearAreStable() {
  var series = new RuntimeTrendSeries(3);
  Assert.That(series.TryLatest(out _), Is.False); Assert.That(series.TryPrevious(out _), Is.False);
  series.Record(1, 10); series.Record(4, 40);
  Assert.That(series.TryLatest(out var latest), Is.True); Assert.That(latest.Bucket, Is.EqualTo(4));
  Assert.That(series.TryPrevious(out var previous), Is.True); Assert.That(previous.Bucket, Is.EqualTo(1)); Assert.That(previous.Value, Is.EqualTo(10));
  series.Clear();
  Assert.That(series.Count, Is.EqualTo(0));
  Assert.That(series.TryLatest(out _), Is.False); Assert.That(series.TryPrevious(out _), Is.False);
  // After a clear an earlier bucket is accepted again (new load generation).
  series.Record(0, 7);
  Assert.That(series.TryLatest(out latest), Is.True); Assert.That(latest.Bucket, Is.EqualTo(0));
 }
 [Test] public void CopyToReusesCallerList() {
  var series = new RuntimeTrendSeries(2);
  series.Record(1, 1); series.Record(2, 2);
  var list = new List<RuntimeTrendSample> { new RuntimeTrendSample(99, 99) };
  int copied = series.CopyTo(list);
  Assert.That(copied, Is.EqualTo(2));
  Assert.That(list.Select(s => s.Bucket), Is.EqualTo(new long[] { 1, 2 }));
  int capacity = list.Capacity;
  series.CopyTo(list);
  Assert.That(list.Capacity, Is.EqualTo(capacity));
  Assert.That(list.Count, Is.EqualTo(2));
 }
 [Test] public void CapacityMustBePositive() =>
  Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeTrendSeries(0));
}
