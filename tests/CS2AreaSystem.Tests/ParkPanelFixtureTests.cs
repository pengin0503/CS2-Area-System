using System.Text;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Generates the UI fixture from the real C# section payload so the React Park tests cannot drift from
// ParkPanelBuilder. Set UPDATE_UI_FIXTURES=1 to rewrite it; otherwise a stale committed fixture fails.
public class ParkPanelFixtureTests {
 private static string FixturePath => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../UI/src/__fixtures__/park-panel.json"));
 private static void Section(RecordingJsonWriter writer, string name, ParkPanelModel model, bool entrances = true) {
  var view = new AreaManagementViewModel {
   StableId = "42", TypeKey = "CS2AS.Type.Park", State = "CS2AS.State." + model.Lifecycle, Summary = entrances ? "CS2AS.Park.Summary.Active" : "CS2AS.Park.Summary.EntranceRequired",
   Level = model.Level, Experience = model.Experience.ToString(System.Globalization.CultureInfo.InvariantCulture), AnchorRole = "Entrance",
   AnchorName = entrances ? "North Gate" : null, Panel = ParkPanelBuilder.Build(model),
   Anchors = entrances ? new[] { new AreaAnchorView { Key = "30.1", Name = "North Gate", Valid = true }, new AreaAnchorView { Key = "31.1", Name = "Old House", Valid = false } } : new AreaAnchorView[0] };
  writer.PropertyName(name); writer.MapBegin(0); view.WriteProperties(writer); writer.MapEnd();
 }
 internal static string Generate() {
  var writer = new RecordingJsonWriter();
  writer.MapBegin(0);
  writer.PropertyName("sections"); writer.MapBegin(0);
  Section(writer, "level1", ParkPanelBuilderTests.Model(level: 1));
  Section(writer, "level2", ParkPanelBuilderTests.Model(level: 2, primary: false));
  Section(writer, "level4", ParkPanelBuilderTests.Model(level: 4));
  Section(writer, "unscanned", ParkPanelBuilderTests.Model(level: 2, landscape: false));
  Section(writer, "suspended", ParkPanelBuilderTests.Model(quality: false, entrances: 0), entrances: false);
  writer.MapEnd();
  writer.PropertyName("enUS"); writer.MapBegin(0);
  foreach (var entry in NativeAreaLocalization.Entries("en-US").OrderBy(e => e.Key, StringComparer.Ordinal)) { writer.PropertyName(entry.Key); writer.Write(entry.Value); }
  writer.MapEnd();
  writer.MapEnd();
  using var parsed = System.Text.Json.JsonDocument.Parse(writer.Json);
  return System.Text.Json.JsonSerializer.Serialize(parsed.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true,
   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n") + "\n";
 }
 [Test] public void CommittedUiFixtureMatchesTheCurrentParkPayload() {
  var expected = Generate();
  if (Environment.GetEnvironmentVariable("UPDATE_UI_FIXTURES") == "1") {
   Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
   File.WriteAllText(FixturePath, expected, new UTF8Encoding(false));
  }
  Assert.That(File.Exists(FixturePath), Is.True, "run the C# tests with UPDATE_UI_FIXTURES=1 to create " + FixturePath);
  Assert.That(File.ReadAllText(FixturePath).Replace("\r\n", "\n"), Is.EqualTo(expected), "UI fixture is stale: rerun with UPDATE_UI_FIXTURES=1");
 }
}
