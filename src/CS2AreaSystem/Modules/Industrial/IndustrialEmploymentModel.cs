using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Industrial
{
    // Jobs/employees of one eligible facility, counted once even when it holds several roles or tenants.
    public struct IndustrialJobFacility
    {
        public int Jobs, Employees;
    }
    public struct IndustrialEmploymentInputs
    {
        public IReadOnlyList<IndustrialJobFacility> Facilities;
    }
    // Employment = 75% aggregate job fill + 25% staffing balance (mean per-facility fill of facilities with jobs).
    // A fill measure, not a headcount: empty capacity cannot raise the score (design §11, §12.1).
    public static class IndustrialEmploymentModel
    {
        public const double FillWeight = 0.75, BalanceWeight = 0.25;
        private static bool Valid(IndustrialJobFacility f) => f.Jobs >= 0 && f.Employees >= 0;
        public static IndustrialMetric JobFill(IReadOnlyList<IndustrialJobFacility> facilities)
        {
            if (facilities == null || facilities.Count == 0) return IndustrialMetric.NotApplicable();
            long jobs = 0, employees = 0; int valid = 0;
            foreach (var f in facilities)
            {
                if (!Valid(f)) continue;
                valid++;
                if (f.Jobs == 0) continue;
                jobs += f.Jobs; employees += System.Math.Min(f.Employees, f.Jobs);
            }
            if (valid == 0) return IndustrialMetric.Unavailable(0, facilities.Count);
            if (jobs == 0) return IndustrialMetric.NotApplicable();
            return IndustrialMetric.Of(100.0 * employees / jobs, valid, facilities.Count);
        }
        public static IndustrialMetric StaffingBalance(IReadOnlyList<IndustrialJobFacility> facilities)
        {
            if (facilities == null || facilities.Count == 0) return IndustrialMetric.NotApplicable();
            double sum = 0; int counted = 0, valid = 0;
            foreach (var f in facilities)
            {
                if (!Valid(f)) continue;
                valid++;
                if (f.Jobs == 0) continue;
                sum += IndustrialMetric.Clamp(100.0 * f.Employees / f.Jobs); counted++;
            }
            if (valid == 0) return IndustrialMetric.Unavailable(0, facilities.Count);
            if (counted == 0) return IndustrialMetric.NotApplicable();
            return IndustrialMetric.Of(sum / counted, valid, facilities.Count);
        }
        public static IndustrialMetric Compute(in IndustrialEmploymentInputs inputs)
        {
            var fill = JobFill(inputs.Facilities);
            if (!fill.HasValue) return fill;
            var balance = StaffingBalance(inputs.Facilities);
            var parts = new[] { IndustrialScoreMath.Of(fill, FillWeight), IndustrialScoreMath.Of(balance, BalanceWeight) };
            IndustrialScoreMath.TryCombine(parts, 1, out var value, out var available, out var anyPartial);
            return available < parts.Length || anyPartial ? IndustrialMetric.Partial(value, fill.Observed, fill.Eligible) : IndustrialMetric.Of(value, fill.Observed, fill.Eligible);
        }
    }
}
