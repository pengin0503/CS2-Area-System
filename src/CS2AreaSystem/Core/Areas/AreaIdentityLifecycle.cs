using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.Areas
{
    [Flags]
    public enum AreaQuarantineReason : byte { None = 0, ZeroStableId = 1, DuplicateStableId = 2, InvalidSavedState = 4, NativeGeometryUnavailable = 8 }
    public readonly struct AreaIdentityRecord
    {
        public Entity Area { get; }
        public ulong StableId { get; }
        public AreaIdentityRecord(Entity area, ulong stableId) { Area = area; StableId = stableId; }
    }
    public readonly struct AreaQuarantineEntry
    {
        public Entity Area { get; }
        public ulong StableId { get; }
        public AreaQuarantineReason Reason { get; }
        public AreaQuarantineEntry(Entity area, ulong stableId, AreaQuarantineReason reason) { Area = area; StableId = stableId; Reason = reason; }
    }
    // Managed Areas whose saved data cannot be interpreted safely in this load. They keep their saved components unchanged
    // (no renumbering, no repair) and are skipped by membership, activation, anchors, policies, finance producers and module
    // runtimes; the UI shows them with only Edit and Delete. Entity-keyed (a recycled index has another version) and reset
    // with every load generation. Revision changes whenever the set changes, for consumers that cache derived data.
    public sealed class AreaQuarantine
    {
        private readonly Dictionary<Entity, AreaQuarantineReason> entries = new Dictionary<Entity, AreaQuarantineReason>();
        public int Revision { get; private set; }
        public int Count => entries.Count;
        public bool Contains(Entity area) => entries.ContainsKey(area);
        public AreaQuarantineReason ReasonOf(Entity area) => entries.TryGetValue(area, out var reason) ? reason : AreaQuarantineReason.None;
        // True when a reason is new for the area, so each problem is reported once per load.
        public bool Add(Entity area, AreaQuarantineReason reason)
        {
            if (reason == AreaQuarantineReason.None) return false;
            var current = ReasonOf(area);
            if ((current & reason) == reason) return false;
            entries[area] = current | reason;
            Revision++;
            return true;
        }
        public void Clear()
        {
            if (entries.Count == 0) return;
            entries.Clear();
            Revision++;
        }
    }
    // The stable-ID lifecycle of one load generation: BeginLoad resets the allocator and the quarantine, Observe reads the
    // live saved IDs once (high-water mark) and quarantines every Area whose ID is 0 or shared. A shared ID quarantines
    // every Area carrying it: which one owns the ID's saved relations cannot be decided from the save.
    public sealed class AreaIdentityLifecycle
    {
        public AreaStableIdAllocator StableIds { get; } = new AreaStableIdAllocator();
        public AreaQuarantine Quarantine { get; } = new AreaQuarantine();
        public int LoadGeneration { get; private set; }
        public bool Observed { get; private set; }
        private readonly Dictionary<ulong, int> counts = new Dictionary<ulong, int>();
        public void BeginLoad()
        {
            LoadGeneration++;
            StableIds.Reset();
            Quarantine.Clear();
            counts.Clear();
            Observed = false;
        }
        // Returns the Areas newly quarantined by this observation.
        public List<AreaQuarantineEntry> Observe(IReadOnlyList<AreaIdentityRecord> records)
        {
            var quarantined = new List<AreaQuarantineEntry>();
            counts.Clear();
            foreach (var record in records)
            {
                if (record.StableId == 0) continue;
                StableIds.ObserveExisting(record.StableId);
                counts[record.StableId] = counts.TryGetValue(record.StableId, out var n) ? n + 1 : 1;
            }
            foreach (var record in records)
            {
                var reason = record.StableId == 0 ? AreaQuarantineReason.ZeroStableId :
                    counts[record.StableId] > 1 ? AreaQuarantineReason.DuplicateStableId : AreaQuarantineReason.None;
                if (Quarantine.Add(record.Area, reason)) quarantined.Add(new AreaQuarantineEntry(record.Area, record.StableId, reason));
            }
            counts.Clear();
            Observed = true;
            return quarantined;
        }
    }
}
