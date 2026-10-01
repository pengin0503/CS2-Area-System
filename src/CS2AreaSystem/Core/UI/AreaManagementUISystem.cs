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
    // Controller of the managed-area UI. Area details render as a section of the vanilla selected-info panel
    // (AreaInfoSection), whose title section already names/renames the area and whose actions section
    // provides "locate". This system owns the mutating actions, anchor assignment, the managed area list
    // (the only route to select a completed Space area) and the pending-assignment banner.
    public partial class AreaManagementUISystem : UISystemBase
    {
        private AreaCoreServicesSystem services;
        private SelectedInfoUISystem selection;
        private PrefabSystem prefabs;
        private NameSystem names;
        private ToolSystem tools;
        private AreaToolSystem areaTool;
        private EndFrameBarrier endFrameBarrier;
        private Game.Audio.AudioManager audio;
        private EntityQuery soundSettings, managedAreas;
        private RawValueBinding areaList, pendingBinding;
        private ValueBinding<bool> listAvailable, listOpen;
        private readonly AreaListVisibility listVisibility = new AreaListVisibility();
        private ValueBinding<string> panelSide;
        private System.Collections.Generic.List<AreaListEntry> listView = new System.Collections.Generic.List<AreaListEntry>();
        private PendingAssignmentView pendingView;
        private Entity pendingArea;
        private ulong pendingId;
        private AreaAnchorRole pendingRole;
        private bool failureLogged;
        public override GameMode gameMode => GameMode.Game;
        public bool IsReady => !GameManager.instance.isGameLoading && World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready;
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            selection = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            names = World.GetOrCreateSystemManaged<NameSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            areaTool = World.GetOrCreateSystemManaged<AreaToolSystem>();
            endFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            audio = World.GetOrCreateSystemManaged<Game.Audio.AudioManager>();
            soundSettings = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());
            managedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            AddBinding(areaList = new RawValueBinding("cs2AreaSystem", "areaList", writer => AreaListViewModel.Write(writer, listView)));
            AddBinding(pendingBinding = new RawValueBinding("cs2AreaSystem", "pendingAssignment", writer => PendingAssignmentView.Write(writer, pendingView)));
            AddBinding(listAvailable = new ValueBinding<bool>("cs2AreaSystem", "areaListAvailable", false));
            AddBinding(listOpen = new ValueBinding<bool>("cs2AreaSystem", "areaListOpen", false));
            AddBinding(panelSide = new ValueBinding<string>("cs2AreaSystem", "panelSide", "right"));
            AddBinding(new TriggerBinding<bool>("cs2AreaSystem", "setAreaListOpen", open => {
                listVisibility.SetOpen(open); listOpen.Update(listVisibility.Open);
                if (!listVisibility.Open && listView.Count != 0) SetList(new System.Collections.Generic.List<AreaListEntry>());
            }));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "selectArea", SelectArea));
            // Edit and Delete are the only actions on a quarantined Area: they let the player fix or remove it.
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "editArea", id => Act(id, Edit, true)));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "deleteArea", id => Act(id, Delete, true)));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "assignHeadquarters", id => Act(id, area => BeginAssignment(area, AreaAnchorRole.Headquarters))));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "assignEntrance", id => Act(id, area => BeginAssignment(area, AreaAnchorRole.Entrance))));
            AddBinding(new TriggerBinding<string, string>("cs2AreaSystem", "removeAnchor", (id, key) => Act(id, area => RemoveAnchor(area, key))));
            // Generic module action: (expected stable ID, action, value). Guarded like every other mutating action.
            AddBinding(new TriggerBinding<string, string, string>("cs2AreaSystem", "moduleAction", ModuleAction));
            AddBinding(new TriggerBinding<string>("cs2AreaSystem", "cancelAnchorAssignment", CancelAssignment));
            // Policy toggle: (expected stable ID, policy ID, "1"/"0"). Guarded like every other mutating action.
            AddBinding(new TriggerBinding<string, string, string>("cs2AreaSystem", "setPolicyEnabled", (id, policy, enabled) => Act(id, area => SetPolicy(area, policy, enabled))));
            // Register themselves as middle sections of the vanilla selected-info panel, area first, policies below it.
            World.GetOrCreateSystemManaged<AreaInfoSection>();
            World.GetOrCreateSystemManaged<AreaPolicyInfoSection>();
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            pendingArea = Entity.Null; failureLogged = false;
            listVisibility.Update(false, false);
            // The system is disabled outside gameplay, so clear the UI now instead of on the next update.
            SetPending(null);
            listAvailable.Update(false); listOpen.Update(false); SetList(new System.Collections.Generic.List<AreaListEntry>());
        }
        protected override void OnUpdate()
        {
            try
            {
                var options = CS2AreaSystem.Core.Settings.AreaSettings.Current;
                listVisibility.Update(IsReady, options.ShowAreaList);
                listAvailable.Update(listVisibility.Available); listOpen.Update(listVisibility.Open);
                if (!listVisibility.Open && listView.Count != 0) SetList(new System.Collections.Generic.List<AreaListEntry>());
                if (!listVisibility.Available) { SetPending(null); return; }
                panelSide.Update(options.PanelSide == CS2AreaSystem.Core.Settings.AreaPanelSide.Left ? "left" : "right");
                if (listOpen.value) PublishList();
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
                // The banner guides the player while the selection is on a building rather than the area itself.
                SetPending(pendingArea != Entity.Null && selected != pendingArea ? new PendingAssignmentView {
                    StableId = pendingId.ToString(CultureInfo.InvariantCulture), Name = names.GetRenderedLabelName(pendingArea), Role = pendingRole.ToString()
                } : null);
                failureLogged = false;
            }
            catch (Exception error)
            {
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Area management UI unavailable"); }
                SetPending(null);
            }
        }
        private bool IsArea(Entity entity) => services.IsValidEntity(entity) && EntityManager.HasComponent<ManagedArea>(entity) && EntityManager.HasComponent<ManagedAreaStableId>(entity);
        // Content of the selected-info section for a managed area; false for anything else.
        public bool TryBuildView(Entity entity, out AreaManagementViewModel view)
        {
            view = null;
            if (!IsReady || !IsArea(entity)) return false;
            var area = EntityManager.GetComponentData<ManagedArea>(entity);
            if (services.IsQuarantined(entity)) { view = QuarantinedView(entity, area); return true; }
            bool known = services.Registry.TryGet(area.TypeId, out var descriptor);
            var role = (descriptor?.Module as IAreaAnchorPolicy)?.RequiredAnchorRole;
            var context = new Core.Modules.AreaPanelContext(World, EntityManager);
            var provider = descriptor?.Module?.PanelProvider;
            var anchors = role.HasValue ? services.Anchors.GetValidAnchors(entity, role.Value) : System.Array.Empty<Entity>();
            var links = role.HasValue ? services.Anchors.GetAnchorLinks(entity, role.Value) : System.Array.Empty<AreaAnchorLinkState>();
            view = new AreaManagementViewModel {
                StableId = EntityManager.GetComponentData<ManagedAreaStableId>(entity).Value.ToString(CultureInfo.InvariantCulture),
                TypeKey = known ? descriptor.DisplayNameKey : "CS2AS.Type.Unavailable",
                State = "CS2AS.State." + area.State,
                Summary = BuildSummary(provider, entity, context),
                Panel = BuildPanel(provider, entity, context),
                AnchorRole = role?.ToString(),
                AnchorName = anchors.Count > 0 ? names.GetRenderedLabelName(anchors[0]) : null,
                Anchors = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(links, link =>
                    new AreaAnchorView { Key = AreaAnchorLinkKey.Format(link.Anchor), Name = link.Exists ? names.GetRenderedLabelName(link.Anchor) : null, Valid = link.Valid })),
                PendingAnchor = pendingArea == entity,
                ConfirmDelete = CS2AreaSystem.Core.Settings.AreaSettings.Current.ConfirmAreaDeletion
            };
            if (EntityManager.HasComponent<ManagedAreaProgress>(entity))
            { var progress = EntityManager.GetComponentData<ManagedAreaProgress>(entity); view.Level = progress.Level; view.Experience = progress.Experience.ToString(CultureInfo.InvariantCulture); }
            return true;
        }
        // No module content, anchors or progress: the saved data of a quarantined Area is not interpreted.
        private AreaManagementViewModel QuarantinedView(Entity entity, ManagedArea area) => new AreaManagementViewModel {
            StableId = AreaUIKey.Format(entity, 0, true),
            TypeKey = services.Registry.TryGet(area.TypeId, out var descriptor) ? descriptor.DisplayNameKey : "CS2AS.Type.Unavailable",
            State = QuarantinedState,
            Summary = "CS2AS.Summary.Quarantined",
            PendingAnchor = false,
            ConfirmDelete = CS2AreaSystem.Core.Settings.AreaSettings.Current.ConfirmAreaDeletion
        };
        public const string QuarantinedState = "CS2AS.State.Quarantined";
        // A failing module panel degrades to the Overview fallback; Core rows/actions keep working.
        private Core.Modules.AreaPanelDocument BuildPanel(Core.Modules.IAreaPanelProvider provider, Entity entity, in Core.Modules.AreaPanelContext context)
        {
            if (provider == null) return Core.Modules.AreaPanelDocument.Overview();
            try { return provider.BuildPanel(entity, context) ?? Core.Modules.AreaPanelDocument.Overview(); }
            catch (Exception error)
            {
                if (!panelFailureLogged) { panelFailureLogged = true; Mod.Log.Error(error, "Area module panel unavailable"); }
                return Core.Modules.AreaPanelDocument.Overview();
            }
        }
        private string BuildSummary(Core.Modules.IAreaPanelProvider provider, Entity entity, in Core.Modules.AreaPanelContext context)
        {
            if (provider == null) return "CS2AS.Summary.ModuleUnavailable";
            try { return provider.BuildSummary(entity, context) ?? "CS2AS.Summary.ModuleUnavailable"; }
            catch (Exception error)
            {
                if (!panelFailureLogged) { panelFailureLogged = true; Mod.Log.Error(error, "Area module summary unavailable"); }
                return "CS2AS.Summary.ModuleUnavailable";
            }
        }
        private bool panelFailureLogged, policyFailureLogged;
        public bool TryBuildPolicySection(Entity entity, out AreaPolicySectionView view)
        {
            view = null;
            if (!IsReady || !IsArea(entity) || services.IsQuarantined(entity)) return false;
            var policies = World.GetExistingSystemManaged<Policies.AreaPolicySystem>();
            if (policies == null) return false;
            try
            {
                int type = EntityManager.GetComponentData<ManagedArea>(entity).TypeId;
                view = new AreaPolicySectionView {
                    StableId = EntityManager.GetComponentData<ManagedAreaStableId>(entity).Value.ToString(CultureInfo.InvariantCulture),
                    Policies = AreaPolicyViews.Build(policies.Registry.ForAreaType(type), id => policies.GetState(entity, id), d => policies.EstimateDailyUpkeep(entity, d))
                };
                return true;
            }
            catch (Exception error)
            {
                if (!policyFailureLogged) { policyFailureLogged = true; Mod.Log.Error(error, "Area policy section unavailable"); }
                return false;
            }
        }
        // AreaPolicySystem re-validates the policy ID, Area type, current Level, lifecycle and effect availability.
        private void SetPolicy(Entity area, string policyText, string enabledText)
        {
            var policies = World.GetExistingSystemManaged<Policies.AreaPolicySystem>();
            if (policies != null && AreaPolicyToggleRequest.TryParse(policyText, enabledText, out var policyId, out var enabled) &&
                policies.TrySetDesired(area, policyId, enabled, out _))
                selection.SetDirty();
        }
        private void ModuleAction(string expectedId, string action, string value)
        {
            try
            {
                var area = selection.selectedEntity;
                if (!IsReady || !IsArea(area) || services.IsQuarantined(area)) return;
                var id = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
                bool guard = AreaUIActionGuard.Matches(area, selection.selectedEntity, tools.selected, id, expectedId, true);
                services.Registry.TryGet(EntityManager.GetComponentData<ManagedArea>(area).TypeId, out var descriptor);
                if (Core.Modules.AreaModuleActionDispatcher.Dispatch(guard, descriptor?.Module?.PanelProvider, area, action, value,
                        new Core.Modules.AreaPanelContext(World, EntityManager), error => Mod.Log.Error(error, "Area module action failed: " + action)))
                    selection.SetDirty();
            }
            catch (Exception error) { Mod.Log.Error(error, "Area module action rejected"); }
        }
        private void RemoveAnchor(Entity area, string key)
        {
            var type = EntityManager.GetComponentData<ManagedArea>(area).TypeId;
            if (!services.Registry.TryGet(type, out var descriptor) || !(descriptor.Module is IAreaAnchorPolicy policy) || !policy.RequiredAnchorRole.HasValue) return;
            var role = policy.RequiredAnchorRole.Value;
            var links = EntityManager.HasBuffer<AreaAnchorLink>(area) ? new EntityAreaAnchorStore(EntityManager).Read(area) : System.Array.Empty<AreaAnchorLink>();
            if (AreaAnchorLinkKey.TryResolve(links, role, key, out var anchor) && services.Anchors.Remove(area, anchor, role)) selection.SetDirty();
        }
        private void SetPending(PendingAssignmentView next)
        {
            if (PendingAssignmentView.Equivalent(pendingView, next)) return;
            pendingView = next; pendingBinding.Update();
        }
        private void PublishList()
        {
            var entries = new System.Collections.Generic.List<AreaListEntry>();
            using (var areas = managedAreas.ToEntityArray(Unity.Collections.Allocator.Temp))
                foreach (var area in areas)
                {
                    var managed = EntityManager.GetComponentData<ManagedArea>(area);
                    bool quarantined = services.IsQuarantined(area);
                    entries.Add(new AreaListEntry {
                        StableId = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value,
                        Key = quarantined ? AreaUIKey.Format(area, 0, true) : null,
                        Name = names.GetRenderedLabelName(area),
                        TypeKey = services.Registry.TryGet(managed.TypeId, out var descriptor) ? descriptor.DisplayNameKey : "CS2AS.Type.Unavailable",
                        State = quarantined ? QuarantinedState : "CS2AS.State." + managed.State
                    });
                }
            SetList(AreaListViewModel.Sort(entries));
        }
        private void SetList(System.Collections.Generic.List<AreaListEntry> next)
        {
            if (AreaListViewModel.Equivalent(listView, next)) return;
            listView = next; areaList.Update();
        }
        private void SelectArea(string expectedId)
        {
            try
            {
                if (!IsReady) return;
                var candidates = new System.Collections.Generic.List<AreaUICandidate>();
                using (var areas = managedAreas.ToEntityArray(Unity.Collections.Allocator.Temp))
                    foreach (var area in areas)
                        candidates.Add(new AreaUICandidate(area, EntityManager.GetComponentData<ManagedAreaStableId>(area).Value, services.IsQuarantined(area)));
                if (!AreaListViewModel.TryFind(candidates, expectedId, out var selected) || !IsArea(selected)) return;
                pendingArea = Entity.Null;
                selection.SetSelection(selected);
                if (CS2AreaSystem.Core.Settings.AreaSettings.Current.FocusCameraOnSelect) selection.Focus(selected);
            }
            catch (Exception error) { Mod.Log.Error(error, "Area selection from the list rejected"); }
        }
        // Actions come from the section of the currently selected area.
        private void Act(string expectedId, Action<Entity> action, bool allowQuarantined = false)
        {
            try
            {
                var area = selection.selectedEntity;
                if (!IsReady || !IsArea(area)) return;
                if (services.IsQuarantined(area))
                {
                    if (allowQuarantined && AreaUIActionGuard.MatchesQuarantined(area, selection.selectedEntity, tools.selected, expectedId)) action(area);
                    return;
                }
                var id = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
                if (AreaUIActionGuard.Matches(area, selection.selectedEntity, tools.selected, id, expectedId, true)) action(area);
            }
            catch (Exception error) { Mod.Log.Error(error, "Area management action rejected"); }
        }
        private void CancelAssignment(string expectedId)
        {
            try
            {
                if (pendingArea == Entity.Null) return;
                var area = pendingArea;
                bool valid = IsArea(area);
                ulong current = valid ? EntityManager.GetComponentData<ManagedAreaStableId>(area).Value : 0;
                if (!AreaUIActionGuard.CanRestorePending(valid, current, pendingId, expectedId))
                {
                    // A deleted/recreated area must not be reselected; a mismatched ID is a stale request.
                    if (!valid || current != pendingId) pendingArea = Entity.Null;
                    return;
                }
                pendingArea = Entity.Null;
                selection.SetSelection(area);
                selection.SetDirty();
            }
            catch (Exception error) { Mod.Log.Error(error, "Area anchor assignment cancel rejected"); }
        }
        private void Edit(Entity area)
        {
            if (!EntityManager.HasComponent<PrefabRef>(area) || !prefabs.TryGetPrefab<AreaPrefab>(EntityManager.GetComponentData<PrefabRef>(area), out var prefab)) return;
            AreaEditWorkflow.Start(area, entity => areaTool.recreate = entity,
                () => areaTool.prefab = prefab,
                () => areaTool.mode = AreaToolSystem.Mode.Edit,
                () => tools.activeTool = areaTool, selection.Focus);
        }
        private void Delete(Entity area)
        {
            // Same path as the vanilla selected-info delete action (Game.UI.InGame.ActionsSection).
            if (!soundSettings.IsEmptyIgnoreFilter)
                audio.PlayUISound(soundSettings.GetSingleton<ToolUXSoundSettingsData>().m_DeletetEntitySound);
            endFrameBarrier.CreateCommandBuffer().AddComponent<Deleted>(area);
            selection.SetSelection(Entity.Null); pendingArea = Entity.Null;
        }
        private void BeginAssignment(Entity area, AreaAnchorRole role)
        {
            var type = EntityManager.GetComponentData<ManagedArea>(area).TypeId;
            if (!services.Registry.TryGet(type, out var descriptor) || !(descriptor.Module is IAreaAnchorPolicy policy) || policy.RequiredAnchorRole != role) return;
            pendingArea = area; pendingRole = role; pendingId = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
            tools.ActivatePrefabTool(null);
            selection.SetDirty();
        }
    }
    public sealed class PendingAssignmentView
    {
        public string StableId, Name, Role;
        public static bool Equivalent(PendingAssignmentView a, PendingAssignmentView b) =>
            ReferenceEquals(a, b) || (a != null && b != null && a.StableId == b.StableId && a.Name == b.Name && a.Role == b.Role);
        public static void Write(IJsonWriter writer, PendingAssignmentView view)
        {
            if (view == null) { writer.WriteNull(); return; }
            writer.TypeBegin("CS2AreaSystem.PendingAssignment");
            writer.PropertyName("stableId"); writer.Write(view.StableId);
            writer.PropertyName("name"); writer.Write(view.Name);
            writer.PropertyName("role"); writer.Write(view.Role);
            writer.TypeEnd();
        }
    }
}
