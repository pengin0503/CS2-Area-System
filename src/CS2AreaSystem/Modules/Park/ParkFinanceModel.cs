using System;
using System.Globalization;
using CS2AreaSystem.Core.Finance;
namespace CS2AreaSystem.Modules.Park
{
    // Management need derives from the Park itself. Vanilla member-building upkeep/revenue are not inputs:
    // the base game already charges them, so Park never books them again (design §14.2, §15.5).
    public struct ParkManagementInputs
    {
        public float AreaSquareMeters, VisitorsPerDay;
        public int LandscapeObjects, ManagedFacilities;
    }
    // The household-payable plan. The amount actually collected is what AreaFinanceHouseholdTransfer returns; it can be
    // lower only when the city balance is at its native upper bound.
    public struct ParkAdmissionPlan
    {
        public long Requested, Collected;
        public long Uncollected => Requested - Collected;
        public long HouseholdDelta => -Collected;
        public long CityDelta => Collected;
    }
    public static class ParkFinanceModel
    {
        public const uint FramesPerDay = AreaFinanceModel.FramesPerDay;
        public const int MaxAdmissionFee = 60, AdmissionFeeStep = 1, AdmissionUnlockLevel = 2;
        public const float MinBudget = 0.5f, MaxBudget = 1.5f, DefaultBudget = 1f, BudgetStep = 0.05f;
        public static bool IsAdmissionUnlocked(int level) => level >= AdmissionUnlockLevel;
        public static int ClampFee(int level, int fee) => IsAdmissionUnlocked(level) ? Math.Min(MaxAdmissionFee, Math.Max(0, fee)) : 0;
        public static int EffectiveFee(int level, int storedFee) => ClampFee(level, storedFee);
        public static bool TryParseFee(int level, string value, out int fee)
        {
            fee = 0;
            if (!IsAdmissionUnlocked(level) || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                double.IsNaN(parsed) || double.IsInfinity(parsed)) return false;
            fee = ClampFee(level, (int)Math.Round(Math.Min(int.MaxValue, Math.Max(int.MinValue, parsed))));
            return true;
        }
        // Collect at most what the household can pay; never drive its money below zero (design §15.3).
        public static long ComputeCollectibleAdmission(long requestedFee, long availableHouseholdMoney) =>
            requestedFee <= 0 || availableHouseholdMoney <= 0 ? 0 : Math.Min(requestedFee, availableHouseholdMoney);
        // One admission per session: Level-gated fee, collected from real household money into city money.
        public static ParkAdmissionPlan PlanAdmission(int level, int storedFee, long householdMoney)
        {
            long fee = EffectiveFee(level, storedFee);
            return new ParkAdmissionPlan { Requested = fee, Collected = ComputeCollectibleAdmission(fee, householdMoney) };
        }
        // Frames to settle since the previous settlement, capped so a time jump or load never back-charges.
        public static uint ElapsedForSettlement(uint lastFrame, uint frame, uint maxFrames) =>
            frame <= lastFrame ? 0u : Math.Min(maxFrames, frame - lastFrame);
        // Area management is an operating cost of Active Parks only.
        public static bool ShouldSettleManagement(CS2AreaSystem.Core.Areas.AreaLifecycleState state) =>
            state == CS2AreaSystem.Core.Areas.AreaLifecycleState.Active;
        // Free parks have no price penalty; a high-quality Park tolerates a higher fee (design §9.6). fairFeeMultiplier scales
        // the fee visitors consider fair (Premium Services); 1.0, or any invalid value, is the unchanged Area behaviour.
        public static float ComputeValueForMoney(int fee, float quality, double fairFeeMultiplier)
        {
            if (fee <= 0) return 100f;
            double q = Math.Min(100, Math.Max(0, float.IsNaN(quality) ? 0 : quality)) / 100.0;
            double fair = (4 + 36 * Math.Pow(q, 1.5)) * ValidMultiplier(fairFeeMultiplier);
            double value = fee <= fair ? 100 - 25 * fee / fair : 75 - 75 * (fee - fair) / (1.5 * fair);
            return (float)Math.Min(100, Math.Max(0, value));
        }
        public static float ClampBudget(float budget) =>
            float.IsNaN(budget) || budget <= 0 ? DefaultBudget : Math.Min(MaxBudget, Math.Max(MinBudget, budget));
        // UI sliders send percent (50–150).
        public static bool TryParseBudgetPercent(string value, out float budget)
        {
            budget = DefaultBudget;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || double.IsNaN(percent) || double.IsInfinity(percent)) return false;
            double snapped = Math.Round(percent / (BudgetStep * 100)) * BudgetStep;
            budget = ClampBudget((float)Math.Max(MinBudget, snapped));
            return true;
        }
        public static float ComputeManagementNeedPerDay(in ParkManagementInputs inputs) =>
            (float)(150 + 350 * Math.Pow(Math.Max(0, inputs.AreaSquareMeters) / 10_000.0, 0.75) + 1.5 * Math.Max(0, inputs.VisitorsPerDay) +
                    8 * Math.Sqrt(Math.Max(0, inputs.LandscapeObjects)) + 40 * Math.Max(0, inputs.ManagedFacilities));
        // Management coverage is funding relative to need: the clamped budget, times the policy funding multiplier
        // (Enhanced Maintenance). 1.0, or any invalid value, is the unchanged Area behaviour.
        public static float ComputeManagementCoverage(float budget, double fundingMultiplier) =>
            (float)(ClampBudget(budget) * ValidMultiplier(fundingMultiplier));
        // 100% coverage funds the estimated need exactly, so every point of coverage is paid for.
        public static float ComputeManagementCostPerDay(float needPerDay, float budget, double fundingMultiplier) =>
            Math.Max(0, needPerDay) * ComputeManagementCoverage(budget, fundingMultiplier);
        private static double ValidMultiplier(double multiplier) =>
            double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0 ? 1.0 : multiplier;
        // Money for an interval of frames, with the shared Area rounding rule (floor, fraction carried).
        public static long Settle(double perDay, uint frames, ref double carry) => AreaFinanceModel.SettleDaily(perDay, frames, ref carry);
        public static ParkAreaState RecordAdmission(ParkAreaState state, long collected)
        { if (collected > 0) state.LifetimeAdmissionRevenue += collected; return state; }
        public static ParkAreaState RecordManagementExpense(ParkAreaState state, long paid)
        { if (paid > 0) state.LifetimeManagementExpense += paid; return state; }
    }
}
