using System;
using System.Collections.Generic;
using System.Globalization;
using CS2AreaSystem.Core.Membership;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public sealed class IndustrialCompanyCandidate
    {
        public string Key, Label;
        public Entity Company, Building;
        public ulong Owner;
        public bool Manual, HasClaimHere, Invalid;
        public bool CanAssign(ulong areaId) => !Invalid && !(Manual && Owner == areaId);
    }
    // Bounded current UI snapshot. Keys are scoped to a load/hour/assignment generation.
    public sealed class IndustrialCompanyCandidateSet
    {
        public const int Capacity = 64;
        private readonly List<IndustrialCompanyCandidate> rows = new List<IndustrialCompanyCandidate>(Capacity);
        public IReadOnlyList<IndustrialCompanyCandidate> Rows => rows;
        public ulong AreaId { get; private set; }
        public ulong AssignmentRevision { get; private set; }
        private int load = -1;
        private long hour = long.MinValue;
        private ulong generation;
        public void Begin(ulong areaId, int loadGeneration, long hourBucket, ulong revision)
        {
            if (AreaId == areaId && load == loadGeneration && hour == hourBucket && AssignmentRevision == revision) return;
            Clear(); AreaId = areaId; load = loadGeneration; hour = hourBucket; AssignmentRevision = revision;
        }
        public void Clear() { rows.Clear(); generation = AreaMembershipIndex.NextRevision(generation); hour = long.MinValue; }
        public void Record(Entity company, Entity building, IndustrialCompanyOwnerResolution owner, string label, bool hasClaimHere = false, bool invalid = false)
        {
            if (company == Entity.Null)
            {
                if (!hasClaimHere) return;
                // Saved missing references remain removable; they can never become assignable candidates.
                invalid = true;
            }
            IndustrialCompanyCandidate row = null;
            foreach (var existing in rows) if (existing.Company == company && existing.Building == building) { row = existing; break; }
            if (row == null)
            {
                if (rows.Count >= Capacity)
                {
                    if (!hasClaimHere) return;
                    int replace = rows.FindLastIndex(candidate => !candidate.HasClaimHere);
                    if (replace < 0) return;
                    rows.RemoveAt(replace);
                }
                row = new IndustrialCompanyCandidate { Company = company, Building = building,
                    Key = string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}.{4}:{5}.{6}", load, AreaId, generation, company.Index, company.Version, building.Index, building.Version) };
                rows.Add(row);
            }
            row.Owner = owner.AreaStableId; row.Manual = owner.Manual; row.Label = label;
            if (hasClaimHere || !row.HasClaimHere) row.Invalid = invalid;
            row.HasClaimHere |= hasClaimHere;
        }
        public bool TryResolve(string key, out IndustrialCompanyCandidate row)
        {
            row = null;
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var candidate in rows) if (candidate.Key == key) { row = candidate; return true; }
            return false;
        }
    }
    public readonly struct IndustrialCompanyLive
    {
        public readonly Entity Company, Building;
        public readonly IndustrialCompanyEligibility Eligibility;
        public IndustrialCompanyLive(Entity company, Entity building, IndustrialCompanyEligibility eligibility)
        { Company = company; Building = building; Eligibility = eligibility; }
    }
    public interface IIndustrialCompanyAssignmentStore
    {
        bool CanWrite(ulong areaId);
        bool HasClaim(ulong areaId, Entity company, Entity building);
        void Assign(ulong areaId, Entity company, Entity building);
        void Remove(ulong areaId, Entity company, Entity building);
    }
    public static class IndustrialCompanyAssignmentActions
    {
        public static bool TryApply(string action, string key, ulong selectedAreaId, IndustrialCompanyCandidateSet candidates,
            IndustrialCompanyAssignmentIndex assignments, Func<Entity, IndustrialCompanyLive> readLive, AreaMembershipIndex membership,
            IIndustrialCompanyAssignmentStore store)
        {
            if ((action != IndustrialGameplayFacade.AssignCompanyHere && action != IndustrialGameplayFacade.ClearCompanyAssignment) ||
                candidates == null || assignments == null || readLive == null || membership == null || store == null ||
                selectedAreaId == 0 || candidates.AreaId != selectedAreaId || candidates.AssignmentRevision != assignments.Revision ||
                !candidates.TryResolve(key, out var candidate) || !store.CanWrite(selectedAreaId)) return false;
            var live = readLive(candidate.Company);
            bool eligible = live.Company == candidate.Company && live.Building == candidate.Building && live.Eligibility.Eligible &&
                IndustrialCompanyOwnership.Related(selectedAreaId, membership.GetAreas(live.Building));
            if (action == IndustrialGameplayFacade.AssignCompanyHere)
            {
                if (!eligible || !candidate.CanAssign(selectedAreaId)) return false;
                store.Assign(selectedAreaId, candidate.Company, candidate.Building);
            }
            else
            {
                // A freshly published invalid claim can be cleaned without operating on a dead/reused company.
                if (!candidate.HasClaimHere || (!eligible && !candidate.Invalid) || !store.HasClaim(selectedAreaId, candidate.Company, candidate.Building)) return false;
                store.Remove(selectedAreaId, candidate.Company, candidate.Building);
            }
            candidates.Clear();
            return true;
        }
    }
}
