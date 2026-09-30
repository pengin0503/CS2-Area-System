using Game;
using Game.Common;
using Game.Tools;
using Unity.Entities;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Tourism;
namespace CS2AreaSystem.Core.Areas
{
    public partial class AreaCoreServicesSystem : GameSystemBase
    {
        public AreaTypeRegistry Registry { get; } = new AreaTypeRegistry();
        public AreaStableIdAllocator StableIds { get; } = new AreaStableIdAllocator();
        public AreaAnchorService Anchors { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            Anchors = new AreaAnchorService(new EntityAreaAnchorStore(EntityManager), IsValidEntity, IsEligibleAnchor);
            Registry.Register(new ParkAreaModule(Anchors).DescribeType());
            Registry.Register(new IndustrialAreaModule(Anchors).DescribeType());
            Registry.Register(new TourismAreaModule().DescribeType());
        }
        public bool IsValidEntity(Entity entity) => EntityManager.Exists(entity) && entity != Entity.Null &&
            !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Destroyed>(entity) && !EntityManager.HasComponent<Temp>(entity);
        private bool IsEligibleAnchor(Entity area, Entity anchor, AreaAnchorRole role)
        {
            if (!IsValidEntity(area) || !EntityManager.HasComponent<ManagedArea>(area) || !EntityManager.HasComponent<Game.Buildings.Building>(anchor)) return false;
            var managed = EntityManager.GetComponentData<ManagedArea>(area);
            if (!Registry.TryGet(managed.TypeId, out var descriptor) || !(descriptor.Module is IAreaAnchorPolicy policy) || policy.RequiredAnchorRole != role) return false;
            var id = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
            foreach (var membership in World.GetOrCreateSystemManaged<Membership.AreaMembershipSystem>().Index.GetAreas(anchor))
                if (membership.AreaStableId == id) return true;
            return false;
        }
        protected override void OnUpdate() { }
    }
}
