using System;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public sealed class IndustrialAreaModule : AnchorAreaModule, IAreaExistingAnchorPolicy, IAreaPolicyProvider, IAreaPolicyUpkeepProvider, Core.Persistence.IAreaSavedStateValidator
    {
        public void ValidateSavedState(Entity area, EntityManager entities, Core.Persistence.AreaSavedStateReport report)
        {
            if (entities.HasComponent<IndustrialAreaState>(area)) IndustrialSavedStateValidator.Validate(entities.GetComponentData<IndustrialAreaState>(area), report);
        }
        private readonly Func<Entity, EntityManager, IndustrialFacilityTraits> readTraits;
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly IndustrialPanelCache panels = new IndustrialPanelCache(250, () => clock.ElapsedMilliseconds);
        public IndustrialAreaModule(AreaAnchorService anchors, Func<Entity, EntityManager, IndustrialFacilityTraits> readTraits = null)
            : base(AreaTypeId.Industrial, anchors, AreaAnchorRole.Headquarters)
        { this.readTraits = readTraits ?? IndustrialFacilityReader.ReadAnchor; }
        // Initial HQ assignment: an existing member building with supported industrial capability (design §5.1).
        public override bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities)
        {
            if (role != AreaAnchorRole.Headquarters) return false;
            try { return IndustrialFacilityClassifier.IsHeadquartersEligible(readTraits(anchor, entities)); }
            catch (Exception) { return false; }
        }
        // Accepted HQ link: Core already checked that it is a valid, existing member building. Vacancy, bankruptcy or poor
        // operating health never invalidate it, so the assignment classifier is deliberately not consulted again.
        public bool AcceptsExistingAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities) => role == AreaAnchorRole.Headquarters;
        public IndustrialHeadquartersState HeadquartersState(Entity area) =>
            ValidateActivation(area) ? IndustrialHeadquartersState.Valid : Anchors.HasLinks(area, AreaAnchorRole.Headquarters) ? IndustrialHeadquartersState.Invalid : IndustrialHeadquartersState.Missing;
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
        // Industrial v1 has no module actions; Core's HQ/Edit/Delete actions are handled by Core.
        public override bool HandleAction(Entity area, string action, string value, in AreaPanelContext context) => false;
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
