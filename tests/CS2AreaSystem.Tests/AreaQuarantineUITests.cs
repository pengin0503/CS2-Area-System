using CS2AreaSystem.Core.UI;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// A quarantined Area (zero or shared stable ID) must stay selectable, editable and deletable without its ID ever being
// used as a key: it is addressed by its entity instead, and only Edit/Delete accept that key.
public class AreaQuarantineUITests {
    private static readonly Entity a = new() { Index = 1, Version = 1 }, b = new() { Index = 2, Version = 1 }, c = new() { Index = 3, Version = 4 };
    private static AreaUICandidate C(Entity e, ulong id, bool q = false) => new AreaUICandidate(e, id, q);
    [Test] public void QuarantinedAreaIsAddressedByEntityKeyNotById() {
        Assert.That(AreaUIKey.Format(a, 7, false), Is.EqualTo("7"));
        Assert.That(AreaUIKey.Format(c, 0, true), Is.EqualTo("q3v4"));
        var areas = new[] { C(a, 7, true), C(b, 7, true), C(c, 0, true) };
        Assert.That(AreaListViewModel.TryFind(areas, "7", out _), Is.False, "a shared ID never selects either area");
        Assert.That(AreaListViewModel.TryFind(areas, "0", out _), Is.False);
        Assert.That(AreaListViewModel.TryFind(areas, "q2v1", out var found), Is.True); Assert.That(found, Is.EqualTo(b));
        Assert.That(AreaListViewModel.TryFind(areas, "q3v3", out _), Is.False, "a stale entity version is rejected");
    }
    [Test] public void HealthyAreasKeepStrictIdSelectionAndIgnoreEntityKeys() {
        var areas = new[] { C(a, 7), C(b, 9, true) };
        Assert.That(AreaListViewModel.TryFind(areas, "7", out var found), Is.True); Assert.That(found, Is.EqualTo(a));
        Assert.That(AreaListViewModel.TryFind(areas, "q1v1", out _), Is.False, "an entity key never addresses a healthy area");
        Assert.That(AreaListViewModel.TryFind(areas, "9", out _), Is.False, "a quarantined area is not addressable by its ID");
    }
    [Test] public void OnlyTheSameLiveSelectionMatchesAQuarantineKey() {
        Assert.That(AreaUIActionGuard.MatchesQuarantined(a, a, a, "q1v1"), Is.True);
        Assert.That(AreaUIActionGuard.MatchesQuarantined(a, a, b, "q1v1"), Is.False);
        Assert.That(AreaUIActionGuard.MatchesQuarantined(a, a, a, "1"), Is.False);
        Assert.That(AreaUIActionGuard.MatchesQuarantined(Entity.Null, Entity.Null, Entity.Null, "q0v0"), Is.False);
    }
    [Test] public void ListKeysAreUniqueEvenForSharedIds() {
        var sorted = AreaListViewModel.Sort(new[] {
            new AreaListEntry { StableId = 7, Key = "q2v1", Name = "B", TypeKey = "t", State = "CS2AS.State.Quarantined" },
            new AreaListEntry { StableId = 7, Key = "q1v1", Name = "A", TypeKey = "t", State = "CS2AS.State.Quarantined" },
            new AreaListEntry { StableId = 3, Name = "C", TypeKey = "t", State = "CS2AS.State.Active" } });
        Assert.That(sorted.Select(e => e.UIKey), Is.EqualTo(new[] { "3", "q1v1", "q2v1" }));
        var same = AreaListViewModel.Sort(sorted.AsEnumerable().Reverse().Select(e => new AreaListEntry { StableId = e.StableId, Key = e.Key, Name = e.Name, TypeKey = e.TypeKey, State = e.State }));
        Assert.That(AreaListViewModel.Equivalent(sorted, same), Is.True);
        sorted[1].Key = "q9v1";
        Assert.That(AreaListViewModel.Equivalent(sorted, same), Is.False);
    }
}
