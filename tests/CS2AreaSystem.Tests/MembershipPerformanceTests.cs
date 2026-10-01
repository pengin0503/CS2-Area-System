using Colossal.Collections;
using Game.Common;
using Colossal.Mathematics;
using CS2AreaSystem.Core.Membership;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
// #51: the boundary search keeps only building candidates; #71: member reads are cached read-only snapshots.
public class MembershipPerformanceTests {
    private struct OnlyEven : IMembershipObjectFilter { public bool Accept(Entity item) => item.Index % 2 == 0; }
    private static QuadTreeBoundsXZ Box(float x) => new QuadTreeBoundsXZ(new Bounds3(new float3(x, 0, 0), new float3(x + 1, 1, 1)));
    [Test] public void IteratorCollectsOnlyAcceptedObjectsInsideTheBounds() {
        var iterator = new MembershipObjectIterator<OnlyEven> { Bounds = new Bounds2(new float2(0, 0), new float2(100, 100)), Candidates = new HashSet<Entity>() };
        for (int i = 1; i <= 1000; i++) iterator.Iterate(Box(i % 50), new Entity { Index = i, Version = 1 }); // 1 building per prop
        iterator.Iterate(Box(500), new Entity { Index = 2000, Version = 1 });
        Assert.That(iterator.Visited, Is.EqualTo(1000), "objects outside the bounds are not examined");
        Assert.That(iterator.Accepted, Is.EqualTo(500));
        Assert.That(iterator.Candidates, Has.Count.EqualTo(500));
        Assert.That(iterator.Candidates.All(e => e.Index % 2 == 0), Is.True, "non-buildings never reach the candidate set");
    }
    private static Entity E(int i) => new() { Index = i, Version = 1 };
    [Test] public void GetMembersIsAReadOnlySnapshotReusedUntilTheAreaChanges() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(E(1), new[] { new AreaMembership(7, 1, true) });
        index.ReplaceMembership(E(2), new[] { new AreaMembership(7, 1, true) });
        var first = index.GetMembers(7);
        Assert.That(first, Is.EquivalentTo(new[] { E(1), E(2) }));
        Assert.That(index.GetMembers(7), Is.SameAs(first), "an unchanged read does not allocate a new snapshot");
        Assert.That(first, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<Entity>>());
        Assert.Throws<NotSupportedException>(() => ((ICollection<Entity>)first).Add(E(9)));
        index.ReplaceMembership(E(3), new[] { new AreaMembership(7, 1, true) });
        var second = index.GetMembers(7);
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(second, Is.EquivalentTo(new[] { E(1), E(2), E(3) }));
        Assert.That(first, Is.EquivalentTo(new[] { E(1), E(2) }), "a previous snapshot is never mutated");
        index.RemoveMember(E(1));
        Assert.That(index.GetMembers(7), Is.EquivalentTo(new[] { E(2), E(3) }));
        index.ReplaceMembership(E(5), new[] { new AreaMembership(8, 1, true) });
        var unrelated = index.GetMembers(7);
        Assert.That(index.GetMembers(7), Is.SameAs(unrelated));
        index.RemoveArea(7);
        Assert.That(index.GetMembers(7), Is.Empty);
        index.Clear();
        Assert.That(index.GetMembers(8), Is.Empty);
    }
    [Test] public void EpochAdvancesOnClearAndAreaRemovalSoRevisionsAreNeverConfused() {
        var index = new AreaMembershipIndex();
        int epoch = index.Epoch;
        index.ReplaceMembership(E(1), new[] { new AreaMembership(7, 1, true) });
        Assert.That(index.Epoch, Is.EqualTo(epoch), "ordinary updates keep the epoch; revisions change instead");
        index.Clear();
        Assert.That(index.Epoch, Is.Not.EqualTo(epoch));
        int afterClear = index.Epoch;
        index.ReplaceMembership(E(1), new[] { new AreaMembership(7, 1, true) });
        index.RemoveArea(7);
        Assert.That(index.Epoch, Is.Not.EqualTo(afterClear));
    }
    [Test] public void MembershipSystemFiltersBuildingsInsideTheSearch() {
        var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Core/Membership/AreaMembershipSystem.cs"));
        Assert.That(source, Does.Contain("new MembershipObjectIterator<BuildingMemberFilter>"));
        Assert.That(source, Does.Not.Contain("private struct ObjectIterator"));
    }
}
