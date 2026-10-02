using CS2AreaSystem.Core.Diagnostics;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Wave A1: Park and Industrial record runtime-only hourly trend samples at their existing evaluation/publication points.
public class RuntimeTrendIntegrationTests {
 private const uint Hour = ParkRuntimeSnapshot.RecentBucketFrames;
 private static ParkRuntimeSnapshot ParkWithQuality(float quality) {
  var snapshot = new ParkRuntimeSnapshot { StableId = 3, ActiveVisitors = 10 };
  snapshot.Quality = new ParkQualitySnapshot { Satisfaction = ParkMetric.Of(60), OccupancyRatio = 0.25f, Result = new ParkQualityResult { Quality = quality } };
  return snapshot;
 }
 private static double Latest(RuntimeTrendSeries series) { Assert.That(series.TryLatest(out var s), Is.True); return s.Value; }
 // ---- Park
 [Test] public void ParkRollingWindowCurrentBucketIsOnlyTheActiveHour() {
  var window = new ParkRollingWindow(Hour, ParkRuntimeSnapshot.RecentBuckets);
  window.Add(5, 10); window.Add(Hour + 5, 20); window.Add(Hour + 6, 3);
  Assert.That(window.CurrentBucket(Hour + 10), Is.EqualTo(23));
  Assert.That(window.CurrentBucket(10), Is.EqualTo(10));
  Assert.That(window.CurrentBucket(2 * Hour + 1), Is.Zero, "a bucket with no amount yet");
  Assert.That(window.Sum(Hour + 10), Is.EqualTo(33), "Sum keeps its last-day semantics");
 }
 [Test] public void ParkWithoutQualityRecordsNothing() {
  var snapshot = new ParkRuntimeSnapshot();
  snapshot.RecordTrends(Hour * 3);
  Assert.That(snapshot.Trends.Quality.Count, Is.Zero);
  Assert.That(snapshot.Trends.Visitors.Count, Is.Zero);
 }
 [Test] public void TwoParkQualityEvaluationsInOneHourProduceOneSample() {
  var snapshot = ParkWithQuality(40);
  snapshot.RecordTrends(Hour * 5 + 10);
  snapshot.Quality.Result = new ParkQualityResult { Quality = 45 };
  snapshot.RecordTrends(Hour * 5 + 900);
  Assert.That(snapshot.Trends.Quality.Count, Is.EqualTo(1));
  Assert.That(Latest(snapshot.Trends.Quality), Is.EqualTo(45).Within(1e-6));
  Assert.That(Latest(snapshot.Trends.Satisfaction), Is.EqualTo(60).Within(1e-6));
  Assert.That(Latest(snapshot.Trends.Occupancy), Is.EqualTo(25).Within(1e-4), "occupancy in percent");
 }
 [Test] public void NextParkHourProducesPreviousAndLatestSamples() {
  var snapshot = ParkWithQuality(40);
  snapshot.RecordTrends(Hour * 5 + 10);
  snapshot.Quality.Result = new ParkQualityResult { Quality = 52 };
  snapshot.RecordTrends(Hour * 6 + 10);
  Assert.That(snapshot.Trends.Quality.TryPrevious(out var previous), Is.True);
  Assert.That(previous.Bucket, Is.EqualTo(5)); Assert.That(previous.Value, Is.EqualTo(40).Within(1e-6));
  Assert.That(snapshot.Trends.Quality.TryLatest(out var latest), Is.True);
  Assert.That(latest.Bucket, Is.EqualTo(6)); Assert.That(latest.Value, Is.EqualTo(52).Within(1e-6));
 }
 [Test] public void ParkFlowTrendsRecordOnlyCompletedFullyObservedHours() {
  var snapshot = ParkWithQuality(40);
  // Hour 5 is only partly observed (the first capture happens inside it).
  snapshot.RecentVisitors.Add(Hour * 5 + 1, 4); snapshot.RecentAdmission.Add(Hour * 5 + 1, 100); snapshot.RecentExpense.Add(Hour * 5 + 1, 50);
  snapshot.RecordTrends(Hour * 5 + 10);
  snapshot.RecentVisitors.Add(Hour * 6 + 1, 30); snapshot.RecentAdmission.Add(Hour * 6 + 1, 600); snapshot.RecentExpense.Add(Hour * 6 + 1, 200);
  snapshot.RecordTrends(Hour * 6 + 10);
  Assert.That(snapshot.Trends.Visitors.Count, Is.Zero, "hour 5 started before observation; hour 6 is still running");
  snapshot.RecentVisitors.Add(Hour * 7 + 1, 7);
  snapshot.RecordTrends(Hour * 7 + 10);
  snapshot.RecordTrends(Hour * 7 + 500);
  Assert.That(snapshot.Trends.Visitors.Count, Is.EqualTo(1));
  Assert.That(snapshot.Trends.Visitors.TryLatest(out var visits), Is.True);
  Assert.That(visits.Bucket, Is.EqualTo(6)); Assert.That(visits.Value, Is.EqualTo(30));
  Assert.That(Latest(snapshot.Trends.AdmissionIncome), Is.EqualTo(600));
  Assert.That(Latest(snapshot.Trends.ManagementExpense), Is.EqualTo(200));
 }
 [Test] public void RecreatedParkRuntimeStoreHasNoTrends() {
  var store = new ParkRuntimeStore();
  var snapshot = store.GetOrCreate(3, Entity.Null);
  snapshot.Quality = ParkWithQuality(40).Quality;
  snapshot.RecordTrends(Hour * 5);
  Assert.That(snapshot.Trends.Quality.Count, Is.EqualTo(1));
  store.Clear();
  Assert.That(store.GetOrCreate(3, Entity.Null).Trends.Quality.Count, Is.Zero, "a load clears the store; old-city samples never reach the new city");
 }
 // ---- Industrial
 private static IndustrialEntityObservation Producer(Entity e, double activity = 100) => new() {
  Facility = e, Roles = IndustrialFacilityRole.Processor, IsProducer = true, ProducerTenants = 1, Available = true, IsOperatingProducer = activity > 0, ProductiveActivity = activity,
  Resources = new[] { new IndustrialResourceActivity { Resource = (ulong)Resource.Furniture, ActualPerDay = activity, CapacityPerDay = 100 } },
  Input = IndustrialFlowState.Healthy, Output = IndustrialFlowState.Healthy, Jobs = 40, Employees = 40, VacanciesByLevel = new int[5], Efficiency = 100, Profitability = 80 };
 private static IndustrialObservationResult Run(IndustrialRuntimeSnapshot snapshot, double start, double end, bool active = true, params IndustrialEntityObservation[] observations) {
  var acc = new IndustrialCycleAccumulator();
  foreach (var o in observations) IndustrialObservationAggregator.Apply(acc, o);
  return IndustrialObservationAggregator.Finalize(acc, snapshot, new IndustrialCycleInfo { CycleId = 1, HourBucket = (long)Math.Floor(start), StartHours = start, EndHours = end,
   MembershipToken = 1, StructureToken = 1, Active = active });
 }
 private static readonly Entity factory = new() { Index = 100, Version = 1 }, mill = new() { Index = 101, Version = 1 };
 [Test] public void ActiveNonDelayedIndustrialResultRecordsAvailableMetrics() {
  var snapshot = new IndustrialRuntimeSnapshot();
  var r = Run(snapshot, 3, 3.1, true, Producer(factory), Producer(mill, 0));
  var t = snapshot.Trends;
  Assert.That(t.Production.TryLatest(out var production), Is.True);
  Assert.That(production.Bucket, Is.EqualTo(3)); Assert.That(production.Value, Is.EqualTo(r.Production.Value).Within(1e-9));
  Assert.That(Latest(t.Employment), Is.EqualTo(r.Employment.Value).Within(1e-9));
  Assert.That(Latest(t.Efficiency), Is.EqualTo(r.Efficiency.Value).Within(1e-9));
  Assert.That(Latest(t.Performance), Is.EqualTo(r.Performance.Value).Within(1e-9));
  Assert.That(Latest(t.ProductiveFacilities), Is.EqualTo(1), "operating producers");
 }
 [Test] public void UnavailableIndustrialMetricIsSkipped() {
  var snapshot = new IndustrialRuntimeSnapshot();
  var r = Run(snapshot, 3, 3.1, true, Producer(factory));
  Assert.That(r.Logistics.HasValue, Is.False, "logistics is still warming up");
  Assert.That(snapshot.Trends.Logistics.Count, Is.Zero, "no fabricated zero sample");
  Assert.That(snapshot.Trends.Production.Count, Is.EqualTo(1));
 }
 [Test] public void DelayedOrInactiveIndustrialResultsDoNotBecomeSamples() {
  var snapshot = new IndustrialRuntimeSnapshot();
  var delayed = Run(snapshot, 3, 4.5, true, Producer(factory));
  Assert.That(delayed.Delayed, Is.True);
  Run(snapshot, 5, 5.1, false, Producer(factory));
  Assert.That(snapshot.Trends.Production.Count, Is.Zero);
  Assert.That(snapshot.Trends.ProductiveFacilities.Count, Is.Zero);
  Run(snapshot, 6, 6.1, true, Producer(factory));
  Run(snapshot, 7, 7.1, true, Producer(factory, 50));
  Assert.That(snapshot.Trends.Production.TryPrevious(out var previous), Is.True);
  Assert.That(previous.Bucket, Is.EqualTo(6));
 }
 [Test] public void IndustrialRuntimeStoreClearDropsTrendState() {
  var store = new IndustrialRuntimeStore();
  var snapshot = store.GetOrCreate(9, Entity.Null);
  Run(snapshot, 3, 3.1, true, Producer(factory));
  Assert.That(snapshot.Trends.Production.Count, Is.EqualTo(1));
  store.Clear();
  Assert.That(store.GetOrCreate(9, Entity.Null).Trends.Production.Count, Is.Zero);
 }
}
