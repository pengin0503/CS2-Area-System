using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Labels
{
    // Gives every newly identified Area (and, once per load, every unnamed saved Area) a vanilla CustomName ("<type> <n>", saved by NameSystem and renamed through the
    // vanilla title section) and selects the Area the player just drew, like a district after placement.
    public partial class AreaPlacementSystem : GameSystemBase
    {
        private ManagedAreaLifecycleSystem lifecycle;
        private AreaCoreServicesSystem services;
        private NameSystem names;
        private ToolSystem tools;
        private AreaToolSystem areaTool;
        private SelectedInfoUISystem selection;
        private EntityQuery managedAreas;
        private int namedGeneration = -1;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            lifecycle = World.GetOrCreateSystemManaged<ManagedAreaLifecycleSystem>();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            names = World.GetOrCreateSystemManaged<NameSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            areaTool = World.GetOrCreateSystemManaged<AreaToolSystem>();
            selection = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            managedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!GameManager.instance.gameMode.IsGame()) return;
            try
            {
                // Areas saved before auto-naming existed carry ManagedArea already and never reach Identified: name the
                // unnamed ones once per load, in stable-ID order so the numbering does not depend on entity layout.
                if (namedGeneration != services.Identity.LoadGeneration && services.Identity.Observed &&
                    World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>().Ready)
                {
                    namedGeneration = services.Identity.LoadGeneration;
                    NameUnnamed(LoadedAreas());
                }
                var identified = lifecycle.Identified;
                if (identified.Count == 0) return;
                var placed = new List<Entity>(identified.Count);
                foreach (var area in identified)
                    if (services.IsValidEntity(area) && EntityManager.HasComponent<ManagedArea>(area)) placed.Add(area);
                NameUnnamed(placed);
                var selected = AreaAutoName.SelectionAfterPlacement(tools.activeTool == areaTool, placed);
                if (selected != Entity.Null) selection.SetSelection(selected);
            }
            catch (Exception error) { Mod.Log.Error(error, "Area naming failed; the Area keeps its default name"); }
        }
        private void NameUnnamed(IReadOnlyList<Entity> areas)
        {
            List<string> taken = null;
            foreach (var area in areas)
            {
                if (names.TryGetCustomName(area, out _)) continue;
                taken ??= TakenNames();
                var name = AreaAutoName.Next(TypeName(EntityManager.GetComponentData<ManagedArea>(area).TypeId), taken);
                names.SetCustomName(area, name);
                taken.Add(name);
            }
        }
        private List<Entity> LoadedAreas()
        {
            var areas = new List<Entity>();
            using (var entities = managedAreas.ToEntityArray(Allocator.Temp)) areas.AddRange(entities);
            areas.Sort((a, b) => StableId(a).CompareTo(StableId(b)));
            return areas;
        }
        private ulong StableId(Entity area) =>
            EntityManager.HasComponent<ManagedAreaStableId>(area) ? EntityManager.GetComponentData<ManagedAreaStableId>(area).Value : ulong.MaxValue;
        private List<string> TakenNames()
        {
            var taken = new List<string>();
            using var areas = managedAreas.ToEntityArray(Allocator.Temp);
            foreach (var area in areas) taken.Add(names.GetRenderedLabelName(area));
            return taken;
        }
        private string TypeName(int typeId)
        {
            if (!services.Registry.TryGet(typeId, out var descriptor)) return null;
            var dictionary = GameManager.instance.localizationManager?.activeDictionary;
            return dictionary != null && dictionary.TryGetValue(descriptor.DisplayNameKey, out var value) ? value : null;
        }
    }
}
