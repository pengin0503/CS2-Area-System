using System;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public sealed class ParkAreaModule : AnchorAreaModule
    {
        private readonly Func<Entity, EntityManager, ParkFacilityTraits> readTraits;
        public ParkAreaModule(AreaAnchorService anchors, Func<Entity, EntityManager, ParkFacilityTraits> readTraits = null)
            : base(AreaTypeId.Park, anchors, AreaAnchorRole.Entrance)
        { this.readTraits = readTraits ?? ParkFacilityReader.Read; }
        public override string BuildSummary(Entity area, in AreaPanelContext context) =>
            ValidateActivation(area) ? "CS2AS.Park.Summary.Active" : "CS2AS.Park.Summary.EntranceRequired";
        public override AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context) =>
            ParkPanelBuilder.Build(ParkGameplayFacade.Read(area, context));
        // Core already validated the stable ID and live selection; only the facade's approved setters run.
        public override bool HandleAction(Entity area, string action, string value, in AreaPanelContext context) =>
            ParkGameplayFacade.HandleAction(area, action, value, context);
        public override bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities)
        {
            if (role != AreaAnchorRole.Entrance) return false;
            try { return ParkFacilityClassifier.IsPlausibleEntrance(readTraits(anchor, entities)); }
            catch (Exception) { return false; }
        }
    }
}
