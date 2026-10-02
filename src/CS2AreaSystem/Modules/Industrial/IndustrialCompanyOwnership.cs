using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Semantics;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Tools;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public struct IndustrialCompanyEligibility
    {
        public bool Live, Industrial, Office, Commercial, BuildingEligible;
        public bool Eligible => Live && Industrial && !Office && !Commercial && BuildingEligible;
    }
    public readonly struct IndustrialCompanyOwnerResolution
    {
        public readonly ulong AreaStableId;
        public readonly bool Manual, Valid;
        public readonly string InvalidReason;
        public IndustrialCompanyOwnerResolution(ulong id, bool manual, string reason = null)
        { AreaStableId = id; Manual = manual; Valid = id != 0; InvalidReason = reason; }
    }
    public static class IndustrialCompanyOwnership
    {
        public static bool Related(ulong areaId, IReadOnlyList<AreaMembership> relations)
        {
            if (relations != null) foreach (var r in relations)
                if (r.AreaStableId == areaId && r.TypeId == (int)AreaTypeId.Industrial) return true;
            return false;
        }
        public static bool IsValidClaim(IndustrialCompanyClaim claim, Entity currentBuilding, IReadOnlyList<AreaMembership> relations, IndustrialCompanyEligibility eligibility) =>
            eligibility.Eligible && claim.Company != Entity.Null && claim.Building == currentBuilding && currentBuilding != Entity.Null && Related(claim.AreaId, relations);
        public static IndustrialCompanyOwnerResolution Resolve(Entity company, Entity building, IReadOnlyList<AreaMembership> geometricRelations,
            IndustrialCompanyAssignmentIndex manualIndex, IndustrialCompanyEligibility eligibility)
        {
            if (company == Entity.Null || building == Entity.Null || !eligibility.Eligible) return new IndustrialCompanyOwnerResolution(0, false, "Ineligible");
            string reason = null;
            if (manualIndex != null && manualIndex.TryGetClaim(company, out var claim))
            {
                if (IsValidClaim(claim, building, geometricRelations, eligibility)) return new IndustrialCompanyOwnerResolution(claim.AreaId, true);
                reason = claim.Building != building ? "Moved" : "OutsideArea";
            }
            if (geometricRelations != null) foreach (var r in geometricRelations)
                if (r.TypeId == (int)AreaTypeId.Industrial && r.IsPrimaryForType) return new IndustrialCompanyOwnerResolution(r.AreaStableId, false, reason);
            return new IndustrialCompanyOwnerResolution(0, false, reason ?? "NoIndustrialArea");
        }
        // Exact live Entity/renter facts. Only referenced companies/buildings are read; no world query.
        public static IndustrialCompanyEligibility ReadLive(Entity company, EntityManager entities, AreaAssetSemanticRegistry registry, out Entity building)
        {
            building = Entity.Null;
            var facts = new IndustrialCompanyEligibility();
            if (!entities.Exists(company) || entities.HasComponent<Deleted>(company) || entities.HasComponent<Temp>(company) ||
                !entities.HasComponent<CompanyData>(company) || !entities.HasComponent<PropertyRenter>(company)) return facts;
            building = entities.GetComponentData<PropertyRenter>(company).m_Property;
            facts.Office = entities.HasComponent<OfficeCompany>(company);
            facts.Commercial = entities.HasComponent<CommercialCompany>(company) || entities.HasComponent<ServiceAvailable>(company);
            facts.Industrial = entities.HasComponent<Game.Companies.ExtractorCompany>(company) || entities.HasComponent<Game.Companies.StorageCompany>(company) ||
                (entities.HasComponent<Game.Companies.ProcessingCompany>(company) && entities.HasComponent<IndustrialCompany>(company));
            facts.Live = entities.Exists(building) && !entities.HasComponent<Deleted>(building) && !entities.HasComponent<Temp>(building);
            if (!facts.Live || facts.Office || facts.Commercial || !facts.Industrial) return facts;
            var traits = IndustrialFacilityReader.ReadAnchor(building, entities);
            // Another tenant must never disqualify this company's otherwise Industrial building.
            traits.HasOfficeCompany = false;
            facts.BuildingEligible = IndustrialFacilityClassifier.Classify(traits, registry) != IndustrialFacilityRole.None;
            return facts;
        }
    }
}
