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
        // Amount of the single bucket holding frame (the active hour for the current frame); Sum is unchanged.
        public long CurrentBucket(uint frame)
        {
            long index = frame / bucketFrames;
            int slot = (int)(index % stamps.Length);
            return stamps[slot] == index ? values[slot] : 0;
        }
        public void Clear() { for (int i = 0; i < stamps.Length; i++) { stamps[i] = -1; values[i] = 0; } }
    }
    // Runtime-only hourly trend samples of one Park (Wave A1). Never serialized; a load clears ParkRuntimeStore.
    public sealed class ParkTrendSnapshot
    {
        public const int Capacity = 24;
        public readonly CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries Visitors = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity),
            Quality = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity), Satisfaction = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity),
            Occupancy = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity), AdmissionIncome = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity),
            ManagementExpense = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity);
        // Bucket of the first capture; flow amounts of hours that began before it were only partly observed.
        public long FirstBucket = -1;
    }
}
namespace CS2AreaSystem.Modules.Park
{
    public sealed class ParkLandscapeSnapshot
    {
        public ParkLandscapeInputs Inputs;
        public ParkLandscapeResult Result;
        // Distinct nearby public-transport access points and transport modes (see ParkTransitAccess).
        public int TransitPoints, TransitTypes;
        // Footpath sample points inside or within EntranceRadius of the Park (for entrance connectivity).
        public System.Collections.Generic.List<(float X, float Z)> PathSamples = new System.Collections.Generic.List<(float X, float Z)>();
        public uint ScannedFrame;
        public bool Available;
        // Advisory findings of this scan (at most ParkLandscapeDiagnostics.MaxDiagnostics); never a Quality input.
        public System.Collections.Generic.IReadOnlyList<ParkLandscapeDiagnostic> Diagnostics = System.Array.Empty<ParkLandscapeDiagnostic>();
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
        public ParkQualitySnapshot Quality;
        public uint QualityFrame;
        public int EntranceCount;
        public ParkEntranceEvidence EntranceEvidence;
        public ParkConnectivitySnapshot Connectivity=new ParkConnectivitySnapshot();
        // Visitors: "recent" = the last in-game day.
        public int PeakOccupancy;
        public readonly ParkRollingWindow RecentVisitors = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public readonly ParkRollingWindow RecentTouristVisitors = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public readonly ParkRollingWindow RecentAdmission = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        public readonly ParkRollingWindow RecentExpense = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        // Admission households could not pay (partial/zero collection), last in-game day.
        public readonly ParkRollingWindow RecentUncollected = new ParkRollingWindow(RecentBucketFrames, RecentBuckets);
        // Park-level experienced state used for visit satisfaction samples; Facility comes from the visited building.
        // Neutral until the first Quality evaluation; visit samples are only taken once Quality exists.
        public ParkVisitExperience Experience = new ParkVisitExperience { Facility = 50, Landscape = 50, Comfort = 50, Maintenance = 50, ValueForMoney = 50, Accessibility = 50 };
        public int ActiveVisitors;
        // Finance settlement remainder (runtime only; at most one currency unit is lost across a load).
        public double ExpenseCarry;
        public float ManagementNeedPerDay, ManagementCostPerDay;
        public readonly ParkTrendSnapshot Trends = new ParkTrendSnapshot();
        // Called after a Quality evaluation. Levels (Quality, satisfaction, occupancy) are sampled into the current hour,
        // replacing an earlier sample of the same hour. Flows (visits, admission, expense) are recorded for the previous,
        // completed hour only, and only when that whole hour was observed, so latest and previous are comparable.
        public void RecordTrends(uint frame)
        {
            if (Quality == null) return;
            long bucket = frame / RecentBucketFrames;
            if (Trends.FirstBucket < 0) Trends.FirstBucket = bucket;
            Trends.Quality.Record(bucket, Quality.Quality);
            if (Quality.Satisfaction.Available) Trends.Satisfaction.Record(bucket, Quality.Satisfaction.Value);
            Trends.Occupancy.Record(bucket, Quality.OccupancyRatio * 100.0);
            long completed = bucket - 1;
            if (completed <= Trends.FirstBucket) return;
            uint previousFrame = (uint)(completed * RecentBucketFrames);
            Trends.Visitors.Record(completed, RecentVisitors.CurrentBucket(previousFrame));
            Trends.AdmissionIncome.Record(completed, RecentAdmission.CurrentBucket(previousFrame));
            Trends.ManagementExpense.Record(completed, RecentExpense.CurrentBucket(previousFrame));
        }
    }
    public sealed class ParkRuntimeStore
    {
        public Action<ParkRuntimeSnapshot> Initialize;
        private readonly System.Collections.Generic.Dictionary<ulong, ParkRuntimeSnapshot> parks = new System.Collections.Generic.Dictionary<ulong, ParkRuntimeSnapshot>();
        public System.Collections.Generic.IEnumerable<ParkRuntimeSnapshot> All => parks.Values;
        public int Count => parks.Count;
        public bool TryGet(ulong id, out ParkRuntimeSnapshot snapshot) => parks.TryGetValue(id, out snapshot);
        public ParkRuntimeSnapshot GetOrCreate(ulong id, Unity.Entities.Entity area)
        {
            if (!parks.TryGetValue(id, out var snapshot)) {
                parks[id] = snapshot = new ParkRuntimeSnapshot { StableId = id, Area = area };
                Initialize?.Invoke(snapshot);
            }
            snapshot.Area = area;
            return snapshot;
        }
        public void Remove(ulong id) => parks.Remove(id);
        public void Clear() => parks.Clear();
    }
}
