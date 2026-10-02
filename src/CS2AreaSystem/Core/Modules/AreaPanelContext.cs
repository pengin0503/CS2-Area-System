using System;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    // Passed only while Core builds the selected area's panel or handles an action for it.
    public readonly struct AreaPanelContext
    {
        public World World { get; }
        public EntityManager Entities { get; }
        // Read-only camera focus on an entity (the vanilla selected-info focus); supplied only for guarded module actions.
        public Action<Entity> Focus { get; }
        public AreaPanelContext(World world, EntityManager entities, Action<Entity> focus = null) { World = world; Entities = entities; Focus = focus; }
    }
}
