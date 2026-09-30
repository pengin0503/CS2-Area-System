using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.City;
using Game.Citizens;
using Game.Economy;
using Game.Simulation;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Park budget and admission (design §14–15). Money only moves between real household resources and the city's
    // PlayerMoney; there is no Park wallet. Member buildings' vanilla upkeep/revenue are never booked here.
    public partial class ParkFinanceSystem : GameSystemBase, IParkVisitListener
    {
        // Management is settled every 1/64 in-game day (~22 in-game minutes).
        public const uint SettlementFrames = ParkFinanceModel.FramesPerDay / 64;
        private ParkRuntimeSystem runtime;
        private CitySystem city;
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private uint lastSettlement;
        private int loadGeneration = -1;
        private bool admissionFailureLogged, settlementFailureLogged;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)SettlementFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            city = World.GetOrCreateSystemManaged<CitySystem>();
            World.GetOrCreateSystemManaged<ParkVisitSystem>().AddListener(this);
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint frame = runtime.SimulationFrame;
            if (loadGeneration != runtime.LoadGeneration) { loadGeneration = runtime.LoadGeneration; lastSettlement = frame; return; }
            uint elapsed = ParkFinanceModel.ElapsedForSettlement(lastSettlement, frame, SettlementFrames * 2);
            lastSettlement = frame;
            if (elapsed == 0) return;
            runtime.CollectParks(parks);
            foreach (var park in parks) Settle(park, frame, elapsed);
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
                long amount = ParkFinanceModel.Settle(snapshot.ManagementCostPerDay, elapsed, ref snapshot.ExpenseCarry);
                if (amount <= 0 || !TryAddCityMoney(-amount)) return;
                EntityManager.SetComponentData(park.Area, ParkFinanceModel.RecordManagementExpense(EntityManager.GetComponentData<ParkAreaState>(park.Area), amount));
                snapshot.RecentExpense.Add(frame, amount);
                settlementFailureLogged = false;
            }
            catch (System.Exception error)
            {
                if (!settlementFailureLogged) { settlementFailureLogged = true; Mod.Log.Error(error, "Park management settlement failed"); }
            }
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
                if (ParkFinanceModel.EffectiveFee(level, state.AdmissionFee) <= 0) return;
                // No household resources buffer: no charge, the visit is still tracked (design §22).
                if (!EntityManager.HasComponent<HouseholdMember>(visit.Citizen)) return;
                var household = EntityManager.GetComponentData<HouseholdMember>(visit.Citizen).m_Household;
                if (!EntityManager.Exists(household) || !EntityManager.HasBuffer<Resources>(household)) return;
                var cityEntity = city.City;
                if (cityEntity == Entity.Null || !EntityManager.HasComponent<PlayerMoney>(cityEntity)) return;
                var resources = EntityManager.GetBuffer<Resources>(household);
                var plan = ParkFinanceModel.PlanAdmission(level, state.AdmissionFee, EconomyUtils.GetResources(Resource.Money, resources));
                uint frame = runtime.SimulationFrame;
                snapshot.UncollectedAdmissionRecent += plan.Uncollected;
                if (plan.Collected <= 0) return;
                EconomyUtils.AddResources(Resource.Money, (int)plan.HouseholdDelta, resources);
                var money = EntityManager.GetComponentData<PlayerMoney>(cityEntity);
                money.Add((int)plan.CityDelta);
                EntityManager.SetComponentData(cityEntity, money);
                EntityManager.SetComponentData(area, ParkFinanceModel.RecordAdmission(state, plan.Collected));
                snapshot.RecentAdmission.Add(frame, plan.Collected);
                admissionFailureLogged = false;
            }
            catch (System.Exception error)
            {
                if (!admissionFailureLogged) { admissionFailureLogged = true; Mod.Log.Error(error, "Park admission failed; nothing collected for this visit"); }
            }
        }
        public void OnVisitCompleted(in ParkVisitEvent visit, Entity area, ParkRuntimeSnapshot snapshot, bool parkActive) { }
        private bool TryAddCityMoney(long amount)
        {
            var cityEntity = city.City;
            if (cityEntity == Entity.Null || !EntityManager.HasComponent<PlayerMoney>(cityEntity)) return false;
            var money = EntityManager.GetComponentData<PlayerMoney>(cityEntity);
            money.Add((int)System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, amount)));
            EntityManager.SetComponentData(cityEntity, money);
            return true;
        }
    }
}
