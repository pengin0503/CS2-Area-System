using System.Collections.Generic;
namespace CS2AreaSystem.Core.Membership
{
    public static class AreaMembershipPolicy
    {
        public static ulong ChoosePrimarySameKind(IReadOnlyList<ulong> ids)
        {
            ulong minimum = 0;
            foreach (var id in ids) if (id != 0 && (minimum == 0 || id < minimum)) minimum = id;
            return minimum;
        }
        public static IReadOnlyList<AreaMembership> Normalize(IReadOnlyList<AreaMembership> memberships)
        {
            var unique = new Dictionary<ulong, AreaMembership>();
            var primaries = new Dictionary<int, ulong>();
            foreach (var relation in memberships)
            {
                if (relation.AreaStableId == 0) continue;
                unique[relation.AreaStableId] = relation;
                if (!primaries.TryGetValue(relation.TypeId, out var minimum) || relation.AreaStableId < minimum)
                    primaries[relation.TypeId] = relation.AreaStableId;
            }
            var result = new List<AreaMembership>();
            foreach (var relation in unique.Values)
                result.Add(new AreaMembership(relation.AreaStableId, relation.TypeId, primaries[relation.TypeId] == relation.AreaStableId));
            result.Sort((a, b) => a.AreaStableId.CompareTo(b.AreaStableId));
            return result.AsReadOnly();
        }
    }
}
