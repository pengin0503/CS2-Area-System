namespace CS2AreaSystem.Modules.Industrial
{
    // One flow channel over the rolling six-hour window. Only validated symptoms enter; storage/cargo presence has
    // no term here by design (design §10).
    public struct IndustrialChannelInputs
    {
        // At least one producer for which the channel exists (e.g. a process with an imported input).
        public bool Applicable;
        // At least one applicable producer whose state was observable.
        public bool Supported;
        // Σ weight·affected·Δt and Σ weight·valid·Δt over covered intervals, and covered game hours.
        public double AffectedWeightedTime, ValidWeightedTime, CoveredHours;
        // Some applicable producers were not observable (partial-data label).
        public bool AnyUnavailable;
    }
    public struct IndustrialLogisticsInputs
    {
        public IndustrialChannelInputs Input, Output;
    }
    // Logistics = 50% input continuity + 50% output/dispatch continuity.
    public static class IndustrialLogisticsModel
    {
        public const double InputWeight = 0.5, OutputWeight = 0.5;
        public const double RequiredHours = IndustrialFlowWindow.RequiredHours;
        public static IndustrialMetric Channel(in IndustrialChannelInputs channel)
        {
            if (!channel.Applicable) return IndustrialMetric.NotApplicable();
            if (!channel.Supported) return IndustrialMetric.Unavailable();
            // No observation is not evidence of health: a channel needs its own four covered hours.
            if (!(channel.CoveredHours >= RequiredHours - 1e-9)) return IndustrialMetric.WarmingUp(channel.CoveredHours, RequiredHours);
            if (!(channel.ValidWeightedTime > 0) || double.IsInfinity(channel.ValidWeightedTime) || double.IsNaN(channel.AffectedWeightedTime) || channel.AffectedWeightedTime < 0)
                return IndustrialMetric.Unavailable(channel.CoveredHours, RequiredHours);
            double share = channel.AffectedWeightedTime / channel.ValidWeightedTime;
            double value = 100.0 * (1.0 - (share > 1 ? 1 : share));
            return channel.AnyUnavailable ? IndustrialMetric.Partial(value, channel.CoveredHours, RequiredHours) : IndustrialMetric.Of(value, channel.CoveredHours, RequiredHours);
        }
        public static IndustrialMetric Compute(in IndustrialLogisticsInputs inputs) => Combine(Channel(inputs.Input), Channel(inputs.Output));
        public static IndustrialMetric Combine(IndustrialMetric input, IndustrialMetric output)
        {
            var parts = new[] { IndustrialScoreMath.Of(input, InputWeight), IndustrialScoreMath.Of(output, OutputWeight) };
            if (!IndustrialScoreMath.TryCombine(parts, 1, out var value, out var available, out var anyPartial))
            {
                if (input.Status == IndustrialMetricStatus.WarmingUp || output.Status == IndustrialMetricStatus.WarmingUp)
                    return IndustrialMetric.WarmingUp(System.Math.Max(input.Observed, output.Observed), RequiredHours);
                if (input.Status == IndustrialMetricStatus.NotApplicable && output.Status == IndustrialMetricStatus.NotApplicable) return IndustrialMetric.NotApplicable();
                return IndustrialMetric.Unavailable();
            }
            // A NotApplicable channel is simply excluded; a missing applicable channel leaves a visible partial label.
            bool missingApplicable = (!input.HasValue && input.Status != IndustrialMetricStatus.NotApplicable) || (!output.HasValue && output.Status != IndustrialMetricStatus.NotApplicable);
            return missingApplicable || anyPartial ? IndustrialMetric.Partial(value, available, 2) : IndustrialMetric.Of(value, available, available);
        }
    }
}
