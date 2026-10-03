using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
namespace CS2AreaSystem.Modules.Industrial
{
    // What one settled support request gives each snapshot receiver (same order as the snapshot) and what nobody gets.
    public readonly struct IndustrialSupportAllocation
    {
        public readonly long[] Amounts;
        public readonly long Unallocated;
        public IndustrialSupportAllocation(long[] amounts, long unallocated) { Amounts = amounts; Unallocated = unallocated; }
    }
    // Pure Industrial support rules (Phase 2 C4). Requested amounts never create company money: only the amount Area
    // Finance actually settled is split, evenly, over the still-valid snapshot receivers in ascending Entity order.
    public static class IndustrialSupportModel
    {
        public const int MinPerCompany = 0, MaxPerCompany = 5000, Step = 250, DefaultPerCompany = 0;
        public static int NormalizeSetting(int value)
        {
            int clamped = Math.Max(MinPerCompany, Math.Min(MaxPerCompany, value));
            return (int)Math.Round(clamped / (double)Step, MidpointRounding.AwayFromZero) * Step;
        }
        public static long DailyRequest(int receivers, int perCompany) =>
            receivers <= 0 || perCompany <= 0 ? 0 : (long)receivers * perCompany;
        // An uninitialized component (schema 0) never counts as scheduled, so day zero is never mistaken for a paid day.
        public static bool AlreadyScheduled(in IndustrialSupportState state, long day) =>
            state.SchemaVersion != 0 && state.LastScheduledDay != IndustrialSupportState.NoDay && state.LastScheduledDay == day;
        public static IndustrialSupportState MarkScheduled(IndustrialSupportState state, long day)
        {
            if (state.SchemaVersion == 0) { int setting = state.DailySupportPerCompany; state = IndustrialSupportState.CreateDefault(); state.DailySupportPerCompany = NormalizeSetting(setting); }
            state.LastScheduledDay = day;
            return state;
        }
        // Equal split with integer remainders to earlier valid receivers, each capped at the accepted per-company amount.
        // Removed/ineligible receivers get nothing and are never replaced; whatever cannot be given is unallocated.
        public static IndustrialSupportAllocation Allocate(long settled, int perCompanyCap, IReadOnlyList<bool> valid)
        {
            int count = valid?.Count ?? 0;
            var amounts = new long[count];
            settled = Math.Max(0, settled);
            int receivers = 0;
            for (int i = 0; i < count; i++) if (valid[i]) receivers++;
            if (receivers == 0 || perCompanyCap <= 0) return new IndustrialSupportAllocation(amounts, settled);
            long share = settled / receivers, remainder = settled % receivers, given = 0;
            for (int i = 0, rank = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                long amount = Math.Min(perCompanyCap, share + (rank++ < remainder ? 1 : 0));
                amounts[i] = amount;
                given += amount;
            }
            return new IndustrialSupportAllocation(amounts, settled - given);
        }
        // Lifetime accounting for one completed request (also zero/aborted ones). Credit is never above what was settled.
        public static IndustrialSupportState RecordCompletion(IndustrialSupportState state, long requested, long settled, long credited)
        {
            settled = Math.Max(0, settled);
            credited = Math.Max(0, Math.Min(settled, credited));
            state.LifetimeRequested = AreaFinanceModel.SaturatingAdd(state.LifetimeRequested, Math.Max(0, requested));
            state.LifetimeSettled = AreaFinanceModel.SaturatingAdd(state.LifetimeSettled, settled);
            state.LifetimeCredited = AreaFinanceModel.SaturatingAdd(state.LifetimeCredited, credited);
            state.LifetimeUnallocated = AreaFinanceModel.SaturatingAdd(state.LifetimeUnallocated, settled - credited);
            return state;
        }
    }
}
