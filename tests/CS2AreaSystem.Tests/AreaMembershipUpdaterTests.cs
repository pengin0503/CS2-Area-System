using NUnit.Framework;
using CS2AreaSystem.Core.Membership;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaMembershipUpdaterTests {
    private sealed class SpatialFixture : IAreaMembershipSpatialSource {
        public Entity Member = new Entity { Index = 1, Version = 1 };
        public IReadOnlyList<AreaMembership> Relations = new[] { new AreaMembership(10, 1, false) };
        public bool AllowFullEnumeration = true;
        public bool NewlyInside;
        public IEnumerable<Entity> GetAllMembers() {
            if (!AllowFullEnumeration) throw new InvalidOperationException("Incremental update attempted a full city enumeration");
            return new[] { Member };
        }
        public IEnumerable<Entity> GetSpatialCandidates(Entity area) => NewlyInside ? new[] { Member } : Array.Empty<Entity>();
        public IReadOnlyList<AreaMembership> FindContainingAreas(Entity member) => Relations;
    }
    [Test] public void OrdinaryMemberChangesNeverRebuildCity() {
        var spatial = new SpatialFixture();
        var index = new AreaMembershipIndex();
        var updates = new AreaMembershipUpdater(index, spatial);
        updates.Rebuild();
        spatial.AllowFullEnumeration = false;
        spatial.Relations = Array.Empty<AreaMembership>();
        updates.UpdateMember(spatial.Member);
        Assert.That(index.GetAreas(spatial.Member), Is.Empty);
        Assert.That(updates.FullRebuildCount, Is.EqualTo(1));
        Assert.That(updates.IncrementalMemberUpdateCount, Is.EqualTo(1));
    }
    [Test] public void AreaShrinkReconsidersPreviousMembersAndExpansionFindsNewOnes() {
        var spatial = new SpatialFixture();
        var index = new AreaMembershipIndex();
        var updates = new AreaMembershipUpdater(index, spatial);
        updates.Rebuild();
        spatial.AllowFullEnumeration = false;
        spatial.Relations = Array.Empty<AreaMembership>();
        updates.UpdateArea(new Entity { Index = 10 }, 10, false);
        Assert.That(index.GetMembers(10), Is.Empty);
        spatial.Relations = new[] { new AreaMembership(10, 1, false) };
        spatial.NewlyInside = true;
        updates.UpdateArea(new Entity { Index = 10 }, 10, false);
        Assert.That(index.GetMembers(10), Does.Contain(spatial.Member));
        updates.UpdateArea(new Entity { Index = 10 }, 10, true);
        Assert.That(index.GetMembers(10), Is.Empty);
        Assert.That(updates.FullRebuildCount, Is.EqualTo(1));
        Assert.That(updates.IncrementalAreaUpdateCount, Is.EqualTo(3));
    }
}
