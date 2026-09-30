using System;
namespace CS2AreaSystem.Modules.Park
{
    // Fixed-size rolling sum over simulation frames (runtime-only "recent" statistics).
    public sealed class ParkRollingWindow
    {
        private readonly uint bucketFrames;
        private readonly long[] stamps, values;
        public ParkRollingWindow(uint bucketFrames, int buckets)
        {
            if (bucketFrames == 0 || buckets <= 0) throw new ArgumentOutOfRangeException(nameof(buckets));
            this.bucketFrames = bucketFrames;
            stamps = new long[buckets]; values = new long[buckets];
            for (int i = 0; i < buckets; i++) stamps[i] = -1;
        }
        public void Add(uint frame, long amount)
        {
            long index = frame / bucketFrames;
            int slot = (int)(index % stamps.Length);
            if (stamps[slot] != index) { stamps[slot] = index; values[slot] = 0; }
            values[slot] += amount;
        }
        public long Sum(uint frame)
        {
            long current = frame / bucketFrames, sum = 0;
            for (int i = 0; i < stamps.Length; i++)
                if (stamps[i] > current - stamps.Length && stamps[i] <= current) sum += values[i];
            return sum;
        }
        public void Clear() { for (int i = 0; i < stamps.Length; i++) { stamps[i] = -1; values[i] = 0; } }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    public sealed class ParkLandscapeSnapshot
    {
        public ParkLandscapeInputs Inputs;
        public ParkLandscapeResult Result;
        public int TransitStops;
        public uint ScannedFrame;
        public bool Available;
    }
    // Derived, runtime-only view of one Park. Rebuilt after load; never serialized.
    public sealed class ParkRuntimeSnapshot
    {
        public const uint RecentBucketFrames = ParkFinanceModel.FramesPerDay / 24;
        public const int RecentBuckets = 24;
        public ulong StableId;
        public Unity.Entities.Entity Area;
        public float AreaSquareMeters;
        public ParkLandscapeSnapshot Landscape = new ParkLandscapeSnapshot();
        public ParkFacilitySnapshot Facilities;
        // Visitors: "recent" = the last in-game day.
        public int PeakOccupancy;
        public readonly ParkRollingWindow RecentVisitors = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public readonly ParkRollingWindow RecentTouristVisitors = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public readonly ParkRollingWindow RecentAdmission = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public readonly ParkRollingWindow RecentExpense = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public long UncollectedAdmissionRecent;
        // Finance settlement remainder (runtime only; at most one currency unit is lost across a load).
        public double ExpenseCarry;
    }
    public sealed class ParkRuntimeStore
    {
        private readonly System.Collections.Generic.Dictionary<ulong, ParkRuntimeSnapshot> parks = new System.Collections.Generic.Dictionary<ulong, ParkRuntimeSnapshot>();
        public System.Collections.Generic.IEnumerable<ParkRuntimeSnapshot> All => parks.Values;
        public int Count => parks.Count;
        public bool TryGet(ulong id, out ParkRuntimeSnapshot snapshot) => parks.TryGetValue(id, out snapshot);
        public ParkRuntimeSnapshot GetOrCreate(ulong id, Unity.Entities.Entity area)
        {
            if (!parks.TryGetValue(id, out var snapshot)) parks[id] = snapshot = new ParkRuntimeSnapshot { StableId = id };
            snapshot.Area = area;
            return snapshot;
        }
        public void Remove(ulong id) => parks.Remove(id);
        public void Clear() => parks.Clear();
    }
}
