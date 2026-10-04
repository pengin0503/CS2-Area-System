using Game.Economy;
namespace CS2AreaSystem.Modules.Industrial
{
    // Phase 3 D2 reference factory (docs/verification/custom-factory-process-contract.md). The asset authors no recipe: it is
    // an Industrial signature building whose only manufactured resource is vanilla Furniture, so only a vanilla Furniture
    // processing company can rent it (PropertyUtils.PropertyAllowsResource; vanilla PropertyProcessingSystem creates one and
    // the find-property flow moves a tenant in) and that company's own IndustrialProcessData is what observation reads.
    // Nothing in scoring refers to this asset.
    public static class IndustrialReferenceFactoryContract
    {
        public const string PrefabName = "AS_FurnitureFactory";
        public const Resource Output = Resource.Furniture;
        // Documentation only: vanilla EconomyUtils.IsProducedFrom makes Furniture from Timber alone. The building leaves its
        // allowed inputs empty (= any), so the company's own recipe decides the inputs and amounts.
        public const Resource VanillaInput = Resource.Timber;
    }
}
