using System;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
using CS2AreaSystem.Core.Semantics;
namespace CS2AreaSystem.Modules.Industrial
{
    public sealed class IndustrialAreaModule : AnchorAreaModule, IAreaExistingAnchorPolicy, IAreaPolicyProvider, IAreaPolicyUpkeepProvider, Core.Persistence.IAreaSavedStateValidator
    {
        public void ValidateSavedState(Entity area, EntityManager entities, Core.Persistence.AreaSavedStateReport report)
        {
            if (entities.HasComponent<IndustrialAreaState>(area)) IndustrialSavedStateValidator.Validate(entities.GetComponentData<IndustrialAreaState>(area), report);
        }
        private readonly Func<Entity, EntityManager, IndustrialFacilityTraits> readTraits;
        private readonly AreaAssetSemanticRegistry semantics;
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly IndustrialPanelCache panels = new IndustrialPanelCache(250, () => clock.ElapsedMilliseconds);
        public IndustrialAreaModule(AreaAnchorService anchors, Func<Entity, EntityManager, IndustrialFacilityTraits> readTraits = null,AreaAssetSemanticRegistry semantics=null)
            : base(AreaTypeId.Industrial, anchors, AreaAnchorRole.Headquarters)
        { this.readTraits = readTraits ?? IndustrialFacilityReader.ReadAnchor;this.semantics=semantics; }
        // Headquarters is an office member building; production/logistics observations remain separate.
        public override bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities)
        {
            if (role != AreaAnchorRole.Headquarters) return false;
            try { return IndustrialFacilityClassifier.IsHeadquartersEligible(readTraits(anchor, entities),semantics); }
            catch (Exception) { return false; }
        }
        // Revalidate static office evidence, including legacy links; vacancy/health do not affect OfficeProperty.
        public bool AcceptsExistingAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities) => AcceptsAnchor(area, anchor, role, entities);
        public IndustrialHeadquartersState HeadquartersState(Entity area) =>
            ValidateActivation(area, default) ? IndustrialHeadquartersState.Valid : Anchors.HasLinks(area, AreaAnchorRole.Headquarters) ? IndustrialHeadquartersState.Invalid : IndustrialHeadquartersState.Missing;
        public override string BuildSummary(Entity area, in AreaPanelContext context)
        {
            switch (HeadquartersState(area))
            {
                case IndustrialHeadquartersState.Valid: return IndustrialPanelBuilder.P + "Summary.Active";
                case IndustrialHeadquartersState.Invalid: return IndustrialPanelBuilder.P + "Summary.HeadquartersInvalid";
                default: return IndustrialPanelBuilder.P + "Summary.HeadquartersRequired";
            }
        }
        public override AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context)
        {
            var captured = context;
            return panels.Get(area, () => {
                var model = IndustrialGameplayFacade.Read(area, captured);
                model.Headquarters = HeadquartersState(area);
                return IndustrialPanelBuilder.Build(model);
            });
        }
        // The only Industrial module action is the read-only diagnostic focus; Core's HQ/Edit/Delete actions are handled by Core.
        public override bool HandleAction(Entity area, string action, string value, in AreaPanelContext context) =>
            IndustrialGameplayFacade.HandleAction(area, action, value, context);
        public System.Collections.Generic.IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions => IndustrialPolicyCatalog.Definitions;
        public bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey)
        {
            if (!IndustrialPolicyCatalog.IsEffectAvailable(policyId, out reasonKey)) return false;
            // A verified adapter still needs the effect system to apply it and to know this Area's failures.
            var effects = context.World?.GetExistingSystemManaged<IndustrialPolicyEffectSystem>();
            if (effects == null || !context.Entities.HasComponent<ManagedAreaStableId>(area)) { reasonKey = AreaPolicyReasons.EffectUnavailable; return false; }
            return effects.Controller.IsAvailable(area, context.Entities.GetComponentData<ManagedAreaStableId>(area).Value, policyId, context, out reasonKey);
        }
        public bool TryGetPolicyUpkeepInputs(Entity area, int policyId, in AreaPolicyContext context, out AreaPolicyUpkeepInputs inputs)
        {
            inputs = default;
            var runtime = context.World?.GetExistingSystemManaged<IndustrialRuntimeSystem>();
            if (runtime == null || !context.Entities.HasComponent<ManagedAreaStableId>(area) ||
                !runtime.Store.TryGet(context.Entities.GetComponentData<ManagedAreaStableId>(area).Value, out var snapshot)) return false;
            var facilities = IndustrialPolicyCatalog.EligibleFacilities(policyId, snapshot.Current);
            if (!facilities.HasValue) return false;
            inputs.EligibleFacilityCount = facilities.Value;
            return true;
        }
    }
}
