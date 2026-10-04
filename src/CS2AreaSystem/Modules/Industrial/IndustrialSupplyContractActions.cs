using System;
using System.Collections.Generic;
using System.Globalization;
namespace CS2AreaSystem.Modules.Industrial
{
    // Runtime-only selection for a new contract of one source Area; never saved, cleared on load.
    public sealed class IndustrialSupplyContractDraft
    {
        public ulong Destination;
        public ulong Resource;
        public int TargetPerDay = IndustrialSupplyContractModel.DefaultTargetPerDay;
    }
    // Phase 3 D1 contract actions. Core has already matched the source StableId and live selection; every value is
    // re-validated here against live state, and existing contracts are addressed only by their stable ContractId.
    public static class IndustrialSupplyContractActions
    {
        public const string DraftDestination = "contractDraftDestination", DraftResource = "contractDraftResource", DraftTarget = "contractDraftTarget",
            Create = "contractCreate", TargetPrefix = "contractTarget.", Toggle = "contractToggle", Remove = "contractRemove";
        public static bool IsContractAction(string action) =>
            action == DraftDestination || action == DraftResource || action == DraftTarget || action == Create || action == Toggle || action == Remove ||
            (action != null && action.StartsWith(TargetPrefix, StringComparison.Ordinal));
        // True when the action changed the draft or the contracts; contractsChanged says the saved buffer/state must be written.
        public static bool TryApply(string action, string value, ulong source, IndustrialSupplyContractDraft draft, IList<IndustrialSupplyContract> contracts,
            ref IndustrialSupplyContractState state, Func<ulong, bool> isLiveDestination, Func<ulong, bool> isOfferedResource, out bool contractsChanged)
        {
            contractsChanged = false;
            if (draft == null || contracts == null || isLiveDestination == null || isOfferedResource == null || !IsContractAction(action)) return false;
            switch (action)
            {
                case DraftDestination:
                    if (!TryId(value, out ulong destination) || !IndustrialSupplyContractModel.IsValidDestination(source, destination) || !isLiveDestination(destination)) return false;
                    if (draft.Destination == destination) return false;
                    draft.Destination = destination; return true;
                case DraftResource:
                    if (!TryId(value, out ulong resource) || !IndustrialSupplyContractModel.IsValidResource(resource) || !isOfferedResource(resource)) return false;
                    if (draft.Resource == resource) return false;
                    draft.Resource = resource; return true;
                case DraftTarget:
                    if (!TryTarget(value, out int target) || draft.TargetPerDay == target) return false;
                    draft.TargetPerDay = target; return true;
                case Create:
                    // The destination may have been deleted, and the resource may no longer be produced, since they were drafted.
                    if (!IndustrialSupplyContractModel.IsValidDestination(source, draft.Destination) || !isLiveDestination(draft.Destination) || !isOfferedResource(draft.Resource)) return false;
                    var edit = IndustrialSupplyContractModel.Upsert(contracts, ref state, source, draft.Destination, draft.Resource, draft.TargetPerDay, out _);
                    contractsChanged = edit == IndustrialSupplyContractEdit.Created || edit == IndustrialSupplyContractEdit.Updated;
                    return contractsChanged;
                case Toggle:
                    if (!TryContractId(value, out int toggled)) return false;
                    return contractsChanged = IndustrialSupplyContractModel.Toggle(contracts, toggled);
                case Remove:
                    if (!TryContractId(value, out int removed)) return false;
                    return contractsChanged = IndustrialSupplyContractModel.Remove(contracts, removed);
                default:
                    if (!TryContractId(action.Substring(TargetPrefix.Length), out int id) || !TryTarget(value, out int newTarget)) return false;
                    return contractsChanged = IndustrialSupplyContractModel.SetTarget(contracts, id, newTarget);
            }
        }
        private static bool TryId(string value, out ulong id) =>
            ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0;
        // Any saved id, including a corrupted zero/negative one, so such a contract stays removable; the model matches it exactly.
        private static bool TryContractId(string value, out int id) =>
            int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out id);
        // Slider values arrive as invariant decimal text; non-finite or unparsable text is rejected, never clamped.
        private static bool TryTarget(string value, out int target)
        {
            target = 0;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw) || double.IsNaN(raw) || double.IsInfinity(raw)) return false;
            target = IndustrialSupplyContractModel.NormalizeTarget(raw >= int.MaxValue ? int.MaxValue : raw <= int.MinValue ? int.MinValue : (int)Math.Round(raw));
            return true;
        }
    }
}
