using NUnit.Framework;
using CS2AreaSystem.Core.Membership;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaMembershipPolicyTests {
    private static readonly Entity Member = new Entity { Index = 100, Version = 1 };
    [Test] public void CrossKindOverlapKeepsBothPrimaryMemberships() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, 1, false), new AreaMembership(20, 3, false) });
        Assert.That(index.GetAreas(Member).Select(x => (x.AreaStableId, x.IsPrimaryForType)),
            Is.EquivalentTo(new[] { (10UL, true), (20UL, true) }));
        Assert.That(index.GetMembers(10), Does.Contain(Member));
        Assert.That(index.GetMembers(20), Does.Contain(Member));
    }
    [Test] public void SameKindUsesSmallestPersistedIdIndependentOfOrderAndRebuild() {
        Assert.That(AreaMembershipPolicy.ChoosePrimarySameKind(new ulong[] { 30, 10 }), Is.EqualTo(10UL));
        Assert.That(AreaMembershipPolicy.ChoosePrimarySameKind(new ulong[] { 10, 30 }), Is.EqualTo(10UL));
        var index = new AreaMembershipIndex();
        for (int rebuild = 0; rebuild < 2; rebuild++) {
            index.Clear();
            index.ReplaceMembership(Member, new[] { new AreaMembership(30, 1, true), new AreaMembership(10, 1, true) });
            Assert.That(index.GetAreas(Member).Single(x => x.IsPrimaryForType).AreaStableId, Is.EqualTo(10UL));
            Assert.That(index.GetAreas(Member), Has.Count.EqualTo(2));
        }
    }
    [Test] public void RemovalPromotesRemainingSameKindOwnerAndCleansReverseIndex() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, 1, false), new AreaMembership(30, 1, false) });
        index.RemoveArea(10);
        Assert.That(index.GetAreas(Member).Single().IsPrimaryForType, Is.True);
        Assert.That(index.GetMembers(10), Is.Empty);
        index.RemoveMember(Member);
        Assert.That(index.GetMembers(30), Is.Empty);
    }
    [Test] public void ReplacementCopiesInputsAndRemovesStaleRelations() {
        var index = new AreaMembershipIndex();
        var input = new[] { new AreaMembership(10, 1, true) };
        index.ReplaceMembership(Member, input);
        input[0] = new AreaMembership(20, 3, true);
        Assert.That(index.GetAreas(Member).Single().AreaStableId, Is.EqualTo(10UL));
        index.ReplaceMembership(Member, input);
        Assert.That(index.GetMembers(10), Is.Empty);
        Assert.That(index.GetMembers(20), Does.Contain(Member));
    }
}
