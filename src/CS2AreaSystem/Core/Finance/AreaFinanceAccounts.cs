using System.Collections.Generic;
namespace CS2AreaSystem.Core.Finance
{
    // Settled (actually applied) Area amounts over the trailing in-game day, for the budget presentation (Task 9).
    // Runtime-only: it starts empty after a load, and CoveredFrames says how much of the day has been observed.
    public sealed class AreaFinanceAccounts
    {
        private readonly struct Settlement { public readonly uint Frame; public readonly AreaFinanceKind Kind; public readonly long Amount;
            public Settlement(uint frame, AreaFinanceKind kind, long amount) { Frame = frame; Kind = kind; Amount = amount; } }
        // Oldest first from head. Amounts of one frame and kind share a record (Park admission is recorded per visit),
        // so the list is bounded by the frames of the trailing day, not by visits.
        private readonly List<Settlement> records = new List<Settlement>();
        private int head;
        private bool started;
        private uint startFrame;
        // Stored settlement records (diagnostic; bounded by distinct frames and kinds within the trailing day).
        public int RecordCount => records.Count - head;
        public void Start(uint frame) { started = true; startFrame = frame; }
        public void Clear() { records.Clear(); head = 0; started = false; startFrame = 0; }
        public void Record(uint frame, AreaFinanceKind kind, long amount)
        {
            if (amount <= 0 || !(AreaFinanceKinds.IsExpense(kind) || AreaFinanceKinds.IsIncome(kind))) return;
            for (int i = records.Count - 1; i >= head && records[i].Frame == frame; i--)
            {
                if (records[i].Kind != kind) continue;
                records[i] = new Settlement(frame, kind, AreaFinanceModel.SaturatingAdd(records[i].Amount, amount));
                return;
            }
            records.Add(new Settlement(frame, kind, amount));
        }
        public uint CoveredFrames(uint frame) => !started ? 0u : System.Math.Min(AreaFinanceModel.FramesPerDay, unchecked(frame - startFrame));
        public long Settled(AreaFinanceKind kind, uint frame)
        {
            Prune(frame);
            long total = 0;
            for (int i = head; i < records.Count; i++) if (records[i].Kind == kind) total = AreaFinanceModel.SaturatingAdd(total, records[i].Amount);
            return total;
        }
        public long AreaManagement(uint frame) => AreaFinanceModel.SaturatingAdd(AreaFinanceModel.SaturatingAdd(
            Settled(AreaFinanceKind.PolicyUpkeep, frame), Settled(AreaFinanceKind.ParkManagement, frame)), Settled(AreaFinanceKind.IndustrialSupport, frame));
        public long AreaServiceFees(uint frame) => Settled(AreaFinanceKind.ParkAdmission, frame);
        // Records arrive in frame order; unsigned distance keeps the window correct across frame-counter wrap.
        private void Prune(uint frame)
        {
            while (head < records.Count && unchecked(frame - records[head].Frame) >= AreaFinanceModel.FramesPerDay) head++;
            if (head == records.Count) { records.Clear(); head = 0; }
            else if (head > 1024 && head * 2 > records.Count) { records.RemoveRange(0, head); head = 0; }
        }
    }
}
