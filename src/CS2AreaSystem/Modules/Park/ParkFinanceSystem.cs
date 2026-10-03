using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Finance;
using Game;
using Game.City;
using Game.Citizens;
using Game.Economy;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Park budget and admission (design §14–15). There is no Park wallet and Park code never touches PlayerMoney itself:
    // management is queued as an Area Management expense in the shared Area finance and recorded when it was applied;
    // admission is a household-to-city AreaFinanceHouseholdTransfer, recorded as Area Service Fees. Member buildings'
    // vanilla upkeep/revenue are never booked here. Admission transfers run in a scheduled job ordered by ECS
    // dependencies, so the main thread never forces the vanilla economy jobs that use Resources/PlayerMoney to complete;
    // results are recorded on the next update or save.
    public partial class ParkFinanceSystem : GameSystemBase, IParkVisitListener, IAreaFinanceListener
    {
        public const uint UpdateFrames = ParkVisitCadence.UpdateInterval;
        // Management is settled every 1/64 in-game day (~22 in-game minutes).
        public const uint SettlementFrames = ParkFinanceModel.FramesPerDay / 64;
        private ParkRuntimeSystem runtime;
        private CitySystem city;
        private AreaFinanceSystem areaFinance;
        private ParkPolicyEffectReader policyEffects;
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly ParkTransactionLedger ledger = new ParkTransactionLedger();
        private ParkTransactionBatch inFlight;
        private NativeArray<ParkAdmissionRequestData> requests;
        private NativeArray<long> collected;
        private JobHandle pending;
        private uint lastSettlement, inFlightFrame;
        private int loadGeneration = -1;
        private CS2AreaSystem.Core.Diagnostics.AreaFailureLog failures;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            city = World.GetOrCreateSystemManaged<CitySystem>();
            areaFinance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            areaFinance.AddListener(this);
            var policies = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Policies.AreaPolicySystem>();
            failures = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Areas.AreaCoreServicesSystem>().Failures;
            policyEffects = new ParkPolicyEffectReader((area, id) => policies.IsEffective(area, id), failures, "Park.Finance.PolicyEffects",
                "Area policy effects unavailable; Park management continues at the budget setting");
            World.GetOrCreateSystemManaged<ParkVisitSystem>().AddListener(this);
        }
        [Preserve]
        protected override void OnDestroy()
        {
            pending.Complete();
            DisposeBuffers();
            World.GetExistingSystemManaged<AreaFinanceSystem>()?.RemoveListener(this);
            World.GetExistingSystemManaged<ParkVisitSystem>()?.RemoveListener(this);
            base.OnDestroy();
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            // 1.6.2f1 invokes preload before CleanupMemory/LoadGameSystem.RunOnce. The job may already have moved
            // money, so complete and record its old-city lifetime counters before discarding the runtime ledger.
            // Do not refill Area finance accounts: its preload subscriber may have reset them before this callback.
            pending.Complete();
            try { RecordResults(true); }
            finally
            {
                DropPending();
                loadGeneration = -1;
                lastSettlement = inFlightFrame = 0;
            }
        }
        [Preserve]
        protected override void OnUpdate()
        {
            pending.Complete();
            if (!runtime.Ready)
            {
                // Defensive unready path even if no preload callback is involved. Only the same generation still
                // owns these entities; never resolve old entity references against a newly loaded city.
                try { if (loadGeneration == runtime.LoadGeneration) RecordResults(true); }
                finally { DropPending(); }
                return;
            }
            uint frame = runtime.SimulationFrame;
            if (loadGeneration != runtime.LoadGeneration)
            {
                loadGeneration = runtime.LoadGeneration; lastSettlement = frame;
                DropPending();
                return;
            }
            RecordResults();
            uint elapsed = ParkFinanceModel.ElapsedForSettlement(lastSettlement, frame, SettlementFrames * 2);
            if (elapsed >= SettlementFrames)
            {
                lastSettlement = frame;
                runtime.CollectParks(parks);
                foreach (var park in parks) Settle(park, frame, elapsed);
            }
            if (ledger.HasPending) Schedule(frame);
        }
        private void Settle(ParkAreaRef park, uint frame, uint elapsed)
        {
            try
            {
                if (!EntityManager.HasComponent<ParkAreaState>(park.Area)) return;
                var snapshot = runtime.Store.GetOrCreate(park.StableId, park.Area);
                var estimate = ReadManagementCost(park.Area, snapshot, frame);
                snapshot.ManagementNeedPerDay = estimate.NeedPerDay;
                // Enhanced Maintenance raises the real management spend; Quality's coverage reads the same multiplier.
                snapshot.ManagementCostPerDay = estimate.CostPerDay;
                snapshot.ManagementCostKnown = true;
                if (!ParkFinanceModel.ShouldSettleManagement(park.State)) return;
                areaFinance.EnqueueExpense(park.StableId, AreaFinanceKind.ParkManagement, ParkFinanceModel.Settle(snapshot.ManagementCostPerDay, elapsed, ref snapshot.ExpenseCarry));
                failures.Recovered(park.Area, "Park.Settlement");
            }
            catch (System.Exception error)
            {
                failures.Report(park.Area, "Park.Settlement", error, "Park management settlement failed for managed area " + park.Area);
            }
        }
        internal ParkManagementEstimate ReadManagementCost(Entity area, ParkRuntimeSnapshot snapshot, uint frame)
        {
            float budget = EntityManager.HasComponent<ManagedAreaSettings>(area) ? EntityManager.GetComponentData<ManagedAreaSettings>(area).BudgetMultiplier : ParkFinanceModel.DefaultBudget;
            return ParkFinanceModel.ComputeLiveManagementCost(snapshot, frame, budget, policyEffects.Read(area));
        }
        internal void FlushForSave() => AreaFinanceSaveBoundary.Flush(runtime.Ready, loadGeneration, runtime.LoadGeneration,
            CompleteAndRecord, () => ledger.HasPending, () => EntityManager.CompleteAllTrackedJobs(), () => Schedule(runtime.SimulationFrame));
        private void CompleteAndRecord()
        {
            pending.Complete();
            RecordResults();
        }
        public void OnVisitStarted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, ParkVisitTracker tracker)
        {
            // Restored visits are fee-satisfied; every other session gets exactly one attempt, whatever its outcome.
            if (visit.FeeSatisfied || !tracker.TryBeginAdmission(visit.Citizen, visit.ParkId)) return;
            try
            {
                if (!EntityManager.HasComponent<ParkAreaState>(area)) return;
                var state = EntityManager.GetComponentData<ParkAreaState>(area);
                int level = EntityManager.HasComponent<ManagedAreaProgress>(area) ? EntityManager.GetComponentData<ManagedAreaProgress>(area).Level : 1;
                long fee = ParkFinanceModel.EffectiveFee(level, state.AdmissionFee);
                ledger.EnqueueAdmission(new ParkAdmissionRequest { Citizen = visit.Citizen, Area = area, ParkId = visit.ParkId, Fee = fee });
            }
            catch (System.Exception error)
            {
                failures.Report(area, "Park.Admission", error, "Park admission failed for managed area " + area + "; nothing collected for this visit");
            }
        }
        public void OnVisitCompleted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, bool parkActive) { }
        private void Schedule(uint frame)
        {
            DisposeBuffers();
            inFlight = ledger.TakeBatch();
            inFlightFrame = frame;
            requests = new NativeArray<ParkAdmissionRequestData>(inFlight.Admissions.Count, Allocator.Persistent);
            for (int i = 0; i < inFlight.Admissions.Count; i++)
                requests[i] = new ParkAdmissionRequestData { Citizen = inFlight.Admissions[i].Citizen, Fee = inFlight.Admissions[i].Fee };
            collected = new NativeArray<long>(requests.Length, Allocator.Persistent);
            var job = new TransferJob {
                Requests = requests, Collected = collected, City = city.City,
                Members = GetComponentLookup<HouseholdMember>(true), Resources = GetBufferLookup<Resources>(false),
                Money = GetComponentLookup<PlayerMoney>(false) };
            pending = job.Schedule(Dependency);
            Dependency = pending;
        }
        private void RecordResults(bool loadBoundary = false)
        {
            if (inFlight == null) return;
            var batch = inFlight; inFlight = null;
            var amounts = new long[collected.Length];
            for (int i = 0; i < amounts.Length; i++) amounts[i] = collected[i];
            var outcomes = ParkTransactionLedger.Resolve(batch, amounts);
            DisposeBuffers();
            // The household transfers already credited the city; these are records only, one failure never skips the rest.
            ParkAdmissionRecording.RecordAll(outcomes.Admissions, recordSink ??= new RecordSink(this), (error, outcome, what) =>
                failures.Report(outcome.Request.Area, "Park.AdmissionRecord:" + what, error, "Park admission " + what + " could not be recorded for Park " +
                    outcome.Request.ParkId + " (collected " + outcome.Collected + "); other admissions are recorded"), loadBoundary);
        }
        private RecordSink recordSink;
        private sealed class RecordSink : IParkAdmissionRecordSink
        {
            private readonly ParkFinanceSystem system;
            public RecordSink(ParkFinanceSystem system) => this.system = system;
            public void RecordRecent(in ParkAdmissionOutcome outcome)
            {
                if (!system.runtime.Store.TryGet(outcome.Request.ParkId, out var snapshot)) return;
                if (outcome.Uncollected > 0) snapshot.RecentUncollected.Add(system.inFlightFrame, outcome.Uncollected);
                if (outcome.Collected > 0) snapshot.RecentAdmission.Add(system.inFlightFrame, outcome.Collected);
            }
            public void RecordServiceFee(in ParkAdmissionOutcome outcome) =>
                system.areaFinance.RecordSettledIncome(outcome.Request.ParkId, AreaFinanceKind.ParkAdmission, outcome.Collected);
            public void RecordLifetimeRevenue(in ParkAdmissionOutcome outcome)
            {
                var area = outcome.Request.Area;
                var entities = system.EntityManager;
                if (entities.Exists(area) && entities.HasComponent<ParkAreaState>(area))
                    entities.SetComponentData(area, ParkFinanceModel.RecordAdmission(entities.GetComponentData<ParkAreaState>(area), outcome.Collected));
            }
        }
        // Management amounts the shared Area finance actually applied to city money (never more than was queued).
        public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame)
        {
            if (!runtime.Ready || loadGeneration != runtime.LoadGeneration) return;
            foreach (var charge in ParkAreaFinance.AppliedManagement(settled))
            {
                if (!runtime.Store.TryGet(charge.AreaStableId, out var snapshot)) continue;
                snapshot.RecentExpense.Add(frame, charge.Amount);
                var area = snapshot.Area;
                if (EntityManager.Exists(area) && EntityManager.HasComponent<ParkAreaState>(area))
                    EntityManager.SetComponentData(area, ParkFinanceModel.RecordManagementExpense(EntityManager.GetComponentData<ParkAreaState>(area), charge.Amount));
            }
        }
        private void DisposeBuffers()
        {
            if (requests.IsCreated) requests.Dispose();
            if (collected.IsCreated) collected.Dispose();
        }
        // Called only after pending.Complete(): neither requests nor results outlive their city. Listeners belong
        // to the system instance, not the save, and deliberately remain registered across this reset.
        private void DropPending()
        {
            inFlight = null;
            DisposeBuffers();
            pending = default;
            ledger.Clear();
            parks.Clear();
        }
        public struct ParkAdmissionRequestData { public Entity Citizen; public long Fee; }
        private struct TransferJob : IJob
        {
            [ReadOnly] public NativeArray<ParkAdmissionRequestData> Requests;
            public NativeArray<long> Collected;
            public Entity City;
            [ReadOnly] public ComponentLookup<HouseholdMember> Members;
            public BufferLookup<Resources> Resources;
            public ComponentLookup<PlayerMoney> Money;
            public void Execute()
            {
                bool hasCity = City != Entity.Null && Money.HasComponent(City);
                for (int i = 0; i < Requests.Length; i++)
                {
                    Collected[i] = 0;
                    var request = Requests[i];
                    // No household resources: no charge; the visit itself is still tracked (design §22).
                    if (!hasCity || !Members.HasComponent(request.Citizen)) continue;
                    var household = Members[request.Citizen].m_Household;
                    if (!Resources.HasBuffer(household)) continue;
                    var money = Money[City];
                    // City credit first, then a household debit of exactly the credited amount.
                    Collected[i] = AreaFinanceHouseholdTransfer.Transfer(ref money, Resources[household], request.Fee);
                    Money[City] = money;
                }
            }
        }
    }
}
