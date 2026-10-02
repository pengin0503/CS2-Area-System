using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Diagnostics
{
    public readonly struct RuntimeTrendSample
    {
        public long Bucket { get; }
        public double Value { get; }
        public RuntimeTrendSample(long bucket, double value) { Bucket = bucket; Value = value; }
    }
    // Fixed-capacity, runtime-only time series (Wave A1): one sample per bucket, oldest evicted at capacity. Never
    // serialized; owners clear it at load generation. Record/TryLatest/TryPrevious do not allocate.
    public sealed class RuntimeTrendSeries
    {
        private readonly RuntimeTrendSample[] samples;
        // Index of the oldest sample in the ring.
        private int start;
        public int Count { get; private set; }
        public int Capacity => samples.Length;
        public RuntimeTrendSeries(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            samples = new RuntimeTrendSample[capacity];
        }
        private int Slot(int offset) => (start + offset) % samples.Length;
        // The same bucket replaces its value; a newer bucket appends; an older bucket or a non-finite value is ignored.
        public void Record(long bucket, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            if (Count > 0)
            {
                int last = Slot(Count - 1);
                long latest = samples[last].Bucket;
                if (bucket < latest) return;
                if (bucket == latest) { samples[last] = new RuntimeTrendSample(bucket, value); return; }
            }
            if (Count < samples.Length) { samples[Slot(Count)] = new RuntimeTrendSample(bucket, value); Count++; return; }
            samples[start] = new RuntimeTrendSample(bucket, value);
            start = (start + 1) % samples.Length;
        }
        public bool TryLatest(out RuntimeTrendSample sample)
        {
            if (Count == 0) { sample = default; return false; }
            sample = samples[Slot(Count - 1)];
            return true;
        }
        public bool TryPrevious(out RuntimeTrendSample sample)
        {
            if (Count < 2) { sample = default; return false; }
            sample = samples[Slot(Count - 2)];
            return true;
        }
        // Replaces the caller's list contents with the samples, oldest -> newest.
        public int CopyTo(List<RuntimeTrendSample> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int i = 0; i < Count; i++) into.Add(samples[Slot(i)]);
            return Count;
        }
        public void Clear() { start = 0; Count = 0; }
    }
}
