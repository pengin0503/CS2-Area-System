using System;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public sealed class IndustrialAreaModule : AnchorAreaModule, IAreaExistingAnchorPolicy
    {
        private readonly Func<Entity, EntityManager, IndustrialFacilityTraits> readTraits;
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
    }
}
