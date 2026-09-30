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
        public bool HasManagedAreas { get; set; } = true;
        public int ContainmentQueries;
        public IEnumerable<Entity> GetAllMembers() {
            if (!AllowFullEnumeration) throw new InvalidOperationException("Incremental update attempted a full city enumeration");
            return new[] { Member };
        }
        public IEnumerable<Entity> GetSpatialCandidates(Entity area) => NewlyInside ? new[] { Member } : Array.Empty<Entity>();
        public IReadOnlyList<AreaMembership> FindContainingAreas(Entity member) { ContainmentQueries++; return Relations; }
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
    [Test] public void RebuildWithoutManagedAreasSkipsTheCityWideScan() {
        var spatial = new SpatialFixture { HasManagedAreas = false, AllowFullEnumeration = false };
        var index = new AreaMembershipIndex();
        var updates = new AreaMembershipUpdater(index, spatial);
        Assert.DoesNotThrow(() => updates.Rebuild());
        Assert.That(spatial.ContainmentQueries, Is.Zero);
        Assert.That(updates.FullRebuildCount, Is.EqualTo(1), "load still records exactly one rebuild");
    }
    [Test] public void MemberUpdatesWithoutManagedAreasSkipContainmentAndDropStaleRelations() {
        var spatial = new SpatialFixture();
        var index = new AreaMembershipIndex();
        var updates = new AreaMembershipUpdater(index, spatial);
        updates.Rebuild();
        Assert.That(index.GetAreas(spatial.Member), Is.Not.Empty);
        spatial.HasManagedAreas = false;
        var before = spatial.ContainmentQueries;
        updates.UpdateMember(spatial.Member);
        Assert.That(spatial.ContainmentQueries, Is.EqualTo(before));
        Assert.That(index.GetAreas(spatial.Member), Is.Empty);
        Assert.That(updates.IncrementalMemberUpdateCount, Is.EqualTo(1));
    }
    [Test] public void FirstAreaCreatedAfterAnEmptyCityStillFindsExistingMembers() {
        var spatial = new SpatialFixture { HasManagedAreas = false };
        var index = new AreaMembershipIndex();
        var updates = new AreaMembershipUpdater(index, spatial);
        updates.Rebuild();
        spatial.AllowFullEnumeration = false;
        spatial.HasManagedAreas = true; spatial.NewlyInside = true;
        updates.UpdateArea(new Entity { Index = 10 }, 10, false);
        Assert.That(index.GetMembers(10), Does.Contain(spatial.Member));
    }
}
public class AreaMembershipCandidatePolicyTests {
    [Test] public void OnlyLiveStaticBuildingsAreMembershipCandidates() {
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(true, true, true, false, false, false), Is.True);
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(true, true, false, false, false, false), Is.False, "trees/props are static objects but not buildings");
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(false, true, true, false, false, false), Is.False);
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(true, false, true, false, false, false), Is.False);
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(true, true, true, true, false, false), Is.False);
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(true, true, true, false, true, false), Is.False);
        Assert.That(AreaMembershipCandidatePolicy.IsCandidate(true, true, true, false, false, true), Is.False);
    }
}
