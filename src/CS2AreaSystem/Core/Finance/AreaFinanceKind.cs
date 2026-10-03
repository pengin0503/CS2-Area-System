namespace CS2AreaSystem.Core.Finance
{
    // What an Area amount is. The kind fixes both the direction and the budget group (design §15):
    // expenses belong to Service Upkeep → Area Management, income to Service Fees → Area Service Fees.
    // Numeric values are fixed. 4 is reserved for the C5 Park event expense.
    public enum AreaFinanceKind : byte
    {
        PolicyUpkeep = 0,
        ParkManagement = 1,
        ParkAdmission = 2,
        // Daily per-company Industrial support (Phase 2 C4): an Area Management expense.
        IndustrialSupport = 3,
    }
    public static class AreaFinanceKinds
    {
        public static bool IsExpense(AreaFinanceKind kind) => kind == AreaFinanceKind.PolicyUpkeep || kind == AreaFinanceKind.ParkManagement || kind == AreaFinanceKind.IndustrialSupport;
        public static bool IsIncome(AreaFinanceKind kind) => kind == AreaFinanceKind.ParkAdmission;
    }
}
