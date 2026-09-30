using NUnit.Framework;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem.Tests;
public class AreaStableIdAllocatorTests {
    [Test] public void AllocatesMonotonicallyAndObservesSavedIds() {
        var ids = new AreaStableIdAllocator();
        Assert.That(ids.Allocate(), Is.EqualTo(1UL));
        Assert.That(ids.Allocate(), Is.EqualTo(2UL));
        ids.ObserveExisting(50);
        ids.ObserveExisting(10);
        Assert.That(ids.Allocate(), Is.EqualTo(51UL));
    }
    [Test] public void ExhaustionNeverReusesZeroOrExistingIds() {
        var ids = new AreaStableIdAllocator(ulong.MaxValue);
        Assert.That(ids.Allocate(), Is.EqualTo(ulong.MaxValue));
        Assert.Throws<OverflowException>(() => ids.Allocate());
        var loaded = new AreaStableIdAllocator();
        loaded.ObserveExisting(ulong.MaxValue);
        Assert.Throws<OverflowException>(() => loaded.Allocate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaStableIdAllocator(0));
    }
}
