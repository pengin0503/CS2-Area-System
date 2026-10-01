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
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly ParkTransactionLedger ledger = new ParkTransactionLedger();
        private ParkTransactionBatch inFlight;
        private NativeArray<ParkAdmissionRequestData> requests;
        private NativeArray<long> collected;
        private JobHandle pending;
        private uint lastSettlement, inFlightFrame;
        private int loadGeneration = -1;
        private bool admissionFailureLogged, settlementFailureLogged;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            city = World.GetOrCreateSystemManaged<CitySystem>();
            areaFinance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            areaFinance.AddListener(this);
            World.GetOrCreateSystemManaged<ParkVisitSystem>().AddListener(this);
        }
        [Preserve]
        protected override void OnDestroy()
        {
            pending.Complete();
            DisposeBuffers();
            base.OnDestroy();
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint frame = runtime.SimulationFrame;
            pending.Complete();
            if (loadGeneration != runtime.LoadGeneration)
            {
                loadGeneration = runtime.LoadGeneration; lastSettlement = frame;
                inFlight = null; DisposeBuffers(); ledger.Clear();
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
                float budget = EntityManager.HasComponent<ManagedAreaSettings>(park.Area) ? EntityManager.GetComponentData<ManagedAreaSettings>(park.Area).BudgetMultiplier : ParkFinanceModel.DefaultBudget;
                var landscape = snapshot.Landscape;
                snapshot.ManagementNeedPerDay = ParkFinanceModel.ComputeManagementNeedPerDay(new ParkManagementInputs {
                    AreaSquareMeters = snapshot.AreaSquareMeters, VisitorsPerDay = snapshot.RecentVisitors.Sum(frame),
                    LandscapeObjects = landscape != null && landscape.Available ? landscape.Result.ObjectCount : 0,
                    ManagedFacilities = snapshot.Facilities?.RelevantFacilities ?? 0 });
                snapshot.ManagementCostPerDay = ParkFinanceModel.ComputeManagementCostPerDay(snapshot.ManagementNeedPerDay, budget);
                if (!ParkFinanceModel.ShouldSettleManagement(park.State)) return;
                areaFinance.EnqueueExpense(park.StableId, AreaFinanceKind.ParkManagement, ParkFinanceModel.Settle(snapshot.ManagementCostPerDay, elapsed, ref snapshot.ExpenseCarry));
                settlementFailureLogged = false;
            }
            catch (System.Exception error)
            {
                if (!settlementFailureLogged) { settlementFailureLogged = true; Mod.Log.Error(error, "Park management settlement failed"); }
            }
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
                if (!admissionFailureLogged) { admissionFailureLogged = true; Mod.Log.Error(error, "Park admission failed; nothing collected for this visit"); }
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
        private void RecordResults()
        {
            if (inFlight == null) return;
            var batch = inFlight; inFlight = null;
            var amounts = new long[collected.Length];
            for (int i = 0; i < amounts.Length; i++) amounts[i] = collected[i];
            var outcomes = ParkTransactionLedger.Resolve(batch, amounts);
            DisposeBuffers();
            try
            {
                foreach (var outcome in outcomes.Admissions)
                {
                    var area = outcome.Request.Area;
                    if (runtime.Store.TryGet(outcome.Request.ParkId, out var snapshot))
                    {
                        if (outcome.Uncollected > 0) snapshot.RecentUncollected.Add(inFlightFrame, outcome.Uncollected);
                        if (outcome.Collected > 0) snapshot.RecentAdmission.Add(inFlightFrame, outcome.Collected);
                    }
                    if (outcome.Collected <= 0) continue;
                    // The household transfer already credited the city; this only records it as Area Service Fees.
                    areaFinance.RecordSettledIncome(outcome.Request.ParkId, AreaFinanceKind.ParkAdmission, outcome.Collected);
                    if (EntityManager.Exists(area) && EntityManager.HasComponent<ParkAreaState>(area))
                        EntityManager.SetComponentData(area, ParkFinanceModel.RecordAdmission(EntityManager.GetComponentData<ParkAreaState>(area), outcome.Collected));
                }
            }
            catch (System.Exception error)
            {
                if (!admissionFailureLogged) { admissionFailureLogged = true; Mod.Log.Error(error, "Park finance records could not be written"); }
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
