using Unity.Entities;
namespace CS2AreaSystem.Core.Policies
{
    // Passed while Core resolves a policy for one Area; modules read their own state through it.
    public readonly struct AreaPolicyContext
    {
        public World World { get; }
        public EntityManager Entities { get; }
        public AreaPolicyContext(World world, EntityManager entities) { World = world; Entities = entities; }
    }
}
