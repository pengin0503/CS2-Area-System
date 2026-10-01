using System;
namespace CS2AreaSystem.Core.Policies
{
    // Policy XP bonus applied by the module progression models to an award they already earned (design §10.1).
    public static class AreaPolicyExperience
    {
        public const double FocusedManagementMultiplier = 1.10;
        // Multiplies a positive base award, rounds half away from zero, then enforces the remaining cap. Invalid
        // multipliers fall back to the base award; nothing is ever granted beyond a non-negative remaining cap.
        public static long Apply(long baseAward, double multiplier, long remainingCap)
        {
            if (baseAward <= 0 || remainingCap <= 0) return 0;
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 0 || multiplier == 1.0)
                return Math.Min(baseAward, remainingCap);
            double scaled = Math.Round(baseAward * multiplier, MidpointRounding.AwayFromZero);
            long award = scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
            return Math.Min(award, remainingCap);
        }
    }
}
