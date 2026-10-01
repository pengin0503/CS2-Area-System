using CS2AreaSystem.Core.Areas;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #62–#65: one identity lifecycle per load generation: reset the allocator, observe the live saved IDs once, and
// quarantine zero/duplicate IDs without renumbering them.
public class AreaIdentityLifecycleTests {
    private static Entity E(int index) => new Entity { Index = index, Version = 1 };
    private static AreaIdentityRecord R(int index, ulong id) => new AreaIdentityRecord(E(index), id);
    [Test] public void NewLoadStartsFromTheSavedIdsOfThatCityOnly() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        identity.Observe(new[] { R(1, 1), R(2, 2), R(3, 40) });
        Assert.That(identity.StableIds.Allocate(), Is.EqualTo(41UL));
        identity.BeginLoad();
        identity.Observe(new[] { R(1, 3) });
        Assert.That(identity.StableIds.Allocate(), Is.EqualTo(4UL), "city B continues from its own high-water mark, not city A's");
    }
    [Test] public void ExhaustedIdSpaceDoesNotCarryIntoTheNextCity() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        identity.Observe(new[] { R(1, ulong.MaxValue) });
        Assert.Throws<OverflowException>(() => identity.StableIds.Allocate());
        identity.BeginLoad();
        identity.Observe(Array.Empty<AreaIdentityRecord>());
        Assert.That(identity.StableIds.Allocate(), Is.EqualTo(1UL));
    }
    [Test] public void IdsAreNeverReusedWithinOneLoad() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        identity.Observe(new[] { R(1, 5), R(2, 7) });
        var allocated = new[] { identity.StableIds.Allocate(), identity.StableIds.Allocate() };
        Assert.That(allocated, Is.EqualTo(new[] { 8UL, 9UL }));
    }
    [Test] public void ZeroIdIsQuarantinedAndNotRenumbered() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        var quarantined = identity.Observe(new[] { R(1, 0), R(2, 4) });
        Assert.That(quarantined.Select(q => q.Area), Is.EqualTo(new[] { E(1) }));
        Assert.That(identity.Quarantine.ReasonOf(E(1)), Is.EqualTo(AreaQuarantineReason.ZeroStableId));
        Assert.That(identity.Quarantine.Contains(E(2)), Is.False);
        Assert.That(identity.StableIds.Allocate(), Is.EqualTo(5UL), "the zero entry neither consumes nor lowers the ID space");
    }
    [Test] public void EveryAreaSharingADuplicateIdIsQuarantined() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        var quarantined = identity.Observe(new[] { R(1, 9), R(2, 3), R(3, 9), R(4, 9) });
        Assert.That(quarantined.Select(q => q.Area), Is.EquivalentTo(new[] { E(1), E(3), E(4) }), "no owner of a shared ID is guessed");
        Assert.That(quarantined.All(q => q.Reason == AreaQuarantineReason.DuplicateStableId && q.StableId == 9), Is.True);
        Assert.That(identity.Quarantine.Contains(E(2)), Is.False);
        Assert.That(identity.StableIds.Allocate(), Is.EqualTo(10UL), "a duplicated ID still reserves the ID space");
    }
    [Test] public void NormalIdsAreNotQuarantined() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        Assert.That(identity.Observe(new[] { R(1, 1), R(2, 2), R(3, 3) }), Is.Empty);
        Assert.That(identity.Quarantine.Count, Is.EqualTo(0));
        Assert.That(identity.Observed, Is.True);
    }
    [Test] public void NewLoadClearsQuarantineAndAdvancesRevision() {
        var identity = new AreaIdentityLifecycle();
        identity.BeginLoad();
        int before = identity.Quarantine.Revision;
        identity.Observe(new[] { R(1, 0) });
        Assert.That(identity.Quarantine.Revision, Is.Not.EqualTo(before));
        int quarantined = identity.Quarantine.Revision;
        identity.BeginLoad();
        Assert.That(identity.Observed, Is.False);
        Assert.That(identity.Quarantine.Contains(E(1)), Is.False);
        Assert.That(identity.Quarantine.Revision, Is.Not.EqualTo(quarantined));
        Assert.That(identity.LoadGeneration, Is.EqualTo(2));
    }
    [Test] public void QuarantineReasonsAccumulateAndAreReportedOnce() {
        var quarantine = new AreaQuarantine();
        Assert.That(quarantine.Add(E(1), AreaQuarantineReason.ZeroStableId), Is.True);
        int revision = quarantine.Revision;
        Assert.That(quarantine.Add(E(1), AreaQuarantineReason.ZeroStableId), Is.False);
        Assert.That(quarantine.Revision, Is.EqualTo(revision), "an unchanged quarantine does not invalidate consumers");
        Assert.That(quarantine.Add(E(1), AreaQuarantineReason.InvalidSavedState), Is.True);
        Assert.That(quarantine.ReasonOf(E(1)), Is.EqualTo(AreaQuarantineReason.ZeroStableId | AreaQuarantineReason.InvalidSavedState));
        Assert.That(quarantine.ReasonOf(E(2)), Is.EqualTo(AreaQuarantineReason.None));
        Assert.That(new Entity { Index = 1, Version = 2 }, Is.Not.EqualTo(E(1)));
        Assert.That(quarantine.Contains(new Entity { Index = 1, Version = 2 }), Is.False, "a recycled entity index is a different area");
    }
    [Test] public void AllocatorResetRestoresAFreshIdSpace() {
        var ids = new AreaStableIdAllocator();
        ids.ObserveExisting(ulong.MaxValue);
        ids.Reset();
        Assert.That(ids.Allocate(), Is.EqualTo(1UL));
    }
}
