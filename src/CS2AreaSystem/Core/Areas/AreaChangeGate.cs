namespace CS2AreaSystem.Core.Areas
{
    // Decides when data derived from managed Areas must be rebuilt (#61): on native change evidence the caller collects
    // (change filters, Created/Updated/Deleted tags), a new load generation, a quarantine change, an explicit
    // invalidation, or after FallbackUpdates updates without any of these, as a low-frequency reconciliation in case
    // evidence was missed. Unchanged updates only count a skip. A rebuild that throws never calls Refreshed, so the next
    // update rebuilds again.
    public sealed class AreaChangeGate
    {
        public const int DefaultFallbackUpdates = 256;
        public int FallbackUpdates { get; }
        private bool invalid = true;
        private int loadGeneration, quarantineRevision, sinceRefresh;
        public long Refreshes { get; private set; }
        public long Skips { get; private set; }
        public AreaChangeGate(int fallbackUpdates = DefaultFallbackUpdates) => FallbackUpdates = fallbackUpdates > 0 ? fallbackUpdates : DefaultFallbackUpdates;
        public void Invalidate() => invalid = true;
        public bool ShouldRefresh(bool evidence, int loadGeneration, int quarantineRevision)
        {
            if (invalid || evidence || loadGeneration != this.loadGeneration || quarantineRevision != this.quarantineRevision) return true;
            if (++sinceRefresh >= FallbackUpdates) return true;
            Skips++;
            return false;
        }
        public void Refreshed(int loadGeneration, int quarantineRevision)
        {
            invalid = false;
            this.loadGeneration = loadGeneration;
            this.quarantineRevision = quarantineRevision;
            sinceRefresh = 0;
            Refreshes++;
        }
    }
}
