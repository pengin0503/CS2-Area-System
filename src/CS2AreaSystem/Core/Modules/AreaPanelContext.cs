using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    // Passed only while Core builds the selected area's panel or handles an action for it.
    public readonly struct AreaPanelContext
    {
        public World World { get; }
        public EntityManager Entities { get; }
        public AreaPanelContext(World world, EntityManager entities) { World = world; Entities = entities; }
    }
}
