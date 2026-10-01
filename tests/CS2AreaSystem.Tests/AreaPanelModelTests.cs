using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Tourism;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaPanelModelTests {
 internal static AreaPanelDocument Document() => new() { Tabs = new[] {
  new AreaPanelTabData { Id = "overview", LabelKey = "Module.Tab.Overview", Items = new[] {
   new AreaPanelItem { Kind = AreaPanelItemKind.Row, Id = "visitors", LabelKey = "Module.Visitors", Value = "12" },
   new AreaPanelItem { Kind = AreaPanelItemKind.Bar, Id = "quality", LabelKey = "Module.Quality", NumericValue = 64.5, Min = 0, Max = 100, TooltipKey = "Module.Quality.Tip" },
   new AreaPanelItem { Kind = AreaPanelItemKind.Warning, Id = "crowded", LabelKey = "Module.Warning.Crowded" },
   new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "upkeep", LabelKey = "Module.Note.Upkeep" } } },
  new AreaPanelTabData { Id = "finances", LabelKey = "Module.Tab.Finances", Items = new[] {
   new AreaPanelItem { Kind = AreaPanelItemKind.Slider, Id = "budget", LabelKey = "Module.Budget", Action = "setBudget", NumericValue = 100, Min = 50, Max = 150, Step = 5, Value = "100%" },
   new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = "reset", LabelKey = "Module.Reset", Action = "resetFee", Value = "0", Disabled = true },
   new AreaPanelItem { Kind = AreaPanelItemKind.Row, Id = "state", LabelKey = "Module.State", ValueKey = "Module.State.Free" } } } } };
 [Test] public void DocumentSerializesEveryItemKindWithDeclaredSizes() {
  var writer = new RecordingJsonWriter();
  Document().Write(writer);
  var tabs = writer.Parse();
  Assert.That(tabs.GetArrayLength(), Is.EqualTo(2));
  var overview = tabs[0];
  Assert.That(overview.GetProperty("id").GetString(), Is.EqualTo("overview"));
  var items = overview.GetProperty("items");
  Assert.That(items.EnumerateArray().Select(i => i.GetProperty("kind").GetString()), Is.EqualTo(new[] { "row", "bar", "warning", "note" }));
  Assert.That(items[1].GetProperty("numericValue").GetDouble(), Is.EqualTo(64.5));
  Assert.That(items[1].GetProperty("tooltipKey").GetString(), Is.EqualTo("Module.Quality.Tip"));
  Assert.That(items[0].GetProperty("numericValue").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
  var slider = tabs[1].GetProperty("items")[0];
  Assert.That(slider.GetProperty("kind").GetString(), Is.EqualTo("slider"));
  Assert.That(slider.GetProperty("action").GetString(), Is.EqualTo("setBudget"));
  Assert.That(slider.GetProperty("min").GetDouble(), Is.EqualTo(50)); Assert.That(slider.GetProperty("max").GetDouble(), Is.EqualTo(150));
  Assert.That(slider.GetProperty("step").GetDouble(), Is.EqualTo(5));
  Assert.That(tabs[1].GetProperty("items")[1].GetProperty("disabled").GetBoolean(), Is.True);
  Assert.That(tabs[1].GetProperty("items")[2].GetProperty("valueKey").GetString(), Is.EqualTo("Module.State.Free"));
 }
 [Test] public void EquivalenceIsStructuralAndDetectsEveryItemField() {
  Assert.That(AreaPanelDocument.Equivalent(Document(), Document()), Is.True);
  var changes = new Action<AreaPanelItem>[] {
   i => i.Kind = "row", i => i.Id = "x", i => i.LabelKey = "x", i => i.Value = "x", i => i.ValueKey = "x", i => i.NumericValue = 1,
   i => i.Min = 1, i => i.Max = 1, i => i.Step = 1, i => i.Disabled = true, i => i.TooltipKey = "x", i => i.Action = "x" };
  foreach (var change in changes) {
   var changed = Document(); change(changed.Tabs[1].Items[0]);
   Assert.That(AreaPanelDocument.Equivalent(Document(), changed), Is.False);
  }
  var retitled = Document(); retitled.Tabs[0].LabelKey = "Other";
  Assert.That(AreaPanelDocument.Equivalent(Document(), retitled), Is.False);
  Assert.That(AreaPanelDocument.Equivalent(Document(), new AreaPanelDocument()), Is.False);
  Assert.That(AreaPanelDocument.Equivalent(null, null), Is.True);
 }
 private sealed class Store : IAreaAnchorStore {
  public Dictionary<Entity,IReadOnlyList<AreaAnchorLink>> Values = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area,out var links)?links:Array.Empty<AreaAnchorLink>();
  public void Write(Entity area,IReadOnlyList<AreaAnchorLink> links) => Values[area]=links.ToArray();
 }
 [Test] public void ModulesWithoutStructuredContentKeepASafeOverviewFallback() {
  var anchors = new AreaAnchorService(new Store(), e => true, (a, e, r) => true);
  // Industrial now builds its own five tabs (IndustrialPanelBuilderTests); Tourism remains a stub.
  foreach (IAreaPanelProvider provider in new IAreaPanelProvider[] { new TourismAreaModule() }) {
   var panel = provider.BuildPanel(default, default);
   Assert.That(panel.Tabs.Select(t => t.Id), Is.EqualTo(new[] { "overview" }));
   Assert.That(panel.Tabs[0].Items, Is.Empty);
   Assert.That(provider.HandleAction(default, "setBudget", "120", default), Is.False, "stub modules accept no module actions");
   Assert.That(provider.BuildSummary(default, default), Is.Not.Empty);
  }
  IAreaPanelProvider industrial = new IndustrialAreaModule(anchors);
  Assert.That(industrial.HandleAction(default, "setBudget", "120", default), Is.False, "Industrial v1 accepts no module actions");
  Assert.That(industrial.BuildSummary(default, default), Is.EqualTo("CS2AS.Industrial.Summary.HeadquartersRequired"));
 }
 private sealed class Provider : IAreaPanelProvider {
  public readonly List<(Entity, string, string)> Calls = new(); public bool Throws;
  public string BuildSummary(Entity area, in AreaPanelContext context) => "s";
  public AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context) => new();
  public bool HandleAction(Entity area, string action, string value, in AreaPanelContext context) {
   if (Throws) throw new InvalidOperationException("module"); Calls.Add((area, action, value)); return true; }
 }
 [Test] public void ModuleActionsRequireTheLiveSelectionGuardAndAreIsolated() {
  var area = new Entity { Index = 3, Version = 1 };
  var provider = new Provider();
  Assert.That(AreaModuleActionDispatcher.Dispatch(false, provider, area, "setBudget", "120", default, _ => { }), Is.False);
  Assert.That(provider.Calls, Is.Empty, "stale stable ID or selection never reaches the module");
  Assert.That(AreaModuleActionDispatcher.Dispatch(true, provider, area, "", "120", default, _ => { }), Is.False);
  Assert.That(AreaModuleActionDispatcher.Dispatch(true, null, area, "setBudget", "120", default, _ => { }), Is.False);
  Assert.That(AreaModuleActionDispatcher.Dispatch(true, provider, area, "setBudget", "120", default, _ => { }), Is.True);
  Assert.That(provider.Calls.Single(), Is.EqualTo((area, "setBudget", "120")));
  Exception seen = null; provider.Throws = true;
  Assert.That(AreaModuleActionDispatcher.Dispatch(true, provider, area, "setBudget", "130", default, e => seen = e), Is.False);
  Assert.That(seen?.Message, Is.EqualTo("module"));
 }
 [Test] public void AnchorLinkKeysResolveOnlyAgainstTheCurrentLinksOfTheRole() {
  var gate = new Entity { Index = 30, Version = 2 }; var hq = new Entity { Index = 40, Version = 1 };
  var links = new[] { new AreaAnchorLink { Anchor = gate, Role = AreaAnchorRole.Entrance }, new AreaAnchorLink { Anchor = hq, Role = AreaAnchorRole.Headquarters } };
  var key = AreaAnchorLinkKey.Format(gate);
  Assert.That(AreaAnchorLinkKey.TryResolve(links, AreaAnchorRole.Entrance, key, out var resolved), Is.True);
  Assert.That(resolved, Is.EqualTo(gate));
  Assert.That(AreaAnchorLinkKey.TryResolve(links, AreaAnchorRole.Entrance, AreaAnchorLinkKey.Format(hq), out _), Is.False, "other role");
  Assert.That(AreaAnchorLinkKey.TryResolve(links, AreaAnchorRole.Entrance, AreaAnchorLinkKey.Format(new Entity { Index = 30, Version = 1 }), out _), Is.False, "stale version");
  Assert.That(AreaAnchorLinkKey.TryResolve(links, AreaAnchorRole.Entrance, "30", out _), Is.False);
  Assert.That(AreaAnchorLinkKey.TryResolve(links, AreaAnchorRole.Entrance, null, out _), Is.False);
 }
}
