using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
using Game;
using Game.Economy;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Phase 2 C4 Industrial support payments. Once per in-game day and Area, the complete roster of companies resolved to
    // the Area by the latest Industrial observation cycle is revalidated against B2 ownership and one Area Management
    // expense (receivers × per-company setting) is queued in Area Finance. The day is saved only after Area Finance
    // accepted the request; the roster and request stay runtime-only, so a load never replays a payment.
    // When Area Finance reports the request completed, only the settled amount is split over the receivers that still
    // resolve to the Area and added to their Resource.Money in a scheduled job (the verified 1.6.2f1
    // EconomyUtils.AddResources path vanilla PayWageSystem uses for company money). The actual increase is credited; the
    // rest is unallocated. Results are recorded on the next update or at the save boundary, after Area Finance's flush.
    public partial class IndustrialSupportSystem : GameSystemBase, IAreaFinanceRequestListener
    {
        public const uint UpdateFrames = AreaFinanceSystem.UpdateFrames;
        private IndustrialRuntimeSystem runtime;
        private AreaFinanceSystem finance;
        private Core.Areas.AreaCoreServicesSystem services;
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private readonly IndustrialSupportLedger ledger = new IndustrialSupportLedger();
        private readonly List<IndustrialSupportCompletion> finished = new List<IndustrialSupportCompletion>();
        private IndustrialSupportCreditBatch crediting;
        private NativeArray<Entity> receivers;
        private NativeArray<int> amounts;
        private NativeArray<long> credited;
        private JobHandle pending;
        private int loadGeneration = -1;
        public IndustrialSupportLedger Ledger => ledger;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>();
            finance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            services = World.GetOrCreateSystemManaged<Core.Areas.AreaCoreServicesSystem>();
            finance.AddRequestListener(this);
        }
        [Preserve]
        protected override void OnDestroy()
        {
            pending.Complete();
            DisposeBuffers();
            World.GetExistingSystemManaged<AreaFinanceSystem>()?.RemoveRequestListener(this);
            base.OnDestroy();
        }
        // 1.6.2f1 calls preload before the old city is cleaned up. A credit job that already ran is recorded on the old
        // Areas; settled-but-uncredited and requested work is cancelled (never resolved against the next city).
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            pending.Complete();
            try { RecordCredits(); }
            finally { DropPending(); loadGeneration = -1; }
        }
        [Preserve]
        protected override void OnUpdate()
        {
            pending.Complete();
            if (!runtime.Ready || !finance.Ready)
            {
                // Only the generation that scheduled the job still owns its entities.
                try { if (loadGeneration == runtime.LoadGeneration) RecordCredits(); }
                finally { DropPending(); }
                return;
            }
            if (loadGeneration != runtime.LoadGeneration) { loadGeneration = runtime.LoadGeneration; DropPending(); }
            RecordCredits();
            if (HasSettled()) ScheduleCredits();
            if (!runtime.AssignmentsReady || !runtime.TryGetClock(out var clock)) return;
            long day = clock.Day(runtime.SimulationFrame);
            runtime.CollectAreas(areas);
            foreach (var area in areas) Schedule(area, day);
        }
        // Called from AreaFinanceSystem on the main thread, once per request batch (also zero/aborted/cancelled ones).
        public void OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame) =>
            IndustrialSupportSettlement.OnCompleted(ledger, finance.LoadGeneration, requested, settled);
        // Runs after AreaFinanceSaveSystem (which applied and completed every queued request) and before the serializer:
        // settled support reaches company money and the Area counters before they are saved.
        internal void FlushForSave() => AreaFinanceSaveBoundary.Flush(runtime.Ready && finance.Ready, loadGeneration, runtime.LoadGeneration,
            CompleteAndRecord, HasSettled, () => EntityManager.CompleteAllTrackedJobs(), ScheduleCredits);
        private void CompleteAndRecord()
        {
            pending.Complete();
            RecordCredits();
        }
        private bool HasSettled()
        {
            foreach (var item in ledger.All) if (item.Stage == IndustrialSupportStage.Settled) return true;
            return false;
        }
        private void Schedule(IndustrialAreaRef area, long day)
        {
            try
            {
                if (!EntityManager.Exists(area.Area) || !EntityManager.HasComponent<IndustrialSupportState>(area.Area)) return;
                var state = EntityManager.GetComponentData<IndustrialSupportState>(area.Area);
                runtime.Store.TryGet(area.StableId, out var snapshot);
                var decision = IndustrialSupportScheduling.Decide(new IndustrialSupportScheduleInputs {
                    Active = area.State == Core.Areas.AreaLifecycleState.Active, State = state, Day = day, HasOutstanding = ledger.HasOutstanding(area.StableId),
                    Roster = snapshot?.Current, SnapshotPaused = snapshot == null || snapshot.Paused,
                    MembershipToken = runtime.CycleSource.MembershipToken(area.StableId), StructureToken = runtime.CycleSource.StructureToken(area.StableId),
                    StillOwned = company => StillOwned(company, area.StableId) });
                if (decision.Outcome != IndustrialSupportScheduleOutcome.Request) return;
                if (IndustrialSupportScheduling.TryRequest(decision, finance.LoadGeneration, area.StableId, area.Area, day,
                    amount => finance.EnqueueExpense(area.StableId, AreaFinanceKind.IndustrialSupport, amount), ref state, ledger))
                    EntityManager.SetComponentData(area.Area, state);
                services.Failures.Recovered(area.Area, "Industrial.Support.Schedule");
            }
            catch (System.Exception error)
            {
                services.Failures.Report(area.Area, "Industrial.Support.Schedule", error, "Industrial support could not be scheduled for managed area " + area.Area + "; nothing was charged");
            }
        }
        // Live B2 resolution of one referenced company; no world query.
        internal bool StillOwned(Entity company, ulong areaId)
        {
            var facts = IndustrialCompanyOwnership.ReadLive(company, EntityManager, services.Semantics, out var building);
            var owner = IndustrialCompanyOwnership.Resolve(company, building, runtime.Membership.GetAreas(building), runtime.Assignments, facts);
            return owner.Valid && owner.AreaStableId == areaId;
        }
        private void ScheduleCredits()
        {
            if (crediting != null) return;
            finished.Clear();
            var batch = IndustrialSupportSettlement.Prepare(ledger, (company, areaId) => StillOwned(company, areaId), finished);
            RecordFinished();
            if (batch.IsEmpty) return;
            DisposeBuffers();
            crediting = batch;
            try
            {
                receivers = new NativeArray<Entity>(batch.Receivers.Count, Allocator.Persistent);
                amounts = new NativeArray<int>(batch.Amounts.Count, Allocator.Persistent);
                credited = new NativeArray<long>(batch.Receivers.Count, Allocator.Persistent);
                for (int i = 0; i < batch.Receivers.Count; i++) { receivers[i] = batch.Receivers[i]; amounts[i] = batch.Amounts[i]; }
                var job = new IndustrialSupportCreditJob { Receivers = receivers, Amounts = amounts, Credited = credited, Resources = GetBufferLookup<Resources>(false) };
                pending = job.Schedule(Dependency);
                Dependency = pending;
            }
            catch (System.Exception error)
            {
                // Not scheduled: nothing reached company money. The settled amount is accounted unallocated, never re-queued.
                pending = default;
                var unscheduled = crediting; crediting = null;
                DisposeBuffers();
                finished.Clear();
                finished.AddRange(IndustrialSupportSettlement.Finish(ledger, unscheduled, null));
                RecordFinished();
                services.Failures.Report(Entity.Null, "Industrial.Support.Credit", error, "Industrial support could not be credited to companies; the settled amount is recorded as unallocated");
            }
        }
        private void RecordCredits()
        {
            if (crediting == null) return;
            var batch = crediting; crediting = null;
            var results = new long[batch.Receivers.Count];
            if (credited.IsCreated) for (int i = 0; i < results.Length && i < credited.Length; i++) results[i] = credited[i];
            DisposeBuffers();
            finished.Clear();
            finished.AddRange(IndustrialSupportSettlement.Finish(ledger, batch, results));
            RecordFinished();
            services.Failures.Recovered(Entity.Null, "Industrial.Support.Credit");
        }
        // Lifetime accounting per Area; one failure never skips the others.
        private void RecordFinished()
        {
            foreach (var done in finished)
            {
                try
                {
                    if (!EntityManager.Exists(done.Area) || !EntityManager.HasComponent<IndustrialSupportState>(done.Area)) continue;
                    var state = EntityManager.GetComponentData<IndustrialSupportState>(done.Area);
                    EntityManager.SetComponentData(done.Area, IndustrialSupportModel.RecordCompletion(state, done.Requested, done.Settled, done.Credited));
                }
                catch (System.Exception error)
                {
                    services.Failures.Report(done.Area, "Industrial.Support.Record", error, "Industrial support accounting failed for managed area " + done.Area + " (settled " + done.Settled + ", credited " + done.Credited + ")");
                }
            }
            finished.Clear();
        }
        private void DisposeBuffers()
        {
            if (receivers.IsCreated) receivers.Dispose();
            if (amounts.IsCreated) amounts.Dispose();
            if (credited.IsCreated) credited.Dispose();
        }
        // Called only after pending.Complete(). Listeners belong to the system instance and stay registered.
        private void DropPending()
        {
            crediting = null;
            DisposeBuffers();
            pending = default;
            ledger.Clear();
            finished.Clear();
            areas.Clear();
        }
    }
}
