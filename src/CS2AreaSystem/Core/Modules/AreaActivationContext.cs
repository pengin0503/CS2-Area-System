using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public readonly struct AreaActivationContext
    {
        public World World { get; }
        public EntityManager Entities { get; }
        public AreaActivationContext(World world, EntityManager entities) { World = world; Entities = entities; }
    }
}
