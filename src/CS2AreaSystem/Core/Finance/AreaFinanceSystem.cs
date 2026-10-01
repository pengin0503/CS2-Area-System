using System;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.City;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Finance
{
    // The one city-money path for Area amounts (design §15). There is no Area wallet: queued expenses and
    // payer-less income are applied to the real city PlayerMoney in a scheduled job, at BudgetApplySystem's cadence
    // and after it, and applied expenses are reported to CityStatisticsSystem as service upkeep. Household payments
    // (Park admission) are transferred by AreaFinanceHouseholdTransfer in their own job and only recorded here.
    // Results are recorded on the next update or at the save boundary; the accounts are what the budget UI reads.
    public partial class AreaFinanceSystem : GameSystemBase
    {
        public const uint UpdateFrames = AreaFinanceModel.FramesPerDay / 1024;
        private AreaFinanceRuntime runtime;
        private CitySystem city;
        private SimulationSystem simulation;
        private CityStatisticsSystem statistics;
        private AreaLoadCoordinatorSystem load;
        private NativeArray<long> applied;
        private JobHandle pending;
        private int loadGeneration, startedGeneration = -1;
        private bool failureLogged, listenerFailureLogged;
        public bool Ready => !GameManager.instance.isGameLoading && load.Ready;
        public int LoadGeneration => loadGeneration;
        public uint SimulationFrame => simulation.frameIndex;
        public bool HasPending => runtime.HasQueued || runtime.HasInFlight;
        // Settled over the trailing in-game day; partial until a day has passed since load (SettledCoveredFrames).
        public long AreaManagementSettled => runtime.AreaManagement(SimulationFrame);
        public long AreaServiceFeesSettled => runtime.AreaServiceFees(SimulationFrame);
        public uint SettledCoveredFrames => runtime.CoveredFrames(SimulationFrame);
        public long Settled(AreaFinanceKind kind) => runtime.Settled(kind, SimulationFrame);
        public override int GetUpdateInterval(SystemUpdatePhase phase) => (int)UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = new AreaFinanceRuntime(error =>
            {
                if (listenerFailureLogged) return;
                listenerFailureLogged = true;
                Mod.Log.Error(error, "An Area finance listener failed; city money and other listeners are unaffected");
            });
            city = World.GetOrCreateSystemManaged<CitySystem>();
            simulation = World.GetOrCreateSystemManaged<SimulationSystem>();
            statistics = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            load = World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>();
        }
        [Preserve]
        protected override void OnDestroy()
        {
            pending.Complete();
            DisposeBuffers();
            base.OnDestroy();
        }
        // A new city starts with no pending requests and no settled history; nothing is carried across loads.
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            pending.Complete();
            DisposeBuffers();
            runtime.Reset();
            loadGeneration++;
            listenerFailureLogged = false;
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!Ready) return;
            pending.Complete();
            if (startedGeneration != loadGeneration) { startedGeneration = loadGeneration; runtime.Start(SimulationFrame); }
            RecordResults();
            if (runtime.HasQueued) Schedule();
        }
        public void AddListener(IAreaFinanceListener listener) => runtime.AddListener(listener);
        public bool EnqueueExpense(ulong areaStableId, AreaFinanceKind kind, long amount) => runtime.EnqueueExpense(areaStableId, kind, amount);
        public bool EnqueueIncome(ulong areaStableId, AreaFinanceKind kind, long amount) => runtime.EnqueueIncome(areaStableId, kind, amount);
        // For income already moved by AreaFinanceHouseholdTransfer: recorded for the accounts only, never credited again.
        public bool RecordSettledIncome(ulong areaStableId, AreaFinanceKind kind, long amount) =>
            runtime.RecordSettledIncome(SimulationFrame, areaStableId, kind, amount);
        internal void FlushForSave() => AreaFinanceSaveBoundary.Flush(Ready, startedGeneration, loadGeneration, CompleteAndRecord,
            () => runtime.HasQueued, () => EntityManager.CompleteAllTrackedJobs(), Schedule);
        private void CompleteAndRecord()
        {
            pending.Complete();
            RecordResults();
        }
        private void Schedule()
        {
            DisposeBuffers();
            var batch = runtime.BeginApply(SimulationFrame);
            if (batch == null) return;
            try
            {
                applied = new NativeArray<long>(2, Allocator.Persistent);
                var queue = statistics.GetSafeStatisticsQueue(out JobHandle statisticsWriters);
                var job = new ApplyJob {
                    City = city.City, Expense = batch.TotalExpense, Income = batch.TotalIncome,
                    Money = GetComponentLookup<PlayerMoney>(false), Statistics = queue, Applied = applied };
                pending = job.Schedule(JobHandle.CombineDependencies(Dependency, statisticsWriters));
                Dependency = pending;
                statistics.AddWriter(pending);
            }
            catch (Exception error)
            {
                // Nothing was applied, so nothing is recorded; the producers' own records follow the applied result.
                DisposeBuffers();
                runtime.AbortApply();
                LogOnce(error, "Area finance could not be applied to city money; this batch was not charged");
            }
        }
        private void RecordResults()
        {
            if (!runtime.HasInFlight) return;
            long expense = applied.IsCreated ? applied[0] : 0, income = applied.IsCreated ? applied[1] : 0;
            DisposeBuffers();
            runtime.CompleteApply(expense, income);
            failureLogged = false;
        }
        private void LogOnce(Exception error, string message)
        {
            if (failureLogged) return;
            failureLogged = true;
            Mod.Log.Error(error, message);
        }
        private void DisposeBuffers()
        {
            if (applied.IsCreated) applied.Dispose();
        }
        private struct ApplyJob : IJob
        {
            public Entity City;
            public long Expense, Income;
            public ComponentLookup<PlayerMoney> Money;
            public CityStatisticsSystem.SafeStatisticQueue Statistics;
            public NativeArray<long> Applied;
            public void Execute()
            {
                Applied[0] = 0; Applied[1] = 0;
                if (City == Entity.Null || !Money.HasComponent(City)) return;
                var money = Money[City];
                var result = AreaFinanceVanillaAdapter.Apply(ref money, Expense, Income);
                Money[City] = money;
                Applied[0] = result.Expense; Applied[1] = result.Income;
                if (result.Expense > 0) Statistics.Enqueue(AreaFinanceVanillaAdapter.ExpenseStatistic(result.Expense));
            }
        }
    }
}
