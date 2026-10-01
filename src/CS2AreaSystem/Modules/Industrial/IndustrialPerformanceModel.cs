using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Industrial
{
    public struct IndustrialBottleneckInputs
    {
        public IndustrialMetric JobFill, BuildingEfficiency, Profitability, InputContinuity, OutputContinuity, ActiveShare;
        public int ProducerFacilities, UnderstaffedFacilities, LowEfficiencyFacilities, LowProfitFacilities, InputAffectedFacilities, OutputAffectedFacilities, IdleProducers;
        public double InputObservedHours, OutputObservedHours;
        // Length of the latest completed observation (current-state diagnostics).
        public double SampleHours;
        // In-boundary cargo facilities and how many are not operating: the only attributable cargo evidence in v1.
        public int CargoFacilities, CargoNonOperational;
    }
    // Composite Performance and diagnostics (design §12, §13). Pure; never a simulation modifier.
    public static class IndustrialPerformanceModel
    {
        public const double ProductionWeight = 0.35, LogisticsWeight = 0.25, EmploymentWeight = 0.20, EfficiencyWeight = 0.20;
        public const double BuildingEfficiencyWeight = 0.70, ProfitabilityWeight = 0.30;
        public const int MaxBottlenecks = 3;
        // Diagnostic thresholds (tuning constants): a metric below its threshold is a candidate bottleneck.
        public const double WorkforceThreshold = 75, EfficiencyThreshold = 70, ProfitabilityThreshold = 40, FlowThreshold = 90, ActiveShareThreshold = 75;
        public static readonly string[] BottleneckKeys = { "WorkforceShortage", "LowEfficiency", "LowProfitability", "InputStarvation", "StorageSaturation", "InsufficientCargo", "LowActiveProducerShare" };
        public static IndustrialMetric ComputeEfficiency(IndustrialMetric buildingEfficiency, IndustrialMetric profitability)
        {
            var parts = new[] { IndustrialScoreMath.Of(buildingEfficiency, BuildingEfficiencyWeight), IndustrialScoreMath.Of(profitability, ProfitabilityWeight) };
            if (!IndustrialScoreMath.TryCombine(parts, 1, out var value, out var available, out var anyPartial))
                return buildingEfficiency.Status == IndustrialMetricStatus.NotApplicable && profitability.Status == IndustrialMetricStatus.NotApplicable
                    ? IndustrialMetric.NotApplicable() : IndustrialMetric.Unavailable();
            return available < parts.Length || anyPartial ? IndustrialMetric.Partial(value, available, parts.Length) : IndustrialMetric.Of(value, available, parts.Length);
        }
        // At least three of four components; one missing renormalizes the other original weights.
        public static IndustrialMetric ComputePerformance(IndustrialMetric production, IndustrialMetric logistics, IndustrialMetric employment, IndustrialMetric efficiency)
        {
            var parts = new[] { IndustrialScoreMath.Of(production, ProductionWeight), IndustrialScoreMath.Of(logistics, LogisticsWeight),
                IndustrialScoreMath.Of(employment, EmploymentWeight), IndustrialScoreMath.Of(efficiency, EfficiencyWeight) };
            if (!IndustrialScoreMath.TryCombine(parts, 3, out var value, out var available, out var anyPartial)) return IndustrialMetric.Unavailable(available, parts.Length);
            return available < parts.Length || anyPartial ? IndustrialMetric.Partial(value, available, parts.Length) : IndustrialMetric.Of(value, available, parts.Length);
        }
        public static IReadOnlyList<IndustrialBottleneck> FindBottlenecks(in IndustrialBottleneckInputs inputs)
        {
            var found = new List<IndustrialBottleneck>();
            void Add(string key, IndustrialMetric metric, double threshold, int affected, double hours)
            {
                if (!metric.HasValue) return;
                double value = IndustrialMetric.Clamp(metric.Value);
                if (value >= threshold) return;
                found.Add(new IndustrialBottleneck { Key = key, Severity = 1.0 - value / 100.0, AffectedFacilities = affected, ObservedHours = hours, EvidenceValue = value });
            }
            Add("WorkforceShortage", inputs.JobFill, WorkforceThreshold, inputs.UnderstaffedFacilities, inputs.SampleHours);
            Add("LowEfficiency", inputs.BuildingEfficiency, EfficiencyThreshold, inputs.LowEfficiencyFacilities, inputs.SampleHours);
            Add("LowProfitability", inputs.Profitability, ProfitabilityThreshold, inputs.LowProfitFacilities, inputs.SampleHours);
            Add("InputStarvation", inputs.InputContinuity, FlowThreshold, inputs.InputAffectedFacilities, inputs.InputObservedHours);
            Add("StorageSaturation", inputs.OutputContinuity, FlowThreshold, inputs.OutputAffectedFacilities, inputs.OutputObservedHours);
            // Insufficient cargo needs attributable evidence: an observed output symptom together with in-boundary cargo
            // facilities that are not operating. A missing terminal alone proves nothing (external routes may serve it).
            if (inputs.CargoFacilities > 0 && inputs.CargoNonOperational > 0)
                Add("InsufficientCargo", inputs.OutputContinuity, FlowThreshold, inputs.CargoNonOperational, inputs.OutputObservedHours);
            Add("LowActiveProducerShare", inputs.ActiveShare, ActiveShareThreshold, inputs.IdleProducers, inputs.SampleHours);
            // Stable order: severity, then the fixed key order.
            found.Sort((a, b) => a.Severity != b.Severity ? b.Severity.CompareTo(a.Severity) : System.Array.IndexOf(BottleneckKeys, a.Key).CompareTo(System.Array.IndexOf(BottleneckKeys, b.Key)));
            if (found.Count > MaxBottlenecks) found.RemoveRange(MaxBottlenecks, found.Count - MaxBottlenecks);
            return found;
        }
    }
}
