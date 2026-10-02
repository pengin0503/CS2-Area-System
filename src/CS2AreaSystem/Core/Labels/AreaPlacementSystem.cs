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
    // Gives every newly identified Area a vanilla CustomName ("<type> <n>", saved by NameSystem and renamed through the
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
            var identified = lifecycle.Identified;
            if (identified.Count == 0 || !GameManager.instance.gameMode.IsGame()) return;
            try
            {
                List<string> taken = null;
                var placed = new List<Entity>(identified.Count);
                foreach (var area in identified)
                {
                    if (!services.IsValidEntity(area) || !EntityManager.HasComponent<ManagedArea>(area)) continue;
                    placed.Add(area);
                    if (names.TryGetCustomName(area, out _)) continue;
                    taken ??= TakenNames();
                    var name = AreaAutoName.Next(TypeName(EntityManager.GetComponentData<ManagedArea>(area).TypeId), taken);
                    names.SetCustomName(area, name);
                    taken.Add(name);
                }
                var selected = AreaAutoName.SelectionAfterPlacement(tools.activeTool == areaTool, placed);
                if (selected != Entity.Null) selection.SetSelection(selected);
            }
            catch (Exception error) { Mod.Log.Error(error, "Area naming after placement failed; the Area keeps its default name"); }
        }
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
