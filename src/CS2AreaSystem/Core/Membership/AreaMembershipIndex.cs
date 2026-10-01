using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
namespace CS2AreaSystem.Core.Membership
{
    public readonly struct AreaMembership
    {
        public ulong AreaStableId { get; }
        public int TypeId { get; }
        public bool IsPrimaryForType { get; }
        public AreaMembership(ulong areaStableId, int typeId, bool isPrimaryForType)
        { AreaStableId = areaStableId; TypeId = typeId; IsPrimaryForType = isPrimaryForType; }
    }
    public sealed class AreaMembershipIndex
    {
        private readonly Dictionary<Entity, IReadOnlyList<AreaMembership>> byMember = new Dictionary<Entity, IReadOnlyList<AreaMembership>>();
        private readonly Dictionary<ulong, HashSet<Entity>> byArea = new Dictionary<ulong, HashSet<Entity>>();
        // Module-neutral per-Area token: advances once whenever that Area's effective relations (member presence or
        // same-kind primary flag) change. Compare by equality only; 0 means unknown/untouched. A token is kept while
        // an existing Area is temporarily empty and removed with the Area.
        private readonly Dictionary<ulong, ulong> revisions = new Dictionary<ulong, ulong>();
        public IReadOnlyList<AreaMembership> GetAreas(Entity member) =>
            byMember.TryGetValue(member, out var values) ? values : Array.Empty<AreaMembership>();
        public IReadOnlyCollection<Entity> GetMembers(ulong areaId) =>
            byArea.TryGetValue(areaId, out var values) ? Array.AsReadOnly(values.ToArray()) : Array.Empty<Entity>();
        public ulong GetAreaRevision(ulong areaStableId) => revisions.TryGetValue(areaStableId, out var revision) ? revision : 0UL;
        // Unchecked increment that never yields the "untouched" value 0.
        public static ulong NextRevision(ulong revision) { revision = unchecked(revision + 1); return revision == 0 ? 1UL : revision; }
        public void ReplaceMembership(Entity member, IReadOnlyList<AreaMembership> memberships)
        {
            var old = GetAreas(member);
            var normalized = AreaMembershipPolicy.Normalize(memberships);
            var changed = ChangedAreas(old, normalized);
            Unlink(member);
            if (normalized.Count > 0)
            {
                byMember[member] = normalized;
                foreach (var relation in normalized)
                {
                    if (!byArea.TryGetValue(relation.AreaStableId, out var members))
                        byArea[relation.AreaStableId] = members = new HashSet<Entity>();
                    members.Add(member);
                }
            }
            foreach (var area in changed) Advance(area);
        }
        public void RemoveMember(Entity member)
        {
            if (!byMember.TryGetValue(member, out var relations)) return;
            Unlink(member);
            foreach (var relation in relations) Advance(relation.AreaStableId);
        }
        public void RemoveArea(ulong areaStableId)
        {
            foreach (var member in GetMembers(areaStableId))
                ReplaceMembership(member, GetAreas(member).Where(x => x.AreaStableId != areaStableId).ToArray());
            byArea.Remove(areaStableId);
            revisions.Remove(areaStableId);
        }
        public void Clear() { byMember.Clear(); byArea.Clear(); revisions.Clear(); }
        private void Unlink(Entity member)
        {
            if (!byMember.TryGetValue(member, out var relations)) return;
            foreach (var relation in relations)
                if (byArea.TryGetValue(relation.AreaStableId, out var members))
                {
                    members.Remove(member);
                    if (members.Count == 0) byArea.Remove(relation.AreaStableId);
                }
            byMember.Remove(member);
        }
        private void Advance(ulong area) => revisions[area] = NextRevision(GetAreaRevision(area));
        // Areas whose relation to the member appeared, disappeared or changed type/primary flag (normalized compare).
        private static HashSet<ulong> ChangedAreas(IReadOnlyList<AreaMembership> before, IReadOnlyList<AreaMembership> after)
        {
            var changed = new HashSet<ulong>();
            var previous = new Dictionary<ulong, AreaMembership>();
            foreach (var relation in before) previous[relation.AreaStableId] = relation;
            foreach (var relation in after)
            {
                if (!previous.TryGetValue(relation.AreaStableId, out var old) || old.TypeId != relation.TypeId || old.IsPrimaryForType != relation.IsPrimaryForType)
                    changed.Add(relation.AreaStableId);
                previous.Remove(relation.AreaStableId);
            }
            foreach (var gone in previous.Keys) changed.Add(gone);
            return changed;
        }
    }
}
