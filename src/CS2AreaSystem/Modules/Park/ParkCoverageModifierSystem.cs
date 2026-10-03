using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Buildings;
using Game.Prefabs;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Release gate for the Phase 2 C6 coverage modifier. Static/API proof exists (park-coverage-modifier-contract.md);
    // the in-game checks in runtime-smoke-checklist.md (C6) have NOT RUN, so the writer is never enabled.
    public static class ParkCoverageModifierGate
    {
        public const bool RuntimeVerified = false;
    }
    // Scales the native ModifiedServiceCoverage capacity/magnitude of buildings whose primary Park owner is an Active
    // managed Park, by that Park's budget factor. Registered directly after ParkAISystem with the same interval, so it
    // inherits its update offset; it writes only from a verified fresh native baseline and never compounds.
    public partial class ParkCoverageModifierSystem : GameSystemBase
    {
        private ParkRuntimeSystem runtime;
        private AreaCoreServicesSystem services;
        private ParkPolicyEffectReader policyEffects;
        private Game.Simulation.ParkAISystem parkAI;
        private ComponentTypeHandle<ModifiedServiceCoverage> coverageHandle;
        private readonly ParkCoverageTracker tracker = new ParkCoverageTracker();
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly Dictionary<ulong, float> factors = new Dictionary<ulong, float>();
        private readonly HashSet<Entity> visited = new HashSet<Entity>(), fresh = new HashSet<Entity>();
        private readonly List<(Entity Building, ulong Owner, ModifiedServiceCoverage Baseline)> candidates = new List<(Entity, ulong, ModifiedServiceCoverage)>();
        private uint handledNativeVersion;
        public ParkCoverageTracker Tracker => tracker;
        public float FactorFor(ulong areaId) => factors.TryGetValue(areaId, out var factor) ? factor : 1f;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / Game.Simulation.ParkAISystem.kUpdatesPerDay;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            parkAI = World.GetOrCreateSystemManaged<Game.Simulation.ParkAISystem>();
            var policies = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Policies.AreaPolicySystem>();
            policyEffects = new ParkPolicyEffectReader((area, id) => policies.IsEffective(area, id), services.Failures, "Park.Coverage.PolicyEffects",
                "Area policy effects unavailable; the Park coverage factor uses the budget only");
            coverageHandle = GetComponentTypeHandle<ModifiedServiceCoverage>(true);
            Enabled = ParkCoverageModifierGate.RuntimeVerified;
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            // The previous world's entities are gone; nothing to write back. Reapply only after a fresh native pass.
            tracker.Clear();
            factors.Clear();
            handledNativeVersion = 0;
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            uint native = parkAI.LastSystemVersion;
            if (native == handledNativeVersion) return;
            handledNativeVersion = native;
            try
            {
                EntityManager.CompleteDependencyBeforeRO<ModifiedServiceCoverage>();
                coverageHandle.Update(this);
                runtime.CollectParks(parks);
                factors.Clear();
                foreach (var park in parks)
                    if (park.State == AreaLifecycleState.Active) factors[park.StableId] = Factor(park.Area);
                visited.Clear();
                var index = runtime.Membership;
                candidates.Clear();
                // Every freshness check happens before the first write: a write stamps its whole chunk with this system's version.
                foreach (var park in parks)
                    foreach (var member in index.GetMembers(park.StableId))
                        if (visited.Add(member) && IsFresh(member, native))
                            candidates.Add((member, ParkVisitModel.PrimaryPark(index.GetAreas(member), (int)AreaTypeId.Park, factors.ContainsKey),
                                EntityManager.GetComponentData<ModifiedServiceCoverage>(member)));
                fresh.Clear();
                foreach (var candidate in candidates) fresh.Add(candidate.Building);
                foreach (var candidate in candidates)
                    if (tracker.OnFreshBaseline(candidate.Building, candidate.Baseline, candidate.Owner, FactorFor(candidate.Owner), out var write))
                        EntityManager.SetComponentData(candidate.Building, write);
                candidates.Clear();
                tracker.RestoreUnvisited(fresh, Read, Write);
                services.Failures.Recovered(Entity.Null, "Park.Coverage");
            }
            catch (System.Exception error)
            {
                services.Failures.Report(Entity.Null, "Park.Coverage", error, "Park coverage modifier failed; tracked buildings restored and the modifier disabled");
                RestoreTracked();
                Enabled = false;
            }
        }
        // Disabling is not assumed to produce another update: callers restore synchronously.
        [Preserve]
        protected override void OnStopRunning()
        {
            RestoreTracked();
            base.OnStopRunning();
        }
        public void Shutdown()
        {
            RestoreTracked();
            Enabled = false;
        }
        // Writes back the native baseline wherever the current value is still this modifier's write (save, disable, failure).
        public void RestoreTracked()
        {
            try { tracker.RestoreAll(Read, Write); }
            catch (System.Exception error)
            {
                tracker.Clear();
                services?.Failures.Report(Entity.Null, "Park.Coverage.Restore", error, "Park coverage restore failed; the next native Park pass rewrites the values");
            }
        }
        private float Factor(Entity area)
        {
            float budget = EntityManager.HasComponent<ManagedAreaSettings>(area) ? EntityManager.GetComponentData<ManagedAreaSettings>(area).BudgetMultiplier : ParkFinanceModel.DefaultBudget;
            return ParkCoverageModifierModel.Factor(budget, policyEffects.Read(area).ManagementFundingMultiplier);
        }
        private bool IsFresh(Entity building, uint native)
        {
            if (!EntityManager.Exists(building) || !EntityManager.HasComponent<ModifiedServiceCoverage>(building) || !EntityManager.HasComponent<PrefabRef>(building)) return false;
            var prefab = EntityManager.GetComponentData<PrefabRef>(building).m_Prefab;
            bool written = EntityManager.HasComponent<ParkData>(prefab) && EntityManager.HasComponent<CoverageData>(prefab);
            return ParkCoverageModifierModel.IsFresh(EntityManager.GetChunk(building).GetChangeVersion(ref coverageHandle), native, written);
        }
        private ModifiedServiceCoverage? Read(Entity building) =>
            EntityManager.Exists(building) && EntityManager.HasComponent<ModifiedServiceCoverage>(building) ? EntityManager.GetComponentData<ModifiedServiceCoverage>(building) : (ModifiedServiceCoverage?)null;
        private void Write(Entity building, ModifiedServiceCoverage value) => EntityManager.SetComponentData(building, value);
    }
}
