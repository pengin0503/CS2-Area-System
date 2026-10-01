using CS2AreaSystem.Core.Persistence;
namespace CS2AreaSystem.Modules.Industrial
{
    // #69: Industrial's saved state after a load. Every field is a counter, a sum of hours or an hour bucket (NoHour = -1
    // before the first); none has a clamped reading, so an impossible value quarantines the Area.
    public static class IndustrialSavedStateValidator
    {
        public static void Validate(in IndustrialAreaState state, AreaSavedStateReport report)
        {
            report.RequireNonNegative("IndustrialAreaState.LifetimePeakEmployees", state.LifetimePeakEmployees);
            report.RequireNonNegative("IndustrialAreaState.XpGrantedToday", state.XpGrantedToday);
            report.RequireNonNegative("IndustrialAreaState.HighThroughputStreakDays", state.HighThroughputStreakDays);
            if (state.LastSettledXpHour < IndustrialAreaState.NoHour) report.Invalid("IndustrialAreaState.LastSettledXpHour", state.LastSettledXpHour);
            if (state.LastCreditedActivityHour < IndustrialAreaState.NoHour) report.Invalid("IndustrialAreaState.LastCreditedActivityHour", state.LastCreditedActivityHour);
            report.RequireFiniteNonNegative("IndustrialAreaState.ActivityWeightedSum", state.ActivityWeightedSum);
            report.RequireFiniteNonNegative("IndustrialAreaState.ActivityObservedHours", state.ActivityObservedHours);
        }
    }
}
