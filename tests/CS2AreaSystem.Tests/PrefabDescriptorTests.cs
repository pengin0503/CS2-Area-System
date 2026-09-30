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
    [Test] public void RuntimeAvailabilityDecidesBetweenPreferredAndFallbackIcons() {
        foreach (var kind in new[]{AreaTypeId.Park,AreaTypeId.Industrial,AreaTypeId.Tourism}) {
            var descriptor=ManagedAreaPrefabFactory.Describe(kind);
            Assert.That(ManagedAreaPrefabFactory.ResolveIcon(descriptor,_=>true),Is.EqualTo(descriptor.IconUri));
            // A non-empty URI whose resource is missing at runtime must fall back without changing identity.
            Assert.That(ManagedAreaPrefabFactory.ResolveIcon(descriptor,uri=>uri==ManagedAreaPrefabFactory.FallbackIconUri),Is.EqualTo(ManagedAreaPrefabFactory.FallbackIconUri));
            Assert.That(descriptor.TypeId,Is.EqualTo((int)kind));
        }
        var unknown=new AreaTypeDescriptor(99,"CS2AS.Unknown","CS2AS.Type.Unknown","Media/Game/Icons/DoesNotExist.svg");
        Assert.That(ManagedAreaPrefabFactory.ResolveIcon(unknown,uri=>uri!="Media/Game/Icons/DoesNotExist.svg"),Is.EqualTo(ManagedAreaPrefabFactory.FallbackIconUri));
    }
    [Test] public void UnverifiableLookupKeepsThePreferredIconInsteadOfDegradingEveryType() {
        var park=ManagedAreaPrefabFactory.Describe(AreaTypeId.Park);
        Assert.That(ManagedAreaPrefabFactory.ResolveIcon(park,_=>false),Is.EqualTo(park.IconUri));
        var blank=new AreaTypeDescriptor(98,"CS2AS.Blank","CS2AS.Type.Blank","");
        Assert.That(ManagedAreaPrefabFactory.ResolveIcon(blank,_=>false),Is.EqualTo(ManagedAreaPrefabFactory.FallbackIconUri));
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
            icons.Add(ManagedAreaPrefabFactory.ResolveIcon(descriptor,_=>true));
        }
        foreach (var icon in icons) {
            Assert.That(icon, Does.StartWith("Media/Game/Icons/"));
            Assert.That(icon, Does.Not.Contain("://"));
        }
    }
}
public class GameUIResourcePathTests {
    [Test] public void CombinesLikeTheGameAssetDatabaseResourceHandler() {
        var root=new Uri("assetdb://global/5f1c2a/");
        Assert.That(GameUIResourcePath.Combine(root,"Media/Game/Icons/LargeParks.svg").ToString(),Is.EqualTo("assetdb://global/5f1c2a/Media/Game/Icons/LargeParks.svg"));
    }
    [TestCase("Media/Game/Icons/LargeParks.svg",true)]
    [TestCase("coui://gameui/Media/Game/Icons/LargeParks.svg",false)]
    [TestCase("/Media/Game/Icons/LargeParks.svg",false)]
    [TestCase("Media/../../secret.svg",false)]
    [TestCase("",false)]
    public void OnlyHostlessGameUIPathsAreLookedUp(string path,bool expected) => Assert.That(GameUIResourcePath.IsHostless(path),Is.EqualTo(expected));
}
