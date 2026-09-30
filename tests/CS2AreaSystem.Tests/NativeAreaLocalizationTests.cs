using CS2AreaSystem.Core.Areas;
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
 [Test] public void ManagementPanelActionsAndStatesAreLocalized() {
  var en=NativeAreaLocalization.Entries("en-US");
  Assert.That(en["CS2AS.UI.EditArea"],Is.EqualTo("Edit area"));
  Assert.That(en["CS2AS.UI.AssignHeadquarters"],Is.EqualTo("Assign headquarters"));
  Assert.That(en["CS2AS.State.Suspended"],Is.EqualTo("Suspended"));
  var ja=NativeAreaLocalization.Entries("ja");
  Assert.That(ja["CS2AS.UI.EditArea"],Is.EqualTo("エリアを編集"));
  Assert.That(ja["CS2AS.State.Suspended"],Is.EqualTo("停止中"));
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryKeyPublishedByTheCoreOrUsedByThePanelIsRegistered(string locale) {
  var entries=NativeAreaLocalization.Entries(locale);
  var keys=new List<string>{"CS2AS.Type.Unavailable","CS2AS.Summary.ModuleUnavailable","CS2AS.Summary.Active","CS2AS.Summary.AnchorRequired","CS2AS.Tab.Overview"};
  keys.AddRange(Enum.GetNames(typeof(AreaLifecycleState)).Select(state=>"CS2AS.State."+state));
  foreach(var kind in new[]{AreaTypeId.Park,AreaTypeId.Industrial,AreaTypeId.Tourism}) {
   var d=CS2AreaSystem.Core.Prefabs.ManagedAreaPrefabFactory.Describe(kind);
   keys.Add(d.DisplayNameKey); keys.Add("Assets.NAME["+d.PrefabName+"]");
  }
  var panel=File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"../../../../../UI/src/AreaManagementPanel.tsx"));
  keys.AddRange(System.Text.RegularExpressions.Regex.Matches(panel,@"'(CS2AS\.[A-Za-z.]+)'").Select(m=>m.Groups[1].Value));
  Assert.That(keys.Count(k=>k.StartsWith("CS2AS.UI.")),Is.GreaterThanOrEqualTo(10),"panel key extraction must find the UI keys");
  foreach(var key in keys.Distinct()) Assert.That(entries.ContainsKey(key),Is.True,locale+" is missing "+key);
 }
}
