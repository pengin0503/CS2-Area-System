using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using Game;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace CS2AreaSystem.Core.Persistence
{
    public partial class AreaLoadCoordinatorSystem : GameSystemBase
    {
        private readonly AreaLoadSequence sequence = new AreaLoadSequence();
        private AreaCoreServicesSystem services;
        private AreaMembershipSystem membership;
        private Game.Areas.SearchSystem search;
        private EntityQuery ids, areas;
        private bool loadingComplete = true;
        public bool RebuildPending => !sequence.Ready;
        public bool Ready => sequence.Ready;
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            membership = World.GetOrCreateSystemManaged<AreaMembershipSystem>();
            search = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            // Live saved identities only: Deleted/Temp entities do not own an ID (#64). ManagedArea is not required, so an
            // entity that kept its ID but lost ManagedArea still reserves it before ManagedAreaLifecycleSystem repairs it.
            ids = GetEntityQuery(new EntityQueryDesc {
                All = new[] {ComponentType.ReadOnly<ManagedAreaStableId>()},
                None = new[] {ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Temp>()}
            });
            areas = GetEntityQuery(new EntityQueryDesc {
                All = new[] {ComponentType.ReadOnly<ManagedArea>(),ComponentType.ReadOnly<ManagedAreaStableId>()},
                None = new[] {ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Temp>()}
            });
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            loadingComplete = false; sequence.Reset(); membership.RequestRebuild();
            // A new load generation: no stable ID or quarantine of the previous city survives (#62).
            services.Identity.BeginLoad();
        }
        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) => loadingComplete = true;
        protected override void OnUpdate()
        {
            if (!RebuildPending || !loadingComplete || GameManager.instance.isGameLoading) return;
            // Existing managed state can recover independently from toolbar exposure or an unrelated
            // area type's prefab readiness. Native search geometry is the authoritative readiness gate.
            search.GetSearchTree(true, out var dependency, out var triangleCounts); dependency.Complete();
            using var entities = areas.ToEntityArray(Allocator.Temp);
            foreach (var area in entities)
                if (EntityManager.HasComponent<Game.Areas.Area>(area) && !triangleCounts.ContainsKey(area)) return;
            sequence.TryRecover(true, ObserveIdentities, membership.RebuildNow, ValidateModules);
        }
        // The only place saved stable IDs are observed (#65): once per load generation, before membership and activation.
        // Saved-state validation (#69) runs in the same step so every quarantine is known before any consumer runs.
        // Zero and duplicate IDs are quarantined and reported, never renumbered (#63).
        private void ObserveIdentities()
        {
            using var entities = ids.ToEntityArray(Allocator.Temp);
            using var values = ids.ToComponentDataArray<ManagedAreaStableId>(Allocator.Temp);
            var records = new AreaIdentityRecord[entities.Length];
            for (int i = 0; i < records.Length; i++) records[i] = new AreaIdentityRecord(entities[i], values[i].Value);
            foreach (var entry in services.Identity.Observe(records))
                Mod.Log.Error("Managed area " + entry.Area + " has " + (entry.Reason == AreaQuarantineReason.ZeroStableId ? "stable ID 0" :
                    "stable ID " + entry.StableId + ", which another managed area also uses") +
                    ". It is quarantined for this session: its saved data is kept unchanged and it takes no part in membership, " +
                    "activation, policies or finance. Delete the area (or the unwanted copy) and reload to recover.");
            // Before membership is rebuilt, so an Area with uninterpretable saved values never enters the index.
            using var managed = areas.ToEntityArray(Allocator.Temp);
            ValidateSavedState(managed);
        }
        // #69: Core and module saved components are checked once per load, before activation. Nothing is rewritten.
        private void ValidateSavedState(NativeArray<Entity> entities)
        {
            var report = new AreaSavedStateReport();
            foreach (var area in entities)
            {
                report.Clear();
                if (EntityManager.HasComponent<ManagedAreaProgress>(area)) CoreSavedStateValidator.ValidateProgress(EntityManager.GetComponentData<ManagedAreaProgress>(area), report);
                if (EntityManager.HasComponent<ManagedAreaSettings>(area)) CoreSavedStateValidator.ValidateSettings(EntityManager.GetComponentData<ManagedAreaSettings>(area), report);
                if (services.Registry.TryGet(EntityManager.GetComponentData<ManagedArea>(area).TypeId, out var descriptor) && descriptor.Module is IAreaSavedStateValidator module)
                {
                    try { module.ValidateSavedState(area, EntityManager, report); }
                    catch (System.Exception error) { Mod.Log.Error(error, "Saved-state validation failed for managed area " + area + "; its module state is not checked"); }
                }
                ulong id = EntityManager.GetComponentData<ManagedAreaStableId>(area).Value;
                if (report.IsInvalid && services.Quarantine.Add(area, AreaQuarantineReason.InvalidSavedState))
                    Mod.Log.Error("Managed area " + area + " (stable ID " + id + ") has saved values that cannot be interpreted: " +
                        report.Describe(AreaSavedValueSeverity.Invalid) + ". It is quarantined for this session and its saved data is kept unchanged.");
                if (report.HasWarnings)
                    Mod.Log.Warn("Managed area " + area + " (stable ID " + id + ") has out-of-range saved values that are clamped when read: " +
                        report.Describe(AreaSavedValueSeverity.Warning));
            }
        }
        public void ValidateModules()
        {
            using var entities = areas.ToEntityArray(Allocator.Temp);
            var live = new System.Collections.Generic.List<Entity>(entities.Length);
            foreach (var entity in entities) if (!services.IsQuarantined(entity)) live.Add(entity);
            services.Anchors.RebuildReverseIndex(live);
            var activation = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Anchors.AreaActivationSystem>();
            foreach (var entity in live) activation.ValidateArea(entity);
        }
    }
}
