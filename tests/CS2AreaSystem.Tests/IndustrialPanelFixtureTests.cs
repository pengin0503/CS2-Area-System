using System.Text;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Generates the Industrial UI fixture from the real C# section payload so the React Industrial tests cannot drift
// from IndustrialPanelBuilder. Set UPDATE_UI_FIXTURES=1 to rewrite it; otherwise a stale committed fixture fails.
public class IndustrialPanelFixtureTests {
 [Test] public void ComparisonFixtureIncludesRealCurrentAndStaleRows() {
  using var json=System.Text.Json.JsonDocument.Parse(Generate());
  var tab=json.RootElement.GetProperty("sections").GetProperty("active").GetProperty("tabs").EnumerateArray().Single(t=>t.GetProperty("id").GetString()=="comparison");
  Assert.That(tab.GetProperty("items").EnumerateArray().Any(i=>i.GetProperty("id").GetString()=="comparison.42.Performance"),Is.True);
  Assert.That(tab.GetProperty("items").EnumerateArray().Any(i=>i.GetProperty("id").GetString()=="comparison.43.stale"),Is.True);
 }
 private static string FixturePath => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../UI/src/__fixtures__/industrial-panel.json"));
 private static void Section(RecordingJsonWriter writer, string name) {
  var model = IndustrialPanelBuilderTests.Model(name);
  if(name=="active") {
   var companies = new IndustrialCompanyCandidateSet(); companies.Begin(42, 1, 206, 1);
   companies.Record(new Unity.Entities.Entity { Index = 501, Version = 1 }, new Unity.Entities.Entity { Index = 101, Version = 1 }, new IndustrialCompanyOwnerResolution(42, false), "Automatic Steelworks");
   companies.Record(new Unity.Entities.Entity { Index = 502, Version = 1 }, new Unity.Entities.Entity { Index = 102, Version = 1 }, new IndustrialCompanyOwnerResolution(42, true), "Manual Steelworks", true);
   model.Companies = companies.Rows;
   // C4 support: a registered module with a 750 setting, one finished half-settled day and a complete 3-company roster.
   model.SupportAvailable = true; model.HasSupport = true;
   model.Support = IndustrialSupportState.CreateDefault(); model.Support.DailySupportPerCompany = 750;
   model.Support = IndustrialSupportModel.RecordCompletion(model.Support, 2250, 1125, 1125);
   model.Current.SupportRoster = new[] { new Unity.Entities.Entity { Index = 501, Version = 1 }, new Unity.Entities.Entity { Index = 503, Version = 1 }, new Unity.Entities.Entity { Index = 504, Version = 1 } };
   model.Current.SupportRosterComplete = true;
   model.SupportPending = IndustrialSupportStage.Requested;
   // Phase 3 D1: two destinations offered, a complete draft and one constrained advisory contract.
   var timber = ((ulong)Game.Economy.Resource.Timber).ToString(System.Globalization.CultureInfo.InvariantCulture);
   model.ContractsAvailable = true;
   model.ContractDraft = new IndustrialSupplyContractDraft { Destination = 43, Resource = (ulong)Game.Economy.Resource.Timber, TargetPerDay = 1500 };
   model.ContractDestinations = new[] { new CS2AreaSystem.Core.Modules.AreaPanelOption { Value = "43", Label = "Paused Industry" }, new CS2AreaSystem.Core.Modules.AreaPanelOption { Value = "44", Label = "Harbor Works" } };
   model.ContractResources = new[] { new CS2AreaSystem.Core.Modules.AreaPanelOption { Value = timber, LabelKey = "Resources.TITLE[Timber]" } };
   model.ContractRows = new[] { new IndustrialSupplyContractRow {
    Contract = new IndustrialSupplyContract { SchemaVersion = 1, ContractId = 5, DestinationStableId = 44, Resource = (ulong)Game.Economy.Resource.Timber, TargetPerDay = 1000, Enabled = true },
    DestinationName = "Harbor Works",
    Evaluation = new IndustrialSupplyContractEvaluation { ContractId = 5, Status = IndustrialSupplyContractStatus.Constrained, SourceCapacityPerDay = 1500, DestinationNeedPerDay = 800, PotentialPerDay = 800 } } };
   var current=new IndustrialRuntimeSnapshot();current.Publish(model.Current);
   var stale=new IndustrialRuntimeSnapshot();stale.Publish(model.Current);
   model.Comparison=IndustrialAreaComparison.Build(new[]{
    new IndustrialComparisonSource{StableId=42,TypeId=2,Name="Riverside Industry",Lifecycle=model.Lifecycle,Snapshot=current},
    new IndustrialComparisonSource{StableId=43,TypeId=2,Name="Paused Industry",Lifecycle=CS2AreaSystem.Core.Areas.AreaLifecycleState.Suspended,Snapshot=stale}},model.NowHours);
  }
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
