using System.Collections.Generic;
namespace CS2AreaSystem.Core.Finance
{
    // Settled (actually applied) Area amounts over the trailing in-game day, for the budget presentation (Task 9).
    // Runtime-only: it starts empty after a load, and CoveredFrames says how much of the day has been observed.
    public sealed class AreaFinanceAccounts
    {
        private readonly struct Settlement { public readonly uint Frame; public readonly AreaFinanceKind Kind; public readonly long Amount;
            public Settlement(uint frame, AreaFinanceKind kind, long amount) { Frame = frame; Kind = kind; Amount = amount; } }
        private readonly Queue<Settlement> records = new Queue<Settlement>();
        private bool started;
        private uint startFrame;
        public void Start(uint frame) { started = true; startFrame = frame; }
        public void Clear() { records.Clear(); started = false; startFrame = 0; }
        public void Record(uint frame, AreaFinanceKind kind, long amount)
        {
            if (amount <= 0 || !(AreaFinanceKinds.IsExpense(kind) || AreaFinanceKinds.IsIncome(kind))) return;
            records.Enqueue(new Settlement(frame, kind, amount));
        }
        public uint CoveredFrames(uint frame) => !started ? 0u : System.Math.Min(AreaFinanceModel.FramesPerDay, unchecked(frame - startFrame));
        public long Settled(AreaFinanceKind kind, uint frame)
        {
            Prune(frame);
            long total = 0;
            foreach (var record in records) if (record.Kind == kind) total = AreaFinanceModel.SaturatingAdd(total, record.Amount);
            return total;
        }
        public long AreaManagement(uint frame) =>
            AreaFinanceModel.SaturatingAdd(Settled(AreaFinanceKind.PolicyUpkeep, frame), Settled(AreaFinanceKind.ParkManagement, frame));
        public long AreaServiceFees(uint frame) => Settled(AreaFinanceKind.ParkAdmission, frame);
        // Records arrive in frame order; unsigned distance keeps the window correct across frame-counter wrap.
        private void Prune(uint frame)
        {
            while (records.Count > 0 && unchecked(frame - records.Peek().Frame) >= AreaFinanceModel.FramesPerDay) records.Dequeue();
        }
    }
}
