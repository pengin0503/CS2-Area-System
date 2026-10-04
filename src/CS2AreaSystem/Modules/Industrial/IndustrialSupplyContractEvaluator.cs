using System;
namespace CS2AreaSystem.Modules.Industrial
{
    // Phase 3 D1 advisory status. Nothing here is a shipment: Available/Constrained compare an estimated potential with the
    // player's target, from the two Areas' existing observations only.
    public enum IndustrialSupplyContractStatus : byte { Unknown, PartialData, NoObservedSupply, NoObservedNeed, Unavailable, Available, Constrained, Disabled, Invalid }
    // One side's last published observation; Stale when the Area is paused/suspended or the result is not an Active cycle.
    public readonly struct IndustrialSupplySide
    {
        public IndustrialObservationResult Result { get; }
        public bool Stale { get; }
        public IndustrialSupplySide(IndustrialObservationResult result, bool stale) { Result = result; Stale = stale; }
    }
    public sealed class IndustrialSupplyContractEvaluation
    {
        public int ContractId;
        public IndustrialSupplyContractStatus Status;
        // A side published a delayed cycle: values cover a longer window and are shown as stale, not as lower bounds.
        public bool Stale;
        // Units/day; null when not known. With PartialData they cover only what was observed.
        public double? SourceCapacityPerDay, DestinationNeedPerDay, PotentialPerDay;
    }
    public static class IndustrialSupplyContractEvaluator
    {
        public static IndustrialSupplyContractEvaluation Evaluate(in IndustrialSupplyContract contract, ulong source, bool destinationLive, IndustrialSupplySide sourceSide,
            IndustrialSupplySide destinationSide)
        {
            var e = new IndustrialSupplyContractEvaluation { ContractId = contract.ContractId };
            if (contract.ContractId <= 0 || !destinationLive || !IndustrialSupplyContractModel.IsValidDestination(source, contract.DestinationStableId) ||
                !IndustrialSupplyContractModel.IsValidResource(contract.Resource)) { e.Status = IndustrialSupplyContractStatus.Invalid; return e; }
            if (!contract.Enabled) { e.Status = IndustrialSupplyContractStatus.Disabled; return e; }
            var src = sourceSide.Result; var dst = destinationSide.Result;
            if (src == null || dst == null || sourceSide.Stale || destinationSide.Stale || !src.Active || !dst.Active) { e.Status = IndustrialSupplyContractStatus.Unknown; return e; }
            bool partial = src.FailedReads > 0 || dst.FailedReads > 0;
            e.Stale = src.Delayed || dst.Delayed;
            // Source capacity: the observed output rate of the contract resource.
            double capacity = 0; bool supplies = false, capacityKnown = true;
            foreach (var total in src.Resources)
            {
                if (total.Resource != contract.Resource) continue;
                supplies = true;
                if (Finite(total.ActualPerDay) && total.ActualPerDay >= 0) capacity += total.ActualPerDay; else { capacityKnown = false; partial = true; }
            }
            // Resource totals count only measured producers; an unmeasured producer of the resource makes capacity a lower bound.
            foreach (var p in src.Processes)
                if (p != null && p.Process.Output == contract.Resource && p.MeasuredCompanies < p.Companies) { supplies = true; partial = true; }
            // Destination need: each observed recipe consuming the resource, scaled from its actual output by recipe amounts.
            double need = 0; bool consumes = false, needKnown = true;
            foreach (var p in dst.Processes)
            {
                if (p == null) continue;
                var k = p.Process;
                int amount = k.Input1 == contract.Resource ? k.Input1Amount : k.Input2 == contract.Resource ? k.Input2Amount : 0;
                if (amount == 0 && k.Input1 != contract.Resource && k.Input2 != contract.Resource) continue;
                consumes = true;
                if (p.MeasuredCompanies < p.Companies) partial = true;
                if (amount <= 0 || k.OutputAmount <= 0) { partial = true; continue; }
                if (!Finite(p.ActualOutputPerDay) || p.ActualOutputPerDay < 0) { needKnown = false; partial = true; continue; }
                need += p.ActualOutputPerDay * amount / k.OutputAmount;
            }
            e.SourceCapacityPerDay = capacityKnown ? capacity : (double?)null;
            e.DestinationNeedPerDay = needKnown ? need : (double?)null;
            if (capacityKnown && needKnown) e.PotentialPerDay = Math.Min(contract.TargetPerDay, Math.Min(capacity, need));
            if (partial) { e.Status = IndustrialSupplyContractStatus.PartialData; return e; }
            if (!supplies && !consumes) e.Status = IndustrialSupplyContractStatus.Unavailable;
            else if (!(capacity > 0)) e.Status = IndustrialSupplyContractStatus.NoObservedSupply;
            else if (!(need > 0)) e.Status = IndustrialSupplyContractStatus.NoObservedNeed;
            else e.Status = e.PotentialPerDay >= contract.TargetPerDay ? IndustrialSupplyContractStatus.Available : IndustrialSupplyContractStatus.Constrained;
            return e;
        }
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
