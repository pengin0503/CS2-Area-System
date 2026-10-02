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
        private EntityQuery changedAreas;
        private EntityQuery missingAnchorLinks;
        private readonly ManagedAreaTypeResolver resolver = new ManagedAreaTypeResolver();
        private readonly List<Entity> identified = new List<Entity>();
        // Areas that received their ManagedArea identity in the latest update (new placements, or unidentified Areas found
        // after a load), in query order. Read by AreaPlacementSystem in the same ModificationEnd; cleared every update.
        public IReadOnlyList<Entity> Identified => identified;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            changedAreas = GetEntityQuery(new EntityQueryDesc {
                // Managed areas are SpacePrefab instances; skip districts, lots and other vanilla areas.
                All = new[] { ComponentType.ReadOnly<Area>(), ComponentType.ReadOnly<Space>(), ComponentType.ReadOnly<PrefabRef>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
            // The management UI edits anchor links during UIUpdate; creating the buffer here keeps that a
            // buffer-content write instead of a structural change outside the modification phase.
            missingAnchorLinks = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>() },
                None = new[] { ComponentType.ReadOnly<Anchors.AreaAnchorLink>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
        }
        [Preserve]
        protected override void OnUpdate()
        {
            identified.Clear();
            // New IDs are allocated only after AreaLoadCoordinatorSystem observed this load's saved IDs.
            if (GameManager.instance.isGameLoading ||
                !World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>().Ready || !services.Identity.Observed) return;
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
                if (!hasManaged) { EntityManager.AddComponentData(entity, identity.Managed); identified.Add(entity); }
                if (!hasId) EntityManager.AddComponentData(entity, identity.StableId);
            }
            if (!missingAnchorLinks.IsEmptyIgnoreFilter) EntityManager.AddComponent<Anchors.AreaAnchorLink>(missingAnchorLinks);
        }
    }
}
