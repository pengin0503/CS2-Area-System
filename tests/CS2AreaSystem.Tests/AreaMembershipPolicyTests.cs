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
    // ---- per-Area membership/ownership revision tokens (module-neutral)
    private static readonly Entity Other = new Entity { Index = 101, Version = 1 };
    private const int Park = 1, Industrial = 2;
    [Test] public void UnknownOrUntouchedAreaRevisionIsZero() {
        var index = new AreaMembershipIndex();
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(0UL));
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true) });
        Assert.That(index.GetAreaRevision(99), Is.EqualTo(0UL));
        Assert.That(index.GetAreaRevision(10), Is.Not.EqualTo(0UL));
    }
    [Test] public void EffectiveChangesAdvanceOnlyAffectedAreas() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true) });
        index.ReplaceMembership(Other, new[] { new AreaMembership(20, Industrial, true) });
        ulong a = index.GetAreaRevision(10), b = index.GetAreaRevision(20);
        index.ReplaceMembership(Other, new[] { new AreaMembership(20, Industrial, true), new AreaMembership(30, Industrial, true) });
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(a), "another Area's membership edit");
        Assert.That(index.GetAreaRevision(20), Is.EqualTo(b), "relation and primary flag unchanged");
        Assert.That(index.GetAreaRevision(30), Is.Not.EqualTo(0UL));
    }
    [Test] public void UnchangedNormalizedMembershipAndNoOpRemovalsDoNotAdvance() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(30, Industrial, false), new AreaMembership(10, Industrial, false) });
        ulong a = index.GetAreaRevision(10), b = index.GetAreaRevision(30);
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(30, Industrial, true) });
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(a)); Assert.That(index.GetAreaRevision(30), Is.EqualTo(b));
        index.RemoveMember(Other);
        index.RemoveArea(99);
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(a)); Assert.That(index.GetAreaRevision(30), Is.EqualTo(b));
    }
    [Test] public void PrimaryOwnerTransferAdvancesBothOldAndNewOwners() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(30, Industrial, true) });
        ulong old = index.GetAreaRevision(30);
        // A smaller-ID Industrial Area now also contains the member and takes primary ownership.
        index.ReplaceMembership(Member, new[] { new AreaMembership(30, Industrial, true), new AreaMembership(10, Industrial, true) });
        Assert.That(index.GetAreaRevision(30), Is.Not.EqualTo(old), "old owner lost primary even though its member list is unchanged");
        Assert.That(index.GetAreaRevision(10), Is.Not.EqualTo(0UL));
        ulong ten = index.GetAreaRevision(10), thirty = index.GetAreaRevision(30);
        index.RemoveArea(10);
        Assert.That(index.GetAreaRevision(30), Is.Not.EqualTo(thirty), "ownership returns to the remaining Area");
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(0UL), "a deleted Area's token is removed after cleanup");
        Assert.That(ten, Is.Not.EqualTo(0UL));
    }
    [Test] public void ParkMembershipChangesLeaveIndustrialTokensUnchanged() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true) });
        ulong industrial = index.GetAreaRevision(10);
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(5, Park, true) });
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(5, Park, true), new AreaMembership(4, Park, true) });
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(industrial));
        Assert.That(index.GetAreaRevision(5), Is.Not.EqualTo(0UL));
    }
    [Test] public void TemporarilyEmptyAreaKeepsItsTokenAndClearDropsAll() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true) });
        index.RemoveMember(Member);
        ulong empty = index.GetAreaRevision(10);
        Assert.That(empty, Is.Not.EqualTo(0UL), "existing empty Area retains a token");
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true) });
        Assert.That(index.GetAreaRevision(10), Is.Not.EqualTo(empty), "no accidental reuse after re-adding");
        index.Clear();
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(0UL));
    }
    [Test] public void TokensAdvanceOncePerEffectiveChangeAndSkipZeroOnWrap() {
        var index = new AreaMembershipIndex();
        index.ReplaceMembership(Member, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(20, Park, true) });
        ulong first = index.GetAreaRevision(10);
        index.ReplaceMembership(Other, new[] { new AreaMembership(10, Industrial, true) });
        Assert.That(index.GetAreaRevision(10), Is.EqualTo(unchecked(first + 1)));
        Assert.That(AreaMembershipIndex.NextRevision(ulong.MaxValue), Is.EqualTo(1UL));
        Assert.That(AreaMembershipIndex.NextRevision(0), Is.EqualTo(1UL));
    }
}
