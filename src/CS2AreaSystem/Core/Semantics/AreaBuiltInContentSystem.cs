using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
namespace CS2AreaSystem.Core.Semantics
{
    // Phase 3 C2: once per load generation (AreaLoadCoordinatorSystem clears the registry on preload), rediscovers the
    // dedicated Area System building prefabs among the loaded runtime prefabs and registers their semantics before
    // AreaActivationSystem evaluates anchors. Prefab entities are never saved.
    public partial class AreaBuiltInContentSystem : GameSystemBase
    {
        private AreaCoreServicesSystem services;
        private PrefabSystem prefabs;
        private EntityQuery buildingPrefabs;
        private readonly AreaBuiltInContentSemanticProvider provider = new AreaBuiltInContentSemanticProvider();
        private readonly List<AreaBuiltInPrefabCandidate> candidates = new List<AreaBuiltInPrefabCandidate>();
        private int generation = -1;
        public AreaBuiltInContentResult LastResult { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            buildingPrefabs = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<BuildingData>());
        }
        protected override void OnUpdate()
        {
            if (generation == services.Identity.LoadGeneration) return;
            generation = services.Identity.LoadGeneration;
            try
            {
                candidates.Clear();
                using (var entities = buildingPrefabs.ToEntityArray(Allocator.Temp))
                    foreach (var entity in entities) Collect(entity);
                LastResult = provider.Refresh(services.Semantics, candidates);
                if (LastResult.Missing != AreaAssetSemantic.None || LastResult.Rejected > 0)
                    Mod.Log.Warn("Area System content: registered " + LastResult.Registered + "; missing " + LastResult.Missing + "; rejected exact-name candidates " +
                        LastResult.Rejected + ". Missing roles keep their Areas Suspended; no fallback is used.");
                else Mod.Log.Info("Area System content: registered " + LastResult.Registered + ".");
            }
            catch (System.Exception error)
            {
                // Fail closed: nothing of this provider stays registered, so no Area activates on stale or partial content.
                services.Semantics.UnregisterProvider(AreaBuiltInContentSemanticProvider.ProviderId);
                LastResult = new AreaBuiltInContentResult(AreaAssetSemantic.None, 0);
                Mod.Log.Error(error, "Area System content discovery failed; dedicated Headquarters/Gate roles are unavailable");
            }
        }
        // Phase 3 D2: the effective runtime prefab data vanilla PropertyProcessingSystem reads (BuildingPropertyData, Signature,
        // the signature zone's ZoneData), so a re-imported asset that no longer matches its contract is rejected.
        private AreaBuiltInBuildingFacts ReadBuilding(Entity entity)
        {
            var facts = new AreaBuiltInBuildingFacts();
            if (EntityManager.HasComponent<BuildingPropertyData>(entity))
            {
                var property = EntityManager.GetComponentData<BuildingPropertyData>(entity);
                facts.HasProperty = true; facts.Manufactured = (ulong)property.m_AllowedManufactured; facts.Sold = (ulong)property.m_AllowedSold;
                facts.Stored = (ulong)property.m_AllowedStored; facts.Residential = property.m_ResidentialProperties;
            }
            facts.Signature = EntityManager.HasComponent<SignatureBuildingData>(entity);
            if (EntityManager.HasComponent<SpawnableBuildingData>(entity))
            {
                var zone = EntityManager.GetComponentData<SpawnableBuildingData>(entity).m_ZonePrefab;
                if (zone != Entity.Null && EntityManager.HasComponent<ZoneData>(zone))
                {
                    var data = EntityManager.GetComponentData<ZoneData>(zone);
                    facts.IndustrialZone = data.m_AreaType == Game.Zones.AreaType.Industrial;
                    facts.OfficeZone = (data.m_ZoneFlags & ZoneFlags.Office) != 0;
                }
            }
            return facts;
        }
        private void Collect(Entity entity)
        {
            if (!prefabs.TryGetPrefab<PrefabBase>(entity, out var prefab) || prefab == null) return;
            // Only exact identifiers are inspected further; every other building prefab is skipped without reading its asset.
            if (!AreaBuiltInAssetContract.TryGetAsset(prefab.name, out var asset)) return;
            var meta = prefab.asset != null ? prefab.asset.GetMeta() : default;
            var candidate = new AreaBuiltInPrefabCandidate(entity, prefab.GetType().Name, prefab.name, prefab.asset != null && meta.packaged, prefab.isBuiltin, meta.packageName,
                asset.RequiredManufactured != 0 ? ReadBuilding(entity) : null);
            candidates.Add(candidate);
            if (candidate.Building != null)
                Mod.Log.Info("Area System content facts " + candidate.PrefabName + " manufactured=" + candidate.Building.Manufactured + " sold=" + candidate.Building.Sold +
                    " stored=" + candidate.Building.Stored + " signature=" + candidate.Building.Signature + " industrialZone=" + candidate.Building.IndustrialZone + " officeZone=" + candidate.Building.OfficeZone);
            Mod.Log.Info("Area System content candidate " + candidate.PrefabType + ":" + candidate.PrefabName + " packaged=" + candidate.Packaged +
                " builtIn=" + candidate.BuiltIn + " package=" + (candidate.PackageName ?? "<none>"));
        }
    }
}
