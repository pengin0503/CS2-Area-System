namespace CS2AreaSystem.Core.Finance
{
    // What an Area amount is. The kind fixes both the direction and the budget group (design §15):
    // expenses belong to Service Upkeep → Area Management, income to Service Fees → Area Service Fees.
    public enum AreaFinanceKind : byte
    {
        PolicyUpkeep,
        ParkManagement,
        ParkAdmission,
    }
    public static class AreaFinanceKinds
    {
        public static bool IsExpense(AreaFinanceKind kind) => kind == AreaFinanceKind.PolicyUpkeep || kind == AreaFinanceKind.ParkManagement;
        public static bool IsIncome(AreaFinanceKind kind) => kind == AreaFinanceKind.ParkAdmission;
    }
}
