using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Finance;
using Game;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Phase 2 C5 Park internal events. A start request queues one Area Management expense (AreaFinanceKind.ParkEvent) at
    // half the Park's current daily management cost; the event starts only when Area Finance reports a positive
    // settlement, for a full in-game day from that frame, and only if the Park is still an eligible Active Park.
    // Zero/aborted/cancelled outcomes clear the runtime-only request. Completions are handled by the Area Finance
    // listener, also inside AreaFinanceSaveSystem's flush, so event state is final before serialization. The update
    // expires ended events and refreshes Quality when a Park's event effects switch on or off.
    public partial class ParkEventSystem : GameSystemBase, IAreaFinanceRequestListener
    {
        public const uint UpdateFrames = ParkQualitySystem.UpdateFrames;
        private ParkRuntimeSystem runtime;
        private AreaFinanceSystem finance;
        private ParkFinanceSystem parkFinance;
        private ParkQualitySystem quality;
        private CS2AreaSystem.Core.Diagnostics.AreaFailureLog failures;
        private readonly ParkEventLedger ledger = new ParkEventLedger();
        private readonly List<ParkEventSettlement> paid = new List<ParkEventSettlement>();
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly List<ParkEventHistoryEntry> history = new List<ParkEventHistoryEntry>();
        public ParkEventLedger Ledger => ledger;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            finance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            parkFinance = World.GetOrCreateSystemManaged<ParkFinanceSystem>();
            quality = World.GetOrCreateSystemManaged<ParkQualitySystem>();
            failures = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>().Failures;
            finance.AddRequestListener(this);
        }
        [Preserve]
        protected override void OnDestroy()
        {
            World.GetExistingSystemManaged<AreaFinanceSystem>()?.RemoveRequestListener(this);
            base.OnDestroy();
        }
        // Area Finance cancels unapplied requests on preload (empty completion); anything left is dropped, never replayed.
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) { ledger.Clear(); paid.Clear(); parks.Clear(); }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint frame = runtime.SimulationFrame;
            runtime.CollectParks(parks);
            foreach (var park in parks) Refresh(park, frame);
        }
        // Expires an ended event and re-evaluates Quality when the effects in force differ from the ones it used.
        private void Refresh(ParkAreaRef park, uint frame)
        {
            try
            {
                if (!EntityManager.Exists(park.Area) || !EntityManager.HasComponent<ParkEventState>(park.Area)) return;
                var state = EntityManager.GetComponentData<ParkEventState>(park.Area);
                var expired = ParkEventModel.Expire(state, frame);
                if (expired.ActiveEventId != state.ActiveEventId) EntityManager.SetComponentData(park.Area, expired);
                if (runtime.Store.TryGet(park.StableId, out var snapshot) && snapshot.Quality != null &&
                    ParkEventModel.QualityNeedsRefresh(snapshot, expired, frame, park.State == AreaLifecycleState.Active))
                    quality.Recompute(park);
                failures.Recovered(park.Area, "Park.Event");
            }
            catch (System.Exception error)
            {
                failures.Report(park.Area, "Park.Event", error, "Park event update failed for managed area " + park.Area);
            }
        }
        // Effects for the other Park systems: neutral unless the event module is registered and enabled (a failed event
        // registration never leaves a saved festival applying effects without its expiry/refresh owner).
        internal static ParkEventEffects ReadEffects(World world, EntityManager entities, Entity area, bool parkActive, uint frame)
        {
            var system = world?.GetExistingSystemManaged<ParkEventSystem>();
            if (system == null || !system.Enabled || !entities.Exists(area) || !entities.HasComponent<ParkEventState>(area)) return ParkEventEffects.Neutral;
            return ParkEventModel.Effects(entities.GetComponentData<ParkEventState>(area), frame, parkActive);
        }
        // Completed visits use their event frame, including an interval that expired before dispatch. No save changes:
        // the existing bounded history already holds the settled Festival intervals.
        internal static ParkEventEffects ReadVisitEffects(World world, EntityManager entities, Entity area, bool parkActive, uint frame)
        {
            var current = ReadEffects(world, entities, area, parkActive, frame);
            if (!current.IsNeutral || !parkActive) return current;
            var system = world?.GetExistingSystemManaged<ParkEventSystem>();
            if (system == null || !system.Enabled || !entities.Exists(area) || !entities.HasComponent<ParkEventState>(area) ||
                !entities.HasBuffer<ParkEventHistoryEntry>(area)) return ParkEventEffects.Neutral;
            var entries = entities.GetBuffer<ParkEventHistoryEntry>(area, true);
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var effects = ParkEventModel.HistoricalEffects(entries[i], frame, parkActive);
                if (!effects.IsNeutral) return effects;
            }
            return ParkEventEffects.Neutral;
        }
        // Panel action (main thread, after Core validated the stable ID and live selection).
        internal ParkEventRequestOutcome RequestStart(Entity area, int eventId)
        {
            var inputs = StartInputs(area, eventId, out var parkId);
            if (parkId == 0) return ParkEventRequestOutcome.NoState;
            return ParkEventFinance.TryRequest(inputs, finance.LoadGeneration, parkId, area,
                amount => finance.EnqueueExpense(parkId, AreaFinanceKind.ParkEvent, amount), ledger);
        }
        // The same inputs decide the panel's start button and the action, so the panel never offers a start the action refuses.
        internal ParkEventRequestInputs StartInputs(Entity area, int eventId, out ulong parkId)
        {
            parkId = 0;
            if (!EntityManager.Exists(area) || EntityManager.HasComponent<Game.Common.Deleted>(area) || !EntityManager.HasComponent<ManagedAreaStableId>(area) ||
                !EntityManager.HasComponent<ManagedArea>(area)) return new ParkEventRequestInputs { Available = Enabled, EventId = eventId };
            var managed = EntityManager.GetComponentData<ManagedArea>(area);
            if (managed.TypeId != (int)AreaTypeId.Park) return new ParkEventRequestInputs { Available = Enabled, EventId = eventId };
            parkId = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
            bool hasState = EntityManager.HasComponent<ParkEventState>(area);
            runtime.Store.TryGet(parkId, out var snapshot);
            uint frame = runtime.SimulationFrame;
            var estimate = parkFinance.ReadManagementCost(area, snapshot, frame);
            return new ParkEventRequestInputs {
                Available = Enabled && finance.Started && runtime.Ready, ParkActive = managed.State == AreaLifecycleState.Active, HasState = hasState,
                State = hasState ? EntityManager.GetComponentData<ParkEventState>(area) : default, Frame = frame, EventId = eventId,
                HasPending = ledger.HasPending(parkId),
                CostKnown = estimate.Known, DailyManagementCost = estimate.CostPerDay };
        }
        // Called from AreaFinanceSystem on the main thread, once per request batch (also zero/aborted/cancelled ones).
        public void OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame)
        {
            paid.Clear();
            ParkEventFinance.OnCompleted(ledger, finance.LoadGeneration, requested, settled, paid);
            foreach (var payment in paid) Record(payment, frame, runtime.SimulationFrame);
            paid.Clear();
        }
        // One paid start: starts a still-eligible Park or only accounts the expense; one failure never skips the others.
        private void Record(in ParkEventSettlement payment, uint accountingFrame, uint frame)
        {
            try
            {
                if (runtime.Store.TryGet(payment.AreaId, out var snapshot)) snapshot.RecentExpense.Add(accountingFrame, payment.Paid);
                var area = payment.Area;
                bool exists = EntityManager.Exists(area) && !EntityManager.HasComponent<Game.Common.Deleted>(area) &&
                    EntityManager.HasComponent<ManagedAreaStableId>(area) && EntityManager.GetComponentData<ManagedAreaStableId>(area).Value == payment.AreaId;
                bool hasState = exists && EntityManager.HasComponent<ParkEventState>(area) && EntityManager.HasBuffer<ParkEventHistoryEntry>(area);
                if (!hasState) return;
                bool parkActive = EntityManager.HasComponent<ManagedArea>(area) && EntityManager.GetComponentData<ManagedArea>(area).State == AreaLifecycleState.Active;
                var state = EntityManager.GetComponentData<ParkEventState>(area);
                var buffer = EntityManager.GetBuffer<ParkEventHistoryEntry>(area, true);
                history.Clear();
                for (int i = 0; i < buffer.Length; i++) history.Add(buffer[i]);
                var outcome = ParkEventFinance.Apply(payment, ParkEventFinance.EligibleToStart(exists, parkActive, hasState, state, frame), frame, ref state, history);
                EntityManager.SetComponentData(area, state);
                if (outcome == ParkEventApplyOutcome.Started)
                {
                    var writable = EntityManager.GetBuffer<ParkEventHistoryEntry>(area);
                    writable.Clear();
                    foreach (var entry in history) writable.Add(entry);
                    // Capacity/occupancy reflect the festival at once instead of at the next Quality poll.
                    quality.Recompute(new ParkAreaRef(area, payment.AreaId, AreaLifecycleState.Active));
                }
                failures.Recovered(area, "Park.EventRecord");
            }
            catch (System.Exception error)
            {
                failures.Report(payment.Area, "Park.EventRecord", error, "Park event payment could not be recorded for managed area " + payment.Area + " (paid " + payment.Paid + ")");
            }
        }
    }
}
