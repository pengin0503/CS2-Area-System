using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
using Unity.Entities;
namespace CS2AreaSystem.Core.Policies
{
    public readonly struct AreaPolicyUpkeepCharge : IEquatable<AreaPolicyUpkeepCharge>
    {
        public readonly ulong AreaStableId;
        public readonly int PolicyId;
        public readonly long Amount;
        public AreaPolicyUpkeepCharge(ulong areaStableId, int policyId, long amount) { AreaStableId = areaStableId; PolicyId = policyId; Amount = amount; }
        public bool Equals(AreaPolicyUpkeepCharge other) => AreaStableId == other.AreaStableId && PolicyId == other.PolicyId && Amount == other.Amount;
        public override bool Equals(object obj) => obj is AreaPolicyUpkeepCharge other && Equals(other);
        public override int GetHashCode() => unchecked((AreaStableId.GetHashCode() * 397 ^ PolicyId) * 397 ^ Amount.GetHashCode());
        public override string ToString() => $"{AreaStableId}:{PolicyId}:{Amount}";
    }
    // Simulation-time policy upkeep (design §13–14). Each settlement bills the policies effective for the Area at that
    // moment for the elapsed frames; a conflict swap is one state change, so two policies of one group are never both
    // billed for the same frames. The fractional carry per Area and policy is runtime-only (#66): it is kept while the
    // policy stays desired (also while Suspended, locked or conflicting), dropped when the policy is no longer desired or
    // the Area is gone (RetainAreas after each upkeep pass), and cleared on load.
    public sealed class AreaPolicyUpkeepSettlement
    {
        private readonly AreaPolicyRuntime runtime;
        private readonly Dictionary<ulong, Dictionary<int, double>> carries = new Dictionary<ulong, Dictionary<int, double>>();
        private readonly List<ManagedAreaPolicyEntry> entries = new List<ManagedAreaPolicyEntry>();
        // Reused per settlement (#73): unique desired IDs without List.Contains, sorted for a stable billing order.
        private readonly HashSet<int> desiredSet = new HashSet<int>();
        private readonly List<int> desired = new List<int>();
        private readonly List<int> staleCarries = new List<int>();
        private readonly List<ulong> staleAreas = new List<ulong>();
        public AreaPolicyUpkeepSettlement(AreaPolicyRuntime runtime) => this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        public int CarryAreaCount => carries.Count;
        public bool HasCarry(ulong areaStableId, int policyId) => carries.TryGetValue(areaStableId, out var byPolicy) && byPolicy.ContainsKey(policyId);
        public static double DailyUpkeep(AreaPolicyDefinition definition, in AreaPolicyUpkeepInputs inputs) =>
            definition.Upkeep.EstimateDaily(inputs.AreaSquareMeters, inputs.EligibleFacilityCount, inputs.ActivityUnits);
        public void Settle(IAreaPolicyStore store, Entity area, uint frames, in AreaPolicyContext context, Func<double> areaSquareMeters, List<AreaPolicyUpkeepCharge> into)
        {
            if (frames == 0 || !store.TryGetArea(area, out var info) || info.Deleted || !store.TryRead(area, entries)) return;
            desiredSet.Clear(); desired.Clear();
            foreach (var entry in entries) if (entry.IsDesiredEnabled && desiredSet.Add(entry.PolicyId)) desired.Add(entry.PolicyId);
            desired.Sort();
            carries.TryGetValue(info.StableId, out var byPolicy);
            if (byPolicy != null && DropUndesired(info.StableId, byPolicy)) byPolicy = null;
            foreach (int policyId in desired)
            {
                if (!runtime.Registry.TryGet(policyId, out var definition) || !runtime.GetState(store, area, policyId, context).Effective) continue;
                var inputs = runtime.GetUpkeepInputs(area, info.StableId, definition, context, areaSquareMeters);
                if (byPolicy == null) carries[info.StableId] = byPolicy = new Dictionary<int, double>();
                byPolicy.TryGetValue(policyId, out double carry);
                long amount = AreaFinanceModel.SettleDaily(DailyUpkeep(definition, inputs), frames, ref carry);
                byPolicy[policyId] = carry;
                if (amount > 0) into.Add(new AreaPolicyUpkeepCharge(info.StableId, policyId, amount));
            }
        }
        // Drops the carries of Areas that no longer exist (the IDs of every live Area that has policy entries).
        public void RetainAreas(ICollection<ulong> liveAreaStableIds)
        {
            if (carries.Count == 0) return;
            staleAreas.Clear();
            foreach (var id in carries.Keys) if (!liveAreaStableIds.Contains(id)) staleAreas.Add(id);
            foreach (var id in staleAreas) carries.Remove(id);
        }
        public void Clear() => carries.Clear();
        // True when the Area has no carry left (its entry was removed).
        private bool DropUndesired(ulong areaStableId, Dictionary<int, double> byPolicy)
        {
            staleCarries.Clear();
            foreach (var policyId in byPolicy.Keys) if (!desiredSet.Contains(policyId)) staleCarries.Add(policyId);
            foreach (var policyId in staleCarries) byPolicy.Remove(policyId);
            if (byPolicy.Count != 0) return false;
            carries.Remove(areaStableId);
            return true;
        }
    }
}
