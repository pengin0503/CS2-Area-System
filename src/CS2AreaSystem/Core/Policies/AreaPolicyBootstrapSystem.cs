using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Policies
{
    // Gives every managed Area an (empty) policy buffer so later policy toggles are buffer-content writes instead of
    // structural changes outside the modification phase. A buffer restored from a save is never replaced: the query
    // only matches Areas without one, which also covers saves made before policies existed.
    public partial class AreaPolicyBootstrapSystem : GameSystemBase
    {
        private AreaLoadCoordinatorSystem load;
        private EntityQuery missingPolicies;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            load = World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>();
            missingPolicies = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>() },
                None = new[] { ComponentType.ReadOnly<ManagedAreaPolicyEntry>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (GameManager.instance.isGameLoading || !load.Ready || missingPolicies.IsEmptyIgnoreFilter) return;
            EntityManager.AddComponent<ManagedAreaPolicyEntry>(missingPolicies);
        }
    }
}
