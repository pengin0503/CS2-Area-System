using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public enum ParkEventRequestOutcome : byte { Request, Unavailable, NoState, Inactive, UnknownEvent, Pending, Active, NoCost }
    public struct ParkEventRequestInputs
    {
        // The event module is enabled and Area Finance has started this load generation.
        public bool Available, ParkActive, HasState, HasPending, CostKnown;
        public ParkEventState State;
        public uint Frame;
        public int EventId;
        public double DailyManagementCost;
    }
    // One accepted start request; runtime-only, keyed by the Area Finance load generation (never saved or replayed).
    public sealed class ParkEventPending
    {
        public int Generation, EventId;
        public ulong AreaId;
        public Entity Area;
        public long Requested;
    }
    public readonly struct ParkEventSettlement
    {
        public readonly ulong AreaId;
        public readonly Entity Area;
        public readonly int EventId;
        public readonly long Requested, Paid;
        public ParkEventSettlement(ulong areaId, Entity area, int eventId, long requested, long paid)
        { AreaId = areaId; Area = area; EventId = eventId; Requested = requested; Paid = paid; }
    }
    public enum ParkEventApplyOutcome : byte { Started, AccountedOnly }
    // At most one pending event request per Park.
    public sealed class ParkEventLedger
    {
        private readonly Dictionary<ulong, ParkEventPending> pending = new Dictionary<ulong, ParkEventPending>();
        public int Count => pending.Count;
        public bool HasPending(ulong areaId) => pending.ContainsKey(areaId);
        public ParkEventPending Get(ulong areaId) => pending.TryGetValue(areaId, out var item) ? item : null;
        public bool TryBegin(int generation, ulong areaId, Entity area, int eventId, long requested)
        {
            if (pending.ContainsKey(areaId)) return false;
            pending[areaId] = new ParkEventPending { Generation = generation, AreaId = areaId, Area = area, EventId = eventId, Requested = requested };
            return true;
        }
        public bool Remove(ulong areaId) => pending.Remove(areaId);
        public void Clear() => pending.Clear();
    }
    // Park event finance lifecycle (Phase 2 C5): request → (Area Finance completion) started, accounted-only, or cleared.
    public static class ParkEventFinance
    {
        public static ParkEventRequestOutcome Decide(in ParkEventRequestInputs inputs, out long cost)
        {
            cost = 0;
            if (!inputs.Available) return ParkEventRequestOutcome.Unavailable;
            if (!inputs.HasState) return ParkEventRequestOutcome.NoState;
            if (!inputs.ParkActive) return ParkEventRequestOutcome.Inactive;
            if (!ParkEventCatalog.IsKnown(inputs.EventId)) return ParkEventRequestOutcome.UnknownEvent;
            if (inputs.HasPending) return ParkEventRequestOutcome.Pending;
            if (ParkEventModel.IsActive(inputs.State, inputs.Frame)) return ParkEventRequestOutcome.Active;
            if (!inputs.CostKnown || !ParkEventModel.TryCost(inputs.DailyManagementCost, out cost)) return ParkEventRequestOutcome.NoCost;
            return ParkEventRequestOutcome.Request;
        }
        // Pending is recorded only after Area Finance accepted the expense; a refused enqueue leaves nothing behind.
        public static ParkEventRequestOutcome TryRequest(in ParkEventRequestInputs inputs, int generation, ulong areaId, Entity area,
            Func<long, bool> enqueue, ParkEventLedger ledger)
        {
            var outcome = Decide(inputs, out var cost);
            if (outcome != ParkEventRequestOutcome.Request) return outcome;
            if (ledger == null || ledger.HasPending(areaId)) return ParkEventRequestOutcome.Pending;
            if (enqueue == null || !enqueue(cost)) return ParkEventRequestOutcome.Unavailable;
            ledger.TryBegin(generation, areaId, area, inputs.EventId, cost);
            return ParkEventRequestOutcome.Request;
        }
        // Area Finance completion listener body: only this generation's requested ParkEvent entries with a pending request
        // are consumed. Zero/aborted/cancelled outcomes clear the request; positive ones are returned for Apply.
        public static void OnCompleted(ParkEventLedger ledger, int generation, AreaFinanceBatch requested, AreaFinanceBatch settled, List<ParkEventSettlement> paid)
        {
            if (ledger == null || requested == null) return;
            foreach (var entry in requested.Expenses)
            {
                if (entry.Kind != AreaFinanceKind.ParkEvent) continue;
                var pending = ledger.Get(entry.AreaStableId);
                if (pending == null || pending.Generation != generation) continue;
                long amount = 0;
                if (settled != null) foreach (var applied in settled.Expenses)
                    if (applied.Kind == AreaFinanceKind.ParkEvent && applied.AreaStableId == entry.AreaStableId) amount = AreaFinanceModel.SaturatingAdd(amount, applied.Amount);
                amount = Math.Max(0, Math.Min(amount, pending.Requested));
                ledger.Remove(entry.AreaStableId);
                if (amount > 0) paid?.Add(new ParkEventSettlement(pending.AreaId, pending.Area, pending.EventId, pending.Requested, amount));
            }
        }
        // Only a still-existing Active Park with state and no running event may start.
        public static bool EligibleToStart(bool exists, bool parkActive, bool hasState, in ParkEventState state, uint frame) =>
            exists && parkActive && hasState && !ParkEventModel.IsActive(state, frame);
        public static ParkEventApplyOutcome Apply(in ParkEventSettlement paid, bool eligible, uint frame, ref ParkEventState state, IList<ParkEventHistoryEntry> history)
        {
            if (!eligible)
            {
                state = ParkEventModel.RecordUnstartedExpense(state, paid.Paid);
                return ParkEventApplyOutcome.AccountedOnly;
            }
            state = ParkEventModel.Start(state, paid.EventId, frame, paid.Paid);
            if (history != null) ParkEventModel.AppendHistory(history, ParkEventModel.HistoryEntry(state, paid.Paid));
            return ParkEventApplyOutcome.Started;
        }
    }
}
