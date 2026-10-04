using System;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
using CS2AreaSystem.Core.Semantics;
namespace CS2AreaSystem.Modules.Park
{
    public sealed class ParkAreaModule : AnchorAreaModule, IAreaPolicyProvider, IAreaPolicyUpkeepProvider, Core.Persistence.IAreaSavedStateValidator
    {
        public void ValidateSavedState(Entity area, EntityManager entities, Core.Persistence.AreaSavedStateReport report)
        {
            if (entities.HasComponent<ParkAreaState>(area)) ParkSavedStateValidator.Validate(entities.GetComponentData<ParkAreaState>(area), report);
            if (entities.HasComponent<ParkEventState>(area)) ParkSavedStateValidator.ValidateEvents(entities.GetComponentData<ParkEventState>(area), report);
        }
        private readonly Func<Entity, EntityManager, ParkFacilityTraits> readTraits;
        private readonly AreaAssetSemanticRegistry semantics;
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly ParkPanelCache panels = new ParkPanelCache(250, () => clock.ElapsedMilliseconds);
        public ParkAreaModule(AreaAnchorService anchors, Func<Entity, EntityManager, ParkFacilityTraits> readTraits = null, AreaAssetSemanticRegistry semantics=null)
            : base(AreaTypeId.Park, anchors, AreaAnchorRole.Headquarters, AreaAnchorRole.Entrance)
        { this.readTraits = readTraits ?? ParkFacilityReader.ReadAnchor; this.semantics=semantics; }
        // Phase 3 C2: Headquarters accepts only AS_ParkHeadquarters and Entrance only AS_ParkGate (see AcceptsAnchor). A role
        // whose AS_ package is not installed offers no pick mode, since no building could be accepted.
        public override bool CanAssignAnchor(Entity area, AreaAnchorRole role, EntityManager entities) =>
            (role == AreaAnchorRole.Headquarters || role == AreaAnchorRole.Entrance) &&
            (semantics == null || AreaBuiltInAssetContract.IsContentPresent(semantics, role == AreaAnchorRole.Headquarters ? AreaAssetSemantic.ParkHeadquarters : AreaAssetSemantic.ParkEntrance));
        public override bool ValidateActivation(Entity area, in AreaActivationContext context) =>
            Anchors.HasValidAnchor(area,AreaAnchorRole.Headquarters) && Anchors.HasValidAnchor(area,AreaAnchorRole.Entrance);
        public override string BuildSummary(Entity area, in AreaPanelContext context) =>
            ParkActivationSummary.Key(ParkActivationSummary.State(Anchors,semantics,area,AreaAnchorRole.Headquarters),
                ParkActivationSummary.State(Anchors,semantics,area,AreaAnchorRole.Entrance));
        public override AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context)
        {
            var captured = context;
            return panels.Get(area, () => ParkPanelBuilder.Build(ParkGameplayFacade.Read(area, captured)));
        }
        // Core already validated the stable ID and live selection; only the facade's approved setters run.
        public override bool HandleAction(Entity area, string action, string value, in AreaPanelContext context)
        {
            bool applied = ParkGameplayFacade.HandleAction(area, action, value, context);
            if (applied) panels.Invalidate();
            return applied;
        }
        public override bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities)
        {
            if (role != AreaAnchorRole.Headquarters && role!=AreaAnchorRole.Entrance) return false;
            try { var traits=readTraits(anchor,entities);return role==AreaAnchorRole.Headquarters?ParkFacilityClassifier.IsHeadquartersEligible(traits,semantics):
                ParkFacilityClassifier.IsEntranceEligible(traits,semantics); }
            catch (Exception) { return false; }
        }
        public System.Collections.Generic.IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions => ParkPolicyCatalog.Definitions;
        public bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey) =>
            ParkPolicyCatalog.IsEffectAvailable(policyId, out reasonKey);
        public bool TryGetPolicyUpkeepInputs(Entity area, int policyId, in AreaPolicyContext context, out AreaPolicyUpkeepInputs inputs)
        {
            inputs = default;
            var runtime = context.World?.GetExistingSystemManaged<ParkRuntimeSystem>();
            if (runtime == null || !context.Entities.HasComponent<ManagedAreaStableId>(area) ||
                !runtime.Store.TryGet(context.Entities.GetComponentData<ManagedAreaStableId>(area).Value, out var snapshot)) return false;
            var facilities = ParkPolicyCatalog.EligibleFacilities(snapshot.Facilities);
            if (!facilities.HasValue) return false;
            inputs.EligibleFacilityCount = facilities.Value;
            return true;
        }
    }
}
