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
            IAreaAnchorPolicy policy = null;
            return AreaAnchorEligibility.Evaluate(
                () => IsValidEntity(area) && EntityManager.HasComponent<ManagedArea>(area) && EntityManager.HasComponent<ManagedAreaStableId>(area),
                () => IsValidEntity(anchor) && EntityManager.HasComponent<Game.Buildings.Building>(anchor),
                () => Registry.TryGet(EntityManager.GetComponentData<ManagedArea>(area).TypeId, out var descriptor) &&
                      (policy = descriptor.Module as IAreaAnchorPolicy) != null && policy.RequiredAnchorRole == role,
                () => IsMember(area, anchor),
                () => AcceptsSafely(policy, area, anchor, role));
        }
        private bool IsMember(Entity area, Entity anchor)
        {
            var id = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
            foreach (var membership in World.GetOrCreateSystemManaged<Membership.AreaMembershipSystem>().Index.GetAreas(anchor))
                if (membership.AreaStableId == id) return true;
            return false;
        }
        private bool policyFailureLogged;
        private bool AcceptsSafely(IAreaAnchorPolicy policy, Entity area, Entity anchor, AreaAnchorRole role)
        {
            try { return policy.AcceptsAnchor(area, anchor, role, EntityManager); }
            catch (System.Exception error)
            {
                if (!policyFailureLogged) { policyFailureLogged = true; Mod.Log.Error(error, "Area anchor policy failed; anchor rejected"); }
                return false;
            }
        }
        protected override void OnUpdate() { }
    }
}
