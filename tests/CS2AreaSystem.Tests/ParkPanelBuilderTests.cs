using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkPanelBuilderTests {
 internal static ParkPanelModel Model(int level = 2, bool landscape = true, bool primary = true, bool quality = true, int entrances = 2) {
  var runtime = new ParkRuntimeSnapshot { StableId = 7, AreaSquareMeters = 10_000, ActiveVisitors = 14, PeakOccupancy = 30, EntranceCount = entrances };
  var inputs = new ParkLandscapeInputs { AreaSquareMeters = 10_000, PathLength = 300, Vegetation = Support.LandscapeInputs.Balanced(50, 10), Amenities = Support.LandscapeInputs.Balanced(6, 2) };
  runtime.Landscape = new ParkLandscapeSnapshot { Available = landscape, Inputs = inputs, Result = ParkLandscapeModel.ComputeLandscape(inputs), TransitPoints = 1, TransitTypes = 1 };
  runtime.Facilities = ParkFacilitySnapshot.Build(new[] { new ParkFacilityTraits { IsBuilding = true, HasPark = true, HasLeisureData = true, LeisureType = Game.Agents.LeisureType.CityPark, LeisureEfficiency = 20 } });
  runtime.RecentVisitors.Add(100, 40); runtime.RecentAdmission.Add(100, 800); runtime.RecentUncollected.Add(100, 13); runtime.RecentExpense.Add(100, 300);
  runtime.ManagementCostPerDay = 900;
  var state = ParkAreaState.CreateDefault(); state.AdmissionFee = 20; state.LifetimeVisitors = 1_000; state.LifetimeTouristVisitors = 200; state.LifetimeResidentVisitors = 800;
  state.LifetimeAdmissionRevenue = 12_345; state.LifetimeManagementExpense = 6_000; state.Satisfaction = new ParkSatisfactionState { Mean = 70, Weight = 40 };
  if (quality) runtime.Quality = ParkQualityEvaluator.Evaluate(new ParkQualityContext { AreaSquareMeters = 10_000, Entrances = entrances, EntranceSpread = 0.5f,
   Landscape = runtime.Landscape, Facilities = runtime.Facilities, Satisfaction = state.Satisfaction, ActiveVisitors = 14, Budget = 1, Level = level,
   AdmissionFee = state.AdmissionFee, IsPrimaryForAllMembers = primary, Active = entrances > 0 });
  var model = new ParkPanelModel { StableId = 7, Lifecycle = entrances > 0 ? AreaLifecycleState.Active : AreaLifecycleState.Suspended, Level = level, Experience = 2_500,
   State = state, Budget = 1.1f, Frame = 200, Runtime = runtime, HasState = true };
  model.Promotion = ParkProgressionModel.EvaluatePromotion(level, model.Experience, ParkProgressionSystem.BuildInputs(runtime.Quality, state));
  return model;
 }
 private static AreaPanelTabData Tab(AreaPanelDocument d, string id) => d.Tabs.Single(t => t.Id == id);
 private static AreaPanelItem Item(AreaPanelTabData t, string id) => t.Items.Single(i => i.Id == id);
 [Test] public void EmitsTheApprovedTabsWithEventsLast() {
  var d = ParkPanelBuilder.Build(Model());
  Assert.That(d.Tabs.Select(t => t.Id), Is.EqualTo(new[] { "overview", "visitors", "finances", "landscape", "progress", "events" }));
  Assert.That(d.ShowsProgress, Is.True, "the Park tabs present Level/XP, so Core omits its numeric rows");
 }
 [Test] public void OverviewShowsLevelQualitySixMetricsVisitorsEntrancesAndNextAction() {
  var o = Tab(ParkPanelBuilder.Build(Model()), "overview");
  Assert.That(Item(o, "level").ValueKey, Is.EqualTo("CS2AS.Park.LevelName.2"));
  Assert.That(Item(o, "experience").Kind, Is.EqualTo(AreaPanelItemKind.Bar));
  Assert.That(Item(o, "quality").Kind, Is.EqualTo(AreaPanelItemKind.Bar));
  foreach (var metric in new[] { "satisfaction", "attractiveness", "accessibility", "variety", "maintenance", "comfort" })
   Assert.That(Item(o, metric).Kind, Is.EqualTo(AreaPanelItemKind.Bar), metric);
  Assert.That(Item(o, "activeVisitors").Value, Is.EqualTo("14"));
  Assert.That(Item(o, "recentVisitors").Value, Is.EqualTo("40"));
  Assert.That(Item(o, "entrances").Value, Is.EqualTo("2"));
  Assert.That(Item(o, "nextAction").ValueKey, Does.StartWith("CS2AS.Park.Action."));
 }
 [Test] public void UnavailableMetricsShowUnavailableAndLowConfidence() {
  var o = Tab(ParkPanelBuilder.Build(Model(landscape: false)), "overview");
  Assert.That(Item(o, "attractiveness").Kind, Is.EqualTo(AreaPanelItemKind.Row));
  Assert.That(Item(o, "attractiveness").ValueKey, Is.EqualTo("CS2AS.Park.Unavailable"));
  Assert.That(o.Items.Any(i => i.Kind == AreaPanelItemKind.Warning && i.LabelKey == "CS2AS.Park.Warning.LowConfidence"), Is.True);
  var none = Tab(ParkPanelBuilder.Build(Model(quality: false)), "overview");
  Assert.That(Item(none, "quality").ValueKey, Is.EqualTo("CS2AS.Park.Calculating"));
 }
 [Test] public void NonPrimaryOverlapIsExplained() {
  var o = Tab(ParkPanelBuilder.Build(Model(primary: false)), "overview");
  Assert.That(o.Items.Any(i => i.Kind == AreaPanelItemKind.Warning && i.LabelKey == "CS2AS.Park.Warning.NotPrimary"), Is.True);
 }
 [Test] public void VisitorsTabSplitsResidentsAndTourists() {
  var v = Tab(ParkPanelBuilder.Build(Model()), "visitors");
  Assert.That(Item(v, "lifetimeVisitors").Value, Is.EqualTo("1,000"));
  Assert.That(Item(v, "residentVisitors").Value, Is.EqualTo("800")); Assert.That(Item(v, "touristVisitors").Value, Is.EqualTo("200"));
  foreach (var id in new[] { "activeVisitors", "recentVisitors", "satisfaction", "residentSatisfaction", "touristSatisfaction", "capacity", "occupancy", "peakOccupancy", "tourismContribution" })
   Assert.That(v.Items.Any(i => i.Id == id), Is.True, id);
 }
 [Test] public void FinancesTabHasBudgetAndLevelGatedFeeWithSeparationNote() {
  var f = Tab(ParkPanelBuilder.Build(Model(level: 1)), "finances");
  var budget = Item(f, "budget");
  Assert.That(budget.Kind, Is.EqualTo(AreaPanelItemKind.Slider)); Assert.That(budget.Action, Is.EqualTo("setBudget"));
  Assert.That(budget.Min, Is.EqualTo(50)); Assert.That(budget.Max, Is.EqualTo(150)); Assert.That(budget.Step, Is.EqualTo(5)); Assert.That(budget.NumericValue, Is.EqualTo(110).Within(0.01));
  var fee = Item(f, "fee");
  Assert.That(fee.Disabled, Is.True, "admission locked at Level 1"); Assert.That(fee.TooltipKey, Is.EqualTo("CS2AS.Park.Fee.Locked"));
  Assert.That(fee.NumericValue, Is.Zero, "a stored fee is shown as inactive at Level 1");
  var paid = Item(Tab(ParkPanelBuilder.Build(Model(level: 2)), "finances"), "fee");
  Assert.That(paid.Disabled, Is.False); Assert.That(paid.Max, Is.EqualTo(ParkFinanceModel.MaxAdmissionFee)); Assert.That(paid.NumericValue, Is.EqualTo(20)); Assert.That(paid.Action, Is.EqualTo("setFee"));
  foreach (var id in new[] { "recentAdmission", "lifetimeAdmission", "managementCost", "recentExpense", "lifetimeExpense", "recentNet" })
   Assert.That(f.Items.Any(i => i.Id == id), Is.True, id);
  Assert.That(Item(f, "recentNet").Value, Is.EqualTo("¢500"));
  Assert.That(Item(f, "recentUncollected").Value, Is.EqualTo("¢13"), "fees households could not pay (last day)");
  Assert.That(Item(f, "recentUncollected").TooltipKey, Is.EqualTo("CS2AS.Park.RecentUncollected.Tip"));
  Assert.That(f.Items.Any(i => i.Kind == AreaPanelItemKind.Note && i.LabelKey == "CS2AS.Park.Note.VanillaSeparate"), Is.True);
 }
 [Test] public void LandscapeTabShowsScoreAndCategoryContributions() {
  var l = Tab(ParkPanelBuilder.Build(Model()), "landscape");
  Assert.That(Item(l, "landscape").Kind, Is.EqualTo(AreaPanelItemKind.Bar));
  foreach (var id in new[] { "vegetation", "amenities", "decoration", "lighting", "paths", "diversity", "pathLength", "transit", "facilityCategories" })
   Assert.That(l.Items.Any(i => i.Id == id), Is.True, id);
  Assert.That(Item(l, "vegetation").Value, Does.EndWith("/ 30"));
 }
 [Test] public void ProgressTabHasChecklistAndLandmarkObjectives() {
  var p = Tab(ParkPanelBuilder.Build(Model(level: 2)), "progress");
  Assert.That(p.Items.Where(i => i.Kind == AreaPanelItemKind.Check).Select(i => i.Id), Does.Contain("check.Quality").And.Contain("check.Variety").And.Contain("check.Hold"));
  Assert.That(Item(p, "check.Quality").NumericValue, Is.EqualTo(1).Or.EqualTo(0));
  var m = Model(level: 4); var landmark = Tab(ParkPanelBuilder.Build(m), "progress");
  Assert.That(landmark.Items.Count(i => i.Id.StartsWith("check.Prestige")), Is.EqualTo(3));
  Assert.That(landmark.Items.Any(i => i.LabelKey == "CS2AS.Park.Note.Prestige"), Is.True);
  var top = Model(level: 5); top.Promotion = ParkProgressionModel.EvaluatePromotion(5, 99_999, new ParkProgressInputs());
  Assert.That(Tab(ParkPanelBuilder.Build(top), "progress").Items.Any(i => i.Id == "maxLevel"), Is.True);
 }
 [Test] public void NoPoliciesAreOfferedAndTheOnlyEventActionIsTheFestivalStart() {
  var d = ParkPanelBuilder.Build(Model(level: 5));
  var actions = d.Tabs.SelectMany(t => t.Items).Where(i => i.Action != null).Select(i => i.Action).Distinct();
  Assert.That(actions, Is.EquivalentTo(new[] { "setBudget", "setFee", "previewBudget", "previewFee" }), "no start control while the event module is unavailable");
  var events = ParkPanelBuilder.Build(ParkEventPanelTests.WithEvents(Model(level: 5)));
  Assert.That(events.Tabs.SelectMany(t => t.Items).Where(i => i.Action != null).Select(i => i.Action).Distinct(),
   Is.EquivalentTo(new[] { "setBudget", "setFee", "previewBudget", "previewFee", "startEvent" }));
 }
 // ---- Wave A5 landscape diagnostics
 internal static readonly Unity.Entities.Entity Prop = new() { Index = 41, Version = 3 };
 internal static ParkPanelModel WithDiagnostics(bool quality, params ParkLandscapeDiagnostic[] diagnostics) {
  var m = Model(quality: quality);
  m.Runtime!.Landscape.Diagnostics = diagnostics.ToList();
  return m;
 }
 [Test] public void LandscapeDiagnosticsAreWarningsWithAFocusButtonOnlyWhenTargeted() {
  var m = WithDiagnostics(false, new ParkLandscapeDiagnostic("Cluttered", 0.8, Prop), new ParkLandscapeDiagnostic("LowLighting", 1, Unity.Entities.Entity.Null),
   new ParkLandscapeDiagnostic("SparseAmenities", 0.5, Prop));
  m.Runtime!.Landscape.Result.Cluttered = true; m.Runtime.Landscape.Result.ClutterPenalty = 12;
  var items = Tab(ParkPanelBuilder.Build(m), "landscape").Items.ToList();
  int clutter = items.FindIndex(i => i.Id == "clutter");
  Assert.That(items[clutter + 1].Id, Is.EqualTo("focus.landscape.Cluttered"), "the existing clutter warning gains only the button");
  Assert.That(items.Count(i => i.LabelKey == "CS2AS.Park.Warning.Cluttered"), Is.EqualTo(1));
  Assert.That(items.Any(i => i.Id == "diagnostic.Cluttered"), Is.False);
  var lighting = Item(Tab(ParkPanelBuilder.Build(m), "landscape"), "diagnostic.LowLighting");
  Assert.That(lighting.Kind, Is.EqualTo(AreaPanelItemKind.Warning));
  Assert.That(lighting.LabelKey, Is.EqualTo("CS2AS.Park.Diagnostic.LowLighting"));
  Assert.That(lighting.TooltipKey, Is.EqualTo("CS2AS.Park.Diagnostic.LowLighting.Tip"));
  Assert.That(items.Any(i => i.Id == "focus.landscape.LowLighting"), Is.False, "no reliable target, no button");
  int sparse = items.FindIndex(i => i.Id == "diagnostic.SparseAmenities");
  var button = items[sparse + 1];
  Assert.That(button.Kind, Is.EqualTo(AreaPanelItemKind.Button));
  Assert.That(button.Id, Is.EqualTo("focus.landscape.SparseAmenities"));
  Assert.That(button.Action, Is.EqualTo(ParkGameplayFacade.FocusDiagnosticTarget));
  Assert.That(button.Value, Is.EqualTo(AreaDiagnosticTargetKey.Format(Prop)));
  Assert.That(button.LabelKey, Is.EqualTo("CS2AS.Park.FocusTarget"));
  Assert.That(button.TooltipKey, Is.EqualTo("CS2AS.Park.FocusTarget.Tip"));
 }
 [Test] public void ShortageLiftedByThePolicyAdjustedBreakdownIsNotShown() {
  var m = WithDiagnostics(true, new ParkLandscapeDiagnostic("LowVegetation", 0.6, Unity.Entities.Entity.Null), new ParkLandscapeDiagnostic("LowLighting", 1, Unity.Entities.Entity.Null));
  Assert.That(m.Runtime!.Quality!.EffectiveLandscape!.Value.Vegetation, Is.GreaterThanOrEqualTo(ParkLandscapeModel.VegetationWeight / 2));
  var items = Tab(ParkPanelBuilder.Build(m), "landscape").Items;
  Assert.That(items.Any(i => i.Id == "diagnostic.LowVegetation"), Is.False);
  Assert.That(items.Any(i => i.Id == "diagnostic.LowLighting"), Is.True);
 }
 [Test] public void FocusIsTheOnlyActionDiagnosticsAdd() {
  var m = WithDiagnostics(false, new ParkLandscapeDiagnostic("SparseLighting", 0.5, Prop));
  var actions = ParkPanelBuilder.Build(m).Tabs.SelectMany(t => t.Items).Where(i => i.Action != null).Select(i => i.Action).Distinct();
  Assert.That(actions, Is.EquivalentTo(new[] { "setBudget", "setFee", "previewBudget", "previewFee", "focusDiagnosticTarget" }));
 }
 [Test] public void DiagnosticKeysAreLocalizationKeys() {
  var keys = ParkPanelBuilder.AllKeys().ToList();
  foreach (var key in ParkLandscapeDiagnostics.Keys.Where(k => k != "Cluttered"))
   Assert.That(keys, Does.Contain("CS2AS.Park.Diagnostic." + key).And.Contain("CS2AS.Park.Diagnostic." + key + ".Tip"));
  Assert.That(keys, Does.Contain("CS2AS.Park.FocusTarget").And.Contain("CS2AS.Park.FocusTarget.Tip"));
 }
 // ---- Wave A1 trends
 private static AreaPanelItem? Find(AreaPanelTabData t, string id) => t.Items.SingleOrDefault(i => i.Id == id);
 [Test] public void NoTrendRowsBeforeAnySample() {
  var d = ParkPanelBuilder.Build(Model());
  Assert.That(d.Tabs.SelectMany(t => t.Items).Any(i => i.Id.EndsWith(".trend")), Is.False);
 }
 internal static ParkPanelModel WithTrends() {
  var m = Model();
  var t = m.Runtime!.Trends;
  t.Quality.Record(4, 50); t.Quality.Record(5, 47.5f);
  t.Visitors.Record(4, 30); t.Visitors.Record(5, 42);
  t.Satisfaction.Record(5, 60);
  t.Occupancy.Record(4, 20); t.Occupancy.Record(5, 20);
  t.AdmissionIncome.Record(4, 600); t.AdmissionIncome.Record(5, 900);
  t.ManagementExpense.Record(4, 300); t.ManagementExpense.Record(5, 250);
  return m;
 }
 [Test] public void TrendRowsFollowTheirHeadlineMetric() {
  var d = ParkPanelBuilder.Build(WithTrends());
  void Adjacent(string tab, string id, string label, string? value) {
   var items = Tab(d, tab).Items.ToList();
   int metric = items.FindIndex(i => i.Id == id);
   Assert.That(metric, Is.GreaterThanOrEqualTo(0), id);
   Assert.That(items[metric + 1].Id, Is.EqualTo(id + ".trend"), tab + "/" + id);
   Assert.That(items[metric + 1].LabelKey, Is.EqualTo(label), id);
   Assert.That(items[metric + 1].Value, Is.EqualTo(value), id);
   Assert.That(items[metric + 1].TooltipKey, Is.EqualTo("CS2AS.Trend.Tip"));
  }
  Adjacent("overview", "quality", "CS2AS.Trend.Down", "-2.5");
  Adjacent("visitors", "recentVisitors", "CS2AS.Trend.Up", "+12");
  Adjacent("visitors", "satisfaction", "CS2AS.Trend.New", "—");
  Adjacent("visitors", "occupancy", "CS2AS.Trend.Flat", "0.0");
  Adjacent("finances", "recentAdmission", "CS2AS.Trend.Up", "+¢300");
  Adjacent("finances", "recentExpense", "CS2AS.Trend.Down", "-¢50");
  Assert.That(Find(Tab(d, "overview"), "recentVisitors.trend"), Is.Null, "one trend row per metric, on its detail tab");
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void TrendKeysAreLocalized(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  foreach (var key in new[] { "CS2AS.Trend.Up", "CS2AS.Trend.Down", "CS2AS.Trend.Flat", "CS2AS.Trend.New", "CS2AS.Trend.Tip" })
   Assert.That(entries.ContainsKey(key), Is.True, locale + " is missing " + key);
  Assert.That(ParkPanelBuilder.AllKeys(), Does.Contain("CS2AS.Trend.New"));
  Assert.That(CS2AreaSystem.Modules.Industrial.IndustrialPanelBuilder.AllKeys(), Does.Contain("CS2AS.Trend.New"));
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryKeyTheParkPanelCanEmitIsLocalized(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  var keys = new HashSet<string>(ParkPanelBuilder.AllKeys());
  foreach (var model in new[] { Model(1), Model(2, landscape: false, primary: false), Model(4), Model(5), Model(quality: false, entrances: 0),
   ParkEventPanelTests.WithEvents(Model()), ParkEventPanelTests.Active(), ParkEventPanelTests.Pending() })
   foreach (var item in ParkPanelBuilder.Build(model).Tabs.SelectMany(t => t.Items.Select(i => (t, i)))) {
    keys.Add(item.t.LabelKey); keys.Add(item.i.LabelKey); if (item.i.ValueKey != null) keys.Add(item.i.ValueKey); if (item.i.TooltipKey != null) keys.Add(item.i.TooltipKey); }
  keys.Add("CS2AS.Park.Summary.Active"); keys.Add("CS2AS.Park.Summary.EntranceRequired"); keys.Add("CS2AS.Park.Summary.EntranceInvalid");
  foreach (var key in keys) Assert.That(entries.ContainsKey(key), Is.True, locale + " is missing " + key);
 }
}
public class ParkPanelCacheTests {
 [Test] public void RebuildsOnlyForANewAreaAfterTheIntervalOrAfterAnAction() {
  long now = 0; int builds = 0;
  var cache = new ParkPanelCache(250, () => now);
  var a = new Unity.Entities.Entity { Index = 1, Version = 1 }; var b = new Unity.Entities.Entity { Index = 2, Version = 1 };
  AreaPanelDocument Build() { builds++; return new AreaPanelDocument(); }
  var first = cache.Get(a, Build);
  now = 100; Assert.That(cache.Get(a, Build), Is.SameAs(first)); Assert.That(builds, Is.EqualTo(1));
  Assert.That(cache.Get(b, Build), Is.Not.SameAs(first), "another Park is never served a cached document"); Assert.That(builds, Is.EqualTo(2));
  now = 400; cache.Get(b, Build); Assert.That(builds, Is.EqualTo(3), "refreshes after the interval");
  cache.Invalidate(); cache.Get(b, Build); Assert.That(builds, Is.EqualTo(4), "an applied action shows immediately");
 }
}

// Phase 2 C5 Task 4: the Events tab shows status, cost, the guarded start button, lifetime totals and history.
public class ParkEventPanelTests {
 internal static ParkPanelModel WithEvents(ParkPanelModel m, ParkEventRequestOutcome start = ParkEventRequestOutcome.Request, long cost = 450) {
  m.EventsAvailable = true; m.HasEvents = true; m.Events = ParkEventState.CreateDefault(); m.EventStart = start; m.EventCost = cost;
  return m;
 }
 internal static ParkPanelModel Active() {
  var m = WithEvents(ParkPanelBuilderTests.Model(), ParkEventRequestOutcome.Active, 0);
  m.Events = ParkEventModel.Start(m.Events, ParkEventCatalog.ParkFestival, 100, 450);
  m.Frame = 100 + ParkEventCatalog.DurationFrames / 2;
  m.EventHistory = new[] { ParkEventModel.HistoryEntry(ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 10, 300), 300), ParkEventModel.HistoryEntry(m.Events, 450) };
  return m;
 }
 internal static ParkPanelModel Pending() {
  var m = WithEvents(ParkPanelBuilderTests.Model(), ParkEventRequestOutcome.Pending, 0);
  m.EventPendingCost = 450;
  return m;
 }
 static AreaPanelTabData Tab(ParkPanelModel m) => ParkPanelBuilder.Build(m).Tabs.Single(t => t.Id == "events");
 static AreaPanelItem Item(AreaPanelTabData t, string id) => t.Items.Single(i => i.Id == id);
 static AreaPanelItem Find(AreaPanelTabData t, string id) => t.Items.SingleOrDefault(i => i.Id == id);
 [Test] public void IdleParkOffersTheFestivalAtItsCost() {
  var tab = Tab(WithEvents(ParkPanelBuilderTests.Model()));
  Assert.That(Item(tab, "events.status").ValueKey, Is.EqualTo("CS2AS.Park.Events.Status.None"));
  Assert.That(Item(tab, "events.cost").Value, Is.EqualTo("¢450"));
  var start = Item(tab, "events.start");
  Assert.That((start.Kind, start.Action, start.Value, start.Disabled, start.LabelKey), Is.EqualTo((AreaPanelItemKind.Button, ParkGameplayFacade.StartEvent, "1", false, "CS2AS.Park.Events.Start.1")));
  Assert.That(tab.Items.Any(i => i.Kind == AreaPanelItemKind.Note && i.LabelKey == "CS2AS.Park.Events.Tip"), Is.True);
 }
 [TestCase(ParkEventRequestOutcome.Inactive)] [TestCase(ParkEventRequestOutcome.NoCost)] [TestCase(ParkEventRequestOutcome.Unavailable)]
 public void BlockedStartIsDisabledWithItsReason(ParkEventRequestOutcome outcome) {
  var start = Item(Tab(WithEvents(ParkPanelBuilderTests.Model(), outcome, 0)), "events.start");
  Assert.That(start.Disabled, Is.True);
  Assert.That(start.TooltipKey, Is.EqualTo("CS2AS.Park.Events.Blocked." + outcome));
 }
 [Test] public void UnknownCostIsCalculatingNotZero() =>
  Assert.That(Item(Tab(WithEvents(ParkPanelBuilderTests.Model(), ParkEventRequestOutcome.NoCost, 0)), "events.cost").ValueKey, Is.EqualTo("CS2AS.Park.Calculating"));
 [Test] public void PendingShowsTheRequestedCostAndNoSecondStart() {
  var tab = Tab(Pending());
  Assert.That(Item(tab, "events.status").ValueKey, Is.EqualTo("CS2AS.Park.Events.Status.Pending"));
  Assert.That(Item(tab, "events.pendingCost").Value, Is.EqualTo("¢450"));
  Assert.That(Item(tab, "events.start").Disabled, Is.True);
 }
 [Test] public void ActiveFestivalShowsRemainingTimeEffectsLifetimeAndNewestFirstHistory() {
  var tab = Tab(Active());
  Assert.That(Item(tab, "events.status").ValueKey, Is.EqualTo("CS2AS.Park.Events.Status.Active"));
  Assert.That(Item(tab, "events.remaining").Value, Is.EqualTo("12"), "half a day left, in in-game hours");
  Assert.That(tab.Items.Any(i => i.Kind == AreaPanelItemKind.Note && i.LabelKey == "CS2AS.Park.Events.Effects"), Is.True);
  Assert.That(Find(tab, "events.cost"), Is.Null);
  Assert.That(Item(tab, "events.lifetimeEvents").Value, Is.EqualTo("1"));
  Assert.That(Item(tab, "events.lifetimeExpense").Value, Is.EqualTo("¢450"));
  var history = tab.Items.Where(i => i.Id.StartsWith("events.history.")).ToList();
  Assert.That(history.Select(h => h.Value), Is.EqualTo(new[] { "¢450", "¢300" }), "newest first");
  Assert.That(history.All(h => h.LabelKey == "CS2AS.Park.Events.Name.1"), Is.True);
 }
 [Test] public void SuspendedParkStillShowsItsPendingPayment() {
  var m = Pending(); m.Lifecycle = AreaLifecycleState.Suspended; m.EventStart = ParkEventRequestOutcome.Inactive;
  var tab = Tab(m);
  Assert.That(Item(tab, "events.status").ValueKey, Is.EqualTo("CS2AS.Park.Events.Status.Pending"), "the charge is still outstanding");
  Assert.That(Item(tab, "events.pendingCost").Value, Is.EqualTo("¢450"));
  Assert.That(Find(tab, "events.cost"), Is.Null);
  Assert.That(Item(tab, "events.start").Disabled, Is.True);
 }
 [Test] public void SuspendedParkShowsTheFestivalAsPaused() {
  var m = Active(); m.Lifecycle = AreaLifecycleState.Suspended;
  Assert.That(Item(Tab(m), "events.status").ValueKey, Is.EqualTo("CS2AS.Park.Events.Status.Paused"), "time continues, effects are neutral");
 }
 [Test] public void UnavailableModuleShowsAWarningAndNoControl() {
  var m = WithEvents(ParkPanelBuilderTests.Model()); m.EventsAvailable = false;
  var tab = Tab(m);
  Assert.That(tab.Items.Any(i => i.Kind == AreaPanelItemKind.Warning && i.LabelKey == "CS2AS.Park.Events.Unavailable"), Is.True);
  Assert.That(tab.Items.Any(i => i.Action != null), Is.False);
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryEventKeyIsLocalized(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  foreach (var key in ParkPanelBuilder.AllKeys().Where(k => k.Contains("Events")).Append("CS2AS.Tab.Events"))
   Assert.That(entries.ContainsKey(key), Is.True, locale + " is missing " + key);
  foreach (var outcome in Enum.GetNames(typeof(ParkEventRequestOutcome)).Where(o => o != "Request"))
   Assert.That(ParkPanelBuilder.AllKeys(), Does.Contain("CS2AS.Park.Events.Blocked." + outcome));
 }
}
// Phase 2 C6: the Finances tab shows the coverage operating factor and whether it is actually applied.
public class ParkCoveragePanelTests {
 static AreaPanelTabData Finances(ParkPanelModel m) => ParkPanelBuilder.Build(m).Tabs.Single(t => t.Id == "finances");
 static AreaPanelItem Find(AreaPanelTabData t, string id) => t.Items.SingleOrDefault(i => i.Id == id);
 [Test] public void GatedModifierShowsTheFactorAndThatItIsNotApplied() {
  var m = ParkPanelBuilderTests.Model(); m.CoverageFactor = 1.05f; m.CoverageActive = false;
  var tab = Finances(m);
  Assert.That(Find(tab, "coverage.factor")?.Value, Is.EqualTo("105%"));
  Assert.That(Find(tab, "coverage.factor")?.TooltipKey, Is.EqualTo("CS2AS.Park.Coverage.Factor.Tip"));
  Assert.That(Find(tab, "coverage.disabled")?.LabelKey, Is.EqualTo("CS2AS.Park.Coverage.Disabled"));
  Assert.That(Find(tab, "coverage.buildings"), Is.Null);
 }
 [Test] public void ActiveModifierShowsHowManyBuildingsItModifies() {
  var m = ParkPanelBuilderTests.Model(); m.CoverageFactor = 0.9f; m.CoverageActive = true; m.CoverageBuildings = 3;
  var tab = Finances(m);
  Assert.That(Find(tab, "coverage.factor")?.Value, Is.EqualTo("90%"));
  Assert.That(Find(tab, "coverage.buildings")?.Value, Is.EqualTo("3"));
  Assert.That(Find(tab, "coverage.disabled"), Is.Null);
 }
 [Test] public void TheModelDefaultIsNeutralAndGated() {
  var m = new ParkPanelModel();
  Assert.That((m.CoverageFactor, m.CoverageActive, m.CoverageBuildings), Is.EqualTo((1f, false, 0)));
 }
 [Test] public void TheFacadeReadsTheFactorFromBudgetAndPolicyFundingForActiveParksOnly() {
  var facade = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkGameplayFacade.cs");
  Assert.That(facade, Does.Contain("ParkCoverageModifierModel.Factor(model.Budget, model.Policies.ManagementFundingMultiplier)"));
  Assert.That(facade, Does.Contain("model.CoverageActive = coverage != null && coverage.Enabled;"));
  Assert.That(facade.IndexOf("ParkCoverageModifierModel.Factor("), Is.GreaterThan(facade.IndexOf("model.Policies=")), "policies are read first");
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryCoverageKeyIsLocalized(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  var keys = ParkPanelBuilder.AllKeys().Where(k => k.Contains(".Coverage.")).ToList();
  Assert.That(keys, Is.SupersetOf(new[] { "CS2AS.Park.Coverage.Factor", "CS2AS.Park.Coverage.Factor.Tip", "CS2AS.Park.Coverage.Disabled", "CS2AS.Park.Coverage.Buildings" }));
  foreach (var key in keys) Assert.That(entries.ContainsKey(key), Is.True, locale + " is missing " + key);
 }
}
