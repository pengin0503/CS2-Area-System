using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Semantics
{
    public readonly struct AreaBuiltInAsset
    {
        public string PrefabName { get; }
        public AreaAssetSemantic Semantic { get; }
        public AreaBuiltInAsset(string prefabName, AreaAssetSemantic semantic) { PrefabName = prefabName; Semantic = semantic; }
    }
    // Phase 3 C2 content contract (docs/content/area-system-built-in-assets-contract.md). These three Area System-owned
    // prefabs are the only Headquarters/Entrance sources; identity is the exact ordinal prefab name of a BuildingPrefab
    // loaded from an Area System content package, never a display name or a partial match.
    public static class AreaBuiltInAssetContract
    {
        public const string PackageId = "AS_AreaSystemAssets";
        public const string ProviderId = "CS2AreaSystem.BuiltInContent";
        public const string PrefabType = "BuildingPrefab";
        public const string OwnedPrefix = "AS_";
        public const string PackageExtension = ".cok";
        public static IReadOnlyList<AreaBuiltInAsset> Required { get; } = Array.AsReadOnly(new[] {
            new AreaBuiltInAsset("AS_ParkHeadquarters", AreaAssetSemantic.ParkHeadquarters),
            new AreaBuiltInAsset("AS_IndustrialHeadquarters", AreaAssetSemantic.IndustrialHeadquarters),
            new AreaBuiltInAsset("AS_ParkGate", AreaAssetSemantic.ParkEntrance) });
        public const AreaAssetSemantic RequiredSemantics = AreaAssetSemantic.ParkHeadquarters | AreaAssetSemantic.IndustrialHeadquarters | AreaAssetSemantic.ParkEntrance;
        public static bool TryGetSemantic(string prefabName, out AreaAssetSemantic semantic)
        {
            semantic = AreaAssetSemantic.None;
            if (prefabName == null) return false;
            foreach (var asset in Required)
                if (string.Equals(asset.PrefabName, prefabName, StringComparison.Ordinal)) { semantic = asset.Semantic; return true; }
            return false;
        }
        // The editor names a package after its main asset (PackageHelper/PdxAssetUploadHandle), so any Area System package
        // is "AS_<main asset>.cok". Base-game/DLC databases and unpackaged local prefabs never qualify.
        public static bool IsOwnedSource(bool packaged, bool builtIn, string packageName) =>
            packaged && !builtIn && packageName != null && packageName.Length > OwnedPrefix.Length + PackageExtension.Length &&
            packageName.StartsWith(OwnedPrefix, StringComparison.Ordinal) && packageName.EndsWith(PackageExtension, StringComparison.Ordinal);
        // A dedicated anchor needs this provider's own Allow; another provider cannot grant the role, while normal registry
        // precedence (for example a higher-priority Deny) still applies.
        public static bool Qualifies(AreaAssetSemanticRegistry registry, Unity.Entities.Entity prefab, AreaAssetSemantic semantic) =>
            registry != null && (semantic & RequiredSemantics) == semantic && semantic != AreaAssetSemantic.None &&
            registry.HasProviderAllow(ProviderId, prefab, semantic) && registry.Resolve(prefab, semantic, false);
        public static bool IsContentPresent(AreaAssetSemanticRegistry registry, AreaAssetSemantic semantic) =>
            registry != null && registry.HasProviderAllow(ProviderId, semantic);
    }
}
