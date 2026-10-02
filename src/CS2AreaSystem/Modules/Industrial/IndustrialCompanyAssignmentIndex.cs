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
        private readonly Dictionary<ulong, IReadOnlyList<Entity>> buildings = new Dictionary<ulong, IReadOnlyList<Entity>>();
        public IReadOnlyList<Entity> GetBuildings(ulong areaId) => buildings.TryGetValue(areaId, out var list) ? list : Array.Empty<Entity>();
        public ulong Revision { get; private set; }
        private readonly Dictionary<ulong, ulong> areaRevisions = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> changedAreas = new HashSet<ulong>();
        public IEnumerable<ulong> ChangedAreas => changedAreas;
        public ulong GetAreaRevision(ulong areaId) => areaRevisions.TryGetValue(areaId, out var revision) ? revision : 0;
        private void MarkChanged(ulong areaId) { if (areaId != 0) changedAreas.Add(areaId); }
        private static bool Same(IndustrialCompanyClaim a, IndustrialCompanyClaim b) => a.AreaId == b.AreaId && a.Building == b.Building;
        public IEnumerable<IndustrialCompanyClaim> Claims => owners.Values;
        public bool TryGetOwner(Entity company, out ulong areaId)
        { bool found = owners.TryGetValue(company, out var claim); areaId = found ? claim.AreaId : 0; return found; }
        public bool TryGetClaim(Entity company, out IndustrialCompanyClaim claim) => owners.TryGetValue(company, out claim);
        public void Rebuild(IEnumerable<IndustrialCompanyClaim> claims, Func<IndustrialCompanyClaim, bool> valid = null,
            Func<Entity, Entity, ulong> fallbackOwner = null)
        {
            next.Clear(); changedAreas.Clear();
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
            // Compare effective claims before swapping: only former/new resolved owners lose their coverage.
            foreach (var pair in owners)
            {
                bool hasNext = next.TryGetValue(pair.Key, out var replacement);
                if (hasNext && Same(pair.Value, replacement)) continue;
                MarkChanged(pair.Value.AreaId);
                if (!hasNext && fallbackOwner != null) MarkChanged(fallbackOwner(pair.Key, pair.Value.Building));
            }
            foreach (var pair in next)
            {
                bool hadOwner = owners.TryGetValue(pair.Key, out var previous);
                if (hadOwner && Same(previous, pair.Value)) continue;
                MarkChanged(pair.Value.AreaId);
                if (!hadOwner && fallbackOwner != null) MarkChanged(fallbackOwner(pair.Key, pair.Value.Building));
            }
            foreach (var areaId in changedAreas) areaRevisions[areaId] = AreaMembershipIndex.NextRevision(GetAreaRevision(areaId));
            (owners, next) = (next, owners);
            buildings.Clear();
            var grouped = new Dictionary<ulong, HashSet<Entity>>();
            foreach (var claim in owners.Values)
            {
                if (!grouped.TryGetValue(claim.AreaId, out var set)) grouped[claim.AreaId] = set = new HashSet<Entity>();
                set.Add(claim.Building);
            }
            foreach (var pair in grouped) { var list = new List<Entity>(pair.Value); list.Sort(Compare); buildings[pair.Key] = list.AsReadOnly(); }
            Revision = AreaMembershipIndex.NextRevision(Revision);
        }
        private static int Compare(Entity a, Entity b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Version.CompareTo(b.Version);
        public void Clear() { owners.Clear(); next.Clear(); buildings.Clear(); areaRevisions.Clear(); changedAreas.Clear(); Revision = AreaMembershipIndex.NextRevision(Revision); }
    }
}
