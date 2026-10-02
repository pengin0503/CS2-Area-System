using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Membership;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public readonly struct IndustrialCompanyClaim
    {
        public readonly ulong AreaId;
        public readonly Entity Company, Building;
        public IndustrialCompanyClaim(ulong areaId, Entity company, Entity building)
        { AreaId = areaId; Company = company; Building = building; }
    }
    // Derived only. Rebuild consumes saved claims, never a whole-world company query.
    public sealed class IndustrialCompanyAssignmentIndex
    {
        private Dictionary<Entity, IndustrialCompanyClaim> owners = new Dictionary<Entity, IndustrialCompanyClaim>();
        private Dictionary<Entity, IndustrialCompanyClaim> next = new Dictionary<Entity, IndustrialCompanyClaim>();
        public ulong Revision { get; private set; }
        public IEnumerable<IndustrialCompanyClaim> Claims => owners.Values;
        public bool TryGetOwner(Entity company, out ulong areaId)
        { bool found = owners.TryGetValue(company, out var claim); areaId = found ? claim.AreaId : 0; return found; }
        public bool TryGetClaim(Entity company, out IndustrialCompanyClaim claim) => owners.TryGetValue(company, out claim);
        public void Rebuild(IEnumerable<IndustrialCompanyClaim> claims, Func<IndustrialCompanyClaim, bool> valid = null)
        {
            next.Clear();
            foreach (var claim in claims)
            {
                if (claim.AreaId == 0 || claim.Company == Entity.Null || claim.Building == Entity.Null || (valid != null && !valid(claim))) continue;
                if (!next.TryGetValue(claim.Company, out var old) || claim.AreaId < old.AreaId ||
                    (claim.AreaId == old.AreaId && Compare(claim.Building, old.Building) < 0)) next[claim.Company] = claim;
            }
            bool changed = owners.Count != next.Count;
            if (!changed) foreach (var pair in next)
                if (!owners.TryGetValue(pair.Key, out var old) || old.AreaId != pair.Value.AreaId || old.Building != pair.Value.Building) { changed = true; break; }
            if (!changed) return;
            (owners, next) = (next, owners);
            Revision = AreaMembershipIndex.NextRevision(Revision);
        }
        private static int Compare(Entity a, Entity b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Version.CompareTo(b.Version);
        public void Clear() { owners.Clear(); next.Clear(); Revision = AreaMembershipIndex.NextRevision(Revision); }
    }
}
