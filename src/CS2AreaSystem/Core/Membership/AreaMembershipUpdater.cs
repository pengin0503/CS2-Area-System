using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.Membership
{
    public interface IAreaMembershipSpatialSource
    {
        IEnumerable<Entity> GetAllMembers();
        IEnumerable<Entity> GetSpatialCandidates(Entity area);
        IReadOnlyList<AreaMembership> FindContainingAreas(Entity member);
    }
    public sealed class AreaMembershipUpdater
    {
        private readonly AreaMembershipIndex index;
        private readonly IAreaMembershipSpatialSource spatial;
        public int FullRebuildCount { get; private set; }
        public int IncrementalMemberUpdateCount { get; private set; }
        public int IncrementalAreaUpdateCount { get; private set; }
        public AreaMembershipUpdater(AreaMembershipIndex index, IAreaMembershipSpatialSource spatial) { this.index = index; this.spatial = spatial; }
        public void Rebuild()
        {
            index.Clear();
            foreach (var member in spatial.GetAllMembers()) index.ReplaceMembership(member, spatial.FindContainingAreas(member));
            FullRebuildCount++;
        }
        public void UpdateMember(Entity member)
        {
            index.ReplaceMembership(member, spatial.FindContainingAreas(member));
            IncrementalMemberUpdateCount++;
        }
        public void UpdateArea(Entity area, ulong stableId, bool deleted)
        {
            if (deleted) index.RemoveArea(stableId);
            else
            {
                var affected = new HashSet<Entity>(index.GetMembers(stableId));
                affected.UnionWith(spatial.GetSpatialCandidates(area));
                foreach (var member in affected) index.ReplaceMembership(member, spatial.FindContainingAreas(member));
            }
            IncrementalAreaUpdateCount++;
        }
    }
}
