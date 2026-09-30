using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Prefabs;
using Game;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace CS2AreaSystem.Core.Persistence
{
    public partial class AreaLoadCoordinatorSystem : GameSystemBase
    {
        private readonly AreaLoadSequence sequence = new AreaLoadSequence();
        private AreaCoreServicesSystem services;
        private AreaMembershipSystem membership;
        private AreaPrefabRegistrationSystem registration;
        private Game.Areas.SearchSystem search;
        private EntityQuery ids, areas;
        private bool loadingComplete = true;
        public bool RebuildPending => !sequence.Ready;
        public bool Ready => sequence.Ready;
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            membership = World.GetOrCreateSystemManaged<AreaMembershipSystem>();
            registration = World.GetOrCreateSystemManaged<AreaPrefabRegistrationSystem>();
            search = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            ids = GetEntityQuery(ComponentType.ReadOnly<ManagedAreaStableId>());
            areas = GetEntityQuery(new EntityQueryDesc {
                All = new[] {ComponentType.ReadOnly<ManagedArea>(),ComponentType.ReadOnly<ManagedAreaStableId>()},
                None = new[] {ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Temp>()}
            });
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            loadingComplete = false; sequence.Reset(); membership.RequestRebuild();
        }
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) => loadingComplete = true;
        protected override void OnUpdate()
        {
            if (!RebuildPending || !loadingComplete || GameManager.instance.isGameLoading || !registration.IsReady) return;
            // Called after native ModificationEnd search updates. Complete their jobs before recovering.
            search.GetSearchTree(true, out var dependency, out var triangleCounts); dependency.Complete();
            using var entities = areas.ToEntityArray(Allocator.Temp);
            foreach (var area in entities)
                if (EntityManager.HasComponent<Game.Areas.Area>(area) && !triangleCounts.ContainsKey(area)) return;
            sequence.TryRecover(true, () => {
                using var values = ids.ToComponentDataArray<ManagedAreaStableId>(Allocator.Temp);
                foreach (var id in values) services.StableIds.ObserveExisting(id.Value);
            }, membership.RebuildNow, ValidateModules);
        }
        public void ValidateModules()
        {
            using var entities = areas.ToEntityArray(Allocator.Temp);
            services.Anchors.RebuildReverseIndex(entities.ToArray());
            var activation = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Anchors.AreaActivationSystem>();
            foreach (var entity in entities) activation.ValidateArea(entity);
        }
    }
}
