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
        public IReadOnlyList<AreaMembership> GetAreas(Entity member) =>
            byMember.TryGetValue(member, out var values) ? values : Array.Empty<AreaMembership>();
        public IReadOnlyCollection<Entity> GetMembers(ulong areaId) =>
            byArea.TryGetValue(areaId, out var values) ? Array.AsReadOnly(values.ToArray()) : Array.Empty<Entity>();
        public void ReplaceMembership(Entity member, IReadOnlyList<AreaMembership> memberships)
        {
            RemoveMember(member);
            var normalized = AreaMembershipPolicy.Normalize(memberships);
            if (normalized.Count == 0) return;
            byMember[member] = normalized;
            foreach (var relation in normalized)
            {
                if (!byArea.TryGetValue(relation.AreaStableId, out var members))
                    byArea[relation.AreaStableId] = members = new HashSet<Entity>();
                members.Add(member);
            }
        }
        public void RemoveMember(Entity member)
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
        public void RemoveArea(ulong areaStableId)
        {
            foreach (var member in GetMembers(areaStableId))
                ReplaceMembership(member, GetAreas(member).Where(x => x.AreaStableId != areaStableId).ToArray());
            byArea.Remove(areaStableId);
        }
        public void Clear() { byMember.Clear(); byArea.Clear(); }
    }
}
