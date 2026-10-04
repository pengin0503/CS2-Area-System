using System.Text;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Generates the UI fixture from the real C# section payload so the React Park tests cannot drift from
// ParkPanelBuilder. Set UPDATE_UI_FIXTURES=1 to rewrite it; otherwise a stale committed fixture fails.
public class ParkPanelFixtureTests {
 // Phase 3 C2: both roles are explicit, assignable dedicated anchors; no automatic path evidence is presented.
 [Test] public void FoundationFixtureIncludesBothAssignableDedicatedRoles() {
  using var json=System.Text.Json.JsonDocument.Parse(Generate());
  var groups=json.RootElement.GetProperty("sections").GetProperty("foundation").GetProperty("anchorGroups");
  Assert.That(groups.EnumerateArray().Select(g=>g.GetProperty("role").GetString()),Is.EqualTo(new[]{"Headquarters","Entrance"}));
  Assert.That(groups.EnumerateArray().All(g=>g.GetProperty("assignmentSupported").GetBoolean()),Is.True);
  Assert.That(groups[1].TryGetProperty("derivedCount",out _),Is.False);
 }
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
  var foundation=ParkPanelBuilderTests.Model();foundation.Headquarters=ParkHeadquartersState.Valid;
  foundation.Runtime.Connectivity=new(){Status=ParkConnectivityStatus.Complete,Connected=1,Total=2,DisconnectedTargets=new[]{ParkPanelBuilderTests.Prop}};
  var foundationView=new AreaManagementViewModel{StableId="42",TypeKey="CS2AS.Type.Park",State="CS2AS.State.Active",Summary="CS2AS.Park.Summary.Active",Panel=ParkPanelBuilder.Build(foundation),
   AnchorGroups=new[]{
    new AreaAnchorGroupView{Role="Headquarters",AssignmentSupported=true,Anchors=new[]{new AreaAnchorView{Key="29.1",Name="Area System Park Headquarters",Valid=true}}},
    new AreaAnchorGroupView{Role="Entrance",AssignmentSupported=true,Anchors=new[]{new AreaAnchorView{Key="31.1",Name="Area System Park Gate",Valid=true}}}}};
  writer.PropertyName("foundation");writer.MapBegin(0);foundationView.WriteProperties(writer);writer.MapEnd();
  Section(writer, "level1", ParkPanelBuilderTests.Model(level: 1));
  Section(writer, "level2", ParkPanelBuilderTests.Model(level: 2, primary: false));
  Section(writer, "level4", ParkPanelBuilderTests.Model(level: 4));
  Section(writer, "unscanned", ParkPanelBuilderTests.Model(level: 2, landscape: false));
  var suspended=ParkPanelBuilderTests.Model(quality: false, entrances: 0);
  Section(writer, "suspended", suspended, entrances: false);
  Section(writer, "trends", ParkPanelBuilderTests.WithTrends());
  Section(writer, "diagnostics", ParkPanelBuilderTests.WithDiagnostics(true, new ParkLandscapeDiagnostic("LowLighting", 1, Unity.Entities.Entity.Null),
   new ParkLandscapeDiagnostic("SparseAmenities", 0.5, ParkPanelBuilderTests.Prop)));
  Section(writer, "eventReady", ParkEventPanelTests.WithEvents(ParkPanelBuilderTests.Model()));
  Section(writer, "eventActive", ParkEventPanelTests.Active());
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
