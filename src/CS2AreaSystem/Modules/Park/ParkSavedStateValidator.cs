using CS2AreaSystem.Core.Persistence;
namespace CS2AreaSystem.Modules.Park
{
    // #69: Park's saved state after a load. The admission fee is clamped by ParkFinanceModel.EffectiveFee on every read, so
    // an out-of-range fee only warns; counters and satisfaction aggregates have no safe reading and quarantine the Area.
    public static class ParkSavedStateValidator
    {
        public static void Validate(in ParkAreaState state, AreaSavedStateReport report)
        {
            if (state.AdmissionFee < 0 || state.AdmissionFee > ParkFinanceModel.MaxAdmissionFee) report.Warning("ParkAreaState.AdmissionFee", state.AdmissionFee);
            report.RequireNonNegative("ParkAreaState.LifetimeVisitors", state.LifetimeVisitors);
            report.RequireNonNegative("ParkAreaState.LifetimeResidentVisitors", state.LifetimeResidentVisitors);
            report.RequireNonNegative("ParkAreaState.LifetimeTouristVisitors", state.LifetimeTouristVisitors);
            report.RequireNonNegative("ParkAreaState.LifetimeAdmissionRevenue", state.LifetimeAdmissionRevenue);
            report.RequireNonNegative("ParkAreaState.LifetimeManagementExpense", state.LifetimeManagementExpense);
            report.RequireNonNegative("ParkAreaState.PassiveXpToday", state.PassiveXpToday);
            Satisfaction("ParkAreaState.Satisfaction", state.Satisfaction, report);
            Satisfaction("ParkAreaState.ResidentSatisfaction", state.ResidentSatisfaction, report);
            Satisfaction("ParkAreaState.TouristSatisfaction", state.TouristSatisfaction, report);
        }
        // Phase 2 C5: counters are non-negative; a started event's interval is exactly one event duration. An unknown
        // active id only warns: effects are neutral for it and it is cleared on the next event update.
        public static void ValidateEvents(in ParkEventState state, AreaSavedStateReport report)
        {
            report.RequireNonNegative("ParkEventState.LifetimeEvents", state.LifetimeEvents);
            report.RequireNonNegative("ParkEventState.LifetimeEventExpense", state.LifetimeEventExpense);
            bool started = state.ActiveEventId != 0 || state.LifetimeEvents > 0;
            if (started && unchecked(state.EndFrameExclusive - state.StartFrame) != ParkEventCatalog.DurationFrames)
                report.Invalid("ParkEventState.EndFrameExclusive", state.EndFrameExclusive);
            if (state.ActiveEventId != 0 && !ParkEventCatalog.IsKnown(state.ActiveEventId)) report.Warning("ParkEventState.ActiveEventId", state.ActiveEventId);
        }
        // ParkSatisfactionState.Add keeps Mean in [0, 100] and Weight in [0, MaxWeight].
        private static void Satisfaction(string field, ParkSatisfactionState value, AreaSavedStateReport report)
        {
            if (float.IsNaN(value.Mean) || value.Mean < 0f || value.Mean > 100f) report.Invalid(field + ".Mean", value.Mean);
            if (float.IsNaN(value.Weight) || value.Weight < 0f || value.Weight > ParkSatisfactionState.MaxWeight) report.Invalid(field + ".Weight", value.Weight);
        }
    }
}
