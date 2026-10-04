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
        // Phase 2 C4: lifetime money counters and the scheduled-day dedupe. The setting itself is normalized on load.
        public static void ValidateSupport(in IndustrialSupportState state, AreaSavedStateReport report)
        {
            if (state.LastScheduledDay < IndustrialSupportState.NoDay) report.Invalid("IndustrialSupportState.LastScheduledDay", state.LastScheduledDay);
            report.RequireNonNegative("IndustrialSupportState.LifetimeRequested", state.LifetimeRequested);
            report.RequireNonNegative("IndustrialSupportState.LifetimeSettled", state.LifetimeSettled);
            report.RequireNonNegative("IndustrialSupportState.LifetimeCredited", state.LifetimeCredited);
            report.RequireNonNegative("IndustrialSupportState.LifetimeUnallocated", state.LifetimeUnallocated);
            bool saturated = state.LifetimeSettled == long.MaxValue || state.LifetimeCredited == long.MaxValue || state.LifetimeUnallocated == long.MaxValue;
            if (!saturated && state.LifetimeCredited >= 0 && state.LifetimeUnallocated >= 0 &&
                state.LifetimeSettled != Core.Finance.AreaFinanceModel.SaturatingAdd(state.LifetimeCredited, state.LifetimeUnallocated))
                report.Invalid("IndustrialSupportState.LifetimeSettled", state.LifetimeSettled);
            // Area Finance never settles more than was requested.
            if (state.LifetimeRequested != long.MaxValue && state.LifetimeSettled != long.MaxValue && state.LifetimeSettled > state.LifetimeRequested)
                report.Invalid("IndustrialSupportState.LifetimeSettled", state.LifetimeSettled);
        }
        // Phase 3 D1: contracts are advisory configuration, so an impossible entry only warns. It stays visible as Invalid
        // and removable instead of quarantining the Area's authoritative progress and finance.
        public static void ValidateContracts(System.Collections.Generic.IReadOnlyList<IndustrialSupplyContract> contracts, in IndustrialSupplyContractState state,
            ulong source, AreaSavedStateReport report)
        {
            if (contracts.Count > IndustrialSupplyContractModel.MaxContracts) report.Warning("IndustrialSupplyContract.Count", contracts.Count);
            var ids = new System.Collections.Generic.HashSet<int>();
            int maxId = 0;
            foreach (var c in contracts)
            {
                if (c.ContractId <= 0 || !ids.Add(c.ContractId)) report.Warning("IndustrialSupplyContract.ContractId", c.ContractId);
                if (c.ContractId > maxId) maxId = c.ContractId;
                if (!IndustrialSupplyContractModel.IsValidResource(c.Resource)) report.Warning("IndustrialSupplyContract.Resource", c.Resource);
                if (!IndustrialSupplyContractModel.IsValidDestination(source, c.DestinationStableId)) report.Warning("IndustrialSupplyContract.DestinationStableId", c.DestinationStableId);
            }
            if (state.NextContractId <= maxId) report.Warning("IndustrialSupplyContractState.NextContractId", state.NextContractId);
        }
    }
}
