using NUnit.Framework;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem.Tests;
public class AreaTypeRegistryTests {
    [Test] public void KeepsThreeDistinctModulesAndRejectsDuplicateTypeWithoutReplacingIt() {
        var registry = new AreaTypeRegistry();
        var park = new AreaTypeDescriptor(1, "CS2AS.ParkArea", "Park", "icon");
        Assert.That(registry.Register(park), Is.True);
        Assert.That(registry.Register(new AreaTypeDescriptor(2, "CS2AS.IndustrialArea", "Industry", "icon")), Is.True);
        Assert.That(registry.Register(new AreaTypeDescriptor(3, "CS2AS.TourismArea", "Tourism", "icon")), Is.True);
        Assert.That(registry.Register(new AreaTypeDescriptor(1, "Other", "Other", "icon")), Is.False);
        Assert.That(registry.TryGet(1, out var actual), Is.True);
        Assert.That(actual, Is.SameAs(park));
        Assert.That(registry.TryGet(9999, out _), Is.False);
        Assert.That(registry.All, Has.Count.EqualTo(3));
    }
}
