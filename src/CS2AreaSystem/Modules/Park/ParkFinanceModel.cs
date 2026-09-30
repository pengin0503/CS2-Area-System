using System;
using System.Globalization;
namespace CS2AreaSystem.Modules.Park
{
    // Management need derives from the Park itself. Vanilla member-building upkeep/revenue are not inputs:
    // the base game already charges them, so Park never books them again (design §14.2, §15.5).
    public struct ParkManagementInputs
    {
        public float AreaSquareMeters, VisitorsPerDay;
        public int LandscapeObjects, ManagedFacilities;
    }
    public static class ParkFinanceModel
    {
        public const uint FramesPerDay = 262144;
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
        // Free parks have no price penalty; a high-quality Park tolerates a higher fee (design §9.6).
        public static float ComputeValueForMoney(int fee, float quality)
        {
            if (fee <= 0) return 100f;
            double q = Math.Min(100, Math.Max(0, float.IsNaN(quality) ? 0 : quality)) / 100.0;
            double fair = 4 + 36 * Math.Pow(q, 1.5);
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
        // 100% budget funds the estimated need; coverage for Maintenance is the clamped budget itself.
        public static float ComputeManagementCostPerDay(float needPerDay, float budget) => Math.Max(0, needPerDay) * ClampBudget(budget);
        // Money for an interval of frames; fractional remainders carry into the next settlement.
        public static long Settle(double perDay, uint frames, ref double carry)
        {
            double exact = Math.Max(0, perDay) * frames / FramesPerDay + carry;
            long amount = (long)Math.Floor(exact + 1e-9);
            carry = Math.Max(0, exact - amount);
            return amount;
        }
        public static ParkAreaState RecordAdmission(ParkAreaState state, long collected)
        { if (collected > 0) state.LifetimeAdmissionRevenue += collected; return state; }
        public static ParkAreaState RecordManagementExpense(ParkAreaState state, long paid)
        { if (paid > 0) state.LifetimeManagementExpense += paid; return state; }
    }
}
