using System.Text;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Generates the Industrial UI fixture from the real C# section payload so the React Industrial tests cannot drift
// from IndustrialPanelBuilder. Set UPDATE_UI_FIXTURES=1 to rewrite it; otherwise a stale committed fixture fails.
public class IndustrialPanelFixtureTests {
 private static string FixturePath => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../UI/src/__fixtures__/industrial-panel.json"));
 private static void Section(RecordingJsonWriter writer, string name) {
  var model = IndustrialPanelBuilderTests.Model(name);
  string summary = model.Headquarters switch {
   IndustrialHeadquartersState.Valid => "CS2AS.Industrial.Summary.Active",
   IndustrialHeadquartersState.Invalid => "CS2AS.Industrial.Summary.HeadquartersInvalid",
   _ => "CS2AS.Industrial.Summary.HeadquartersRequired" };
  var view = new AreaManagementViewModel {
   StableId = "42", TypeKey = "CS2AS.Type.Industrial", State = "CS2AS.State." + model.Lifecycle, Summary = summary,
   Level = model.Level, Experience = model.Experience.ToString(System.Globalization.CultureInfo.InvariantCulture), AnchorRole = "Headquarters",
   AnchorName = model.Headquarters == IndustrialHeadquartersState.Valid ? "Riverside Steelworks" : null, Panel = IndustrialPanelBuilder.Build(model),
   Anchors = model.Headquarters == IndustrialHeadquartersState.Missing ? new AreaAnchorView[0]
    : new[] { new AreaAnchorView { Key = "30.1", Name = model.Headquarters == IndustrialHeadquartersState.Valid ? "Riverside Steelworks" : null, Valid = model.Headquarters == IndustrialHeadquartersState.Valid } } };
  writer.PropertyName(name); writer.MapBegin(0); view.WriteProperties(writer); writer.MapEnd();
 }
 internal static string Generate() {
  var writer = new RecordingJsonWriter();
  writer.MapBegin(0);
  writer.PropertyName("sections"); writer.MapBegin(0);
  foreach (var kind in IndustrialPanelBuilderTests.Kinds) Section(writer, kind);
  writer.MapEnd();
  writer.PropertyName("enUS"); writer.MapBegin(0);
  foreach (var entry in NativeAreaLocalization.Entries("en-US").Where(e => e.Key.StartsWith("CS2AS.", StringComparison.Ordinal)).OrderBy(e => e.Key, StringComparer.Ordinal))
  { writer.PropertyName(entry.Key); writer.Write(entry.Value); }
  writer.MapEnd();
  writer.MapEnd();
  using var parsed = System.Text.Json.JsonDocument.Parse(writer.Json);
  return System.Text.Json.JsonSerializer.Serialize(parsed.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true,
   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n") + "\n";
 }
 [Test] public void CommittedUiFixtureMatchesTheCurrentIndustrialPayload() {
  var expected = Generate();
  if (Environment.GetEnvironmentVariable("UPDATE_UI_FIXTURES") == "1") {
   Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
   File.WriteAllText(FixturePath, expected, new UTF8Encoding(false));
  }
  Assert.That(File.Exists(FixturePath), Is.True, "run the C# tests with UPDATE_UI_FIXTURES=1 to create " + FixturePath);
  Assert.That(File.ReadAllText(FixturePath).Replace("\r\n", "\n"), Is.EqualTo(expected), "UI fixture is stale: rerun with UPDATE_UI_FIXTURES=1");
 }
}
