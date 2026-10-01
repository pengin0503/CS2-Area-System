using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.UI
{
    // One revision for everything the Area UI derives from managed Areas: the Area list, its selection lookup and the Core
    // part of the selected-info view (#56 #70 #74). It advances on native change evidence (change filters on ManagedArea,
    // ManagedAreaProgress and AreaAnchorLink; Created/Updated/Deleted/BatchesUpdated tags, BatchesUpdated being what a
    // rename adds), on a new load generation, a quarantine or anchor-service change, on an explicit Invalidate after our
    // own actions, and every FallbackFrames observed frames as a low-cadence reconciliation for anything without evidence
    // (for example a renamed anchor building). Observation runs at most once per frame, so every consumer of one frame
    // sees the same value.
    public sealed class AreaPresentationRevision
    {
        public const int DefaultFallbackFrames = 64;
        public int FallbackFrames { get; }
        public ulong Value { get; private set; } = 1;
        public long Advances { get; private set; }
        private bool observed;
        private int lastFrame, sinceAdvance, loadGeneration, quarantineRevision;
        private long anchorRevision;
        public AreaPresentationRevision(int fallbackFrames = DefaultFallbackFrames) => FallbackFrames = fallbackFrames > 0 ? fallbackFrames : DefaultFallbackFrames;
        public bool NeedsObservation(int frame) => !observed || frame != lastFrame;
        public void Observe(int frame, bool evidence, int loadGeneration, int quarantineRevision, long anchorRevision)
        {
            if (!NeedsObservation(frame)) return;
            bool changed = !observed || evidence || loadGeneration != this.loadGeneration || quarantineRevision != this.quarantineRevision ||
                anchorRevision != this.anchorRevision || ++sinceAdvance >= FallbackFrames;
            observed = true; lastFrame = frame;
            this.loadGeneration = loadGeneration; this.quarantineRevision = quarantineRevision; this.anchorRevision = anchorRevision;
            if (changed) Advance();
        }
        public void Invalidate() => Advance();
        private void Advance() { Value = Value == ulong.MaxValue ? 1 : Value + 1; sinceAdvance = 0; Advances++; }
    }
    // A single cached value for one key that includes the presentation revision; any key change rebuilds.
    public sealed class AreaPresentationCache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private bool has;
        private TKey key;
        private TValue value;
        public long Builds { get; private set; }
        public long Hits { get; private set; }
        public bool TryGet(TKey key, out TValue value)
        {
            if (has && this.key.Equals(key)) { Hits++; value = this.value; return true; }
            value = default;
            return false;
        }
        public TValue Set(TKey key, TValue value)
        {
            has = true; this.key = key; this.value = value; Builds++;
            return value;
        }
        public void Clear() { has = false; key = default; value = default; }
    }
    // Everything the Core part of the selected-info view depends on besides the revision.
    public readonly struct AreaCoreViewKey : IEquatable<AreaCoreViewKey>
    {
        public readonly Entity Area;
        public readonly ulong Revision;
        public readonly bool PendingAnchor, ConfirmDelete;
        public AreaCoreViewKey(Entity area, ulong revision, bool pendingAnchor, bool confirmDelete)
        { Area = area; Revision = revision; PendingAnchor = pendingAnchor; ConfirmDelete = confirmDelete; }
        public bool Equals(AreaCoreViewKey other) =>
            Area == other.Area && Revision == other.Revision && PendingAnchor == other.PendingAnchor && ConfirmDelete == other.ConfirmDelete;
        public override bool Equals(object obj) => obj is AreaCoreViewKey other && Equals(other);
        public override int GetHashCode() => unchecked((Area.GetHashCode() * 397 ^ Revision.GetHashCode()) * 4 + (PendingAnchor ? 2 : 0) + (ConfirmDelete ? 1 : 0));
    }
    // The list rows and the selection lookup built from one pass at one revision.
    public sealed class AreaListSnapshot
    {
        public List<AreaListEntry> Entries;
        public List<AreaUICandidate> Candidates;
    }
}
