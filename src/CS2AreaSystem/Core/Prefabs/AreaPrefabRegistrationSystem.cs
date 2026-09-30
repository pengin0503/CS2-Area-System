using System;
using System.Collections.Generic;
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
        private UIAssetMenuPrefab menu;
        private UIAssetCategoryPrefab category;
        private readonly AreaTypeReadiness readiness = new AreaTypeReadiness();
        private readonly Dictionary<int, AreaPrefab> pendingPrefabs = new Dictionary<int, AreaPrefab>();
        private readonly HashSet<int> registrationFailureLogged = new HashSet<int>();
        private readonly HashSet<int> toolbarTypes = new HashSet<int>();
        private readonly HashSet<int> toolbarItemFailureLogged = new HashSet<int>();
        private readonly GameUIResourceAvailability uiResources = new GameUIResourceAvailability();
        private readonly Dictionary<int, string> resolvedIcons = new Dictionary<int, string>();
        private bool missingHostLogged;
        private bool toolbarRegistrationFailureLogged;
        public bool IsReady
        {
            get
            {
                if (services == null) return false;
                bool any = false;
                foreach (var type in services.Registry.All)
                {
                    any = true;
                    if (!readiness.IsReady(type.TypeId)) return false;
                }
                return any;
            }
        }
        public bool IsTypeReady(int typeId) => readiness.IsReady(typeId);
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
            foreach (var type in services.Registry.All)
            {
                EnsurePrefabRegistered(type);
                readiness.Set(type.TypeId, IsPrefabReady(type));
            }
            TryAttachToolbar();
        }
        private void EnsurePrefabRegistered(AreaTypeDescriptor type)
        {
            bool registeredWithPrefabSystem = type.Prefab != null && prefabs.TryGetEntity(type.Prefab, out var registeredEntity) && EntityManager.Exists(registeredEntity);
            if (!AreaPrefabRegistrationPolicy.ShouldAttempt(type.Prefab != null, registeredWithPrefabSystem)) return;

            AreaPrefab candidate = type.Prefab;
            if (candidate != null)
            {
                type.Prefab = null;
                pendingPrefabs[type.TypeId] = candidate;
            }
            if (candidate == null && !pendingPrefabs.TryGetValue(type.TypeId, out candidate))
            {
                try
                {
                    candidate = new ManagedAreaPrefabFactory().CreateAreaPrefab(type, ResolveIcon(type));
                    pendingPrefabs[type.TypeId] = candidate;
                }
                catch (Exception error)
                {
                    if (registrationFailureLogged.Add(type.TypeId)) Mod.Log.Error(error, "Area prefab creation failed: " + type.PrefabName);
                    return;
                }
            }
            try
            {
                if (prefabs.TryGetEntity(candidate, out var existing) && EntityManager.Exists(existing))
                {
                    type.Prefab = candidate;
                    pendingPrefabs.Remove(type.TypeId);
                    registrationFailureLogged.Remove(type.TypeId);
                    return;
                }
                if (prefabs.AddPrefab(candidate))
                {
                    type.Prefab = candidate;
                    pendingPrefabs.Remove(type.TypeId);
                    registrationFailureLogged.Remove(type.TypeId);
                    return;
                }
                if (registrationFailureLogged.Add(type.TypeId))
                    Mod.Log.Error("Could not register " + type.PrefabName + "; this area type will retry without blocking other types.");
            }
            catch (Exception error)
            {
                if (registrationFailureLogged.Add(type.TypeId)) Mod.Log.Error(error, "Area prefab registration failed: " + type.PrefabName);
            }
        }
        // Resolved once per type so the prefab and its toolbar entry always use the same icon.
        private string ResolveIcon(AreaTypeDescriptor type)
        {
            if (resolvedIcons.TryGetValue(type.TypeId, out var icon)) return icon;
            icon = ManagedAreaPrefabFactory.ResolveIcon(type, IconAvailable);
            if (icon != type.IconUri) Mod.Log.Warn("Area icon " + type.IconUri + " is unavailable in this game version; using " + icon + " for " + type.PrefabName);
            else if (!IconAvailable(icon)) Mod.Log.Warn("Game UI resources could not be verified; keeping area icon " + icon + " for " + type.PrefabName);
            resolvedIcons[type.TypeId] = icon;
            return icon;
        }
        private bool IconAvailable(string path)
        {
            try { return uiResources.Exists(path); }
            catch (Exception error) { Mod.Log.Warn(error, "Game UI resource lookup failed for " + path); return false; }
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
        private bool NeedsToolbarWork()
        {
            foreach (var type in services.Registry.All)
                if (IsTypeReady(type.TypeId) && !toolbarTypes.Contains(type.TypeId)) return true;
            return false;
        }
        private void TryAttachToolbar()
        {
            if (!NeedsToolbarWork()) return;
            UIToolbarGroupPrefab host = null;
            using var candidates = toolbarGroups.ToEntityArray(Allocator.Temp);
            foreach (var entity in candidates)
                if (prefabs.TryGetPrefab<UIToolbarGroupPrefab>(entity, out var group) && group.name == "Tools Toolbar Group") { host = group; break; }
            if (host == null)
            {
                if (!missingHostLogged) { missingHostLogged = true; Mod.Log.Warn("Tools Toolbar Group unavailable; areas remain loadable. Toolbar attachment will retry."); }
                return;
            }
            missingHostLogged = false;
            try
            {
                var factory = new AreaToolbarPrefabFactory();
                if (menu == null) menu = factory.CreateAreasMenu(host);
                if (!prefabs.TryGetEntity(menu, out var menuEntity) || !EntityManager.Exists(menuEntity))
                {
                    if (!prefabs.AddPrefab(menu))
                    {
                        if (!toolbarRegistrationFailureLogged) { toolbarRegistrationFailureLogged = true; Mod.Log.Error("Area toolbar menu registration failed; simulation remains available and registration will retry."); }
                        return;
                    }
                }
                if (category == null) category = factory.CreateAreaTypesCategory(menu);
                if (!prefabs.TryGetEntity(category, out var categoryEntity) || !EntityManager.Exists(categoryEntity))
                {
                    if (!prefabs.AddPrefab(category))
                    {
                        if (!toolbarRegistrationFailureLogged) { toolbarRegistrationFailureLogged = true; Mod.Log.Error("Area toolbar category registration failed; simulation remains available and registration will retry."); }
                        return;
                    }
                    categoryEntity = prefabs.GetEntity(category);
                }
                toolbarRegistrationFailureLogged = false;
                foreach (var type in services.Registry.All)
                {
                    if (!IsTypeReady(type.TypeId) || toolbarTypes.Contains(type.TypeId)) continue;
                    try
                    {
                        int priority = Math.Max(0, type.TypeId - 1);
                        string icon = ResolveIcon(type);
                        factory.AttachAreaPrefab(type.Prefab, category, priority, icon);
                        var areaPrefabEntity = prefabs.GetEntity(type.Prefab);
                        EntityManager.SetComponentData(areaPrefabEntity, new UIObjectData { m_Group = categoryEntity, m_Priority = priority });
                        category.AddElement(EntityManager, areaPrefabEntity);
                        toolbarTypes.Add(type.TypeId);
                        toolbarItemFailureLogged.Remove(type.TypeId);
                    }
                    catch (Exception error)
                    {
                        if (toolbarItemFailureLogged.Add(type.TypeId))
                            Mod.Log.Error(error, "Area toolbar item failed: " + type.PrefabName + "; other area types remain available and this item will retry.");
                    }
                }
            }
            catch (Exception error)
            {
                if (!toolbarRegistrationFailureLogged) { toolbarRegistrationFailureLogged = true; Mod.Log.Error(error, "Area toolbar failed; simulation remains available and registration will retry."); }
            }
        }
    }
}
