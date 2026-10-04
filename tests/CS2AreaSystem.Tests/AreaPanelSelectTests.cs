using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 3: a generic, bounded select item. The module still validates every chosen value on the action.
public class AreaPanelSelectTests
{
 private static AreaPanelItem Select(int options, string value = "b") => new() {
  Kind = AreaPanelItemKind.Select, Id = "pick", LabelKey = "M.Pick", Value = value, Action = "pick",
  Options = Enumerable.Range(0, options).Select(i => new AreaPanelOption { Value = i == 1 ? "b" : "v" + i, Label = i == 0 ? "Plain" : null, LabelKey = i == 0 ? null : "M.Opt" + i }).ToArray() };
 private static System.Text.Json.JsonElement Write(params AreaPanelItem[] items) {
  var writer = new RecordingJsonWriter();
  new AreaPanelDocument { Tabs = new[] { new AreaPanelTabData { Id = "t", LabelKey = "T", Items = items } } }.Write(writer);
  return writer.Parse()[0].GetProperty("items");
 }
 [Test] public void SelectSerializesItsOptionsAndOtherKindsCarryNone() {
  var items = Write(Select(3), new AreaPanelItem { Id = "r", LabelKey = "R" });
  var select = items[0];
  Assert.That(select.GetProperty("kind").GetString(), Is.EqualTo("select"));
  Assert.That(select.GetProperty("value").GetString(), Is.EqualTo("b"));
  var options = select.GetProperty("options");
  Assert.That(options.GetArrayLength(), Is.EqualTo(3));
  Assert.That(options[0].GetProperty("value").GetString(), Is.EqualTo("v0"));
  Assert.That(options[0].GetProperty("label").GetString(), Is.EqualTo("Plain"));
  Assert.That(options[0].GetProperty("labelKey").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
  Assert.That(options[1].GetProperty("labelKey").GetString(), Is.EqualTo("M.Opt1"));
  Assert.That(items[1].TryGetProperty("options", out _), Is.False, "non-select items keep their existing payload");
 }
 [Test] public void OptionsAreBoundedWithMatchingDeclaredSize() {
  var options = Write(Select(AreaPanelOption.MaxOptions + 10))[0].GetProperty("options");
  Assert.That(options.GetArrayLength(), Is.EqualTo(AreaPanelOption.MaxOptions));
  Assert.That(AreaPanelOption.MaxOptions, Is.EqualTo(64));
 }
 [Test] public void EquivalenceSeesOptionChanges() {
  var a = new AreaPanelDocument { Tabs = new[] { new AreaPanelTabData { Id = "t", Items = new[] { Select(3) } } } };
  var b = new AreaPanelDocument { Tabs = new[] { new AreaPanelTabData { Id = "t", Items = new[] { Select(3) } } } };
  Assert.That(AreaPanelDocument.Equivalent(a, b), Is.True);
  var c = new AreaPanelDocument { Tabs = new[] { new AreaPanelTabData { Id = "t", Items = new[] { Select(4) } } } };
  Assert.That(AreaPanelDocument.Equivalent(a, c), Is.False);
  var d = Select(3); d.Options = d.Options!.Select((o, i) => i == 2 ? new AreaPanelOption { Value = o.Value, LabelKey = "M.Other" } : o).ToArray();
  Assert.That(AreaPanelDocument.Equivalent(a, new AreaPanelDocument { Tabs = new[] { new AreaPanelTabData { Id = "t", Items = new[] { d } } } }), Is.False);
 }
}
