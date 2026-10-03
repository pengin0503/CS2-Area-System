using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
using Game.Economy;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
namespace CS2AreaSystem.Modules.Industrial
{
    // One finished request, for lifetime accounting on the Area: credited <= settled; the rest is unallocated.
    public readonly struct IndustrialSupportCompletion
    {
        public readonly ulong AreaId;
        public readonly Entity Area;
        public readonly long Requested, Settled, Credited;
        public IndustrialSupportCompletion(ulong areaId, Entity area, long requested, long settled, long credited)
        { AreaId = areaId; Area = area; Requested = requested; Settled = settled; Credited = credited; }
    }
    // The receiver credits of every settled request taken in one pass, flattened for one job. Items keep their slice.
    public sealed class IndustrialSupportCreditBatch
    {
        public readonly struct Item
        {
            public readonly IndustrialSupportPending Pending;
            public readonly int Start, Count;
            public Item(IndustrialSupportPending pending, int start, int count) { Pending = pending; Start = start; Count = count; }
        }
        public readonly List<Item> Items = new List<Item>();
        public readonly List<Entity> Receivers = new List<Entity>();
        public readonly List<int> Amounts = new List<int>();
        public bool IsEmpty => Items.Count == 0;
    }
    public static class IndustrialSupportCredit
    {
        // The actual increase of the receiver's Money resource (EconomyUtils.AddResources clamps to int), never above
        // the allocation and never negative.
        public static long Credited(int before, int after, int amount) => Math.Max(0, Math.Min((long)amount, (long)after - before));
    }
    // Settlement lifecycle (Phase 2 C4): Requested → (Area Finance completion) Settled → (credit job) finished. Every
    // transition happens once; a finished request leaves the ledger and is never replayed.
    public static class IndustrialSupportSettlement
    {
        // Area Finance completion listener body: only this generation's requested support entries move to Settled.
        public static void OnCompleted(IndustrialSupportLedger ledger, int generation, AreaFinanceBatch requested, AreaFinanceBatch settled)
        {
            if (ledger == null || requested == null) return;
            foreach (var entry in requested.Expenses)
            {
                if (entry.Kind != AreaFinanceKind.IndustrialSupport) continue;
                var pending = ledger.Get(entry.AreaStableId);
                if (pending == null || pending.Generation != generation || pending.Stage != IndustrialSupportStage.Requested) continue;
                long amount = 0;
                if (settled != null) foreach (var applied in settled.Expenses)
                    if (applied.Kind == AreaFinanceKind.IndustrialSupport && applied.AreaStableId == entry.AreaStableId) amount = AreaFinanceModel.SaturatingAdd(amount, applied.Amount);
                pending.Settled = Math.Max(0, Math.Min(amount, pending.Requested));
                pending.Stage = IndustrialSupportStage.Settled;
            }
        }
        // Takes every settled request: zero settlements finish at once; positive ones are allocated over the receivers that
        // still resolve to their Area (each check isolated) and move to Crediting.
        public static IndustrialSupportCreditBatch Prepare(IndustrialSupportLedger ledger, Func<Entity, ulong, bool> stillOwned, List<IndustrialSupportCompletion> finished)
        {
            var batch = new IndustrialSupportCreditBatch();
            var settledItems = new List<IndustrialSupportPending>();
            foreach (var pending in ledger.All) if (pending.Stage == IndustrialSupportStage.Settled) settledItems.Add(pending);
            settledItems.Sort((a, b) => a.AreaId.CompareTo(b.AreaId));
            foreach (var pending in settledItems)
            {
                if (pending.Settled <= 0)
                {
                    ledger.Remove(pending.AreaId);
                    finished?.Add(new IndustrialSupportCompletion(pending.AreaId, pending.Area, pending.Requested, 0, 0));
                    continue;
                }
                var valid = new bool[pending.Receivers.Count];
                for (int i = 0; i < valid.Length; i++)
                {
                    try { valid[i] = stillOwned != null && stillOwned(pending.Receivers[i], pending.AreaId); }
                    catch (Exception) { valid[i] = false; }
                }
                var allocation = IndustrialSupportModel.Allocate(pending.Settled, pending.PerCompany, valid);
                batch.Items.Add(new IndustrialSupportCreditBatch.Item(pending, batch.Receivers.Count, valid.Length));
                for (int i = 0; i < valid.Length; i++)
                {
                    batch.Receivers.Add(pending.Receivers[i]);
                    batch.Amounts.Add((int)Math.Max(0, Math.Min(int.MaxValue, allocation.Amounts[i])));
                }
                pending.Stage = IndustrialSupportStage.Crediting;
            }
            return batch;
        }
        // Records what the credit job actually added (null or short arrays count as nothing credited) and removes the
        // requests. A request that is no longer the ledger's crediting entry (already finished) is skipped.
        public static List<IndustrialSupportCompletion> Finish(IndustrialSupportLedger ledger, IndustrialSupportCreditBatch batch, IReadOnlyList<long> credited)
        {
            var result = new List<IndustrialSupportCompletion>();
            if (batch == null) return result;
            foreach (var item in batch.Items)
            {
                var pending = item.Pending;
                if (!ReferenceEquals(ledger.Get(pending.AreaId), pending) || pending.Stage != IndustrialSupportStage.Crediting) continue;
                long total = 0;
                for (int i = item.Start; i < item.Start + item.Count; i++)
                    if (credited != null && i < credited.Count) total = AreaFinanceModel.SaturatingAdd(total, Math.Max(0, Math.Min(batch.Amounts[i], credited[i])));
                total = Math.Min(total, pending.Settled);
                ledger.Remove(pending.AreaId);
                result.Add(new IndustrialSupportCompletion(pending.AreaId, pending.Area, pending.Requested, pending.Settled, total));
            }
            return result;
        }
    }
    // The only Industrial write to company money (Phase 2 C4): adds each receiver's allocation to its Resource.Money with
    // EconomyUtils.AddResources, the 1.6.2f1 path PayWageSystem.PayJob uses for company money through a BufferLookup in
    // an IJob, and reports the actual increase (AddResources clamps to int). Scheduled with the owning system's Dependency.
    public struct IndustrialSupportCreditJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Receivers;
        [ReadOnly] public NativeArray<int> Amounts;
        public NativeArray<long> Credited;
        public BufferLookup<Resources> Resources;
        public void Execute()
        {
            for (int i = 0; i < Receivers.Length; i++)
            {
                Credited[i] = 0;
                int amount = Amounts[i];
                if (amount <= 0 || !Resources.HasBuffer(Receivers[i])) continue;
                var buffer = Resources[Receivers[i]];
                int before = EconomyUtils.GetResources(Resource.Money, buffer);
                int after = EconomyUtils.AddResources(Resource.Money, amount, buffer);
                Credited[i] = IndustrialSupportCredit.Credited(before, after, amount);
            }
        }
    }
}
