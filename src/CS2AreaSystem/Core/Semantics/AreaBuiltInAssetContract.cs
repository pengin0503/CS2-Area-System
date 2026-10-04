using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Semantics
{
    public readonly struct AreaBuiltInAsset
    {
        public string PrefabName { get; }
        public AreaAssetSemantic Semantic { get; }
        // Phase 3 D2: when non-zero, the loaded prefab must manufacture exactly this vanilla Resource bit (see Matches).
        public ulong RequiredManufactured { get; }
        public AreaBuiltInAsset(string prefabName, AreaAssetSemantic semantic, ulong requiredManufactured = 0)
        { PrefabName = prefabName; Semantic = semantic; RequiredManufactured = requiredManufactured; }
    }
    // Static building facts of a loaded prefab entity, read only for exact-name candidates.
    public sealed class AreaBuiltInBuildingFacts
    {
        public bool HasProperty;
        public ulong Manufactured, Sold, Stored;
        public int Residential;
        public bool Signature, IndustrialZone, OfficeZone;
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
        // Phase 3 D2 optional content (docs/verification/custom-factory-process-contract.md): never an anchor, never required.
        // The reference factory is an Industrial signature building manufacturing vanilla Furniture only (512).
        public static IReadOnlyList<AreaBuiltInAsset> Optional { get; } = Array.AsReadOnly(new[] {
            new AreaBuiltInAsset("AS_FurnitureFactory", AreaAssetSemantic.IndustrialProducer, (ulong)Game.Economy.Resource.Furniture) });
        public static bool TryGetSemantic(string prefabName, out AreaAssetSemantic semantic)
        {
            bool found = TryGetAsset(prefabName, out var asset);
            semantic = found ? asset.Semantic : AreaAssetSemantic.None;
            return found;
        }
        public static bool TryGetAsset(string prefabName, out AreaBuiltInAsset asset)
        {
            asset = default;
            if (prefabName == null) return false;
            foreach (var list in new[] { Required, Optional })
                foreach (var candidate in list)
                    if (string.Equals(candidate.PrefabName, prefabName, StringComparison.Ordinal)) { asset = candidate; return true; }
            return false;
        }
        // A content asset with a manufacturing contract must be what vanilla PropertyProcessingSystem fills with exactly one
        // vanilla processing company: an Industrial (non-office) signature building manufacturing only that resource, selling,
        // storing and housing nothing. Assets without such a contract (the C2 anchors) are identified by name/source only.
        public static bool Matches(in AreaBuiltInAsset asset, AreaBuiltInBuildingFacts facts) =>
            asset.RequiredManufactured == 0 || (facts != null && facts.HasProperty && facts.Manufactured == asset.RequiredManufactured && facts.Sold == 0 &&
            facts.Stored == 0 && facts.Residential == 0 && facts.Signature && facts.IndustrialZone && !facts.OfficeZone);
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
