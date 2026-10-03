using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public enum IndustrialSupportScheduleOutcome : byte { Request, Disabled, Inactive, AlreadyScheduled, Outstanding, NoEvidence, Partial, Stale, NoReceivers }
    public struct IndustrialSupportScheduleInputs
    {
        public bool Active, HasOutstanding, SnapshotPaused;
        public IndustrialSupportState State;
        public long Day;
        // Latest published observation of the Area and the generation tokens the Area has now.
        public IndustrialObservationResult Roster;
        public ulong MembershipToken, StructureToken;
        // Live B2 revalidation: the company still exists, is eligible and resolves to this Area.
        public Func<Entity, bool> StillOwned;
    }
    public readonly struct IndustrialSupportScheduleDecision
    {
        public readonly IndustrialSupportScheduleOutcome Outcome;
        public readonly IReadOnlyList<Entity> Receivers;
        public readonly int PerCompany;
        public readonly long Requested;
        public IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome outcome, IReadOnlyList<Entity> receivers = null, int perCompany = 0, long requested = 0)
        { Outcome = outcome; Receivers = receivers ?? Array.Empty<Entity>(); PerCompany = perCompany; Requested = requested; }
    }
    // Daily request decision (Phase 2 C4): one request per Area and in-game day, only from a complete roster of the
    // current generation whose receivers all still resolve to the Area. Anything less queues nothing and may succeed
    // later the same day; no skipped day is ever paid back.
    public static class IndustrialSupportScheduling
    {
        public static int CompareEntities(Entity a, Entity b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Version.CompareTo(b.Version);
        public static IndustrialSupportScheduleDecision Decide(in IndustrialSupportScheduleInputs inputs)
        {
            int perCompany = IndustrialSupportModel.NormalizeSetting(inputs.State.DailySupportPerCompany);
            if (perCompany <= 0) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Disabled);
            if (!inputs.Active) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Inactive);
            if (IndustrialSupportModel.AlreadyScheduled(inputs.State, inputs.Day)) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.AlreadyScheduled);
            if (inputs.HasOutstanding) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Outstanding);
            var roster = inputs.Roster;
            if (roster == null) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.NoEvidence);
            if (!roster.SupportRosterComplete || roster.FailedReads > 0) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Partial);
            if (inputs.SnapshotPaused || !roster.Active || roster.MembershipToken != inputs.MembershipToken || roster.StructureToken != inputs.StructureToken)
                return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Stale);
            if (roster.SupportRoster.Count == 0) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.NoReceivers);
            foreach (var company in roster.SupportRoster)
                if (inputs.StillOwned == null || !inputs.StillOwned(company)) return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Stale);
            var receivers = new Entity[roster.SupportRoster.Count];
            for (int i = 0; i < receivers.Length; i++) receivers[i] = roster.SupportRoster[i];
            return new IndustrialSupportScheduleDecision(IndustrialSupportScheduleOutcome.Request, Array.AsReadOnly(receivers), perCompany,
                IndustrialSupportModel.DailyRequest(receivers.Length, perCompany));
        }
        // Queues the request; the day and the pending roster are committed only when Area Finance accepted it.
        public static bool TryRequest(in IndustrialSupportScheduleDecision decision, int generation, ulong areaId, Entity area, long day,
            Func<long, bool> enqueue, ref IndustrialSupportState state, IndustrialSupportLedger ledger)
        {
            if (decision.Outcome != IndustrialSupportScheduleOutcome.Request || decision.Requested <= 0 || ledger.HasOutstanding(areaId)) return false;
            if (!enqueue(decision.Requested)) return false;
            ledger.TryBegin(generation, areaId, area, decision.PerCompany, decision.Receivers, decision.Requested);
            state = IndustrialSupportModel.MarkScheduled(state, day);
            return true;
        }
    }
    public enum IndustrialSupportStage : byte { Requested, Settled, Crediting }
    // One accepted request: runtime-only, never saved, never replayed.
    public sealed class IndustrialSupportPending
    {
        public int Generation;
        public ulong AreaId;
        public Entity Area;
        public int PerCompany;
        public IReadOnlyList<Entity> Receivers;
        public long Requested, Settled;
        public IndustrialSupportStage Stage;
    }
    // At most one outstanding request per Area, keyed by the load generation it was accepted in.
    public sealed class IndustrialSupportLedger
    {
        private readonly Dictionary<ulong, IndustrialSupportPending> pending = new Dictionary<ulong, IndustrialSupportPending>();
        public int Count => pending.Count;
        public IEnumerable<IndustrialSupportPending> All => pending.Values;
        public bool HasOutstanding(ulong areaId) => pending.ContainsKey(areaId);
        public IndustrialSupportPending Get(ulong areaId) => pending.TryGetValue(areaId, out var item) ? item : null;
        public bool TryBegin(int generation, ulong areaId, Entity area, int perCompany, IReadOnlyList<Entity> receivers, long requested)
        {
            if (pending.ContainsKey(areaId)) return false;
            pending[areaId] = new IndustrialSupportPending { Generation = generation, AreaId = areaId, Area = area, PerCompany = perCompany,
                Receivers = receivers ?? Array.Empty<Entity>(), Requested = requested, Stage = IndustrialSupportStage.Requested };
            return true;
        }
        public bool Remove(ulong areaId) => pending.Remove(areaId);
        public void Clear() => pending.Clear();
    }
}
