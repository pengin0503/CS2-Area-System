using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.Semantics
{
    // One runtime building prefab as seen by the content system; prefab entities are valid for the current load only.
    public readonly struct AreaBuiltInPrefabCandidate
    {
        public Entity Prefab { get; }
        public string PrefabType { get; }
        public string PrefabName { get; }
        public bool Packaged { get; }
        public bool BuiltIn { get; }
        public string PackageName { get; }
        public AreaBuiltInPrefabCandidate(Entity prefab, string prefabType, string prefabName, bool packaged, bool builtIn, string packageName)
        { Prefab = prefab; PrefabType = prefabType; PrefabName = prefabName; Packaged = packaged; BuiltIn = builtIn; PackageName = packageName; }
    }
    public readonly struct AreaBuiltInContentResult
    {
        public AreaAssetSemantic Registered { get; }
        public int Rejected { get; }
        public AreaAssetSemantic Missing => AreaBuiltInAssetContract.RequiredSemantics & ~Registered;
        public AreaBuiltInContentResult(AreaAssetSemantic registered, int rejected) { Registered = registered; Rejected = rejected; }
    }
    // Phase 3 C2: registers the three dedicated Area System prefabs. Every refresh drops this provider's previous
    // registrations first, so a stale load generation or removed content never keeps a role.
    public sealed class AreaBuiltInContentSemanticProvider
    {
        public const string ProviderId = AreaBuiltInAssetContract.ProviderId;
        public const int Priority = 0;
        public AreaBuiltInContentResult Refresh(AreaAssetSemanticRegistry registry, IEnumerable<AreaBuiltInPrefabCandidate> candidates)
        {
            registry.UnregisterProvider(ProviderId);
            var registered = AreaAssetSemantic.None; int rejected = 0;
            foreach (var candidate in candidates)
            {
                if (!AreaBuiltInAssetContract.TryGetSemantic(candidate.PrefabName, out var semantic)) continue;
                if (candidate.Prefab == Entity.Null || !string.Equals(candidate.PrefabType, AreaBuiltInAssetContract.PrefabType, System.StringComparison.Ordinal) ||
                    !AreaBuiltInAssetContract.IsOwnedSource(candidate.Packaged, candidate.BuiltIn, candidate.PackageName) ||
                    !registry.Register(ProviderId, candidate.Prefab, new AreaAssetSemanticPatch(semantic, AreaAssetSemantic.None, Priority))) { rejected++; continue; }
                registered |= semantic;
            }
            return new AreaBuiltInContentResult(registered, rejected);
        }
    }
}
