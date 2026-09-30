using CS2AreaSystem.Core.UI;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class NativeAreaLocalizationTests {
 [Test] public void NativeMenuAndAllThreeAssetsHaveDisplayNames() {
  var entries=NativeAreaLocalization.Entries("en-US");
  Assert.That(entries["Assets.NAME[CS2AS Areas Menu]"],Is.EqualTo("Areas"));
  Assert.That(entries["Assets.NAME[CS2AS Area Types]"],Is.EqualTo("Area Types"));
  Assert.That(entries["Assets.NAME[CS2AS.ParkArea]"],Is.EqualTo("Park Area"));
  Assert.That(entries["Assets.NAME[CS2AS.IndustrialArea]"],Is.EqualTo("Industrial Area"));
  Assert.That(entries["Assets.NAME[CS2AS.TourismArea]"],Is.EqualTo("Tourism Area"));
 }
}
