using System;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Areas;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Prefabs
{
    public partial class AreaPrefabRegistrationSystem : GameSystemBase
    {
        private PrefabSystem prefabs;
        private AreaCoreServicesSystem services;
        private EntityQuery toolbarGroups;
        private UIAssetCategoryPrefab category;
        private bool registered;
        private bool toolbarAttached;
        private bool missingHostLogged;
        public bool IsReady { get; private set; }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            toolbarGroups = GetEntityQuery(ComponentType.ReadOnly<UIToolbarGroupData>(), ComponentType.ReadOnly<PrefabData>());
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!registered)
            {
                registered = true;
                foreach (var type in services.Registry.All)
                {
                    try
                    {
                        type.Prefab = new ManagedAreaPrefabFactory().CreateAreaPrefab(type);
                        if (!prefabs.AddPrefab(type.Prefab)) Mod.Log.Error("Could not register " + type.PrefabName);
                    }
                    catch (Exception error) { Mod.Log.Error(error, "Area prefab registration failed: " + type.PrefabName); }
                }
            }
            IsReady = true;
            foreach (var type in services.Registry.All)
            {
                if (!IsPrefabReady(type)) IsReady = false;
            }
            // Only expose native toolbar assets once every archetype is initialized.
            if (IsReady && !toolbarAttached) TryAttachToolbar();
        }
        public bool IsPrefabReady(AreaTypeDescriptor type)
        {
            if (type.Prefab == null || !prefabs.TryGetEntity(type.Prefab, out var entity) || !EntityManager.Exists(entity)) return false;
            if (!EntityManager.HasComponent<AreaData>(entity) || !EntityManager.HasComponent<AreaGeometryData>(entity)) return false;
            var geometry = EntityManager.GetComponentData<AreaGeometryData>(entity);
            return AreaPrefabReadiness.CanUse(true, EntityManager.GetComponentData<AreaData>(entity).m_Archetype.Valid,
                geometry.m_Type == ManagedAreaPrefabFactory.NativeGeometryType && geometry.m_Flags == 0 &&
                NativeAreaCreationPolicy.CanCreate(false, geometry.m_Type, geometry.m_Flags, EntityManager.HasComponent<RenderedAreaData>(entity)));
        }
        private void TryAttachToolbar()
        {
            UIToolbarGroupPrefab host = null;
            using var candidates = toolbarGroups.ToEntityArray(Allocator.Temp);
            foreach (var entity in candidates)
                if (prefabs.TryGetPrefab<UIToolbarGroupPrefab>(entity, out var group) && group.name == "Tools Toolbar Group") { host = group; break; }
            if (host == null)
            {
                if (!missingHostLogged) { missingHostLogged = true; Mod.Log.Warn("Tools Toolbar Group unavailable; areas remain loadable. Toolbar attachment will retry."); }
                return;
            }
            try
            {
                var factory = new AreaToolbarPrefabFactory();
                var menu = factory.CreateAreasMenu(host);
                category = factory.CreateAreaTypesCategory(menu);
                if (!prefabs.AddPrefab(menu) || !prefabs.AddPrefab(category)) { Mod.Log.Error("Area toolbar registration failed; simulation remains available."); return; }
                var categoryEntity = prefabs.GetEntity(category);
                int priority = 0;
                foreach (var type in services.Registry.All)
                {
                    factory.AttachAreaPrefab(type.Prefab, category, priority++, type.IconUri);
                    var areaPrefabEntity = prefabs.GetEntity(type.Prefab);
                    EntityManager.SetComponentData(areaPrefabEntity, new UIObjectData { m_Group = categoryEntity, m_Priority = priority });
                    category.AddElement(EntityManager, areaPrefabEntity);
                }
                toolbarAttached = true;
            }
            catch (Exception error) { Mod.Log.Error(error, "Area toolbar failed; simulation remains available."); }
        }
    }
}
