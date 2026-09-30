using System.Collections.Generic;
using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Prefabs;
using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Areas
{
    public partial class ManagedAreaLifecycleSystem : GameSystemBase
    {
        private AreaCoreServicesSystem services;
        private PrefabSystem prefabs;
        private AreaPrefabRegistrationSystem registration;
        private ToolSystem tools;
        private EntityQuery changedAreas;
        private EntityQuery savedIds;
        private bool observedSavedIds;
        private readonly HashSet<int> blockedTypes = new HashSet<int>();
        private readonly ManagedAreaTypeResolver resolver = new ManagedAreaTypeResolver();
        public bool CanCreate(int typeId) => registration.IsTypeReady(typeId) && !blockedTypes.Contains(typeId);
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            registration = World.GetOrCreateSystemManaged<AreaPrefabRegistrationSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            changedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Area>(), ComponentType.ReadOnly<PrefabRef>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
            savedIds = GetEntityQuery(ComponentType.ReadOnly<ManagedAreaStableId>());
        }
        protected override void OnGameLoaded(Context context) { observedSavedIds = false; }
        [Preserve]
        protected override void OnUpdate()
        {
            ValidateTool();
            if (GameManager.instance.isGameLoading ||
                !World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>().Ready) return;
            if (!observedSavedIds)
            {
                using var loaded = savedIds.ToComponentDataArray<ManagedAreaStableId>(Allocator.Temp);
                foreach (var id in loaded) services.StableIds.ObserveExisting(id.Value);
                observedSavedIds = true;
            }
            using var areas = changedAreas.ToEntityArray(Allocator.Temp);
            foreach (var entity in areas)
            {
                if (!prefabs.TryGetPrefab<AreaPrefab>(EntityManager.GetComponentData<PrefabRef>(entity), out var prefab) ||
                    !resolver.TryResolve(prefab.name, out var kind)) continue;
                bool hasManaged = EntityManager.HasComponent<ManagedArea>(entity);
                bool hasId = EntityManager.HasComponent<ManagedAreaStableId>(entity);
                if (hasManaged && hasId) continue;
                var identity = ManagedAreaIdentity.Ensure((int)kind,
                    hasManaged ? EntityManager.GetComponentData<ManagedArea>(entity) : (ManagedArea?)null,
                    hasId ? EntityManager.GetComponentData<ManagedAreaStableId>(entity) : (ManagedAreaStableId?)null, services.StableIds);
                if (!hasManaged) EntityManager.AddComponentData(entity, identity.Managed);
                if (!hasId) EntityManager.AddComponentData(entity, identity.StableId);
            }
        }
        private void ValidateTool()
        {
            var prefab = tools.activePrefab;
            if (prefab == null || !resolver.TryResolve(prefab.name, out var kind)) return;
            bool native = tools.activeTool is AreaToolSystem;
            bool ready = CanCreate((int)kind);
            if (!native && blockedTypes.Add((int)kind))
                Mod.Log.Error("Managed area prefab selected an unexpected tool; creation disabled for " + prefab.name);
            if (!AreaToolActivationPolicy.CanCreate(ready, native)) tools.ActivatePrefabTool(null);
        }
    }
}
