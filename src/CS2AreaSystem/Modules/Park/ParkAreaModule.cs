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
        public override bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities)
        {
            if (role != AreaAnchorRole.Entrance) return false;
            try { return ParkFacilityClassifier.IsPlausibleEntrance(readTraits(anchor, entities)); }
            catch (Exception) { return false; }
        }
    }
}
