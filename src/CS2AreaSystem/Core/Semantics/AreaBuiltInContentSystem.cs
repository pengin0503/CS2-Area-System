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
        private void Collect(Entity entity)
        {
            if (!prefabs.TryGetPrefab<PrefabBase>(entity, out var prefab) || prefab == null) return;
            // Only exact identifiers are inspected further; every other building prefab is skipped without reading its asset.
            if (!AreaBuiltInAssetContract.TryGetSemantic(prefab.name, out _)) return;
            var meta = prefab.asset != null ? prefab.asset.GetMeta() : default;
            var candidate = new AreaBuiltInPrefabCandidate(entity, prefab.GetType().Name, prefab.name, prefab.asset != null && meta.packaged, prefab.isBuiltin, meta.packageName);
            candidates.Add(candidate);
            Mod.Log.Info("Area System content candidate " + candidate.PrefabType + ":" + candidate.PrefabName + " packaged=" + candidate.Packaged +
                " builtIn=" + candidate.BuiltIn + " package=" + (candidate.PackageName ?? "<none>"));
        }
    }
}
