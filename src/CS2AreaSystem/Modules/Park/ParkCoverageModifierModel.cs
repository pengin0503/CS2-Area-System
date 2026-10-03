using System;
using System.Collections.Generic;
using Game.Buildings;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Phase 2 C6 Park coverage operating modifier (pure part). The factor scales the native ModifiedServiceCoverage
    // capacity/magnitude of primary-owned Park buildings; m_Range is never touched (see park-coverage-modifier-contract.md).
    public static class ParkCoverageModifierModel
    {
        public const float MinFactor = 0.90f, MaxFactor = 1.10f, BudgetSlope = 0.20f;
        // clamp(1 + 0.20 × (normalized budget × effective management funding − 1), 0.90, 1.10); invalid inputs are neutral defaults.
        public static float Factor(float budgetMultiplier, double fundingMultiplier)
        {
            float effective = ParkFinanceModel.ComputeManagementCoverage(budgetMultiplier, fundingMultiplier);
            return Math.Min(MaxFactor, Math.Max(MinFactor, 1f + BudgetSlope * (effective - 1f)));
        }
        // Fresh native baseline (contract §4): the building's chunk was last written in ParkAISystem's latest update (its
        // LastSystemVersion), the mod has not handled that update yet (checked by the caller), and its prefab has the
        // ParkData/CoverageData the native job needs to actually write it. Version 0 means "never".
        public static bool IsFresh(uint chunkChangeVersion, uint nativeWriterVersion, bool nativeWriterWritesIt) =>
            nativeWriterWritesIt && nativeWriterVersion != 0 && chunkChangeVersion == nativeWriterVersion;
        public static ModifiedServiceCoverage Apply(in ModifiedServiceCoverage baseline, float factor) =>
            new ModifiedServiceCoverage { m_Range = baseline.m_Range, m_Capacity = baseline.m_Capacity * factor, m_Magnitude = baseline.m_Magnitude * factor };
        // Bitwise: "still our write" must not treat a different native value as equal.
        public static bool Same(in ModifiedServiceCoverage a, in ModifiedServiceCoverage b) =>
            Bits(a.m_Range) == Bits(b.m_Range) &&
            Bits(a.m_Capacity) == Bits(b.m_Capacity) &&
            Bits(a.m_Magnitude) == Bits(b.m_Magnitude);
        // BitConverter.SingleToInt32Bits is not available on the game's .NET Framework profile.
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatBits { [System.Runtime.InteropServices.FieldOffset(0)] public float Float; [System.Runtime.InteropServices.FieldOffset(0)] public int Int; }
        private static int Bits(float value) => new FloatBits { Float = value }.Int;
    }
    // Runtime-only record of the buildings this modifier wrote: the native baseline and the value written over it.
    // Nothing here is saved; a write happens only from a verified fresh native baseline, never from a previous write.
    public sealed class ParkCoverageTracker
    {
        private struct Entry { public ModifiedServiceCoverage Baseline, Written; public ulong AreaId; }
        private readonly Dictionary<Entity, Entry> entries = new Dictionary<Entity, Entry>();
        private readonly List<Entity> scratch = new List<Entity>();
        public int Count => entries.Count;
        public bool IsTracked(Entity building) => entries.ContainsKey(building);
        public int CountFor(ulong areaId) { int n = 0; foreach (var entry in entries.Values) if (entry.AreaId == areaId) n++; return n; }
        // A fresh native value for a building: returns the value to write, or false (no owner or a neutral factor) and forgets it.
        public bool OnFreshBaseline(Entity building, in ModifiedServiceCoverage baseline, ulong ownerAreaId, float factor, out ModifiedServiceCoverage write)
        {
            write = baseline;
            if (ownerAreaId == 0 || factor == 1f || float.IsNaN(factor)) { entries.Remove(building); return false; }
            write = ParkCoverageModifierModel.Apply(baseline, factor);
            entries[building] = new Entry { Baseline = baseline, Written = write, AreaId = ownerAreaId };
            return true;
        }
        // Returns the baseline to write back only while the current value is still this modifier's write; always forgets it.
        public bool TryRestore(Entity building, in ModifiedServiceCoverage current, out ModifiedServiceCoverage baseline)
        {
            baseline = current;
            if (!entries.TryGetValue(building, out var entry)) return false;
            entries.Remove(building);
            if (!ParkCoverageModifierModel.Same(current, entry.Written)) return false;
            baseline = entry.Baseline;
            return true;
        }
        // read returns null for a building that no longer exists or no longer carries the component.
        public void RestoreAll(Func<Entity, ModifiedServiceCoverage?> read, Action<Entity, ModifiedServiceCoverage> write)
        {
            scratch.Clear();
            scratch.AddRange(entries.Keys);
            foreach (var building in scratch)
            {
                var current = read(building);
                if (current == null) { entries.Remove(building); continue; }
                if (TryRestore(building, current.Value, out var baseline)) write(building, baseline);
            }
            scratch.Clear();
        }
        // Buildings no longer handled by any Park this pass: restored while still ours, otherwise forgotten.
        public void RestoreUnvisited(ICollection<Entity> visited, Func<Entity, ModifiedServiceCoverage?> read, Action<Entity, ModifiedServiceCoverage> write)
        {
            scratch.Clear();
            foreach (var building in entries.Keys) if (!visited.Contains(building)) scratch.Add(building);
            foreach (var building in scratch)
            {
                var current = read(building);
                if (current == null) { entries.Remove(building); continue; }
                if (TryRestore(building, current.Value, out var baseline)) write(building, baseline);
            }
            scratch.Clear();
        }
        public void Clear() => entries.Clear();
    }
}
