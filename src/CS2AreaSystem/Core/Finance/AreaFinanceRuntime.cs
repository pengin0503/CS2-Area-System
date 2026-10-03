using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Finance
{
    // Told about the amounts actually applied to city money, once per applied batch (e.g. Park lifetime counters).
    public interface IAreaFinanceListener
    {
        void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame);
    }
    // Told once about every request batch that left the queue, whatever happened to it: applied, partly or not at all
    // applied (settled omits zero entries), aborted before reaching city money, or cancelled by a reset. Requested
    // identifies every Area/kind pair so module pending state (C4 support, C5 events) can always be cleared.
    public interface IAreaFinanceRequestListener
    {
        void OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame);
    }
    // The state behind AreaFinanceSystem, free of ECS so it can be tested: queued requests, the one batch being
    // applied, the settled accounts and the listeners. Only BeginApply/CompleteApply move a batch towards city money;
    // RecordSettledIncome records a household transfer that already credited the city and never queues a credit.
    public sealed class AreaFinanceRuntime
    {
        private readonly AreaFinanceLedger ledger = new AreaFinanceLedger();
        private readonly AreaFinanceAccounts accounts = new AreaFinanceAccounts();
        private readonly ListenerSet<IAreaFinanceListener> listeners = new ListenerSet<IAreaFinanceListener>();
        private readonly ListenerSet<IAreaFinanceRequestListener> requestListeners = new ListenerSet<IAreaFinanceRequestListener>();
        private readonly Action<Exception> reportListenerError;
        private uint inFlightFrame, lastFrame;
        public AreaFinanceRuntime(Action<Exception> reportListenerError) =>
            this.reportListenerError = reportListenerError ?? throw new ArgumentNullException(nameof(reportListenerError));
        public bool HasQueued => ledger.HasPending;
        public bool HasInFlight => InFlight != null;
        public AreaFinanceBatch InFlight { get; private set; }
        public bool AddListener(IAreaFinanceListener listener) => listeners.Add(listener);
        public bool RemoveListener(IAreaFinanceListener listener) => listeners.Remove(listener);
        public bool AddRequestListener(IAreaFinanceRequestListener listener) => requestListeners.Add(listener);
        public bool RemoveRequestListener(IAreaFinanceRequestListener listener) => requestListeners.Remove(listener);
        public void Start(uint frame) { accounts.Start(frame); lastFrame = frame; }
        // A new city: queued requests are cancelled without a charge (reported with an empty settlement), an in-flight
        // batch whose job never reported is dropped the same way. Callers record an applied job before resetting.
        public void Reset()
        {
            var cancelledInFlight = InFlight;
            InFlight = null;
            var cancelledQueued = ledger.HasPending ? ledger.TakeBatch() : null;
            ledger.Clear();
            accounts.Clear();
            if (cancelledInFlight != null) NotifyCompleted(cancelledInFlight, AreaFinanceBatch.Empty, inFlightFrame);
            if (cancelledQueued != null) NotifyCompleted(cancelledQueued, AreaFinanceBatch.Empty, lastFrame);
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
            inFlightFrame = lastFrame = frame;
            return InFlight;
        }
        // Nothing reached city money; the batch is dropped without a record and reported as completed with nothing settled.
        public void AbortApply()
        {
            var aborted = InFlight;
            InFlight = null;
            if (aborted != null) NotifyCompleted(aborted, AreaFinanceBatch.Empty, inFlightFrame);
        }
        // Records what was actually applied and reports it. Returns null when no batch was in flight.
        public AreaFinanceBatch CompleteApply(long appliedExpense, long appliedIncome)
        {
            if (InFlight == null) return null;
            var settled = InFlight.Resolve(appliedExpense, appliedIncome);
            var requested = InFlight;
            InFlight = null;
            foreach (var entry in settled.Expenses) accounts.Record(inFlightFrame, entry.Kind, entry.Amount);
            foreach (var entry in settled.Incomes) accounts.Record(inFlightFrame, entry.Kind, entry.Amount);
            foreach (var listener in listeners.Items)
            {
                try { listener.OnAreaFinanceSettled(settled, inFlightFrame); }
                catch (Exception error) { reportListenerError(error); }
            }
            NotifyCompleted(requested, settled, inFlightFrame);
            return settled;
        }
        private void NotifyCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame)
        {
            foreach (var listener in requestListeners.Items)
            {
                try { listener.OnAreaFinanceCompleted(requested, settled, frame); }
                catch (Exception error) { reportListenerError(error); }
            }
        }
        public long AreaManagement(uint frame) => accounts.AreaManagement(frame);
        public long AreaServiceFees(uint frame) => accounts.AreaServiceFees(frame);
        public long Settled(AreaFinanceKind kind, uint frame) => accounts.Settled(kind, frame);
        public uint CoveredFrames(uint frame) => accounts.CoveredFrames(frame);
    }
}
