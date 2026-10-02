using System;
using System.Collections.Generic;
using Game.Economy;
using Game.Prefabs;
namespace CS2AreaSystem.Modules.Industrial
{
    // Exact vanilla recipe (IndustrialProcessData) of a producer tenant: up to two material inputs and one output, with
    // their integer recipe amounts. An input without a resource or amount is no input (stored as 0/0). Recipes are
    // equal only when every resource and amount is equal; a shared output never merges two recipes.
    public readonly struct IndustrialProcessKey : IEquatable<IndustrialProcessKey>
    {
        public ulong Input1 { get; }
        public int Input1Amount { get; }
        public ulong Input2 { get; }
        public int Input2Amount { get; }
        public ulong Output { get; }
        public int OutputAmount { get; }
        public IndustrialProcessKey(ulong input1, int input1Amount, ulong input2, int input2Amount, ulong output, int outputAmount)
        {
            bool uses1 = input1 != 0 && input1Amount > 0, uses2 = input2 != 0 && input2Amount > 0;
            Input1 = uses1 ? input1 : 0; Input1Amount = uses1 ? input1Amount : 0;
            Input2 = uses2 ? input2 : 0; Input2Amount = uses2 ? input2Amount : 0;
            Output = output; OutputAmount = outputAmount;
        }
        public int InputCount => (Input1 != 0 ? 1 : 0) + (Input2 != 0 ? 1 : 0);
        public bool Equals(IndustrialProcessKey other) => Input1 == other.Input1 && Input1Amount == other.Input1Amount && Input2 == other.Input2 &&
            Input2Amount == other.Input2Amount && Output == other.Output && OutputAmount == other.OutputAmount;
        public override bool Equals(object obj) => obj is IndustrialProcessKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Output.GetHashCode();
                hash = hash * 31 + OutputAmount; hash = hash * 31 + Input1.GetHashCode(); hash = hash * 31 + Input1Amount;
                hash = hash * 31 + Input2.GetHashCode(); hash = hash * 31 + Input2Amount;
                return hash;
            }
        }
        public static bool operator ==(IndustrialProcessKey a, IndustrialProcessKey b) => a.Equals(b);
        public static bool operator !=(IndustrialProcessKey a, IndustrialProcessKey b) => !a.Equals(b);
        // Deterministic order by resource ids, then amounts.
        public static int CompareIds(IndustrialProcessKey a, IndustrialProcessKey b)
        {
            int c = a.Output.CompareTo(b.Output); if (c != 0) return c;
            c = a.Input1.CompareTo(b.Input1); if (c != 0) return c;
            c = a.Input2.CompareTo(b.Input2); if (c != 0) return c;
            c = a.OutputAmount.CompareTo(b.OutputAmount); if (c != 0) return c;
            c = a.Input1Amount.CompareTo(b.Input1Amount); if (c != 0) return c;
            return a.Input2Amount.CompareTo(b.Input2Amount);
        }
    }
    // One producer tenant's process in one observation; ActualPerDay is meaningful only when Measured.
    public readonly struct IndustrialProcessActivity
    {
        public IndustrialProcessKey Process { get; }
        public bool Measured { get; }
        public double ActualPerDay { get; }
        public IndustrialProcessActivity(IndustrialProcessKey process, bool measured, double actualPerDay)
        { Process = process; Measured = measured; ActualPerDay = measured ? actualPerDay : 0; }
    }
    // Area-level process relation: producer companies running the recipe, how many of them had a measured production
    // rate this cycle, and their summed actual output (units/day; unusable facilities contribute 0).
    public sealed class IndustrialProcessSummary
    {
        public IndustrialProcessKey Process;
        public int Companies, MeasuredCompanies;
        public double ActualOutputPerDay;
    }
    public static class IndustrialProcessDiagnostics
    {
        // Copies a valid recipe (output resource present) into the blittable company read; used inside the read job.
        public static bool TryCopy(in IndustrialProcessData process, ref IndustrialCompanyRead company)
        {
            if (process.m_Output.m_Resource == Resource.NoResource) { company.HasProcess = false; return false; }
            company.HasProcess = true;
            company.ProcessInput1 = (ulong)process.m_Input1.m_Resource; company.ProcessInput1Amount = process.m_Input1.m_Amount;
            company.ProcessInput2 = (ulong)process.m_Input2.m_Resource; company.ProcessInput2Amount = process.m_Input2.m_Amount;
            company.ProcessOutput = (ulong)process.m_Output.m_Resource; company.ProcessOutputAmount = process.m_Output.m_Amount;
            return true;
        }
        public static IndustrialProcessKey KeyOf(in IndustrialCompanyRead c) =>
            new IndustrialProcessKey(c.ProcessInput1, c.ProcessInput1Amount, c.ProcessInput2, c.ProcessInput2Amount, c.ProcessOutput, c.ProcessOutputAmount);
        // Groups one facility's processes into the cycle totals (called only for eligible producers).
        public static void Add(Dictionary<IndustrialProcessKey, IndustrialProcessSummary> into, IndustrialProcessActivity[] processes, bool available)
        {
            if (processes == null) return;
            foreach (var p in processes)
            {
                if (!into.TryGetValue(p.Process, out var summary)) into[p.Process] = summary = new IndustrialProcessSummary { Process = p.Process };
                summary.Companies++;
                if (!p.Measured) continue;
                summary.MeasuredCompanies++;
                summary.ActualOutputPerDay += available ? Math.Max(0, p.ActualPerDay) : 0;
            }
        }
        // Descending actual output, then deterministic resource ids.
        public static void Sort(List<IndustrialProcessSummary> processes) =>
            processes.Sort((a, b) => { int c = b.ActualOutputPerDay.CompareTo(a.ActualOutputPerDay); return c != 0 ? c : IndustrialProcessKey.CompareIds(a.Process, b.Process); });
    }
}
