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
