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
    // billed for the same frames. The fractional carry per Area and policy is runtime-only.
    public sealed class AreaPolicyUpkeepSettlement
    {
        private readonly AreaPolicyRuntime runtime;
        private readonly Dictionary<(ulong, int), double> carries = new Dictionary<(ulong, int), double>();
        private readonly List<ManagedAreaPolicyEntry> entries = new List<ManagedAreaPolicyEntry>();
        public AreaPolicyUpkeepSettlement(AreaPolicyRuntime runtime) => this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        public static double DailyUpkeep(AreaPolicyDefinition definition, in AreaPolicyUpkeepInputs inputs) =>
            definition.Upkeep.EstimateDaily(inputs.AreaSquareMeters, inputs.EligibleFacilityCount, inputs.ActivityUnits);
        public void Settle(IAreaPolicyStore store, Entity area, uint frames, in AreaPolicyContext context, Func<double> areaSquareMeters, List<AreaPolicyUpkeepCharge> into)
        {
            if (frames == 0 || !store.TryGetArea(area, out var info) || info.Deleted || !store.TryRead(area, entries)) return;
            var desired = new List<int>();
            foreach (var entry in entries) if (entry.IsDesiredEnabled && !desired.Contains(entry.PolicyId)) desired.Add(entry.PolicyId);
            desired.Sort();
            foreach (int policyId in desired)
            {
                if (!runtime.Registry.TryGet(policyId, out var definition) || !runtime.GetState(store, area, policyId, context).Effective) continue;
                var inputs = runtime.GetUpkeepInputs(area, info.StableId, definition, context, areaSquareMeters);
                var key = (info.StableId, policyId);
                carries.TryGetValue(key, out double carry);
                long amount = AreaFinanceModel.SettleDaily(DailyUpkeep(definition, inputs), frames, ref carry);
                carries[key] = carry;
                if (amount > 0) into.Add(new AreaPolicyUpkeepCharge(info.StableId, policyId, amount));
            }
        }
        public void Clear() => carries.Clear();
    }
}
