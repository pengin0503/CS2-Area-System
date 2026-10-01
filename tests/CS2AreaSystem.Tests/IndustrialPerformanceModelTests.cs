using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class IndustrialPerformanceModelTests {
 private const double Tol = 1e-9;
 private static IndustrialMetric M(double v) => IndustrialMetric.Of(v);
 private static readonly IndustrialMetric U = IndustrialMetric.Unavailable();
 private static IndustrialChannelInputs Channel(double affectedShare, double hours = 6, bool anyUnavailable = false) =>
  new() { Applicable = true, Supported = true, AffectedWeightedTime = affectedShare * hours, ValidWeightedTime = hours, CoveredHours = hours, AnyUnavailable = anyUnavailable };
 private static IndustrialProductionInputs Production(double activity, int known, int operating, params double[] outputs) =>
  new() { Activity = M(activity), KnownProducers = known, OperatingProducers = operating, OutputActivity = outputs };
 // ---- exact weights
 [Test] public void ProductionUses65ActivityPlus25ActiveSharePlus10Diversity() {
  Assert.That(IndustrialProductionModel.Compute(Production(80, 4, 3, 10, 10, 10)).Value, Is.EqualTo(0.65 * 80 + 0.25 * 75 + 0.10 * 100).Within(Tol));
  Assert.That(IndustrialProductionModel.Compute(Production(100, 4, 4, 1)).Value, Is.EqualTo(95).Within(Tol));
 }
 [Test] public void LogisticsIsTheMeanOfInputAndOutputContinuity() {
  var r = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = Channel(0.5), Output = Channel(0.25) });
  Assert.That(r.Value, Is.EqualTo(62.5).Within(Tol)); Assert.That(r.Status, Is.EqualTo(IndustrialMetricStatus.Available));
  Assert.That(IndustrialLogisticsModel.Channel(Channel(0)).Value, Is.EqualTo(100).Within(Tol), "healthy observable channel");
  Assert.That(IndustrialLogisticsModel.Channel(Channel(1)).Value, Is.EqualTo(0).Within(Tol), "continuously affected");
 }
 [Test] public void LogisticsInputsHaveNoInfrastructurePresenceTerms() {
  var names = typeof(IndustrialLogisticsInputs).GetFields().Concat(typeof(IndustrialChannelInputs).GetFields()).Select(f => f.Name.ToLowerInvariant());
  Assert.That(names.Any(n => n.Contains("storage") || n.Contains("cargo") || n.Contains("warehouse") || n.Contains("terminal")), Is.False);
 }
 [Test] public void EmploymentUses75AggregateFillPlus25MeanFacilityFill() {
  var r = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = new[] { Job(100, 50), Job(20, 20) } });
  double fill = 100.0 * 70 / 120, balance = (50 + 100) / 2.0;
  Assert.That(r.Value, Is.EqualTo(0.75 * fill + 0.25 * balance).Within(Tol));
  Assert.That(IndustrialEmploymentModel.JobFill(new[] { Job(100, 50), Job(20, 20) }).Value, Is.EqualTo(fill).Within(Tol));
  Assert.That(IndustrialEmploymentModel.StaffingBalance(new[] { Job(100, 50), Job(20, 20) }).Value, Is.EqualTo(balance).Within(Tol));
 }
 [Test] public void LargeEmptyCapacityDoesNotOutscoreAFilledSmallDistrict() {
  var large = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = Enumerable.Repeat(Job(100, 20), 50).ToArray() });
  var small = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = new[] { Job(20, 18) } });
  Assert.That(large.Value, Is.LessThan(small.Value));
 }
 [Test] public void EfficiencyUses70BuildingPlus30Profitability() {
  Assert.That(IndustrialPerformanceModel.ComputeEfficiency(M(80), M(50)).Value, Is.EqualTo(0.7 * 80 + 0.3 * 50).Within(Tol));
 }
 [Test] public void PerformanceUses35ProductionPlus25LogisticsPlus20EmploymentPlus20Efficiency() {
  Assert.That(IndustrialPerformanceModel.ComputePerformance(M(95), M(100), M(100), M(100)).Value, Is.EqualTo(98.25).Within(Tol));
  Assert.That(IndustrialPerformanceModel.ComputePerformance(M(60), M(40), M(80), M(70)).Value, Is.EqualTo(0.35 * 60 + 0.25 * 40 + 0.2 * 80 + 0.2 * 70).Within(Tol));
 }
 // ---- availability and renormalization
 [Test] public void AnUnsetMetricIsUnavailableNeverAZero() {
  IndustrialMetric unset = default;
  Assert.That(unset.Status, Is.EqualTo(IndustrialMetricStatus.Unavailable)); Assert.That(unset.HasValue, Is.False);
  Assert.That(IndustrialMetric.Of(double.NaN).HasValue, Is.False); Assert.That(IndustrialMetric.Of(double.PositiveInfinity).HasValue, Is.False);
 }
 [Test] public void OneMissingPerformanceComponentRenormalizesTheOtherThreeOriginalWeights() {
  var r = IndustrialPerformanceModel.ComputePerformance(U, M(80), M(60), M(100));
  Assert.That(r.Value, Is.EqualTo((0.25 * 80 + 0.2 * 60 + 0.2 * 100) / 0.65).Within(Tol)); Assert.That(r.Value, Is.EqualTo(80).Within(Tol));
  Assert.That(r.Status, Is.EqualTo(IndustrialMetricStatus.Partial), "the missing component is marked");
  Assert.That(IndustrialPerformanceModel.ComputePerformance(M(50), U, M(60), M(100)).Value, Is.EqualTo((0.35 * 50 + 0.2 * 60 + 0.2 * 100) / 0.75).Within(Tol));
  Assert.That(IndustrialPerformanceModel.ComputePerformance(M(50), M(70), U, M(100)).Value, Is.EqualTo((0.35 * 50 + 0.25 * 70 + 0.2 * 100) / 0.8).Within(Tol));
  Assert.That(IndustrialPerformanceModel.ComputePerformance(M(50), M(70), M(60), U).Value, Is.EqualTo((0.35 * 50 + 0.25 * 70 + 0.2 * 60) / 0.8).Within(Tol));
 }
 [Test] public void TwoMissingPerformanceComponentsMakePerformanceUnavailable() {
  Assert.That(IndustrialPerformanceModel.ComputePerformance(U, U, M(60), M(100)).HasValue, Is.False);
  Assert.That(IndustrialPerformanceModel.ComputePerformance(U, IndustrialMetric.NotApplicable(), M(60), M(100)).HasValue, Is.False, "NotApplicable is not a score either");
 }
 [Test] public void OneMissingEfficiencySourceUsesTheOtherAtFullWeight() {
  var building = IndustrialPerformanceModel.ComputeEfficiency(M(80), U);
  Assert.That(building.Value, Is.EqualTo(80).Within(Tol)); Assert.That(building.Status, Is.EqualTo(IndustrialMetricStatus.Partial));
  Assert.That(IndustrialPerformanceModel.ComputeEfficiency(U, M(30)).Value, Is.EqualTo(30).Within(Tol));
  Assert.That(IndustrialPerformanceModel.ComputeEfficiency(U, U).HasValue, Is.False, "both missing: unavailable, not zero");
 }
 [Test] public void ZeroValidJobsIsNotApplicableAndNegativeCapacityIsInvalid() {
  var none = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = new[] { Job(0, 0) } });
  Assert.That(none.Status, Is.EqualTo(IndustrialMetricStatus.NotApplicable)); Assert.That(none.HasValue, Is.False);
  Assert.That(IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = Array.Empty<IndustrialJobFacility>() }).Status, Is.EqualTo(IndustrialMetricStatus.NotApplicable));
  var negative = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = new[] { Job(-5, 0) } });
  Assert.That(negative.Status, Is.EqualTo(IndustrialMetricStatus.Unavailable), "negative capacity is invalid, not a healthy zero");
  var mixed = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = new[] { Job(-5, 0), Job(10, 10) } });
  Assert.That(mixed.Status, Is.EqualTo(IndustrialMetricStatus.Partial)); Assert.That(mixed.Value, Is.EqualTo(100).Within(Tol));
  Assert.That(mixed.Observed, Is.EqualTo(1)); Assert.That(mixed.Eligible, Is.EqualTo(2));
  var zeroExcluded = IndustrialEmploymentModel.StaffingBalance(new[] { Job(0, 0), Job(10, 5) });
  Assert.That(zeroExcluded.Value, Is.EqualTo(50).Within(Tol), "zero-job facilities are excluded, not credited as fully staffed");
 }
 [Test] public void OneUnavailableOrNotApplicableLogisticsChannelReweightsTheOther() {
  var na = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = new IndustrialChannelInputs { Applicable = false }, Output = Channel(0.2) });
  Assert.That(na.Value, Is.EqualTo(80).Within(Tol)); Assert.That(na.Status, Is.EqualTo(IndustrialMetricStatus.Available), "NotApplicable is excluded without a partial label");
  var unavailable = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = new IndustrialChannelInputs { Applicable = true, Supported = false }, Output = Channel(0.2) });
  Assert.That(unavailable.Value, Is.EqualTo(80).Within(Tol)); Assert.That(unavailable.Status, Is.EqualTo(IndustrialMetricStatus.Partial));
  var none = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = new IndustrialChannelInputs { Applicable = true, Supported = false }, Output = new IndustrialChannelInputs { Applicable = false } });
  Assert.That(none.HasValue, Is.False);
  var neither = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = new IndustrialChannelInputs { Applicable = false }, Output = new IndustrialChannelInputs { Applicable = false } });
  Assert.That(neither.HasValue, Is.False);
 }
 [Test] public void LogisticsNeedsFourObservedHoursPerChannel() {
  var warming = IndustrialLogisticsModel.Channel(Channel(0, 3.99));
  Assert.That(warming.Status, Is.EqualTo(IndustrialMetricStatus.WarmingUp)); Assert.That(warming.HasValue, Is.False, "no symptoms and little coverage is not 100");
  Assert.That(IndustrialLogisticsModel.Channel(Channel(0, 4)).Value, Is.EqualTo(100).Within(Tol));
  var noCoverage = IndustrialLogisticsModel.Channel(new IndustrialChannelInputs { Applicable = true, Supported = true });
  Assert.That(noCoverage.HasValue, Is.False);
  var mixed = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = Channel(0, 2), Output = Channel(0.5) });
  Assert.That(mixed.Value, Is.EqualTo(50).Within(Tol)); Assert.That(mixed.Status, Is.EqualTo(IndustrialMetricStatus.Partial), "warm-up channel excluded with its reason preserved");
  var both = IndustrialLogisticsModel.Compute(new IndustrialLogisticsInputs { Input = Channel(0, 2), Output = Channel(0, 1) });
  Assert.That(both.Status, Is.EqualTo(IndustrialMetricStatus.WarmingUp));
  var partialFacilities = IndustrialLogisticsModel.Channel(Channel(0.1, 6, anyUnavailable: true));
  Assert.That(partialFacilities.Status, Is.EqualTo(IndustrialMetricStatus.Partial));
 }
 [Test] public void FlowWindowIntegratesElapsedTimeInsideTheSixHourWindow() {
  var window = new IndustrialFlowWindow();
  for (int h = 0; h < 8; h++) window.Add(h, h + 1, h < 4 ? 2 : 0, 2);
  Assert.That(window.CoveredHours(8), Is.EqualTo(6).Within(Tol));
  var (affected, valid) = window.Totals(8);
  Assert.That(affected / valid, Is.EqualTo(2.0 / 6).Within(Tol), "hours 2..3 affected inside window 2..8");
  window.Add(9, 9.5, 1, 2);
  Assert.That(window.CoveredHours(9.5), Is.EqualTo(5).Within(Tol), "the 8..9 gap adds no coverage");
 }
 [Test] public void ActivityAggregationUsesJobWeightsAndFlagsUnknownJobs() {
  var r = IndustrialWeighting.Mean(new[] { new IndustrialWeightedValue { Value = 100, Jobs = 300 }, new IndustrialWeightedValue { Value = 0, Jobs = 0 } });
  Assert.That(r.Value, Is.EqualTo(100.0 * 100 / 101).Within(Tol), "w = clamp(jobs, 1, 100)");
  var unknown = IndustrialWeighting.Mean(new[] { new IndustrialWeightedValue { Value = 50 }, new IndustrialWeightedValue { Value = 150, Jobs = 10 } });
  Assert.That(unknown.Value, Is.EqualTo((50 * 1 + 100 * 10) / 11.0).Within(Tol), "values clamp to 100 for scoring");
  Assert.That(unknown.Status, Is.EqualTo(IndustrialMetricStatus.Partial), "missing jobs use weight 1 and a partial label");
  var missing = IndustrialWeighting.Mean(new[] { new IndustrialWeightedValue { Value = null, Jobs = 10 }, new IndustrialWeightedValue { Value = 60, Jobs = 10 } });
  Assert.That(missing.Value, Is.EqualTo(60).Within(Tol)); Assert.That(missing.Observed, Is.EqualTo(1)); Assert.That(missing.Eligible, Is.EqualTo(2));
  Assert.That(IndustrialWeighting.Mean(new[] { new IndustrialWeightedValue { Value = double.NaN, Jobs = 1 } }).HasValue, Is.False);
  Assert.That(IndustrialWeighting.Mean(Array.Empty<IndustrialWeightedValue>()).Status, Is.EqualTo(IndustrialMetricStatus.NotApplicable));
 }
 [Test] public void ProductionRequiresValidatedActivityAndRenormalizesOptionalInputs() {
  var noActivity = IndustrialProductionModel.Compute(new IndustrialProductionInputs { Activity = U, KnownProducers = 3, OperatingProducers = 3, OutputActivity = new double[] { 1 } });
  Assert.That(noActivity.HasValue, Is.False, "throughput is never fabricated");
  Assert.That(IndustrialProductionModel.Compute(new IndustrialProductionInputs { Activity = M(100) }).Status, Is.EqualTo(IndustrialMetricStatus.NotApplicable), "no known producers");
  var unknownOutputs = IndustrialProductionModel.Compute(new IndustrialProductionInputs { Activity = M(80), KnownProducers = 2, OperatingProducers = 1, OutputActivity = null });
  Assert.That(unknownOutputs.Value, Is.EqualTo((0.65 * 80 + 0.25 * 50) / 0.9).Within(Tol)); Assert.That(unknownOutputs.Status, Is.EqualTo(IndustrialMetricStatus.Partial));
 }
 [Test] public void ActiveShareCountsConfirmedOperatingProducersOnly() {
  Assert.That(IndustrialProductionModel.ActiveShare(4, 1).Value, Is.EqualTo(25).Within(Tol));
  Assert.That(IndustrialProductionModel.ActiveShare(0, 0).Status, Is.EqualTo(IndustrialMetricStatus.NotApplicable));
  Assert.That(IndustrialProductionModel.ActiveShare(-1, 0).HasValue, Is.False);
 }
 // ---- diversity
 [Test] public void DiversityHasDiminishingReturnsAndAMeaningfulnessFloor() {
  var active = M(70);
  Assert.That(IndustrialProductionModel.Diversity(active, new double[] { 10 }).Value, Is.EqualTo(50));
  Assert.That(IndustrialProductionModel.Diversity(active, new double[] { 10, 5 }).Value, Is.EqualTo(75));
  Assert.That(IndustrialProductionModel.Diversity(active, new double[] { 10, 5, 2 }).Value, Is.EqualTo(100));
  Assert.That(IndustrialProductionModel.Diversity(active, new double[] { 10, 5, 2, 9, 9 }).Value, Is.EqualTo(100));
  Assert.That(IndustrialProductionModel.MeaningfulOutputs(new double[] { 100, 19.99, 1, 1, 1, 1 }), Is.EqualTo(1), "spammed tiny outputs do not count");
  Assert.That(IndustrialProductionModel.MeaningfulOutputs(new double[] { 100, 20 }), Is.EqualTo(2), "exactly 20% of the dominant output is meaningful");
  Assert.That(IndustrialProductionModel.Diversity(M(0), new double[] { 0, 0 }).Value, Is.EqualTo(0), "zero verified activity has diversity 0");
  Assert.That(IndustrialProductionModel.Diversity(U, new double[] { 10 }).HasValue, Is.False, "only when activity itself is available");
  Assert.That(IndustrialProductionModel.Diversity(active, null).HasValue, Is.False, "unknown outputs are unavailable");
  var spam = IndustrialProductionModel.Compute(Production(30, 10, 10, 10, 10, 10, 10, 10));
  var strong = IndustrialProductionModel.Compute(Production(100, 10, 10, 10));
  Assert.That(spam.Value, Is.LessThan(strong.Value), "variety cannot outweigh strong sustained production");
 }
 // ---- bottlenecks
 [Test] public void BottlenecksAreOrderedBySeverityAndLimitedToThree() {
  var list = IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs {
   JobFill = M(40), BuildingEfficiency = M(50), Profitability = M(10), InputContinuity = M(70), OutputContinuity = M(85), ActiveShare = M(60),
   ProducerFacilities = 5, UnderstaffedFacilities = 3, LowEfficiencyFacilities = 2, LowProfitFacilities = 4, InputAffectedFacilities = 2, OutputAffectedFacilities = 1, IdleProducers = 2,
   InputObservedHours = 6, OutputObservedHours = 5, SampleHours = 1 });
  Assert.That(list.Select(b => b.Key), Is.EqualTo(new[] { "LowProfitability", "WorkforceShortage", "LowEfficiency" }));
  Assert.That(list[0].Severity, Is.EqualTo(0.9).Within(Tol)); Assert.That(list[0].AffectedFacilities, Is.EqualTo(4)); Assert.That(list[0].EvidenceValue, Is.EqualTo(10));
  var all = IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs { InputContinuity = M(50), InputAffectedFacilities = 3, InputObservedHours = 5.5, OutputContinuity = M(60), OutputAffectedFacilities = 2, OutputObservedHours = 6 });
  Assert.That(all.Select(b => b.Key), Is.EqualTo(new[] { "InputStarvation", "StorageSaturation" }));
  Assert.That(all[0].ObservedHours, Is.EqualTo(5.5)); Assert.That(all[0].AffectedFacilities, Is.EqualTo(3));
 }
 [Test] public void UnavailableMetricsAndHealthyValuesProduceNoBottleneck() {
  Assert.That(IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs { JobFill = U, BuildingEfficiency = U, Profitability = U,
   InputContinuity = IndustrialMetric.WarmingUp(2, 4), OutputContinuity = IndustrialMetric.NotApplicable(), ActiveShare = U }), Is.Empty);
  Assert.That(IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs { JobFill = M(95), BuildingEfficiency = M(100), Profitability = M(70),
   InputContinuity = M(100), OutputContinuity = M(100), ActiveShare = M(100) }), Is.Empty);
 }
 [Test] public void InsufficientCargoNeedsAttributableEvidenceNotJustAMissingTerminal() {
  var noTerminal = IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs { OutputContinuity = M(40), OutputAffectedFacilities = 2, OutputObservedHours = 6, CargoFacilities = 0 });
  Assert.That(noTerminal.Select(b => b.Key), Is.EqualTo(new[] { "StorageSaturation" }), "absence of an in-boundary terminal is not evidence");
  var idleTerminal = IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs { OutputContinuity = M(40), OutputAffectedFacilities = 2, OutputObservedHours = 6, CargoFacilities = 2, CargoNonOperational = 1 });
  Assert.That(idleTerminal.Select(b => b.Key), Does.Contain("InsufficientCargo"));
  var healthyFlow = IndustrialPerformanceModel.FindBottlenecks(new IndustrialBottleneckInputs { OutputContinuity = M(100), CargoFacilities = 2, CargoNonOperational = 2 });
  Assert.That(healthyFlow, Is.Empty, "an idle terminal without a flow symptom is not a bottleneck");
 }
 [Test] public void NoResultIsNaNOrInfinite() {
  var values = new[] { 0, 50, 100, 120, -10, double.NaN, double.PositiveInfinity };
  foreach (var v in values) {
   foreach (var m in new[] { IndustrialPerformanceModel.ComputePerformance(IndustrialMetric.Of(v), M(50), M(50), M(50)), IndustrialPerformanceModel.ComputeEfficiency(IndustrialMetric.Of(v), M(50)),
    IndustrialProductionModel.Compute(new IndustrialProductionInputs { Activity = IndustrialMetric.Of(v), KnownProducers = 2, OperatingProducers = 1, OutputActivity = new[] { v } }) })
    if (m.HasValue) { Assert.That(double.IsNaN(m.Value) || double.IsInfinity(m.Value), Is.False, v.ToString()); Assert.That(m.Value, Is.InRange(0, 100)); }
  }
  Assert.That(IndustrialLogisticsModel.Channel(new IndustrialChannelInputs { Applicable = true, Supported = true, CoveredHours = 6, AffectedWeightedTime = 1, ValidWeightedTime = 0 }).HasValue, Is.False);
 }
 private static IndustrialJobFacility Job(int jobs, int employees) => new() { Jobs = jobs, Employees = employees };
}
