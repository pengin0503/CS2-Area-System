using System;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public sealed class ParkAreaModule : AnchorAreaModule, IAreaDerivedAnchorPolicy, IAreaPolicyProvider, IAreaPolicyUpkeepProvider, Core.Persistence.IAreaSavedStateValidator
    {
        public void ValidateSavedState(Entity area, EntityManager entities, Core.Persistence.AreaSavedStateReport report)
        {
            if (entities.HasComponent<ParkAreaState>(area)) ParkSavedStateValidator.Validate(entities.GetComponentData<ParkAreaState>(area), report);
        }
        private readonly Func<Entity, EntityManager, ParkFacilityTraits> readTraits;
        private readonly Func<Entity, AreaActivationContext, ParkEntranceEvidence> readEntrances;
        private static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly ParkPanelCache panels = new ParkPanelCache(250, () => clock.ElapsedMilliseconds);
        public ParkAreaModule(AreaAnchorService anchors, Func<Entity, EntityManager, ParkFacilityTraits> readTraits = null,
            Func<Entity, AreaActivationContext, ParkEntranceEvidence> readEntrances = null)
            : base(AreaTypeId.Park, anchors, AreaAnchorRole.Headquarters, AreaAnchorRole.Entrance)
        { this.readTraits = readTraits ?? ParkFacilityReader.ReadAnchor; this.readEntrances = readEntrances ?? ReadEntrances; }
        private static ParkEntranceEvidence ReadEntrances(Entity area, AreaActivationContext context)
        {
            if(context.World == null || !context.Entities.HasComponent<ManagedAreaStableId>(area))return null;
            var runtime=context.World.GetExistingSystemManaged<ParkRuntimeSystem>();
            return runtime!=null && runtime.Entrances.TryGet(context.Entities.GetComponentData<ManagedAreaStableId>(area).Value,out var evidence)?evidence:null;
        }
        public override bool CanAssignAnchor(Entity area, AreaAnchorRole role, EntityManager entities) => role == AreaAnchorRole.Headquarters;
        public override bool ValidateActivation(Entity area, in AreaActivationContext context)
        {
            if(!Anchors.HasValidAnchor(area,AreaAnchorRole.Headquarters))return false;
            var evidence=readEntrances(area,context);
            return evidence!=null && evidence.Status==ParkEntranceEvidenceStatus.Complete && evidence.Count>0;
        }
        public override string BuildSummary(Entity area, in AreaPanelContext context)
        {
            var evidence=readEntrances(area,new AreaActivationContext(context.World,context.Entities));
            return ParkActivationSummary.Key(Anchors.HasValidAnchor(area,AreaAnchorRole.Headquarters),Anchors.HasLinks(area,AreaAnchorRole.Headquarters),
                evidence!=null&&evidence.Status==ParkEntranceEvidenceStatus.Complete&&evidence.Count>0,evidence==null||evidence.Status==ParkEntranceEvidenceStatus.Unknown);
        }
        public bool TryGetDerivedAnchor(Entity area,AreaAnchorRole role,in AreaActivationContext context,out int? count,out string statusKey)
        {
            count=null;statusKey=null;if(role!=AreaAnchorRole.Entrance)return false;
            var evidence=readEntrances(area,context);
            bool complete=evidence!=null&&evidence.Status==ParkEntranceEvidenceStatus.Complete;
            count=complete?(int?)evidence.Count:null;statusKey=complete?"CS2AS.UI.PathEntrancesComplete":"CS2AS.UI.PathEntrancesUnknown";return true;
        }
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
            if (role != AreaAnchorRole.Headquarters) return false;
            try { return ParkFacilityClassifier.IsHeadquartersEligible(readTraits(anchor, entities)); }
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
