using NUnit.Framework;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem.Tests;
public class ManagedAreaTypeResolverTests {
    [TestCase("CS2AS.ParkArea", AreaTypeId.Park)]
    [TestCase("CS2AS.IndustrialArea", AreaTypeId.Industrial)]
    [TestCase("CS2AS.TourismArea", AreaTypeId.Tourism)]
    public void ResolvesOnlyManagedPrefabs(string name, AreaTypeId expected) {
        Assert.That(new ManagedAreaTypeResolver().TryResolve(name, out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }
    [TestCase("District")] [TestCase("Forestry Area")] [TestCase("cs2as.parkarea")] [TestCase(null)]
    public void RejectsUnrelatedOrIncorrectlyCasedPrefab(string name) =>
        Assert.That(new ManagedAreaTypeResolver().TryResolve(name, out _), Is.False);
    [Test] public void NativeEditsDoNotReplaceExistingIdentityOrSavedUnknownType() {
        var ids = new AreaStableIdAllocator();
        var created = ManagedAreaIdentity.Ensure(1, null, null, ids);
        Assert.That(created.StableId.Value, Is.EqualTo(1UL));
        var saved = new ManagedArea { SchemaVersion = 1, TypeId = 9999, State = AreaLifecycleState.ModuleUnavailable };
        var edit = ManagedAreaIdentity.Ensure(1, saved, new ManagedAreaStableId { Value = 42 }, ids);
        Assert.That(edit.StableId.Value, Is.EqualTo(42UL));
        Assert.That(edit.Managed.TypeId, Is.EqualTo(9999));
        Assert.That(ids.Allocate(), Is.EqualTo(2UL));
    }
    [TestCase(false, true, false)] [TestCase(true, false, false)] [TestCase(true, true, true)]
    public void CreationRequiresReadyPrefabAndNativeTool(bool ready, bool nativeTool, bool allowed) =>
        Assert.That(AreaToolActivationPolicy.CanCreate(ready, nativeTool), Is.EqualTo(allowed));
}
