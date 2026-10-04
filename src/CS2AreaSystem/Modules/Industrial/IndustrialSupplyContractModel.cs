using System;
using System.Collections.Generic;
using Game.Economy;
namespace CS2AreaSystem.Modules.Industrial
{
    public enum IndustrialSupplyContractEdit : byte { Rejected, Created, Updated, Unchanged }
    // Pure configuration rules of Phase 3 D1 supply contracts. Nothing here touches transfers, routing, prices or demand.
    public static class IndustrialSupplyContractModel
    {
        public const int MaxContracts = 32;
        public const int MinTargetPerDay = 50, MaxTargetPerDay = 50000, TargetStep = 50, DefaultTargetPerDay = 1000;
        // Exactly one defined vanilla resource bit below Resource.Last, never Money or NoResource.
        public static bool IsValidResource(ulong resource) =>
            resource != 0 && (resource & (resource - 1)) == 0 && resource != (ulong)Resource.Money && resource < (ulong)Resource.Last &&
            Enum.IsDefined(typeof(Resource), resource);
        public static bool IsValidDestination(ulong source, ulong destination) => destination != 0 && destination != source;
        // Clamps to [Min, Max] and snaps to the step, midpoints rounding away from zero.
        public static int NormalizeTarget(int target)
        {
            if (target <= MinTargetPerDay) return MinTargetPerDay;
            if (target >= MaxTargetPerDay) return MaxTargetPerDay;
            int snapped = (int)Math.Round(target / (double)TargetStep, MidpointRounding.AwayFromZero) * TargetStep;
            return Math.Min(MaxTargetPerDay, Math.Max(MinTargetPerDay, snapped));
        }
        public static int IndexOf(IList<IndustrialSupplyContract> contracts, int contractId)
        {
            for (int i = 0; i < contracts.Count; i++) if (contracts[i].ContractId == contractId) return i;
            return -1;
        }
        // The same destination + resource is one contract: a repeat updates (and re-enables) it instead of adding a duplicate.
        public static IndustrialSupplyContractEdit Upsert(IList<IndustrialSupplyContract> contracts, ref IndustrialSupplyContractState state, ulong source,
            ulong destination, ulong resource, int target, out int contractId)
        {
            contractId = 0;
            if (contracts == null || !IsValidDestination(source, destination) || !IsValidResource(resource)) return IndustrialSupplyContractEdit.Rejected;
            target = NormalizeTarget(target);
            for (int i = 0; i < contracts.Count; i++)
            {
                var c = contracts[i];
                if (c.DestinationStableId != destination || c.Resource != resource) continue;
                contractId = c.ContractId;
                if (c.TargetPerDay == target && c.Enabled) return IndustrialSupplyContractEdit.Unchanged;
                c.TargetPerDay = target; c.Enabled = true; contracts[i] = c;
                return IndustrialSupplyContractEdit.Updated;
            }
            int next = state.NextContractId < 1 ? 1 : state.NextContractId;
            if (contracts.Count >= MaxContracts || next == int.MaxValue) return IndustrialSupplyContractEdit.Rejected;
            for (int i = 0; i < contracts.Count; i++) if (contracts[i].ContractId >= next) next = contracts[i].ContractId + 1;
            if (next <= 0 || next == int.MaxValue) return IndustrialSupplyContractEdit.Rejected;
            contracts.Add(new IndustrialSupplyContract { SchemaVersion = IndustrialSupplyContract.CurrentSchemaVersion, ContractId = next,
                DestinationStableId = destination, Resource = resource, TargetPerDay = target, Enabled = true });
            contractId = next;
            state.SchemaVersion = IndustrialSupplyContractState.CurrentSchemaVersion; state.NextContractId = next + 1;
            return IndustrialSupplyContractEdit.Created;
        }
        public static bool SetTarget(IList<IndustrialSupplyContract> contracts, int contractId, int target)
        {
            int i = IndexOf(contracts, contractId);
            if (i < 0) return false;
            var c = contracts[i]; target = NormalizeTarget(target);
            if (c.TargetPerDay == target) return false;
            c.TargetPerDay = target; contracts[i] = c; return true;
        }
        public static bool Toggle(IList<IndustrialSupplyContract> contracts, int contractId)
        {
            int i = IndexOf(contracts, contractId);
            if (i < 0) return false;
            var c = contracts[i]; c.Enabled = !c.Enabled; contracts[i] = c; return true;
        }
        public static bool Remove(IList<IndustrialSupplyContract> contracts, int contractId)
        {
            int i = IndexOf(contracts, contractId);
            if (i < 0) return false;
            contracts.RemoveAt(i); return true;
        }
    }
}
