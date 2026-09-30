using NUnit.Framework;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Prefabs;
namespace CS2AreaSystem.Tests;
public class PrefabDescriptorTests {
    [TestCase(AreaTypeId.Park, "CS2AS.ParkArea", "Media/Game/Icons/LargeParks.svg")]
    [TestCase(AreaTypeId.Industrial, "CS2AS.IndustrialArea", "Media/Game/Icons/ZoneIndustrial.svg")]
    [TestCase(AreaTypeId.Tourism, "CS2AS.TourismArea", "Media/Game/Icons/Attraction.svg")]
    public void MapsTypeToNativeAssetMetadata(AreaTypeId kind, string name, string icon) {
        var descriptor = ManagedAreaPrefabFactory.Describe(kind);
        var metadata = ManagedAreaPrefabFactory.Map(descriptor, _ => true);
        Assert.That(metadata.Name, Is.EqualTo(name));
        Assert.That(metadata.IconUri, Is.EqualTo(icon));
        Assert.That(metadata.AllowOverlap, Is.True);
        Assert.That(metadata.AllowEditing, Is.True);
    }
    [Test] public void MissingIconFallsBackWithoutChangingType() {
        var descriptor = ManagedAreaPrefabFactory.Describe(AreaTypeId.Park);
        var metadata = ManagedAreaPrefabFactory.Map(descriptor, uri => uri.EndsWith("SurfaceAreaTool.svg"));
        Assert.That(metadata.IconUri, Is.EqualTo("Media/Game/Icons/SurfaceAreaTool.svg"));
        Assert.That(metadata.TypeId, Is.EqualTo(1));
        Assert.That(descriptor.PrefabName, Is.EqualTo("CS2AS.ParkArea"));
    }
    [Test] public void ProductionResolverRejectsUnknownIconsAndKeepsVerifiedOnes() {
        foreach (var kind in new[]{AreaTypeId.Park,AreaTypeId.Industrial,AreaTypeId.Tourism}) {
            var descriptor=ManagedAreaPrefabFactory.Describe(kind);
            Assert.That(ManagedAreaPrefabFactory.ResolveIcon(descriptor),Is.EqualTo(descriptor.IconUri));
        }
        var unknown=new AreaTypeDescriptor(99,"CS2AS.Unknown","CS2AS.Type.Unknown","Media/Game/Icons/DoesNotExist.svg");
        Assert.That(ManagedAreaPrefabFactory.ResolveIcon(unknown),Is.EqualTo(ManagedAreaPrefabFactory.FallbackIconUri));
    }
    [Test] public void ToolbarUnavailableDoesNotBlockInitializedAreaSimulation() {
        Assert.That(AreaPrefabReadiness.CanUse(true, true, true), Is.True);
        Assert.That(AreaPrefabReadiness.CanUse(true, false, true), Is.False);
        Assert.That(AreaPrefabReadiness.CanUse(true, true, false), Is.False);
        Assert.That(AreaToolbarPrefabFactory.ShouldAttach(false), Is.False);
    }
}
public class GameIconPathTests {
    // 1.6.2f1 vanilla UI references icons as host-less "Media/Game/Icons/..." paths;
    // coui://gameui is not a registered host (ResourceHandler: Invalid host locations map).
    [Test] public void AllIconsUseTheVanillaHostlessMediaPath() {
        var icons = new List<string> { ManagedAreaPrefabFactory.FallbackIconUri };
        foreach (var kind in new[]{AreaTypeId.Park,AreaTypeId.Industrial,AreaTypeId.Tourism}) {
            var descriptor = ManagedAreaPrefabFactory.Describe(kind);
            icons.Add(descriptor.IconUri);
            icons.Add(ManagedAreaPrefabFactory.ResolveIcon(descriptor));
        }
        foreach (var icon in icons) {
            Assert.That(icon, Does.StartWith("Media/Game/Icons/"));
            Assert.That(icon, Does.Not.Contain("://"));
        }
    }
}
