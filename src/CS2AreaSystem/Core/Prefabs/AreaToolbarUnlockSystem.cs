using Game;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Prefabs
{
    // The Areas menu and Area Types category are UIGroupPrefabs: they start Locked and 1.6.2f1 UnlockSystem only
    // re-evaluates UnlockRequirement on load, on Unlock events or for Updated entities. ToolbarUISystem also
    // rebuilds its toolbar/category/asset bindings only when an Unlock event for a UIObjectData prefab exists.
    // Groups attached after those points therefore stayed locked and the opened menu was empty. This system
    // applies the vanilla RequireAny rule (a group unlocks when any element is unlocked) the same way
    // UnlockSystem.UnlockPrefab does and posts an Unlock event so the toolbar bindings refresh.
    // Registered at MainLoop like UnlockSystem, so ToolbarUISystem sees the event in the same frame.
    public partial class AreaToolbarUnlockSystem : GameSystemBase
    {
        private AreaPrefabRegistrationSystem registration;
        private PrefabSystem prefabs;
        private EntityArchetype unlockEvent;
        private int publishedVersion = -1;
        private bool reloaded;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            registration = World.GetOrCreateSystemManaged<AreaPrefabRegistrationSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            unlockEvent = EntityManager.CreateArchetype(ComponentType.ReadWrite<Event>(), ComponentType.ReadWrite<Unlock>());
        }
        protected override void OnGameLoaded(Colossal.Serialization.Entities.Context context) { reloaded = true; }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!ToolbarUnlockPolicy.NeedsPublish(registration.ToolbarVersion, publishedVersion, reloaded)) return;
            if (registration.Menu == null || registration.Category == null ||
                !prefabs.TryGetEntity(registration.Menu, out var menu) || !prefabs.TryGetEntity(registration.Category, out var category) ||
                !IsInitializedGroupOf(menu, category)) return;
            bool changed = UnlockIfAnyElementUnlocked(category);
            changed |= UnlockIfAnyElementUnlocked(menu);
            // Always post one event after new items or a load so ToolbarUISystem rebinds, even if already unlocked.
            using (var created = EntityManager.CreateEntity(unlockEvent, 1, Unity.Collections.Allocator.Temp))
                EntityManager.SetComponentData(created[0], new Unlock(category));
            if (changed) Mod.Log.Info("Area toolbar menu unlocked for the native toolbar.");
            publishedVersion = registration.ToolbarVersion;
            reloaded = false;
        }
        // PrefabInitializeSystem has run: the category has its menu link and the menu lists the category.
        private bool IsInitializedGroupOf(Entity menu, Entity category)
        {
            if (!EntityManager.HasBuffer<UIGroupElement>(menu) || !EntityManager.HasBuffer<UIGroupElement>(category)) return false;
            if (EntityManager.GetBuffer<UIGroupElement>(category, true).Length == 0) return false;
            foreach (var element in EntityManager.GetBuffer<UIGroupElement>(menu, true))
                if (element.m_Prefab == category) return true;
            return false;
        }
        private bool UnlockIfAnyElementUnlocked(Entity group)
        {
            if (!EntityManager.HasComponent<Locked>(group) || !EntityManager.IsComponentEnabled<Locked>(group)) return false;
            bool anyUnlocked = false;
            foreach (var element in EntityManager.GetBuffer<UIGroupElement>(group, true))
                if (!EntityManager.HasComponent<Locked>(element.m_Prefab) || !EntityManager.IsComponentEnabled<Locked>(element.m_Prefab)) { anyUnlocked = true; break; }
            if (!ToolbarUnlockPolicy.ShouldUnlock(true, anyUnlocked)) return false;
            EntityManager.SetComponentEnabled<Locked>(group, false);
            return true;
        }
    }
    public static class ToolbarUnlockPolicy
    {
        public static bool NeedsPublish(int currentVersion, int publishedVersion, bool reloaded) => currentVersion > 0 && (reloaded || currentVersion != publishedVersion);
        // Vanilla UIGroupPrefab uses UnlockFlags.RequireAny over its elements.
        public static bool ShouldUnlock(bool locked, bool anyElementUnlocked) => locked && anyElementUnlocked;
    }
}
