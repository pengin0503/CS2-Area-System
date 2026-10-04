using System;
using System.Collections.Generic;
using System.Globalization;
using CS2AreaSystem.Core.Modules;
using Game.Economy;
namespace CS2AreaSystem.Modules.Industrial
{
    // One existing Industrial Area as a contract destination: Selectable is false while it is quarantined (it still exists, so
    // its contracts are not Invalid, but it is not offered for new ones).
    public struct IndustrialSupplyContractArea
    {
        public ulong StableId;
        public string Name;
        public bool Selectable;
        public IndustrialSupplySide Side;
    }
    // Phase 3 D1: fills the Contracts tab read model from already collected rows. Read-only; evaluations come from the
    // exact-input cache, so an unchanged panel refresh recomputes nothing.
    public static class IndustrialSupplyContractView
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static void Fill(IndustrialPanelModel model, ulong source, IReadOnlyList<IndustrialSupplyContract> contracts, IndustrialSupplySide sourceSide,
            IReadOnlyList<IndustrialSupplyContractArea> areas, IndustrialSupplyContractCache cache, IndustrialSupplyContractDraft draft)
        {
            model.ContractsAvailable = true;
            model.ContractDraft = draft;
            var byId = new Dictionary<ulong, IndustrialSupplyContractArea>(areas.Count);
            var destinations = new List<AreaPanelOption>();
            foreach (var area in areas)
            {
                if (area.StableId == 0 || byId.ContainsKey(area.StableId)) continue;
                byId[area.StableId] = area;
                if (area.Selectable && IndustrialSupplyContractModel.IsValidDestination(source, area.StableId) && destinations.Count < AreaPanelOption.MaxOptions)
                    destinations.Add(new AreaPanelOption { Value = area.StableId.ToString(Invariant), Label = Label(area) });
            }
            model.ContractDestinations = destinations;
            var resources = new List<AreaPanelOption>();
            var seen = new HashSet<ulong>();
            if (sourceSide.Result != null)
                foreach (var total in sourceSide.Result.Resources)
                {
                    if (resources.Count >= AreaPanelOption.MaxOptions) break;
                    if (!IndustrialSupplyContractModel.IsValidResource(total.Resource) || !seen.Add(total.Resource)) continue;
                    resources.Add(new AreaPanelOption { Value = total.Resource.ToString(Invariant), LabelKey = "Resources.TITLE[" + Enum.GetName(typeof(Resource), (Resource)total.Resource) + "]" });
                }
            model.ContractResources = resources;
            int count = Math.Min(contracts.Count, IndustrialSupplyContractModel.MaxContracts);
            var shown = new List<IndustrialSupplyContract>(count);
            for (int i = 0; i < count; i++) shown.Add(contracts[i]);
            var evaluations = cache.Evaluate(source, shown, sourceSide, id => byId.TryGetValue(id, out var a) ? (true, a.Side) : (false, default(IndustrialSupplySide)));
            var rows = new List<IndustrialSupplyContractRow>(count);
            for (int i = 0; i < count; i++)
                rows.Add(new IndustrialSupplyContractRow { Contract = shown[i], Evaluation = evaluations[i],
                    DestinationName = byId.TryGetValue(shown[i].DestinationStableId, out var a) ? Label(a) : null });
            model.ContractRows = rows;
        }
        private static string Label(in IndustrialSupplyContractArea area) => string.IsNullOrEmpty(area.Name) ? area.StableId.ToString(Invariant) : area.Name;
    }
}
