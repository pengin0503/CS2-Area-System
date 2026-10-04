using System.Reflection;
using CS2AreaSystem.Core.Settings;
using CS2AreaSystem.Core.UI;
using Game.Settings;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaSettingsTests {
 // ModSetting (1.6.2f1): id = assembly + "." + namespace + "." + mod type name.
 [Test] public void LocaleIdsUseTheModSettingFormula() {
  Assert.That(AreaSettingsLocalization.Id,Is.EqualTo("CS2AreaSystem.CS2AreaSystem.Mod"));
  Assert.That(AreaSettingsLocalization.Label("ShowAreaList"),Is.EqualTo("Options.OPTION[CS2AreaSystem.CS2AreaSystem.Mod.AreaSystemSettings.ShowAreaList]"));
  Assert.That(AreaSettingsLocalization.EnumValue(AreaPanelSide.Left),Is.EqualTo("Options.CS2AreaSystem.CS2AreaSystem.Mod.AREAPANELSIDE[Left]"));
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryOptionTabGroupAndEnumValueIsLocalized(string locale) {
  var entries=NativeAreaLocalization.Entries(locale);
  var options=typeof(AreaSystemSettings).GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly)
   .Where(p=>p.GetCustomAttribute<SettingsUISectionAttribute>()!=null).ToList();
  Assert.That(options.Count,Is.GreaterThanOrEqualTo(6));
  var keys=new List<string>{AreaSettingsLocalization.Section,AreaSettingsLocalization.Tab(AreaSystemSettings.MainTab)};
  keys.AddRange(new[]{AreaSystemSettings.InterfaceGroup,AreaSystemSettings.GameplayGroup,AreaSystemSettings.ResetGroup}.Select(AreaSettingsLocalization.Group));
  foreach(var option in options) {
   keys.Add(AreaSettingsLocalization.Label(option.Name)); keys.Add(AreaSettingsLocalization.Description(option.Name));
   if(option.GetCustomAttribute<SettingsUIConfirmationAttribute>()!=null) keys.Add(AreaSettingsLocalization.Warning(option.Name));
  }
  keys.AddRange(Enum.GetValues(typeof(AreaPanelSide)).Cast<AreaPanelSide>().Select(v=>AreaSettingsLocalization.EnumValue(v)));
  foreach(var key in keys) Assert.That(entries.ContainsKey(key),Is.True,locale+" is missing "+key);
 }
 [Test] public void SettingsExposeEveryConsumerFacingValueAndDefaultsAreSafe() {
  var names=typeof(IAreaSettings).GetProperties().Select(p=>p.Name).ToList();
  foreach(var name in names) Assert.That(typeof(AreaSystemSettings).GetProperty(name)?.GetCustomAttribute<SettingsUISectionAttribute>(),Is.Not.Null,name+" must be shown in Options");
  var defaults=AreaSettings.Current;
  Assert.That(defaults,Is.SameAs(AreaSettingsDefaults.Instance),"before OnLoad the defaults are active");
  Assert.That(defaults.ShowAreaList&&defaults.FocusCameraOnSelect&&defaults.ConfirmAreaDeletion&&defaults.AllowManagedAreaOverlap&&defaults.EnableParkCoverageModifier,Is.True);
  Assert.That(defaults.PanelSide,Is.EqualTo(AreaPanelSide.Right));
 }
 [Test] public void SettingsFileFollowsTheModsSettingsConvention() {
  var location=typeof(AreaSystemSettings).GetCustomAttribute<Colossal.IO.AssetDatabase.FileLocationAttribute>();
  Assert.That(location?.fileName,Is.EqualTo("ModsSettings/CS2AreaSystem/CS2AreaSystem.coc"));
 }
}
