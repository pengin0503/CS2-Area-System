using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Finance
{
    // Told about the amounts actually applied to city money, once per applied batch (e.g. Park lifetime counters).
    public interface IAreaFinanceListener
    {
        void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame);
    }
    // The state behind AreaFinanceSystem, free of ECS so it can be tested: queued requests, the one batch being
    // applied, the settled accounts and the listeners. Only BeginApply/CompleteApply move a batch towards city money;
    // RecordSettledIncome records a household transfer that already credited the city and never queues a credit.
    public sealed class AreaFinanceRuntime
    {
        private readonly AreaFinanceLedger ledger = new AreaFinanceLedger();
        private readonly AreaFinanceAccounts accounts = new AreaFinanceAccounts();
        private readonly List<IAreaFinanceListener> listeners = new List<IAreaFinanceListener>();
        private readonly Action<Exception> reportListenerError;
        private uint inFlightFrame;
        public AreaFinanceRuntime(Action<Exception> reportListenerError) =>
            this.reportListenerError = reportListenerError ?? throw new ArgumentNullException(nameof(reportListenerError));
        public bool HasQueued => ledger.HasPending;
        public bool HasInFlight => InFlight != null;
        public AreaFinanceBatch InFlight { get; private set; }
        public void AddListener(IAreaFinanceListener listener)
        {
            if (listener != null && !listeners.Contains(listener)) listeners.Add(listener);
        }
        public void Start(uint frame) => accounts.Start(frame);
        public void Reset()
        {
            ledger.Clear();
            accounts.Clear();
            InFlight = null;
        }
        public bool EnqueueExpense(ulong areaStableId, AreaFinanceKind kind, long amount) => ledger.EnqueueExpense(areaStableId, kind, amount);
        public bool EnqueueIncome(ulong areaStableId, AreaFinanceKind kind, long amount) => ledger.EnqueueIncome(areaStableId, kind, amount);
        public bool RecordSettledIncome(uint frame, ulong areaStableId, AreaFinanceKind kind, long amount)
        {
            if (!AreaFinanceKinds.IsIncome(kind) || amount <= 0) return false;
            accounts.Record(frame, kind, amount);
            return true;
        }
        // Takes the queued requests for application, or null when there is nothing to apply or a batch still awaits its result.
        public AreaFinanceBatch BeginApply(uint frame)
        {
            if (InFlight != null || !ledger.HasPending) return null;
            InFlight = ledger.TakeBatch();
            inFlightFrame = frame;
            return InFlight;
        }
        // Nothing reached city money; the batch is dropped without a record.
        public void AbortApply() => InFlight = null;
        // Records what was actually applied and reports it. Returns null when no batch was in flight.
        public AreaFinanceBatch CompleteApply(long appliedExpense, long appliedIncome)
        {
            if (InFlight == null) return null;
            var settled = InFlight.Resolve(appliedExpense, appliedIncome);
            InFlight = null;
            foreach (var entry in settled.Expenses) accounts.Record(inFlightFrame, entry.Kind, entry.Amount);
            foreach (var entry in settled.Incomes) accounts.Record(inFlightFrame, entry.Kind, entry.Amount);
            foreach (var listener in listeners)
            {
                try { listener.OnAreaFinanceSettled(settled, inFlightFrame); }
                catch (Exception error) { reportListenerError(error); }
            }
            return settled;
        }
        public long AreaManagement(uint frame) => accounts.AreaManagement(frame);
        public long AreaServiceFees(uint frame) => accounts.AreaServiceFees(frame);
        public long Settled(AreaFinanceKind kind, uint frame) => accounts.Settled(kind, frame);
        public uint CoveredFrames(uint frame) => accounts.CoveredFrames(frame);
    }
}
