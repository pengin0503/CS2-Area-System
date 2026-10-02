using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialPanelBuilderTests {
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static IndustrialEntityObservation Producer(int index, double activity, int jobs, int employees, Resource resource,
  IndustrialFlowState input = IndustrialFlowState.Healthy, IndustrialFlowState output = IndustrialFlowState.Healthy, double efficiency = 100, double profit = 70) => new() {
  Facility = E(index), Roles = IndustrialFacilityRole.Processor, IsProducer = true, ProducerTenants = 1, Available = true, IsOperatingProducer = activity > 0, ProductiveActivity = activity,
  Resources = new[] { new IndustrialResourceActivity { Resource = (ulong)resource, ActualPerDay = activity * 12, CapacityPerDay = 1200 } }, Input = input, Output = output,
  Jobs = jobs, Employees = employees, VacanciesByLevel = new[] { jobs - employees, 0, 0, 0, 0 }, Efficiency = efficiency, Profitability = profit };
 private static IndustrialEntityObservation Warehouse(int index) => new() { Facility = E(index), Roles = IndustrialFacilityRole.Storage | IndustrialFacilityRole.SupportingWorkplace,
  Available = true, Jobs = 10, Employees = 8, VacanciesByLevel = new[] { 2, 0, 0, 0, 0 }, StoredAmount = 3000, StorageCapacity = 12000, Efficiency = 100, Profitability = 60,
  Input = IndustrialFlowState.NotApplicable, Output = IndustrialFlowState.NotApplicable };
 private static IndustrialObservationResult Observe(int hours, Func<int, IndustrialEntityObservation[]> at, int nonPrimary = 0, IndustrialRuntimeSnapshot snapshot = null) {
  snapshot ??= new IndustrialRuntimeSnapshot();
  IndustrialObservationResult last = null!;
  for (int h = 0; h <= hours; h++) {
   var acc = new IndustrialCycleAccumulator();
   foreach (var o in at(h)) IndustrialObservationAggregator.Apply(acc, o);
   last = IndustrialObservationAggregator.Finalize(acc, snapshot, new IndustrialCycleInfo { CycleId = h + 1, HourBucket = 200 + h, StartHours = 200 + h, EndHours = 200.2 + h,
    MembershipToken = 1, StructureToken = 1, Active = true, NonPrimaryMembers = nonPrimary });
  }
  return last;
 }
 internal static IndustrialPanelModel Model(string kind) {
  var m = new IndustrialPanelModel { StableId = 42, Lifecycle = AreaLifecycleState.Active, Level = 2, Experience = 2_340, HasState = true,
   Headquarters = IndustrialHeadquartersState.Valid, NowHours = 206.5 };
  m.State = IndustrialAreaState.CreateDefault(8);
  m.State.XpGrantedToday = 120; m.State.ActivityObservedHours = 6; m.State.ActivityWeightedSum = 6 * 88; m.State.HighThroughputStreakDays = 3; m.State.LifetimePeakEmployees = 160;
  switch (kind) {
   case "active": {
    var runtime = new IndustrialRuntimeSnapshot();
    m.Current = Observe(6, h => new[] { Producer(1, 95, 40, 38, Resource.Furniture), Producer(2, 90, 30, 30, Resource.Steel), Producer(3, 80, 50, 45, Resource.Paper), Warehouse(4) }, snapshot: runtime);
    m.Trends = runtime.Trends;
    m.HasXp = true; m.Xp = new IndustrialXpResult { Hourly = 9, Award = 9, OperatingScore = 88.4 };
    m.LastCompletedDay = new IndustrialDayResult { Day = 7, ObservedHours = 17.5, Average = 91, Reason = "InsufficientCoverage" };
    break; }
   case "suspended":
    m.Lifecycle = AreaLifecycleState.Suspended; m.Headquarters = IndustrialHeadquartersState.Missing; m.Level = 1; m.Experience = 0;
    break;
   case "paused":
    m.Lifecycle = AreaLifecycleState.Suspended; m.Headquarters = IndustrialHeadquartersState.Invalid; m.Paused = true;
    m.Current = Observe(5, h => new[] { Producer(1, 70, 40, 30, Resource.Furniture) });
    m.HasXp = true; m.Xp = new IndustrialXpResult { Hourly = 6, BlockedReason = "Suspended" };
    break;
   case "unavailable":
    m.Current = Observe(2, h => new[] { Producer(1, 0, 20, 20, Resource.Grain) with { ProductiveActivity = null, IsOperatingProducer = false, Resources = Array.Empty<IndustrialResourceActivity>(), Efficiency = null, Profitability = null } });
    m.HasXp = true; m.Xp = new IndustrialXpResult { BlockedReason = "NoOperatingProducer" };
    break;
   case "overlapping":
    m.Current = Observe(6, h => new[] { Producer(1, 60, 40, 20, Resource.Furniture, input: h % 2 == 0 ? IndustrialFlowState.Affected : IndustrialFlowState.Healthy, profit: 15),
     Producer(2, 0, 30, 10, Resource.Furniture, output: IndustrialFlowState.Affected, efficiency: 40) }, nonPrimary: 3);
    m.HasXp = true; m.Xp = new IndustrialXpResult { Hourly = 4, Award = 0, OperatingScore = 41, BlockedReason = "DailyCap" };
    m.State.XpGrantedToday = 240;
    break;
   case "maxLevel":
    m.Level = 5; m.Experience = 80_000;
    m.Current = Observe(6, h => new[] { Producer(1, 100, 100, 100, Resource.Furniture), Producer(2, 100, 100, 100, Resource.Steel), Producer(3, 100, 100, 100, Resource.Paper) });
    m.HasXp = true; m.Xp = new IndustrialXpResult { Hourly = 10, Award = 10, OperatingScore = 99 };
    break;
   default: throw new ArgumentException(kind);
  }
  m.Promotion = IndustrialProgressionModel.EvaluatePromotion(m.Level, m.Experience, IndustrialProgressionSystem.BuildInputs(m.Current, m.State));
  return m;
 }
 internal static readonly string[] Kinds = { "active", "suspended", "paused", "unavailable", "overlapping", "maxLevel" };
 private static AreaPanelTabData Tab(AreaPanelDocument d, string id) => d.Tabs.Single(t => t.Id == id);
 private static AreaPanelItem Item(AreaPanelTabData t, string id) => t.Items.Single(i => i.Id == id);
 private static bool Has(AreaPanelTabData t, string id) => t.Items.Any(i => i.Id == id);
 [Test] public void EmitsExactlyTheFiveApprovedTabsAndHidesCoreProgressRows() {
  var d = IndustrialPanelBuilder.Build(Model("active"));
  Assert.That(d.Tabs.Select(t => t.Id), Is.EqualTo(new[] { "overview", "production", "logistics", "employment", "progress" }));
  Assert.That(d.ShowsProgress, Is.True);
 }
 [Test] public void OverviewShowsLevelScoresFacilitiesJobsBottlenecksAndLastUpdate() {
  var o = Tab(IndustrialPanelBuilder.Build(Model("active")), "overview");
  Assert.That(Item(o, "level").ValueKey, Is.EqualTo("CS2AS.Industrial.LevelName.2"));
  Assert.That(Item(o, "experience").Value, Is.EqualTo("2,340 / 5,000"));
  Assert.That(Item(o, "headquarters").ValueKey, Is.EqualTo("CS2AS.Industrial.Hq.Valid"));
  foreach (var id in new[] { "performance", "production", "logistics", "employment", "efficiency" }) Assert.That(Item(o, id).Kind, Is.EqualTo(AreaPanelItemKind.Bar), id);
  Assert.That(Item(o, "activeFacilities").Value, Is.EqualTo("3 / 3"));
  Assert.That(Item(o, "employees").Value, Is.EqualTo("121 / 130"));
  Assert.That(Item(o, "lastUpdate").Value, Is.EqualTo("8, 14:12"), "game day 8, 14:12 (end of the hour-206 cycle)");
  Assert.That(Has(o, "bottlenecks"), Is.True, "healthy district: no bottleneck row");
  Assert.That(o.Items.Any(i => i.Id == "hqHealth" && i.Kind == AreaPanelItemKind.Note), Is.True, "HQ health shown without a debuff");
 }
 [Test] public void BottlenecksShowEvidenceAffectedCountWindowAndSuggestion() {
  var o = Tab(IndustrialPanelBuilder.Build(Model("overlapping")), "overview");
  var warnings = o.Items.Where(i => i.Kind == AreaPanelItemKind.Warning && i.Id.StartsWith("bottleneck.")).ToList();
  Assert.That(warnings.Count, Is.InRange(1, 3));
  foreach (var w in warnings) {
   Assert.That(w.Value, Does.Match(@"^\(\d+% · ×\d+ · \d+\.\d h\)$"), w.Id);
   Assert.That(w.TooltipKey, Is.EqualTo(w.LabelKey + ".Tip"));
   Assert.That(o.Items.Any(i => i.Kind == AreaPanelItemKind.Note && i.Id == "suggestion." + w.Id.Substring("bottleneck.".Length)), Is.True);
  }
  Assert.That(Item(o, "overlap").Value, Is.EqualTo("3"), "non-primary overlap is shown, never counted");
 }
 [Test] public void UnavailableDataIsNeverRenderedAsZero() {
  var d = IndustrialPanelBuilder.Build(Model("unavailable"));
  var o = Tab(d, "overview"); var p = Tab(d, "production"); var l = Tab(d, "logistics");
  Assert.That(Item(o, "production").Kind, Is.EqualTo(AreaPanelItemKind.Row)); Assert.That(Item(o, "production").ValueKey, Is.EqualTo("CS2AS.Industrial.Status.Unavailable"));
  Assert.That(Item(p, "activity").ValueKey, Is.EqualTo("CS2AS.Industrial.Status.Unavailable"));
  Assert.That(Item(o, "logistics").ValueKey, Is.EqualTo("CS2AS.Industrial.Status.WarmingUp"));
  Assert.That(Item(o, "logistics.coverage").Value, Is.EqualTo("2.0 / 4.0 h"));
  Assert.That(Item(o, "efficiency").ValueKey, Is.EqualTo("CS2AS.Industrial.Status.Unavailable"));
  Assert.That(Item(l, "storageUtilization").ValueKey, Is.EqualTo("CS2AS.Industrial.Status.NotApplicable"));
  foreach (var item in d.Tabs.SelectMany(t => t.Items).Where(i => i.ValueKey != null && i.ValueKey.StartsWith("CS2AS.Industrial.Status.")))
   Assert.That(item.Kind, Is.Not.EqualTo(AreaPanelItemKind.Bar), item.Id);
 }
 [Test] public void PartialCoverageIsLabeled() {
  var o = Tab(IndustrialPanelBuilder.Build(Model("active")), "overview");
  // The warehouse has no flow role; the active district is fully observed.
  Assert.That(o.Items.Any(i => i.Id.EndsWith(".coverage") && i.LabelKey == "CS2AS.Industrial.PartialData"), Is.False);
  var m = Model("active");
  m.Current!.Production = IndustrialMetric.Partial(80, 2, 3);
  var partial = Tab(IndustrialPanelBuilder.Build(m), "overview");
  Assert.That(Item(partial, "production.coverage").Value, Is.EqualTo("2 / 3"));
 }
 [Test] public void SuspendedWithoutHeadquartersExplainsAndWaits() {
  var o = Tab(IndustrialPanelBuilder.Build(Model("suspended")), "overview");
  Assert.That(o.Items.Any(i => i.Kind == AreaPanelItemKind.Warning && i.LabelKey == "CS2AS.Industrial.Warning.HeadquartersRequired"), Is.True);
  Assert.That(Item(o, "performance").ValueKey, Is.EqualTo("CS2AS.Industrial.Calculating"));
  Assert.That(Item(o, "headquarters").ValueKey, Is.EqualTo("CS2AS.Industrial.Hq.Missing"));
 }
 [Test] public void PausedSnapshotIsShownOnlyAsStale() {
  var d = IndustrialPanelBuilder.Build(Model("paused"));
  var o = Tab(d, "overview");
  Assert.That(o.Items.Any(i => i.LabelKey == "CS2AS.Industrial.Warning.Paused"), Is.True);
  Assert.That(o.Items.Any(i => i.LabelKey == "CS2AS.Industrial.Warning.HeadquartersInvalid"), Is.True);
  Assert.That(d.Tabs.SelectMany(t => t.Items).Where(i => i.Id != "experience").Any(i => i.Kind == AreaPanelItemKind.Bar), Is.False, "no paused value is drawn as a current score");
  Assert.That(Item(o, "performance").TooltipKey, Is.EqualTo("CS2AS.Industrial.Status.Stale"));
  Assert.That(Item(Tab(d, "progress"), "xpBlocked").LabelKey, Is.EqualTo("CS2AS.Industrial.XpBlocked.Suspended"));
 }
 [Test] public void ProductionTabShowsRolesActivityResourcesAndSampledLabel() {
  var p = Tab(IndustrialPanelBuilder.Build(Model("active")), "production");
  Assert.That(Item(p, "extractors").Value, Is.EqualTo("0")); Assert.That(Item(p, "processors").Value, Is.EqualTo("3"));
  Assert.That(Item(p, "activeShare").Kind, Is.EqualTo(AreaPanelItemKind.Bar)); Assert.That(Item(p, "activity").Kind, Is.EqualTo(AreaPanelItemKind.Bar));
  Assert.That(Item(p, "resource.Furniture").LabelKey, Is.EqualTo("Resources.TITLE[Furniture]"), "vanilla resource title key");
  Assert.That(Item(p, "resource.Furniture").Value, Is.EqualTo("1,140 /d"));
  Assert.That(Item(p, "meaningfulOutputs").Value, Is.EqualTo("3 / 3"));
  Assert.That(p.Items.Any(i => i.Kind == AreaPanelItemKind.Note && i.LabelKey == "CS2AS.Industrial.Note.Sampled"), Is.True);
 }
 [Test] public void LogisticsTabShowsStorageCargoFlowAndNoPlacementBonus() {
  var l = Tab(IndustrialPanelBuilder.Build(Model("active")), "logistics");
  Assert.That(Item(l, "storageFacilities").Value, Is.EqualTo("1"));
  Assert.That(Item(l, "storageUtilization").Value, Is.EqualTo("3,000 / 12,000 (25%)"));
  Assert.That(Item(l, "cargoFacilities").Value, Is.EqualTo("0 / 0"));
  Assert.That(Item(l, "inputContinuity").Kind, Is.EqualTo(AreaPanelItemKind.Bar)); Assert.That(Item(l, "outputContinuity").Kind, Is.EqualTo(AreaPanelItemKind.Bar));
  Assert.That(Item(l, "inputHours").Value, Is.EqualTo("6.0 / 6.0 h"));
  Assert.That(Item(l, "logistics").NumericValue, Is.EqualTo(100));
  Assert.That(l.Items.Select(i => i.LabelKey), Does.Contain("CS2AS.Industrial.Note.NoPlacementBonus").And.Contain("CS2AS.Industrial.Note.ExternalCargo"));
  var flow = Tab(IndustrialPanelBuilder.Build(Model("overlapping")), "logistics");
  Assert.That(flow.Items.Any(i => i.Id == "bottleneck.InputStarvation" || i.Id == "bottleneck.StorageSaturation"), Is.True);
 }
 [Test] public void EmploymentTabShowsJobsVacanciesFillAndEducationMismatch() {
  var e = Tab(IndustrialPanelBuilder.Build(Model("active")), "employment");
  Assert.That(Item(e, "jobs").Value, Is.EqualTo("130")); Assert.That(Item(e, "employees").Value, Is.EqualTo("121")); Assert.That(Item(e, "vacancies").Value, Is.EqualTo("9"));
  Assert.That(Item(e, "jobFill").Kind, Is.EqualTo(AreaPanelItemKind.Bar)); Assert.That(Item(e, "employment").Kind, Is.EqualTo(AreaPanelItemKind.Bar));
  Assert.That(Item(e, "producerJobs").Value, Is.EqualTo("113 / 120")); Assert.That(Item(e, "supportJobs").Value, Is.EqualTo("8 / 10"));
  Assert.That(Item(e, "vacancy.0").Value, Is.EqualTo("9"));
  Assert.That(e.Items.Any(i => i.LabelKey == "CS2AS.Industrial.Note.OfficesExcluded"), Is.True);
 }
 [Test] public void ProgressTabShowsEveryGateXpCapStreakAndNoBuffNote() {
  var p = Tab(IndustrialPanelBuilder.Build(Model("active")), "progress");
  Assert.That(p.Items.Where(i => i.Kind == AreaPanelItemKind.Check).Select(i => i.Id),
   Is.EqualTo(new[] { "check.Experience", "check.Performance", "check.ActiveFacilities", "check.Employees", "check.Logistics" }));
  Assert.That(Item(p, "check.Experience").Value, Is.EqualTo("2,340 / 5,000")); Assert.That(Item(p, "check.Experience").NumericValue, Is.Zero);
  Assert.That(Item(p, "hourlyXp").Value, Is.EqualTo("9 / 10")); Assert.That(Item(p, "dailyXp").Value, Is.EqualTo("120 / 240"));
  Assert.That(Item(p, "streakDays").Value, Is.EqualTo("3 / 7")); Assert.That(Item(p, "dayAverage").Value, Is.EqualTo("88.0 / 80")); Assert.That(Item(p, "dayHours").Value, Is.EqualTo("6.0 / 18.0 h"));
  Assert.That(Item(p, "lastDay").ValueKey, Is.EqualTo("CS2AS.Industrial.Day.InsufficientCoverage"));
  Assert.That(p.Items.Any(i => i.LabelKey == "CS2AS.Industrial.Note.NoBuff"), Is.True);
  var capped = Tab(IndustrialPanelBuilder.Build(Model("overlapping")), "progress");
  Assert.That(Item(capped, "xpBlocked").LabelKey, Is.EqualTo("CS2AS.Industrial.XpBlocked.DailyCap"));
  var top = Tab(IndustrialPanelBuilder.Build(Model("maxLevel")), "progress");
  Assert.That(Has(top, "maxLevel"), Is.True); Assert.That(top.Items.Any(i => i.Kind == AreaPanelItemKind.Check), Is.False);
 }
 [Test] public void Level4ShowsPrestigeObjectives() {
  var m = Model("active"); m.Level = 4; m.Experience = 36_000;
  m.Promotion = IndustrialProgressionModel.EvaluatePromotion(4, m.Experience, IndustrialProgressionSystem.BuildInputs(m.Current, m.State));
  var p = Tab(IndustrialPanelBuilder.Build(m), "progress");
  Assert.That(p.Items.Count(i => i.Id.StartsWith("check.Prestige")), Is.EqualTo(5));
  Assert.That(p.Items.Any(i => i.LabelKey == "CS2AS.Industrial.Note.Prestige"), Is.True);
 }
 [Test] public void PanelIsReadOnlyAtEveryLevel() {
  foreach (var kind in Kinds) {
   var d = IndustrialPanelBuilder.Build(Model(kind));
   // Wave A3: the read-only diagnostic focus button is the only action the Industrial panel may emit.
   var actions = d.Tabs.SelectMany(t => t.Items).Where(i => i.Action != null || i.Kind == AreaPanelItemKind.Slider || i.Kind == AreaPanelItemKind.Button);
   Assert.That(actions.All(i => i.Kind == AreaPanelItemKind.Button && i.Action == IndustrialGameplayFacade.FocusDiagnosticTarget), Is.True, kind);
  }
  var low = Tab(IndustrialPanelBuilder.Build(Model("suspended") is var s ? s : null), "production");
  Assert.That(low, Is.Not.Null, "all diagnostic tabs exist at Level 1");
  var module = new IndustrialAreaModule(new AreaAnchorService(new NoStore(), e => true, (a, e, r) => true), (e, _) => default);
  Assert.That(module.HandleAction(E(1), "setBudget", "1", default), Is.False, "no Industrial-specific action");
 }
 [Test] public void SummaryExplainsHeadquartersState() {
  var store = new NoStore();
  var anchors = new AreaAnchorService(store, e => e != E(99), (a, e, r) => true);
  var module = new IndustrialAreaModule(anchors, (e, _) => default);
  Assert.That(module.BuildSummary(E(1), default), Is.EqualTo("CS2AS.Industrial.Summary.HeadquartersRequired"));
  store.Links[E(1)] = new[] { new AreaAnchorLink { Anchor = E(99), Role = AreaAnchorRole.Headquarters } };
  Assert.That(module.BuildSummary(E(1), default), Is.EqualTo("CS2AS.Industrial.Summary.HeadquartersInvalid"));
  store.Links[E(1)] = new[] { new AreaAnchorLink { Anchor = E(5), Role = AreaAnchorRole.Headquarters } };
  Assert.That(module.BuildSummary(E(1), default), Is.EqualTo("CS2AS.Industrial.Summary.Active"));
 }
 // ---- Wave A1 trends
 private static AreaPanelItem? Find(AreaPanelTabData t, string id) => t.Items.SingleOrDefault(i => i.Id == id);
 [Test] public void TrendRowsFollowEachHeadlineMetricOnlyWhenSamplesExist() {
  var none = IndustrialPanelBuilder.Build(Model("unavailable"));
  Assert.That(none.Tabs.SelectMany(t => t.Items).Any(i => i.Id.EndsWith(".trend")), Is.False, "no samples, no trend rows");
  var o = IndustrialPanelBuilder.Build(Model("active")).Tabs.Single(t => t.Id == "overview");
  foreach (var id in new[] { "performance", "production", "logistics", "employment", "efficiency", "activeFacilities" }) {
   var trend = Find(o, id + ".trend");
   Assert.That(trend, Is.Not.Null, id);
   int metric = o.Items.ToList().FindIndex(i => i.Id == id), row = o.Items.ToList().IndexOf(trend!);
   Assert.That(row, Is.GreaterThan(metric), id + " trend follows its metric");
   Assert.That(o.Items.Skip(metric + 1).Take(row - metric - 1).All(i => i.Id.StartsWith(id + ".")), Is.True, id + " trend is adjacent");
   Assert.That(trend!.TooltipKey, Is.EqualTo("CS2AS.Trend.Tip"));
  }
 }
 [Test] public void TrendDeltaUsesLatestAndPreviousSamplesWithoutPercentForCounts() {
  var m = Model("active");
  var trends = new IndustrialTrendSnapshot();
  trends.Performance.Record(10, 60); trends.Performance.Record(11, 64.25);
  trends.ProductiveFacilities.Record(10, 5); trends.ProductiveFacilities.Record(11, 3);
  trends.Production.Record(11, 80);
  trends.Employment.Record(10, 70); trends.Employment.Record(11, 70.01);
  m.Trends = trends;
  var o = IndustrialPanelBuilder.Build(m).Tabs.Single(t => t.Id == "overview");
  Assert.That(Find(o, "performance.trend")!.LabelKey, Is.EqualTo("CS2AS.Trend.Up")); Assert.That(Find(o, "performance.trend")!.Value, Is.EqualTo("+4.3"));
  Assert.That(Find(o, "activeFacilities.trend")!.LabelKey, Is.EqualTo("CS2AS.Trend.Down")); Assert.That(Find(o, "activeFacilities.trend")!.Value, Is.EqualTo("-2"));
  Assert.That(Find(o, "production.trend")!.LabelKey, Is.EqualTo("CS2AS.Trend.New"), "one sample is new, never a fabricated delta");
  Assert.That(Find(o, "production.trend")!.Value, Is.EqualTo("—"));
  Assert.That(Find(o, "employment.trend")!.LabelKey, Is.EqualTo("CS2AS.Trend.Flat"), "a change below display precision is flat");
  Assert.That(Find(o, "employment.trend")!.Value, Is.EqualTo("0.0"));
  Assert.That(o.Items.Where(i => i.Id.EndsWith(".trend")).All(i => i.Value == null || !i.Value.Contains('%')), Is.True);
 }
 // ---- Wave A4 processes
 private static IndustrialProcessSummary Process(Resource in1, Resource in2, Resource output, int companies, int measured, double actual) =>
  new() { Process = new IndustrialProcessKey((ulong)in1, in1 == Resource.NoResource ? 0 : 1, (ulong)in2, in2 == Resource.NoResource ? 0 : 1, (ulong)output, 1),
   Companies = companies, MeasuredCompanies = measured, ActualOutputPerDay = actual };
 [Test] public void ProductionTabShowsEachProcessAsLocalizedInputAndOutputRows() {
  var m = Model("active");
  m.Current.Processes = new List<IndustrialProcessSummary> { Process(Resource.NoResource, Resource.NoResource, Resource.Wood, 1, 1, 80),
   Process(Resource.Plastics, Resource.Wood, Resource.Furniture, 2, 2, 50) };
  var p = IndustrialPanelBuilder.Build(m).Tabs.Single(t => t.Id == "production");
  var rows = p.Items.Where(i => i.Id.StartsWith("process.")).ToList();
  Assert.That(rows.Select(i => i.Id), Is.EqualTo(new[] { "process.0.output", "process.0.actual", "process.0.companies",
   "process.1.input.1", "process.1.input.2", "process.1.output", "process.1.actual", "process.1.companies" }));
  Assert.That(rows[0].LabelKey, Is.EqualTo("Resources.TITLE[Wood]")); Assert.That(rows[0].ValueKey, Is.EqualTo("CS2AS.Industrial.Process.Output"));
  Assert.That(rows[1].LabelKey, Is.EqualTo("CS2AS.Industrial.Process.ActualPerDay")); Assert.That(rows[1].Value, Is.EqualTo("80 /d"));
  Assert.That(rows[2].LabelKey, Is.EqualTo("CS2AS.Industrial.Process.Companies")); Assert.That(rows[2].Value, Is.EqualTo("1"));
  Assert.That(rows[3].LabelKey, Is.EqualTo("Resources.TITLE[Plastics]")); Assert.That(rows[3].ValueKey, Is.EqualTo("CS2AS.Industrial.Process.Input"));
  Assert.That(rows[4].LabelKey, Is.EqualTo("Resources.TITLE[Wood]")); Assert.That(rows[4].ValueKey, Is.EqualTo("CS2AS.Industrial.Process.Input"));
  Assert.That(rows[5].LabelKey, Is.EqualTo("Resources.TITLE[Furniture]"));
  Assert.That(rows.All(i => i.TooltipKey == "CS2AS.Industrial.Process.Tip"), Is.True);
  Assert.That(rows.All(i => i.Value == null || !i.Value.Contains("Resources.TITLE")), Is.True, "no raw localization key in a sentence");
  int production = p.Items.ToList().FindIndex(i => i.Id == "production");
  Assert.That(p.Items.ToList().FindIndex(i => i.Id == "process.1.companies"), Is.LessThan(production), "processes precede the Production score");
 }
 [Test] public void PartialOrUnmeasuredProcessOutputIsShownHonestly() {
  var m = Model("active");
  m.Current.Processes = new List<IndustrialProcessSummary> { Process(Resource.Wood, Resource.NoResource, Resource.Paper, 3, 1, 20),
   Process(Resource.Grain, Resource.NoResource, Resource.Food, 2, 0, 0) };
  var p = IndustrialPanelBuilder.Build(m).Tabs.Single(t => t.Id == "production");
  AreaPanelItem Row(string id) => p.Items.Single(i => i.Id == id);
  Assert.That(Row("process.0.actual").Value, Is.EqualTo("20 /d"));
  Assert.That(Row("process.0.coverage").LabelKey, Is.EqualTo("CS2AS.Industrial.PartialData")); Assert.That(Row("process.0.coverage").Value, Is.EqualTo("1 / 3"));
  Assert.That(Row("process.1.actual").ValueKey, Is.EqualTo("CS2AS.Industrial.Status.Unavailable"), "no measured company: never a zero rate");
 }
 [Test] public void StaleProcessRowsKeepTheStaleContext() {
  var m = Model("paused");
  m.Current.Processes = new List<IndustrialProcessSummary> { Process(Resource.Wood, Resource.NoResource, Resource.Paper, 1, 1, 20) };
  var p = IndustrialPanelBuilder.Build(m).Tabs.Single(t => t.Id == "production");
  var actual = p.Items.Single(i => i.Id == "process.0.actual");
  Assert.That(actual.Kind, Is.EqualTo(AreaPanelItemKind.Row));
  Assert.That(actual.TooltipKey, Is.EqualTo("CS2AS.Industrial.Status.Stale"));
  Assert.That(IndustrialPanelBuilder.Build(Model("suspended")).Tabs.SelectMany(t => t.Items).Any(i => i.Id.StartsWith("process.")), Is.False, "no observation, no process rows");
 }
 // ---- Wave A3 bottleneck focus
 [Test] public void BottleneckFocusButtonFollowsOnlyTargetBearingBottlenecks() {
  var m = Model("overlapping");
  Assert.That(m.Current.AllBottlenecks.Count(b => b.Target != Entity.Null), Is.GreaterThanOrEqualTo(2), "fixture has targets");
  var untargeted = m.Current.AllBottlenecks.First(b => b.Key != m.Current.Bottlenecks[0].Key);
  untargeted.Target = Entity.Null;
  var d = IndustrialPanelBuilder.Build(m);
  foreach (var tab in d.Tabs) {
   var items = tab.Items.ToList();
   for (int i = 0; i < items.Count; i++) {
    if (items[i].Kind != AreaPanelItemKind.Warning || !items[i].Id.StartsWith("bottleneck.")) continue;
    var key = items[i].Id.Substring("bottleneck.".Length);
    var bottleneck = m.Current.AllBottlenecks.Single(b => b.Key == key);
    Assert.That(items[i + 1].Id, Is.EqualTo("suggestion." + key));
    if (bottleneck.Target == Entity.Null) { Assert.That(i + 2 >= items.Count || items[i + 2].Kind != AreaPanelItemKind.Button, Is.True, key + " has no target"); continue; }
    var button = items[i + 2];
    Assert.That(button.Kind, Is.EqualTo(AreaPanelItemKind.Button), key);
    Assert.That(button.Id, Is.EqualTo("focus." + key));
    Assert.That(button.Action, Is.EqualTo("focusDiagnosticTarget"));
    Assert.That(button.Value, Is.EqualTo(AreaDiagnosticTargetKey.Format(bottleneck.Target)));
    Assert.That(button.LabelKey, Is.EqualTo("CS2AS.Industrial.FocusTarget"));
    Assert.That(button.TooltipKey, Is.EqualTo("CS2AS.Industrial.FocusTarget.Tip"));
   }
  }
  Assert.That(d.Tabs.SelectMany(t => t.Items).Any(i => i.Id == "focus." + untargeted.Key), Is.False);
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryKeyTheIndustrialPanelCanEmitIsLocalized(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  var keys = new HashSet<string>(IndustrialPanelBuilder.AllKeys());
  foreach (var kind in Kinds)
   foreach (var tab in IndustrialPanelBuilder.Build(Model(kind)).Tabs) {
    keys.Add(tab.LabelKey);
    foreach (var i in tab.Items) { keys.Add(i.LabelKey); if (i.ValueKey != null) keys.Add(i.ValueKey); if (i.TooltipKey != null) keys.Add(i.TooltipKey); }
   }
  // Resource titles are vanilla localization (Resources.TITLE[...]), provided by the game.
  foreach (var key in keys.Where(k => !k.StartsWith("Resources.TITLE["))) Assert.That(entries.ContainsKey(key), Is.True, locale + " is missing " + key);
 }
 [Test] public void PanelCacheRebuildsForAnotherAreaOrAfterTheInterval() {
  long now = 0; int builds = 0;
  var cache = new IndustrialPanelCache(250, () => now);
  AreaPanelDocument Build() { builds++; return new AreaPanelDocument(); }
  var first = cache.Get(E(1), Build);
  now = 100; Assert.That(cache.Get(E(1), Build), Is.SameAs(first));
  Assert.That(cache.Get(E(2), Build), Is.Not.SameAs(first));
  now = 400; cache.Get(E(2), Build); Assert.That(builds, Is.EqualTo(3));
 }
 private sealed class NoStore : IAreaAnchorStore {
  public readonly Dictionary<Entity, IReadOnlyList<AreaAnchorLink>> Links = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Links.TryGetValue(area, out var l) ? l : Array.Empty<AreaAnchorLink>();
  public void Write(Entity area, IReadOnlyList<AreaAnchorLink> links) => Links[area] = links.ToArray();
 }
}
