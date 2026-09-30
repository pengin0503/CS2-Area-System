using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace CS2AreaSystem.Core.Anchors
{
    public partial class AreaActivationSystem : GameSystemBase
    {
        private AreaCoreServicesSystem services;
        private EntityQuery changedAnchors, changedAreas;
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            changedAnchors = GetEntityQuery(new EntityQueryDesc {
                All = new[] {ComponentType.ReadOnly<Game.Buildings.Building>()},
                Any = new[] {ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Destroyed>()},
                None = new[] {ComponentType.ReadOnly<Temp>()}
            });
            changedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] {ComponentType.ReadOnly<ManagedArea>()},
                Any = new[] {ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Deleted>()},
                None = new[] {ComponentType.ReadOnly<Temp>()}
            });
        }
        protected override void OnUpdate()
        {
            if (!World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready) return;
            using var anchors = changedAnchors.ToEntityArray(Allocator.Temp);
            foreach (var anchor in anchors) services.Anchors.InvalidateAnchor(anchor);
            using var areas = changedAreas.ToEntityArray(Allocator.Temp);
            foreach (var area in areas)
            {
                if (EntityManager.HasComponent<Deleted>(area)) services.Anchors.ForgetArea(area);
                else services.Anchors.InvalidateArea(area);
            }
            foreach (var area in services.Anchors.DrainDirty()) ValidateArea(area);
        }
        public void ValidateArea(Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<ManagedArea>(entity)) return;
            var area = EntityManager.GetComponentData<ManagedArea>(entity);
            var previous = area.State;
            bool known = services.Registry.TryGet(area.TypeId, out var descriptor) && descriptor.Module != null;
            bool deleted = EntityManager.HasComponent<Deleted>(entity);
            try
            {
                if (known && previous == AreaLifecycleState.Created) descriptor.Module.OnAreaCreated(entity);
                area.State = AreaLifecyclePolicy.Resolve(known, known && !deleted && descriptor.Module.ValidateActivation(entity), deleted);
                if (known && previous != area.State)
                {
                    if (area.State == AreaLifecycleState.Active) descriptor.Module.OnAreaActivated(entity);
                    if (area.State == AreaLifecycleState.Suspended) descriptor.Module.OnAreaSuspended(entity);
                    if (area.State == AreaLifecycleState.Deleted) descriptor.Module.OnAreaDeleted(entity);
                }
            }
            catch (System.Exception error) { Mod.Log.Error(error, "Area module failed: " + area.TypeId); area.State = deleted ? AreaLifecycleState.Deleted : AreaLifecycleState.ModuleUnavailable; }
            EntityManager.SetComponentData(entity, area);
        }
    }
}
