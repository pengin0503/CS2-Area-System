using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
using Game;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Phase 2 C4 Industrial support payments. Once per in-game day and Area, the complete roster of companies resolved to
    // the Area by the latest Industrial observation cycle is revalidated against B2 ownership and one Area Management
    // expense (receivers × per-company setting) is queued in Area Finance. The day is saved only after Area Finance
    // accepted the request; the roster and request stay runtime-only, so a load never replays a payment.
    public partial class IndustrialSupportSystem : GameSystemBase
    {
        public const uint UpdateFrames = AreaFinanceSystem.UpdateFrames;
        private IndustrialRuntimeSystem runtime;
        private AreaFinanceSystem finance;
        private Core.Areas.AreaCoreServicesSystem services;
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private readonly IndustrialSupportLedger ledger = new IndustrialSupportLedger();
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
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            ledger.Clear();
            loadGeneration = -1;
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready || !finance.Ready) return;
            if (loadGeneration != runtime.LoadGeneration) { loadGeneration = runtime.LoadGeneration; ledger.Clear(); }
            if (!runtime.AssignmentsReady || !runtime.TryGetClock(out var clock)) return;
            long day = clock.Day(runtime.SimulationFrame);
            runtime.CollectAreas(areas);
            foreach (var area in areas) Schedule(area, day);
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
    }
}
