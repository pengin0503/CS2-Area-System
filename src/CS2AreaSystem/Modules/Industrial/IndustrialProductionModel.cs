using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Industrial
{
    // Shared weighted combination of availability-aware metrics. Only Available/Partial values enter; the weights of
    // the available parts are renormalized (design §12.1).
    public static class IndustrialScoreMath
    {
        public struct Part { public IndustrialMetric Metric; public double Weight; }
        public static Part Of(IndustrialMetric metric, double weight) => new Part { Metric = metric, Weight = weight };
        public static bool TryCombine(IReadOnlyList<Part> parts, int minimumAvailable, out double value, out int available, out bool anyPartial)
        {
            double sum = 0, weights = 0; available = 0; anyPartial = false; value = 0;
            foreach (var part in parts)
            {
                if (!part.Metric.HasValue) continue;
                available++;
                anyPartial |= part.Metric.Status == IndustrialMetricStatus.Partial;
                sum += IndustrialMetric.Clamp(part.Metric.Value) * part.Weight; weights += part.Weight;
            }
            if (available < minimumAvailable || !(weights > 0)) return false;
            value = IndustrialMetric.Clamp(sum / weights);
            return true;
        }
    }
    public struct IndustrialWeightedValue
    {
        public double? Value;
        public int? Jobs;
    }
    // Per-company/per-facility weighting w = clamp(validJobs, 1, 100); unknown or invalid jobs use weight 1 and
    // mark the result Partial (design §12.1).
    public static class IndustrialWeighting
    {
        public const int MaxWeight = 100;
        public static bool KnownJobs(int? jobs) => jobs.HasValue && jobs.Value >= 0;
        public static double Weight(int? jobs) => KnownJobs(jobs) ? Math.Min(MaxWeight, Math.Max(1, jobs.Value)) : 1;
        public static IndustrialMetric Mean(IReadOnlyList<IndustrialWeightedValue> items)
        {
            if (items == null || items.Count == 0) return IndustrialMetric.NotApplicable();
            double sum = 0, weights = 0; int observed = 0; bool unknownJobs = false;
            foreach (var item in items)
            {
                if (!item.Value.HasValue || double.IsNaN(item.Value.Value) || double.IsInfinity(item.Value.Value)) continue;
                observed++;
                if (!KnownJobs(item.Jobs)) unknownJobs = true;
                double w = Weight(item.Jobs);
                sum += IndustrialMetric.Clamp(item.Value.Value) * w; weights += w;
            }
            if (observed == 0) return IndustrialMetric.Unavailable(0, items.Count);
            var metric = IndustrialMetric.Of(sum / weights, observed, items.Count);
            return unknownJobs ? IndustrialMetric.Partial(metric.Value, observed, items.Count) : metric;
        }
    }
    public struct IndustrialProductionInputs
    {
        // Validated productive activity (job-weighted, clamped 0..100) of producers with an attainable reference.
        public IndustrialMetric Activity;
        public int KnownProducers, OperatingProducers;
        // Activity per output resource (capacity-normalized, comparable); null when outputs are unknown.
        public IReadOnlyList<double> OutputActivity;
    }
    // Production = 65% activity + 25% active-producer share + 10% output diversity (design §9.2, §12.1).
    public static class IndustrialProductionModel
    {
        public const double ActivityWeight = 0.65, ActiveShareWeight = 0.25, DiversityWeight = 0.10;
        public const double MeaningfulShareOfDominant = 0.20;
        public static IndustrialMetric ActiveShare(int knownProducers, int operatingProducers)
        {
            if (knownProducers < 0 || operatingProducers < 0 || operatingProducers > knownProducers) return IndustrialMetric.Unavailable();
            if (knownProducers == 0) return IndustrialMetric.NotApplicable();
            return IndustrialMetric.Of(100.0 * operatingProducers / knownProducers, knownProducers, knownProducers);
        }
        // Outputs whose activity is at least 20% of the dominant output (and positive).
        public static int MeaningfulOutputs(IReadOnlyList<double> outputActivity)
        {
            if (outputActivity == null) return 0;
            double dominant = 0;
            foreach (var v in outputActivity) if (!double.IsNaN(v) && !double.IsInfinity(v) && v > dominant) dominant = v;
            if (!(dominant > 0)) return 0;
            int count = 0;
            foreach (var v in outputActivity) if (!double.IsNaN(v) && !double.IsInfinity(v) && v > 0 && v >= MeaningfulShareOfDominant * dominant - 1e-12) count++;
            return count;
        }
        public static double DiversityScore(int meaningfulOutputs) => meaningfulOutputs <= 0 ? 0 : meaningfulOutputs == 1 ? 50 : meaningfulOutputs == 2 ? 75 : 100;
        public static IndustrialMetric Diversity(IndustrialMetric activity, IReadOnlyList<double> outputActivity)
        {
            if (!activity.HasValue || outputActivity == null) return IndustrialMetric.Unavailable();
            if (!(activity.Value > 0)) return IndustrialMetric.Of(0);
            return IndustrialMetric.Of(DiversityScore(MeaningfulOutputs(outputActivity)));
        }
        public static IndustrialMetric Compute(in IndustrialProductionInputs inputs)
        {
            if (inputs.KnownProducers < 0) return IndustrialMetric.Unavailable();
            if (inputs.KnownProducers == 0) return IndustrialMetric.NotApplicable();
            // Validated activity is required: Production is never published from counts or diversity alone.
            if (!inputs.Activity.HasValue) return IndustrialMetric.Unavailable(inputs.Activity.Observed, inputs.Activity.Eligible);
            var share = ActiveShare(inputs.KnownProducers, inputs.OperatingProducers);
            var diversity = Diversity(inputs.Activity, inputs.OutputActivity);
            var parts = new[] { IndustrialScoreMath.Of(inputs.Activity, ActivityWeight), IndustrialScoreMath.Of(share, ActiveShareWeight), IndustrialScoreMath.Of(diversity, DiversityWeight) };
            IndustrialScoreMath.TryCombine(parts, 1, out var value, out var available, out var anyPartial);
            return available < parts.Length || anyPartial
                ? IndustrialMetric.Partial(value, available, parts.Length)
                : IndustrialMetric.Of(value, inputs.Activity.Observed, inputs.Activity.Eligible);
        }
    }
}
