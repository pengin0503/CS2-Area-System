using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
namespace CS2AreaSystem.Modules.Park
{
    // Park-internal event effects as pure model inputs (no post-hoc Quality rewrite). Neutral is the identity.
    public readonly struct ParkEventEffects : IEquatable<ParkEventEffects>
    {
        public static readonly ParkEventEffects Neutral = new ParkEventEffects(1f, 0f, 1.0);
        public readonly float CapacityShare, SatisfactionBonus;
        public readonly double VisitXpMultiplier;
        public ParkEventEffects(float capacityShare, float satisfactionBonus, double visitXpMultiplier)
        { CapacityShare = capacityShare; SatisfactionBonus = satisfactionBonus; VisitXpMultiplier = visitXpMultiplier; }
        public bool IsNeutral => Equals(Neutral);
        // Known completed-visit samples only, clamped 0..100; an unknown sample stays unknown.
        public float AdjustSatisfaction(float sample, bool hasSatisfaction)
        {
            if (!hasSatisfaction || float.IsNaN(sample) || SatisfactionBonus == 0f) return sample;
            float clamped = sample < 0f ? 0f : sample > 100f ? 100f : sample;
            float adjusted = clamped + SatisfactionBonus;
            return adjusted < 0f ? 0f : adjusted > 100f ? 100f : adjusted;
        }
        public float ComposeCapacityShare(float policyShare) => policyShare * CapacityShare;
        public double ComposeXpMultiplier(double policyMultiplier) => policyMultiplier * VisitXpMultiplier;
        public bool Equals(ParkEventEffects other) => CapacityShare == other.CapacityShare && SatisfactionBonus == other.SatisfactionBonus && VisitXpMultiplier == other.VisitXpMultiplier;
        public override bool Equals(object obj) => obj is ParkEventEffects other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CapacityShare, SatisfactionBonus, VisitXpMultiplier);
    }
    public static class ParkEventModel
    {
        public const int HistoryCapacity = 16;
        // max(1, round-to-even(50% of the daily management cost)); unavailable/invalid estimates disable the start.
        public static bool TryCost(double dailyManagementCost, out long cost)
        {
            cost = 0;
            if (double.IsNaN(dailyManagementCost) || double.IsInfinity(dailyManagementCost) || dailyManagementCost < 0) return false;
            double half = Math.Round(dailyManagementCost * ParkEventCatalog.CostShareOfDailyManagement, MidpointRounding.ToEven);
            cost = half >= long.MaxValue ? long.MaxValue : Math.Max(1L, (long)half);
            return true;
        }
        // Unsigned elapsed-frame arithmetic: correct across the frame-counter wrap; a frame before the start is outside.
        public static bool IsActive(in ParkEventState state, uint frame) =>
            ParkEventCatalog.IsKnown(state.ActiveEventId) && unchecked(frame - state.StartFrame) < unchecked(state.EndFrameExclusive - state.StartFrame);
        // Effects are decided on read: an ended event, an unknown id or a non-Active Park is neutral.
        public static ParkEventEffects Effects(in ParkEventState state, uint frame, bool parkActive)
        {
            if (!parkActive || !IsActive(state, frame)) return ParkEventEffects.Neutral;
            return new ParkEventEffects(ParkEventCatalog.FestivalCapacityShare, ParkEventCatalog.FestivalSatisfactionBonus, ParkEventCatalog.FestivalVisitXpMultiplier);
        }
        // History retains ended intervals after Expire clears ActiveEventId, or a later Festival replaces the current state.
        public static ParkEventEffects HistoricalEffects(in ParkEventHistoryEntry entry, uint frame, bool parkActive) =>
            Effects(new ParkEventState { ActiveEventId = entry.EventId, StartFrame = entry.StartFrame, EndFrameExclusive = entry.EndFrameExclusive }, frame, parkActive);
        // Quality caches the event effects it was evaluated with; a different current reading (start, read-time expiry,
        // suspension or reactivation) means the cached derived values must be recomputed, never kept.
        public static bool QualityNeedsRefresh(ParkRuntimeSnapshot snapshot, in ParkEventState state, uint frame, bool parkActive) =>
            snapshot != null && !snapshot.QualityEventEffects.Equals(Effects(state, frame, parkActive));
        public static ParkEventState Start(ParkEventState state, int eventId, uint frame, long paid)
        {
            if (state.SchemaVersion == 0) state.SchemaVersion = ParkEventState.CurrentSchemaVersion;
            state.ActiveEventId = eventId;
            state.StartFrame = frame;
            state.EndFrameExclusive = unchecked(frame + ParkEventCatalog.DurationFrames);
            state.LifetimeEvents = state.LifetimeEvents == long.MaxValue ? long.MaxValue : state.LifetimeEvents + 1;
            state.LifetimeEventExpense = AreaFinanceModel.SaturatingAdd(state.LifetimeEventExpense, Math.Max(0, paid));
            return state;
        }
        // A positive settlement whose Park lost eligibility before it arrived: the money is accounted, nothing starts.
        public static ParkEventState RecordUnstartedExpense(ParkEventState state, long paid)
        {
            if (state.SchemaVersion == 0) state.SchemaVersion = ParkEventState.CurrentSchemaVersion;
            state.LifetimeEventExpense = AreaFinanceModel.SaturatingAdd(state.LifetimeEventExpense, Math.Max(0, paid));
            return state;
        }
        // Clears an ended or unknown active id (keeping the last interval and counters); an active event is unchanged.
        public static ParkEventState Expire(ParkEventState state, uint frame)
        {
            if (state.ActiveEventId != 0 && !IsActive(state, frame)) state.ActiveEventId = 0;
            return state;
        }
        public static ParkEventHistoryEntry HistoryEntry(in ParkEventState started, long settled) => new ParkEventHistoryEntry {
            SchemaVersion = ParkEventHistoryEntry.CurrentSchemaVersion, EventId = started.ActiveEventId, StartFrame = started.StartFrame,
            EndFrameExclusive = started.EndFrameExclusive, SettledExpense = Math.Max(0, settled) };
        // Oldest first; keeps the newest HistoryCapacity entries.
        public static void AppendHistory(IList<ParkEventHistoryEntry> history, ParkEventHistoryEntry entry)
        {
            history.Add(entry);
            while (history.Count > HistoryCapacity) history.RemoveAt(0);
        }
    }
}
