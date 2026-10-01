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
        // Stable IDs and the quarantine of the current load generation (AreaLoadCoordinatorSystem drives the lifecycle).
        public AreaIdentityLifecycle Identity { get; } = new AreaIdentityLifecycle();
        public AreaStableIdAllocator StableIds => Identity.StableIds;
        public AreaQuarantine Quarantine => Identity.Quarantine;
        public bool IsQuarantined(Entity area) => Identity.Quarantine.Contains(area);
        // Shared failure-log suppression; a new generation starts with every load (AreaLoadCoordinatorSystem).
        public Diagnostics.AreaFailureLog Failures { get; } = new Diagnostics.AreaFailureLog((error, message) => Mod.Log.Error(error, message), message => Mod.Log.Warn(message));
        public AreaAnchorService Anchors { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            Anchors = new AreaAnchorService(new EntityAreaAnchorStore(EntityManager), IsValidEntity, IsEligibleAnchor, IsAcceptedExistingAnchor);
            Registry.Register(new ParkAreaModule(Anchors).DescribeType());
            Registry.Register(new IndustrialAreaModule(Anchors).DescribeType());
            Registry.Register(new TourismAreaModule().DescribeType());
        }
        public bool IsValidEntity(Entity entity) => EntityManager.Exists(entity) && entity != Entity.Null &&
            !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Destroyed>(entity) && !EntityManager.HasComponent<Temp>(entity);
        // Assignment: Core checks, then the module's assignment predicate.
        private bool IsEligibleAnchor(Entity area, Entity anchor, AreaAnchorRole role) => EvaluateAnchor(area, anchor, role, false);
        // Persisted link: Core checks, then the module's existing-link predicate when it has one.
        private bool IsAcceptedExistingAnchor(Entity area, Entity anchor, AreaAnchorRole role) => EvaluateAnchor(area, anchor, role, true);
        private bool EvaluateAnchor(Entity area, Entity anchor, AreaAnchorRole role, bool existing)
        {
            IAreaAnchorPolicy policy = null;
            return AreaAnchorEligibility.Evaluate(
                () => IsValidEntity(area) && !IsQuarantined(area) && EntityManager.HasComponent<ManagedArea>(area) && EntityManager.HasComponent<ManagedAreaStableId>(area),
                () => IsValidEntity(anchor) && EntityManager.HasComponent<Game.Buildings.Building>(anchor),
                () => Registry.TryGet(EntityManager.GetComponentData<ManagedArea>(area).TypeId, out var descriptor) &&
                      (policy = descriptor.Module as IAreaAnchorPolicy) != null && policy.RequiredAnchorRole == role,
                () => IsMember(area, anchor),
                () => existing && policy is IAreaExistingAnchorPolicy accepted ? AcceptsExistingSafely(accepted, area, anchor, role) : AcceptsSafely(policy, area, anchor, role));
        }
        private bool IsMember(Entity area, Entity anchor)
        {
            var id = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
            foreach (var membership in World.GetOrCreateSystemManaged<Membership.AreaMembershipSystem>().Index.GetAreas(anchor))
                if (membership.AreaStableId == id) return true;
            return false;
        }
        private bool AcceptsSafely(IAreaAnchorPolicy policy, Entity area, Entity anchor, AreaAnchorRole role)
        {
            try { bool accepted = policy.AcceptsAnchor(area, anchor, role, EntityManager); Failures.Recovered(area, "Anchors.Policy"); return accepted; }
            catch (System.Exception error)
            {
                Failures.Report(area, "Anchors.Policy", error, "Area anchor policy failed for managed area " + area + "; anchor rejected");
                return false;
            }
        }
        private bool AcceptsExistingSafely(IAreaExistingAnchorPolicy policy, Entity area, Entity anchor, AreaAnchorRole role)
        {
            try { bool accepted = policy.AcceptsExistingAnchor(area, anchor, role, EntityManager); Failures.Recovered(area, "Anchors.ExistingPolicy"); return accepted; }
            catch (System.Exception error)
            {
                Failures.Report(area, "Anchors.ExistingPolicy", error, "Area existing-anchor policy failed for managed area " + area + "; anchor rejected");
                return false;
            }
        }
        protected override void OnUpdate() { }
    }
}
