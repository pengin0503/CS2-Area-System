using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Game;
using Game.Common;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Policies
{
    // Owns the authoritative policy registry (common + module providers) and resolves per-Area state on demand from the
    // Area's saved ManagedAreaPolicyEntry buffer, its lifecycle state and Level. Derived state is never persisted.
    public partial class AreaPolicySystem : GameSystemBase
    {
        private AreaPolicyRuntime runtime;
        private EntityAreaPolicyStore store;
        private AreaPolicyUpkeepSettlement upkeep;
        public AreaPolicyRegistry Registry => runtime.Registry;
        public int LoadGeneration => runtime.LoadGeneration;
        private AreaPolicyContext Context => new AreaPolicyContext(World, EntityManager);
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            var modules = new Dictionary<int, IAreaPolicyProvider>();
            foreach (var descriptor in World.GetOrCreateSystemManaged<AreaCoreServicesSystem>().Registry.All)
                if (descriptor.Module is IAreaPolicyProvider provider) modules[descriptor.TypeId] = provider;
            runtime = AreaPolicyRuntime.Build(CommonAreaPolicies.Provider, modules, message => Mod.Log.Error(message));
            store = new EntityAreaPolicyStore(EntityManager);
            upkeep = new AreaPolicyUpkeepSettlement(runtime);
        }
        // A new city starts a new failure-log generation and no upkeep carry from the previous one.
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            runtime.NewLoadGeneration();
            upkeep.Clear();
        }
        // Policy state is resolved on demand; consumers registered later in the frame read the current buffer.
        [Preserve]
        protected override void OnUpdate() { }
        public bool TrySetDesired(Entity area, int policyId, bool enabled, out string reasonKey) =>
            runtime.TrySetDesired(store, area, policyId, enabled, Context, out reasonKey);
        public AreaPolicyRuntimeState GetState(Entity area, int policyId) => runtime.GetState(store, area, policyId, Context);
        public bool IsEffective(Entity area, int policyId) => runtime.IsEffective(store, area, policyId, Context);
        public double GetExperienceMultiplier(Entity area) => runtime.GetExperienceMultiplier(store, area, Context);
        // Same inputs as the settlement, so the UI estimate and the charged amount use one formula.
        public double EstimateDailyUpkeep(Entity area, AreaPolicyDefinition definition) =>
            runtime.EstimateDailyUpkeep(store, area, definition, Context, () => SurfaceArea(area));
        internal void SettleUpkeep(Entity area, uint frames, List<AreaPolicyUpkeepCharge> into) =>
            upkeep.Settle(store, area, frames, Context, () => SurfaceArea(area), into);
        internal void ClearUpkeep() => upkeep.Clear();
        // Area geometry is the hectare source (Game.Areas.Geometry.m_SurfaceArea, square metres).
        private double SurfaceArea(Entity area) =>
            EntityManager.HasComponent<Game.Areas.Geometry>(area) ? EntityManager.GetComponentData<Game.Areas.Geometry>(area).m_SurfaceArea : 0;
        private sealed class EntityAreaPolicyStore : IAreaPolicyStore
        {
            private readonly EntityManager entities;
            public EntityAreaPolicyStore(EntityManager entities) { this.entities = entities; }
            public bool TryGetArea(Entity area, out AreaPolicyAreaInfo info)
            {
                info = default;
                if (area == Entity.Null || !entities.Exists(area) || !entities.HasComponent<ManagedArea>(area) || !entities.HasComponent<ManagedAreaStableId>(area)) return false;
                var managed = entities.GetComponentData<ManagedArea>(area);
                int level = entities.HasComponent<ManagedAreaProgress>(area) ? entities.GetComponentData<ManagedAreaProgress>(area).Level : AreaPolicyDefinition.MinAreaLevel;
                info = new AreaPolicyAreaInfo(entities.GetComponentData<ManagedAreaStableId>(area).Value, managed.TypeId, managed.State, level,
                    entities.HasComponent<Deleted>(area));
                return true;
            }
            public bool TryRead(Entity area, List<ManagedAreaPolicyEntry> into)
            {
                into.Clear();
                if (!entities.HasBuffer<ManagedAreaPolicyEntry>(area)) return false;
                var buffer = entities.GetBuffer<ManagedAreaPolicyEntry>(area, true);
                for (int i = 0; i < buffer.Length; i++) into.Add(buffer[i]);
                return true;
            }
            public void Write(Entity area, IReadOnlyList<ManagedAreaPolicyEntry> entries)
            {
                var buffer = entities.GetBuffer<ManagedAreaPolicyEntry>(area);
                buffer.Clear();
                for (int i = 0; i < entries.Count; i++) buffer.Add(entries[i]);
            }
        }
    }
}
