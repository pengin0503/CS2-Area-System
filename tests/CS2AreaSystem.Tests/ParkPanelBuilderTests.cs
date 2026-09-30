using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkPanelBuilderTests {
 internal static ParkPanelModel Model(int level = 2, bool landscape = true, bool primary = true, bool quality = true, int entrances = 2) {
  var runtime = new ParkRuntimeSnapshot { StableId = 7, AreaSquareMeters = 10_000, ActiveVisitors = 14, PeakOccupancy = 30, EntranceCount = entrances };
  var inputs = new ParkLandscapeInputs { AreaSquareMeters = 10_000, PathLength = 300, Vegetation = new() { Count = 50, DistinctPrefabs = 10 }, Amenities = new() { Count = 6, DistinctPrefabs = 2 } };
  runtime.Landscape = new ParkLandscapeSnapshot { Available = landscape, Inputs = inputs, Result = ParkLandscapeModel.ComputeLandscape(inputs), TransitStops = 1 };
  runtime.Facilities = ParkFacilitySnapshot.Build(new[] { new ParkFacilityTraits { IsBuilding = true, HasPark = true, HasLeisureData = true, LeisureType = Game.Agents.LeisureType.CityPark, LeisureEfficiency = 20 } });
  runtime.RecentVisitors.Add(100, 40); runtime.RecentAdmission.Add(100, 800); runtime.RecentExpense.Add(100, 300);
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
 [Test] public void EmitsTheFiveApprovedTabs() {
  var d = ParkPanelBuilder.Build(Model());
  Assert.That(d.Tabs.Select(t => t.Id), Is.EqualTo(new[] { "overview", "visitors", "finances", "landscape", "progress" }));
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
 [Test] public void NoPoliciesOrEventsAreOffered() {
  var d = ParkPanelBuilder.Build(Model(level: 5));
  var actions = d.Tabs.SelectMany(t => t.Items).Where(i => i.Action != null).Select(i => i.Action).Distinct();
  Assert.That(actions, Is.EquivalentTo(new[] { "setBudget", "setFee" }));
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryKeyTheParkPanelCanEmitIsLocalized(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  var keys = new HashSet<string>(ParkPanelBuilder.AllKeys());
  foreach (var model in new[] { Model(1), Model(2, landscape: false, primary: false), Model(4), Model(5), Model(quality: false, entrances: 0) })
   foreach (var item in ParkPanelBuilder.Build(model).Tabs.SelectMany(t => t.Items.Select(i => (t, i)))) {
    keys.Add(item.t.LabelKey); keys.Add(item.i.LabelKey); if (item.i.ValueKey != null) keys.Add(item.i.ValueKey); if (item.i.TooltipKey != null) keys.Add(item.i.TooltipKey); }
  keys.Add("CS2AS.Park.Summary.Active"); keys.Add("CS2AS.Park.Summary.EntranceRequired");
  foreach (var key in keys) Assert.That(entries.ContainsKey(key), Is.True, locale + " is missing " + key);
 }
}
