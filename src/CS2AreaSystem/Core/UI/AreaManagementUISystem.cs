using System;
using System.Globalization;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Unity.Entities;
namespace CS2AreaSystem.Core.UI
{
    public partial class AreaManagementUISystem : UISystemBase
    {
        private AreaCoreServicesSystem services;
        private SelectedInfoUISystem selection;
        private PrefabSystem prefabs;
        private NameSystem names;
        private ToolSystem tools;
        private RawValueBinding selectedArea;
        private ValueBinding<bool> visible;
        private Entity published, pendingArea;
        private ulong pendingId;
        private AreaAnchorRole pendingRole;
        private AreaManagementViewModel view;
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            selection = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            names = World.GetOrCreateSystemManaged<NameSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            AddBinding(selectedArea = new RawValueBinding("cs2AreaSystem", "selectedArea", writer => { if (view == null) writer.WriteNull(); else view.Write(writer); }));
            AddBinding(visible = new ValueBinding<bool>("cs2AreaSystem", "panelVisible", false));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "editArea", id => Act(id, Edit)));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "locateArea", id => Act(id, selection.Focus)));
            AddBinding(new TriggerBinding<string, string>("cs2AreaSystem", "renameArea", (id, name) => Act(id, area => {
                if (!string.IsNullOrWhiteSpace(name) && name.Length <= 128) names.SetCustomName(area, name.Trim());
            })));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "deleteArea", id => Act(id, area => {
                EntityManager.AddComponent<Deleted>(area); selection.SetSelection(Entity.Null); pendingArea = Entity.Null;
            })));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "assignHeadquarters", id => Act(id, area => BeginAssignment(area, AreaAnchorRole.Headquarters))));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "assignEntrance", id => Act(id, area => BeginAssignment(area, AreaAnchorRole.Entrance))));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "cancelAnchorAssignment", id => {
                if (pendingArea != Entity.Null && id == pendingId.ToString(CultureInfo.InvariantCulture)) { selection.SetSelection(pendingArea); pendingArea = Entity.Null; }
            }));
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        { base.OnGamePreload(purpose, mode); pendingArea = published = Entity.Null; view = null; }
        protected override void OnUpdate()
        {
            try
            {
                if (GameManager.instance.isGameLoading || !World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready) { Publish(Entity.Null); return; }
                var selected = selection.selectedEntity;
                if (pendingArea != Entity.Null)
                {
                    if (!IsArea(pendingArea) || EntityManager.GetComponentData<ManagedAreaStableId>(pendingArea).Value != pendingId)
                        pendingArea = Entity.Null;
                    else if (selected != pendingArea && IsArea(selected)) pendingArea = Entity.Null;
                    else if (selected != pendingArea && services.IsValidEntity(selected) && EntityManager.HasComponent<Game.Buildings.Building>(selected) &&
                        services.Anchors.Assign(pendingArea, selected, pendingRole))
                    { selection.SetSelection(pendingArea); selected = pendingArea; pendingArea = Entity.Null; }
                }
                Publish(pendingArea != Entity.Null ? pendingArea : selected);
            }
            catch (Exception error) { Mod.Log.Error(error, "Area management UI unavailable"); view = null; published = Entity.Null; visible.Update(false); }
        }
        private bool IsArea(Entity entity) => services.IsValidEntity(entity) && EntityManager.HasComponent<ManagedArea>(entity) && EntityManager.HasComponent<ManagedAreaStableId>(entity);
        private void Publish(Entity entity)
        {
            published = IsArea(entity) ? entity : Entity.Null;
            view = null;
            if (published != Entity.Null)
            {
                var area = EntityManager.GetComponentData<ManagedArea>(published);
                bool known = services.Registry.TryGet(area.TypeId, out var descriptor);
                view = new AreaManagementViewModel {
                    StableId = EntityManager.GetComponentData<ManagedAreaStableId>(published).Value.ToString(CultureInfo.InvariantCulture),
                    Name = names.GetRenderedLabelName(published), TypeKey = known ? descriptor.DisplayNameKey : "Unavailable module (" + area.TypeId + ")",
                    State = area.State.ToString(), Summary = descriptor?.Module?.PanelProvider?.BuildSummary(published) ?? "Module unavailable",
                    Tabs = descriptor?.Module?.PanelProvider?.Tabs ?? new Core.Modules.AreaPanelTab[0],
                    AnchorRole = (descriptor?.Module as IAreaAnchorPolicy)?.RequiredAnchorRole?.ToString(), PendingAnchor = pendingArea != Entity.Null
                };
                if (EntityManager.HasComponent<ManagedAreaProgress>(published))
                { var progress = EntityManager.GetComponentData<ManagedAreaProgress>(published); view.Level = progress.Level; view.Experience = progress.Experience.ToString(CultureInfo.InvariantCulture); }
            }
            visible.Update(view != null); selectedArea.Update();
        }
        private void Act(string expectedId, Action<Entity> action)
        {
            try
            {
                if (GameManager.instance.isGameLoading || !World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready || !IsArea(published)) return;
                var id = EntityManager.GetComponentData<ManagedAreaStableId>(published).Value;
                if (AreaUIActionGuard.Matches(published, selection.selectedEntity, id, expectedId, IsArea(published))) action(published);
            }
            catch (Exception error) { Mod.Log.Error(error, "Area management action rejected"); }
        }
        private void Edit(Entity area)
        {
            if (!EntityManager.HasComponent<PrefabRef>(area) || !prefabs.TryGetPrefab<AreaPrefab>(EntityManager.GetComponentData<PrefabRef>(area), out var prefab)) return;
            // Native ActionsSection lot-edit pattern: select prefab and activate AreaToolSystem.
            tools.ActivatePrefabTool(prefab);
            if (tools.activeTool is AreaToolSystem native) native.mode = AreaToolSystem.Mode.Edit;
        }
        private void BeginAssignment(Entity area, AreaAnchorRole role)
        {
            var type = EntityManager.GetComponentData<ManagedArea>(area).TypeId;
            if (!services.Registry.TryGet(type, out var descriptor) || !(descriptor.Module is IAreaAnchorPolicy policy) || policy.RequiredAnchorRole != role) return;
            pendingArea = area; pendingRole = role; pendingId = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
            tools.ActivatePrefabTool(null);
        }
    }
}
