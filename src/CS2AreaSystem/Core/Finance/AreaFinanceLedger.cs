using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Finance
{
    public readonly struct AreaFinanceEntry : IEquatable<AreaFinanceEntry>
    {
        public readonly ulong AreaStableId;
        public readonly AreaFinanceKind Kind;
        public readonly long Amount;
        public AreaFinanceEntry(ulong areaStableId, AreaFinanceKind kind, long amount) { AreaStableId = areaStableId; Kind = kind; Amount = amount; }
        public bool Equals(AreaFinanceEntry other) => AreaStableId == other.AreaStableId && Kind == other.Kind && Amount == other.Amount;
        public override bool Equals(object obj) => obj is AreaFinanceEntry other && Equals(other);
        public override int GetHashCode() => unchecked((AreaStableId.GetHashCode() * 397 ^ (int)Kind) * 397 ^ Amount.GetHashCode());
        public override string ToString() => $"{AreaStableId}:{Kind}:{Amount}";
    }
    // One settlement's worth of Area amounts: one entry per Area and kind, in first-request order, so the audit trail
    // survives aggregation. The same type describes what was requested and (after Resolve) what was applied.
    public sealed class AreaFinanceBatch
    {
        private readonly List<AreaFinanceEntry> expenses, incomes;
        public IReadOnlyList<AreaFinanceEntry> Expenses => expenses;
        public IReadOnlyList<AreaFinanceEntry> Incomes => incomes;
        public long TotalExpense { get; }
        public long TotalIncome { get; }
        public bool IsEmpty => expenses.Count == 0 && incomes.Count == 0;
        // Nothing settled (an abort or cancellation). Shared and never mutated.
        public static AreaFinanceBatch Empty { get; } = new AreaFinanceBatch(new List<AreaFinanceEntry>(), new List<AreaFinanceEntry>());
        internal AreaFinanceBatch(List<AreaFinanceEntry> expenses, List<AreaFinanceEntry> incomes)
        {
            this.expenses = expenses; this.incomes = incomes;
            TotalExpense = Sum(expenses); TotalIncome = Sum(incomes);
        }
        public long Total(AreaFinanceKind kind)
        {
            long total = 0;
            foreach (var entry in AreaFinanceKinds.IsIncome(kind) ? incomes : expenses)
                if (entry.Kind == kind) total = AreaFinanceModel.SaturatingAdd(total, entry.Amount);
            return total;
        }
        // Allocates the amounts actually applied to city money in entry order. Only the native PlayerMoney bounds can
        // make them smaller than requested; nothing is ever recorded above the request or below zero.
        public AreaFinanceBatch Resolve(long appliedExpense, long appliedIncome) =>
            new AreaFinanceBatch(Allocate(expenses, appliedExpense), Allocate(incomes, appliedIncome));
        private static List<AreaFinanceEntry> Allocate(List<AreaFinanceEntry> requested, long applied)
        {
            var result = new List<AreaFinanceEntry>();
            long remaining = Math.Max(0, applied);
            foreach (var entry in requested)
            {
                if (remaining == 0) break;
                long amount = Math.Min(remaining, entry.Amount);
                result.Add(new AreaFinanceEntry(entry.AreaStableId, entry.Kind, amount));
                remaining -= amount;
            }
            return result;
        }
        private static long Sum(List<AreaFinanceEntry> entries)
        {
            long total = 0;
            foreach (var entry in entries) total = AreaFinanceModel.SaturatingAdd(total, entry.Amount);
            return total;
        }
    }
    // Requests wait here until the next scheduled application. Taking a batch empties the pending set, so every
    // amount is applied at most once.
    public sealed class AreaFinanceLedger
    {
        private List<AreaFinanceEntry> expenses = new List<AreaFinanceEntry>(), incomes = new List<AreaFinanceEntry>();
        public bool HasPending => expenses.Count > 0 || incomes.Count > 0;
        public bool EnqueueExpense(ulong areaStableId, AreaFinanceKind kind, long amount) =>
            AreaFinanceKinds.IsExpense(kind) && Add(expenses, areaStableId, kind, amount);
        public bool EnqueueIncome(ulong areaStableId, AreaFinanceKind kind, long amount) =>
            AreaFinanceKinds.IsIncome(kind) && Add(incomes, areaStableId, kind, amount);
        public AreaFinanceBatch TakeBatch()
        {
            var batch = new AreaFinanceBatch(expenses, incomes);
            expenses = new List<AreaFinanceEntry>(); incomes = new List<AreaFinanceEntry>();
            return batch;
        }
        public void Clear() { expenses = new List<AreaFinanceEntry>(); incomes = new List<AreaFinanceEntry>(); }
        private static bool Add(List<AreaFinanceEntry> entries, ulong areaStableId, AreaFinanceKind kind, long amount)
        {
            if (amount <= 0) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.AreaStableId != areaStableId || entry.Kind != kind) continue;
                entries[i] = new AreaFinanceEntry(areaStableId, kind, AreaFinanceModel.SaturatingAdd(entry.Amount, amount));
                return true;
            }
            entries.Add(new AreaFinanceEntry(areaStableId, kind, amount));
            return true;
        }
    }
}
