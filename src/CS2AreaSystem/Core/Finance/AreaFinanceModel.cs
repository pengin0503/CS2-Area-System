using System;
namespace CS2AreaSystem.Core.Finance
{
    // Simulation-time settlement (design §14). No wall clock: amounts derive only from simulation frames.
    public static class AreaFinanceModel
    {
        // One in-game day in simulation frames (TimeSystem/BudgetApplySystem use 262144 in 1.6.2f1).
        public const uint FramesPerDay = 262144;
        // Rounding rule: floor, with the fraction carried into the next settlement. The epsilon absorbs binary
        // representation error so an exact daily total is not lost to 0.99999999 remainders.
        private const double FloorEpsilon = 1e-9;
        public static long SettleDaily(double perDay, uint frames, ref double carry)
        {
            // The carry is derived state in [0, 1); anything else is corrupt and is never turned into money.
            if (double.IsNaN(carry) || carry < 0 || carry >= 1) carry = 0;
            if (double.IsNaN(perDay) || double.IsInfinity(perDay) || perDay < 0 || frames == 0) return 0;
            double exact = perDay * ((double)frames / FramesPerDay) + carry;
            if (double.IsInfinity(exact) || exact >= long.MaxValue) { carry = 0; return long.MaxValue; }
            long amount = (long)Math.Floor(exact + FloorEpsilon);
            carry = Math.Max(0, exact - amount);
            return amount;
        }
        // Frames to settle since the previous settlement, capped so a time jump never back-charges; a frame counter that
        // moved backwards (a load) settles nothing.
        public static uint ElapsedFrames(uint lastFrame, uint frame, uint maxFrames) =>
            frame <= lastFrame ? 0u : Math.Min(maxFrames, frame - lastFrame);
        public static long SaturatingAdd(long a, long b)
        {
            if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
            if (b < 0 && a < long.MinValue - b) return long.MinValue;
            return a + b;
        }
    }
}
