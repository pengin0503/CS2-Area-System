using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Tourism;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class TourismAreaModuleTests {
 [TestCase("en-US", "boundary only", "No tourism gameplay")]
 [TestCase("ja-JP", "境界のみ", "観光のゲームプレイ機能")]
 public void BoundaryOnlyModuleExplainsItsLimitInTheMenuAndSelectedPanel(string locale, string boundary, string gameplay) {
  var module = new TourismAreaModule();
  var entries = NativeAreaLocalization.Entries(locale);
  var context = default(AreaPanelContext);
  var summary = module.BuildSummary(Entity.Null, context);
  Assert.That(entries[summary], Does.Contain(boundary).IgnoreCase.And.Contain(gameplay));
  Assert.That(entries["Assets.DESCRIPTION[CS2AS.TourismArea]"], Does.Contain(boundary).IgnoreCase.And.Contain(gameplay));
  Assert.That(module.AnchorRoles, Is.Empty);
  Assert.That(module.ValidateActivation(Entity.Null, default), Is.True, "existing boundary saves remain supported");
 }
}
