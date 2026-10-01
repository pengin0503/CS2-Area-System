using System;
namespace CS2AreaSystem.Core.Areas
{
    public sealed class AreaStableIdAllocator
    {
        private ulong next;
        private bool exhausted;
        public AreaStableIdAllocator(ulong nextValue = 1)
        {
            if (nextValue == 0) throw new ArgumentOutOfRangeException(nameof(nextValue));
            next = nextValue;
        }
        public ulong Allocate()
        {
            if (exhausted) throw new OverflowException("Area stable IDs exhausted.");
            ulong value = next;
            if (value == ulong.MaxValue) exhausted = true; else next++;
            return value;
        }
        // A new load generation: IDs are unique within one save, never across cities.
        public void Reset() { next = 1; exhausted = false; }
        public void ObserveExisting(ulong value)
        {
            if (value == ulong.MaxValue) exhausted = true;
            else if (value >= next) next = value + 1;
        }
    }
}
