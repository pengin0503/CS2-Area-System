using System.Text.Json;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Runs the committed synthetic examples (docs/verification/industrial-score-examples.json) through the real models.
public class IndustrialScoreExampleTests {
 private const double Tol = 1e-5;
 internal sealed class Scores {
  public IndustrialMetric Production, Logistics, Employment, Efficiency, Performance;
  public int KnownProducers, OperatingProducers;
 }
 internal static string ExamplesPath() {
  var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
  while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "verification", "industrial-score-examples.json"))) dir = dir.Parent;
  Assert.That(dir, Is.Not.Null, "repository root");
  return Path.Combine(dir!.FullName, "docs", "verification", "industrial-score-examples.json");
 }
 internal static IEnumerable<JsonElement> Examples() {
  using var doc = JsonDocument.Parse(File.ReadAllText(ExamplesPath()));
  Assert.That(doc.RootElement.GetProperty("synthetic").GetBoolean(), Is.True, "examples are labeled synthetic");
  return doc.RootElement.GetProperty("examples").EnumerateArray().Select(e => e.Clone()).ToList();
 }
 private static double? Num(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
 private static IEnumerable<JsonElement> Repeat(JsonElement e, string name) {
  int repeat = (int)(Num(e, "repeat") ?? 1);
  var items = e.GetProperty(name).EnumerateArray().ToList();
  for (int i = 0; i < repeat; i++) foreach (var item in items) yield return item;
 }
 private static IndustrialChannelInputs Channel(double? share, double hours) {
  if (!share.HasValue || hours <= 0) return new IndustrialChannelInputs { Applicable = true, Supported = false };
  // Integrate hourly samples through the real rolling window (weight 1 per covered hour).
  var window = new IndustrialFlowWindow();
  for (int h = 0; h < hours; h++) window.Add(h, h + 1, share.Value, 1);
  var (affected, valid) = window.Totals(hours);
  return new IndustrialChannelInputs { Applicable = true, Supported = true, AffectedWeightedTime = affected, ValidWeightedTime = valid, CoveredHours = window.CoveredHours(hours) };
 }
 internal static Scores Compute(JsonElement e) {
  var activity = IndustrialWeighting.Mean(Repeat(e, "activity").Select(a => new IndustrialWeightedValue { Value = Num(a, "value"), Jobs = (int?)Num(a, "jobs") }).ToArray());
  double[] outputs = e.GetProperty("outputs").ValueKind == JsonValueKind.Array ? e.GetProperty("outputs").EnumerateArray().Select(o => o.GetDouble()).ToArray() : null;
  var s = new Scores { KnownProducers = (int)Num(e, "knownProducers")!.Value, OperatingProducers = (int)Num(e, "operatingProducers")!.Value };
  s.Production = IndustrialProductionModel.Compute(new IndustrialProductionInputs { Activity = activity, KnownProducers = s.KnownProducers, OperatingProducers = s.OperatingProducers, OutputActivity = outputs });
  double hours = Num(e, "coveredHours") ?? 0;
  s.Logistics = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = Channel(Num(e, "inputAffectedShare"), hours), Output = Channel(Num(e, "outputAffectedShare"), hours) });
  s.Employment = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = Repeat(e, "jobs").Select(j => new IndustrialJobFacility { Jobs = (int)Num(j, "jobs")!.Value, Employees = (int)Num(j, "employees")!.Value }).ToArray() });
  var efficiency = Repeat(e, "efficiency").ToList();
  var building = IndustrialWeighting.Mean(efficiency.Select(f => new IndustrialWeightedValue { Value = Num(f, "efficiency"), Jobs = (int?)Num(f, "jobs") }).ToArray());
  var profit = IndustrialWeighting.Mean(efficiency.Select(f => new IndustrialWeightedValue { Value = IndustrialSourceNormalization.ProfitViability((int)Num(f, "profitIndex")!.Value), Jobs = (int?)Num(f, "jobs") }).ToArray());
  s.Efficiency = IndustrialPerformanceModel.ComputeEfficiency(building, profit);
  s.Performance = IndustrialPerformanceModel.ComputePerformance(s.Production, s.Logistics, s.Employment, s.Efficiency);
  return s;
 }
 private static void Expect(JsonElement expected, string name, IndustrialMetric actual, string id) {
  if (!expected.TryGetProperty(name, out var v)) return;
  if (v.ValueKind == JsonValueKind.Null) Assert.That(actual.HasValue, Is.False, id + " " + name + " unavailable");
  else { Assert.That(actual.HasValue, Is.True, id + " " + name); Assert.That(actual.Value, Is.EqualTo(v.GetDouble()).Within(Tol), id + " " + name); }
 }
 [Test] public void EveryExampleMatchesTheRealModels() {
  var examples = Examples().ToList();
  Assert.That(examples.Select(e => e.GetProperty("id").GetString()), Is.EqualTo(new[] { "healthy-small", "healthy-large", "no-dedicated-infrastructure", "partial-flow", "stopped", "one-unavailable", "two-unavailable" }));
  foreach (var e in examples) {
   string id = e.GetProperty("id").GetString()!;
   var s = Compute(e); var x = e.GetProperty("expected");
   Expect(x, "production", s.Production, id); Expect(x, "logistics", s.Logistics, id); Expect(x, "employment", s.Employment, id);
   Expect(x, "efficiency", s.Efficiency, id); Expect(x, "performance", s.Performance, id);
  }
 }
 [Test] public void HealthySmallAndLargeDistrictsHaveIdenticalNormalizedScores() {
  var examples = Examples().ToDictionary(e => e.GetProperty("id").GetString()!);
  var small = Compute(examples["healthy-small"]); var large = Compute(examples["healthy-large"]);
  foreach (var (a, b) in new[] { (small.Production, large.Production), (small.Logistics, large.Logistics), (small.Employment, large.Employment), (small.Efficiency, large.Efficiency), (small.Performance, large.Performance) })
   Assert.That(a.Value, Is.EqualTo(b.Value).Within(Tol));
  Assert.That(large.KnownProducers, Is.GreaterThan(small.KnownProducers), "scale differs only in gate counts");
 }
 [Test] public void InfrastructurePresenceDoesNotChangeLogistics() {
  var examples = Examples().ToDictionary(e => e.GetProperty("id").GetString()!);
  Assert.That(Compute(examples["no-dedicated-infrastructure"]).Logistics.Value, Is.EqualTo(100).Within(Tol));
  Assert.That(examples["healthy-large"].GetProperty("cargoFacilities").GetInt32(), Is.GreaterThan(0));
  Assert.That(Compute(examples["healthy-large"]).Logistics.Value, Is.EqualTo(Compute(examples["no-dedicated-infrastructure"]).Logistics.Value).Within(Tol));
 }
 [Test] public void EveryExampleXpMatchesTheRealProgressionModel() {
  foreach (var e in Examples()) {
   string id = e.GetProperty("id").GetString()!;
   var x = e.GetProperty("expected");
   if (!x.TryGetProperty("hourlyXp", out var hourly)) continue;
   var s = Compute(e);
   var inputs = new IndustrialProgressInputs { Production = s.Production, Logistics = s.Logistics, Employment = s.Employment, Efficiency = s.Efficiency,
    Performance = s.Performance, ActiveProductiveFacilities = s.OperatingProducers, Fresh = true };
   var xp = IndustrialProgressionModel.ComputeHourlyXp(inputs, true, 0);
   Assert.That(xp.Award, Is.EqualTo(hourly.GetInt32()), id + " hourly XP");
   if (x.TryGetProperty("xpOperatingScore", out var score)) {
    if (score.ValueKind == JsonValueKind.Null) Assert.That(xp.OperatingScore, Is.Null, id);
    else Assert.That(xp.OperatingScore!.Value, Is.EqualTo(score.GetDouble()).Within(Tol), id + " XP score");
   }
   if (x.TryGetProperty("xpBlockedReason", out var reason)) Assert.That(xp.BlockedReason, Is.EqualTo(reason.GetString()), id);
  }
 }
}
