using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Areas;
using Game.Common;
using Game.Notifications;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace CS2AreaSystem.Core.Prefabs
{
    // Additive management-layer policy. Native validation still runs unchanged.
    // Runs after its Components system and before IconCommandSystem/end barrier.
    public partial class ManagedAreaOverlapSystem : GameSystemBase
    {
        private AreaCoreServicesSystem services;
        private PrefabSystem prefabs;
        private ToolSystem tools;
        private AreaToolSystem areaTool;
        private Game.Areas.SearchSystem areaSearch;
        private Game.Objects.SearchSystem objectSearch;
        private Game.Net.SearchSystem netSearch;
        private WaterSystem water;
        private TerrainSystem terrain;
        private ModificationEndBarrier barrier;
        private IconCommandSystem icons;
        private EntityQuery previews, errorPrefabs;
        private bool failureLogged;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            areaTool = World.GetOrCreateSystemManaged<AreaToolSystem>();
            areaSearch = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            objectSearch = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            netSearch = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            water = World.GetOrCreateSystemManaged<WaterSystem>();
            terrain = World.GetOrCreateSystemManaged<TerrainSystem>();
            barrier = World.GetOrCreateSystemManaged<ModificationEndBarrier>();
            icons = World.GetOrCreateSystemManaged<IconCommandSystem>();
            previews = GetEntityQuery(ComponentType.ReadOnly<Area>(), ComponentType.ReadOnly<Space>(),
                ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(), ComponentType.Exclude<Deleted>());
            errorPrefabs = GetEntityQuery(ComponentType.ReadOnly<NotificationIconData>(), ComponentType.ReadOnly<ToolErrorData>());
        }
        [Preserve]
        protected override void OnUpdate()
        {
            try
            {
                if (!CS2AreaSystem.Core.Settings.AreaSettings.Current.AllowManagedAreaOverlap ||
                    tools.activeTool != areaTool || areaTool.prefab == null || tools.actionMode.IsEditor() ||
                    !TryDescribePrefab(prefabs.GetEntity(areaTool.prefab), out _) || previews.IsEmptyIgnoreFilter) return;
                ApplyManagedPolicy();
            }
            catch (Exception error)
            {
                // Fail closed: without complete native evidence, keep blocking errors.
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Managed overlap validation unavailable; native errors remain authoritative."); }
            }
        }
        private void ApplyManagedPolicy()
        {
            var objectTree = objectSearch.GetStaticSearchTree(true, out var objectDependency);
            var netTree = netSearch.GetNetSearchTree(true, out var netDependency);
            var areaTree = areaSearch.GetSearchTree(true, out var areaDependency);
            var surface = water.GetSurfaceData(out var waterDependency);
            objectDependency.Complete(); netDependency.Complete(); areaDependency.Complete(); waterDependency.Complete();
            var height = terrain.GetHeightData();
            var data = new ValidationSystem.EntityData(this);
            var errors = new List<ErrorData>();
            using var queue = new NativeQueue<ErrorData>(Allocator.TempJob);
            var writer = queue.AsParallelWriter();
            using var entities = previews.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!IsOwned(entity)) continue;
                var temp = EntityManager.GetComponentData<Temp>(entity);
                if ((temp.m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) != 0) continue;
                var area = EntityManager.GetComponentData<Area>(entity);
                if ((area.m_Flags & AreaFlags.Slave) != 0) continue;
                var owner = EntityManager.HasComponent<Owner>(entity) ? EntityManager.GetComponentData<Owner>(entity) : default;
                var geometry = EntityManager.HasComponent<Geometry>(entity) ? EntityManager.GetComponentData<Geometry>(entity) : default;
                var reference = EntityManager.GetComponentData<PrefabRef>(entity);
                Game.Areas.ValidationHelpers.ValidateArea(false, entity, temp, owner, area, geometry, default,
                    EntityManager.GetBuffer<Node>(entity, true), reference, data, objectTree, netTree, areaTree, surface, height, writer);
                foreach (var triangle in EntityManager.GetBuffer<Triangle>(entity, true))
                    Game.Areas.ValidationHelpers.ValidateTriangle(false, (area.m_Flags & AreaFlags.Complete) == 0,
                        (area.m_Flags & AreaFlags.CounterClockwise) != 0, entity, temp, owner, triangle,
                        data, objectTree, netTree, areaTree, surface, height, writer);
            }
            while (queue.TryDequeue(out var error)) errors.Add(error);
            var allowed = new HashSet<Entity>(ManagedOverlapValidation.FindOverlapOnlyEntities(errors, IsOwned, Permits));
            if (allowed.Count == 0) return;
            // Queue after native validation's ECB; playback never exposes an unvalidated frame.
            var commands = barrier.CreateCommandBuffer();
            foreach (var entity in allowed)
            {
                commands.RemoveComponent<Game.Tools.Error>(entity);
                commands.AddComponent<Warning>(entity);
                commands.AddComponent<BatchesUpdated>(entity);
            }
            Entity overlapIcon = Entity.Null;
            using var candidates = errorPrefabs.ToEntityArray(Allocator.Temp);
            foreach (var candidate in candidates)
                if (EntityManager.GetComponentData<ToolErrorData>(candidate).m_Error == ErrorType.OverlapExisting) { overlapIcon = candidate; break; }
            if (overlapIcon == Entity.Null) return;
            var iconCommands = icons.CreateCommandBuffer();
            foreach (var error in errors)
                if (error.m_ErrorType == ErrorType.OverlapExisting && allowed.Contains(error.m_TempEntity) && allowed.Contains(error.m_PermanentEntity))
                    iconCommands.Add(error.m_TempEntity, overlapIcon, error.m_Position, IconPriority.Warning,
                        IconClusterLayer.Default, (IconFlags)0, error.m_PermanentEntity, isTemp: true);
        }
        private bool IsOwned(Entity entity) => TryDescribeArea(entity, out _);
        private bool TryDescribeArea(Entity entity, out AreaTypeDescriptor type)
        {
            type = null;
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<Area>(entity) ||
                !EntityManager.HasComponent<Space>(entity) || EntityManager.HasComponent<Deleted>(entity) ||
                !EntityManager.HasComponent<PrefabRef>(entity)) return false;
            if (!TryDescribePrefab(EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab, out type)) return false;
            // Unknown future TypeId must retain native validation rather than assume a module policy.
            if (EntityManager.HasComponent<ManagedArea>(entity))
                return services.Registry.TryGet(EntityManager.GetComponentData<ManagedArea>(entity).TypeId, out type);
            return EntityManager.HasComponent<Temp>(entity);
        }
        private bool TryDescribePrefab(Entity entity, out AreaTypeDescriptor type)
        {
            type = null;
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<SpaceData>(entity) ||
                !EntityManager.HasComponent<AreaGeometryData>(entity)) return false;
            var geometry = EntityManager.GetComponentData<AreaGeometryData>(entity);
            if (geometry.m_Type != AreaType.Space || geometry.m_Flags != 0) return false;
            foreach (var descriptor in services.Registry.All)
                if (descriptor.Prefab != null && prefabs.TryGetEntity(descriptor.Prefab, out var registered) && registered == entity)
                { type = descriptor; return true; }
            return false;
        }
        // Option "Allow managed areas to overlap": when off, native overlap errors between managed areas remain.
        private bool Permits(Entity first, Entity second) => CS2AreaSystem.Core.Settings.AreaSettings.Current.AllowManagedAreaOverlap &&
            TryDescribeArea(first, out var a) && TryDescribeArea(second, out var b) &&
            a.Module != null && b.Module != null && a.Module.AllowsOverlapWith(b.TypeId) && b.Module.AllowsOverlapWith(a.TypeId);
    }
}
