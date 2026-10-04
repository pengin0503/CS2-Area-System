using System;
using System.Collections.Generic;
using System.Globalization;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Diagnostics;
using CS2AreaSystem.Core.Modules;
using Game.Economy;
namespace CS2AreaSystem.Modules.Industrial
{
    public enum IndustrialHeadquartersState : byte { Missing, Valid, Invalid }
    // Read model for one selected Industrial Area, assembled only while the panel is built.
    public sealed class IndustrialPanelModel
    {
        public ulong StableId;
        public AreaLifecycleState Lifecycle;
        public int Level = 1;
        public long Experience;
        public IndustrialAreaState State;
        public bool HasState;
        public IndustrialHeadquartersState Headquarters;
        public IndustrialObservationResult Current;
        public bool Paused;
        public bool HasXp;
        public IndustrialXpResult Xp;
        public IndustrialDayResult? LastCompletedDay;
        public IndustrialPromotionResult Promotion;
        public double NowHours;
        // Runtime-only trend samples of this Area (null when no runtime snapshot exists).
        public IndustrialTrendSnapshot Trends;
        public IReadOnlyList<IndustrialComparisonRow> Comparison=Array.Empty<IndustrialComparisonRow>();
        public IReadOnlyList<IndustrialCompanyCandidate> Companies = Array.Empty<IndustrialCompanyCandidate>();
        // Phase 2 C4: the support module is registered and enabled; the Area's support state; the outstanding request stage.
        public bool SupportAvailable, HasSupport;
        public IndustrialSupportState Support;
        public IndustrialSupportStage? SupportPending;
        // Phase 3 D1: the contract module is registered and this Area has contract storage; saved contracts (bounded) with
        // their advisory evaluation; the runtime draft and the currently offered destinations/resources.
        public bool ContractsAvailable;
        public IReadOnlyList<IndustrialSupplyContractRow> ContractRows = Array.Empty<IndustrialSupplyContractRow>();
        public IndustrialSupplyContractDraft ContractDraft = new IndustrialSupplyContractDraft();
        public IReadOnlyList<AreaPanelOption> ContractDestinations = Array.Empty<AreaPanelOption>(), ContractResources = Array.Empty<AreaPanelOption>();
        // More live destinations exist than the bounded select can offer.
        public bool ContractDestinationsTruncated;
    }
    // One saved contract as shown: DestinationName is null when the destination is not a live Industrial Area.
    public sealed class IndustrialSupplyContractRow
    {
        public IndustrialSupplyContract Contract;
        public string DestinationName;
        public IndustrialSupplyContractEvaluation Evaluation;
    }
    // Builds the seven Industrial tabs (design §16) through the module-neutral panel contract. Pure and read-only: the only
    // item with an action is the diagnostic focus button (camera only); unavailable data is never rendered as zero.
    public static class IndustrialPanelBuilder
    {
        public const string P = "CS2AS.Industrial.";
        public static readonly string[] Tabs = { "overview", "production", "logistics", "employment", "progress", "comparison", "companies", "support", "contracts" };
        public static readonly string[] Components = { "Production", "Logistics", "Employment", "Efficiency" };
        public static readonly string[] Statuses = { "Unavailable", "NotApplicable", "WarmingUp", "Stale" };
        public static readonly string[] XpReasons = { "Suspended", "Stale", "NoOperatingProducer", "InsufficientInputs", "DailyCap", "AlreadySettled" };
        public static readonly string[] DayReasons = { "Qualified", "InsufficientCoverage", "LowAverage" };
        public static readonly string[] Checks = { "Experience", "Performance", "ActiveFacilities", "Employees", "Logistics", "Production", "LogisticsRoute",
            "PrestigeLogistics", "PrestigeEmployment", "PrestigeEfficiency", "PrestigeOutputs", "PrestigeStreak" };
        public static readonly string[] Warnings = { "HeadquartersRequired", "HeadquartersInvalid", "Paused", "Delayed", "Observing", "ReadFailures" };
        public static readonly string[] Notes = { "Sampled", "NoPlacementBonus", "ExternalCargo", "OfficesExcluded", "NoBuff", "Prestige", "HqHealth", "Overlap" };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static AreaPanelDocument Build(IndustrialPanelModel model)
        {
            return new AreaPanelDocument { ShowsProgress = true, Tabs = new[] {
                new AreaPanelTabData { Id = "overview", LabelKey = "CS2AS.Tab.Overview", Items = Overview(model) },
                new AreaPanelTabData { Id = "production", LabelKey = "CS2AS.Tab.Production", Items = Production(model) },
                new AreaPanelTabData { Id = "logistics", LabelKey = "CS2AS.Tab.Logistics", Items = Logistics(model) },
                new AreaPanelTabData { Id = "employment", LabelKey = "CS2AS.Tab.Employment", Items = Employment(model) },
                new AreaPanelTabData { Id = "progress", LabelKey = "CS2AS.Tab.Progress", Items = Progress(model) },
                new AreaPanelTabData { Id = "comparison", LabelKey = "CS2AS.Tab.Comparison", Items = Comparison(model) },
                new AreaPanelTabData { Id = "companies", LabelKey = "CS2AS.Tab.Companies", Items = Companies(model) },
                new AreaPanelTabData { Id = "support", LabelKey = "CS2AS.Tab.Support", Items = Support(model) },
                new AreaPanelTabData { Id = "contracts", LabelKey = "CS2AS.Tab.Contracts", Items = Contracts(model) } } };
        }
        public static readonly string[] ContractKeys = { "Tip", "Unavailable", "Destination", "Resource", "Target", "Target.Tip", "Create", "Cap", "Empty", "Status", "Status.Tip",
            "SourceCapacity", "DestinationNeed", "Potential", "Potential.Tip", "Disable", "Enable", "Remove", "DestinationMissing", "DestinationsTruncated" };
        // Phase 3 D1: advisory supply contracts. Configuration plus estimates from both Areas' existing observations only;
        // nothing here moves goods. Unknown estimates are never shown as 0 and partial ones are shown as lower bounds.
        private static List<AreaPanelItem> Contracts(IndustrialPanelModel m)
        {
            var items = new List<AreaPanelItem> { new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "contracts.tip", LabelKey = P + "Contracts.Tip" } };
            if (!m.ContractsAvailable) { items.Add(Warning("contracts.unavailable", P + "Contracts.Unavailable")); return items; }
            var draft = m.ContractDraft ?? new IndustrialSupplyContractDraft();
            var destinations = m.ContractDestinations ?? Array.Empty<AreaPanelOption>();
            var resources = m.ContractResources ?? Array.Empty<AreaPanelOption>();
            var rows = m.ContractRows ?? Array.Empty<IndustrialSupplyContractRow>();
            string destination = draft.Destination == 0 ? null : draft.Destination.ToString(Invariant), resource = draft.Resource == 0 ? null : draft.Resource.ToString(Invariant);
            items.Add(Select("contracts.draft.destination", P + "Contracts.Destination", IndustrialSupplyContractActions.DraftDestination, destination, destinations));
            if (m.ContractDestinationsTruncated) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "contracts.destinations.truncated", LabelKey = P + "Contracts.DestinationsTruncated" });
            items.Add(Select("contracts.draft.resource", P + "Contracts.Resource", IndustrialSupplyContractActions.DraftResource, resource, resources));
            items.Add(TargetSlider("contracts.draft.target", IndustrialSupplyContractActions.DraftTarget, draft.TargetPerDay));
            bool existing = false;
            foreach (var row in rows) if (row?.Contract.DestinationStableId == draft.Destination && row.Contract.Resource == draft.Resource) { existing = true; break; }
            bool full = rows.Count >= IndustrialSupplyContractModel.MaxContracts;
            bool offered = destination != null && resource != null && Offered(destinations, destination) && Offered(resources, resource);
            items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = "contracts.create", LabelKey = P + "Contracts.Create", Action = IndustrialSupplyContractActions.Create,
                Disabled = !offered || (full && !existing) });
            if (full) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "contracts.cap", LabelKey = P + "Contracts.Cap" });
            int shown = 0;
            var ids = new HashSet<int>();
            foreach (var row in rows)
            {
                // A corrupted duplicate id is shown once; actions address the first match, so removing it reveals the next.
                if (row == null || shown >= IndustrialSupplyContractModel.MaxContracts || !ids.Add(row.Contract.ContractId)) continue;
                shown++;
                var c = row.Contract;
                string id = "contract." + c.ContractId.ToString(Invariant) + ".", contractId = c.ContractId.ToString(Invariant);
                var status = row.Evaluation?.Status ?? IndustrialSupplyContractStatus.Unknown;
                string name = ResourceName(c.Resource);
                items.Add(Row(id + "resource", name == null ? P + "Contracts.Resource" : "Resources.TITLE[" + name + "]", row.DestinationName,
                    row.DestinationName == null ? P + "Contracts.DestinationMissing" : null));
                items.Add(Row(id + "status", P + "Contracts.Status", valueKey: P + "Contracts.Status." + status, tooltip: P + "Contracts.Status.Tip"));
                if (status != IndustrialSupplyContractStatus.Invalid && status != IndustrialSupplyContractStatus.Disabled)
                {
                    bool partial = status == IndustrialSupplyContractStatus.PartialData, stale = row.Evaluation?.Stale == true;
                    items.Add(Estimate(id + "capacity", P + "Contracts.SourceCapacity", row.Evaluation?.SourceCapacityPerDay, partial, stale, null));
                    items.Add(Estimate(id + "need", P + "Contracts.DestinationNeed", row.Evaluation?.DestinationNeedPerDay, partial, stale, null));
                    items.Add(Estimate(id + "potential", P + "Contracts.Potential", row.Evaluation?.PotentialPerDay, partial, stale, P + "Contracts.Potential.Tip"));
                }
                items.Add(TargetSlider(id + "target", IndustrialSupplyContractActions.TargetPrefix + contractId, c.TargetPerDay));
                items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = id + "toggle", LabelKey = P + (c.Enabled ? "Contracts.Disable" : "Contracts.Enable"),
                    Action = IndustrialSupplyContractActions.Toggle, Value = contractId });
                // Always removable, including contracts whose destination was deleted.
                items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = id + "remove", LabelKey = P + "Contracts.Remove", Action = IndustrialSupplyContractActions.Remove, Value = contractId });
            }
            if (shown == 0) items.Add(Row("contracts.empty", P + "Contracts.Empty"));
            return items;
        }
        private static AreaPanelItem Select(string id, string label, string action, string value, IReadOnlyList<AreaPanelOption> options) =>
            new AreaPanelItem { Kind = AreaPanelItemKind.Select, Id = id, LabelKey = label, Action = action, Value = value, Options = options, Disabled = options.Count == 0 };
        private static AreaPanelItem TargetSlider(string id, string action, int target)
        {
            int value = IndustrialSupplyContractModel.NormalizeTarget(target);
            return new AreaPanelItem { Kind = AreaPanelItemKind.Slider, Id = id, LabelKey = P + "Contracts.Target", Action = action, NumericValue = value,
                Min = IndustrialSupplyContractModel.MinTargetPerDay, Max = IndustrialSupplyContractModel.MaxTargetPerDay, Step = IndustrialSupplyContractModel.TargetStep,
                Value = Number(value) + " /d", TooltipKey = P + "Contracts.Target.Tip" };
        }
        private static AreaPanelItem Estimate(string id, string label, double? value, bool partial, bool stale, string tooltip) =>
            value.HasValue ? Row(id, label, (partial ? "≥" : "") + Number(value.Value) + " /d", tooltip: partial ? P + "PartialData" : stale ? P + "Status.Stale" : tooltip)
                : Row(id, label, valueKey: P + "Status.Unavailable", tooltip: tooltip);
        private static bool Offered(IReadOnlyList<AreaPanelOption> options, string value)
        {
            foreach (var o in options) if (o != null && o.Value == value) return true;
            return false;
        }
        public static readonly string[] SupportStages = { "None", "Requested", "Settled", "Crediting" };
        // Phase 2 C4: the per-company daily support setting and its auditable accounting. The slider exists only while the
        // support module is registered, so an unsupported request is never silently accepted.
        private static List<AreaPanelItem> Support(IndustrialPanelModel m)
        {
            var items = new List<AreaPanelItem> { new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "support.tip", LabelKey = P + "Support.Tip" } };
            if (!m.SupportAvailable || !m.HasSupport) { items.Add(Warning("support.unavailable", P + "Support.Unavailable")); return items; }
            var s = m.Support;
            int setting = IndustrialSupportModel.NormalizeSetting(s.DailySupportPerCompany);
            items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Slider, Id = "support.perCompany", LabelKey = P + "Support.PerCompany", Action = IndustrialGameplayFacade.SetSupportPerCompany,
                NumericValue = setting, Min = IndustrialSupportModel.MinPerCompany, Max = IndustrialSupportModel.MaxPerCompany, Step = IndustrialSupportModel.Step,
                Value = setting > 0 ? Money(setting) : null, ValueKey = setting > 0 ? null : P + "Support.Off", TooltipKey = P + "Support.PerCompany.Tip" });
            var r = m.Current;
            if (r == null) items.Add(Row("support.receivers", P + "Support.Receivers", valueKey: P + "Calculating"));
            else if (!r.SupportRosterComplete) items.Add(Row("support.receivers", P + "Support.Receivers", valueKey: P + "Status.Unavailable", tooltip: P + "Warning.ReadFailures"));
            else
            {
                items.Add(Row("support.receivers", P + "Support.Receivers", Number(r.SupportRoster.Count), tooltip: Stale(m) ? P + "Status.Stale" : P + "Support.Receivers.Tip"));
                items.Add(Row("support.dailyRequest", P + "Support.DailyRequest", Money(IndustrialSupportModel.DailyRequest(r.SupportRoster.Count, setting)), tooltip: P + "Support.DailyRequest.Tip"));
            }
            items.Add(Row("support.pending", P + "Support.Pending", valueKey: P + "Support.Stage." + (m.SupportPending?.ToString() ?? "None")));
            items.Add(Row("support.lifetimeRequested", P + "Support.LifetimeRequested", Money(s.LifetimeRequested)));
            items.Add(Row("support.lifetimeSettled", P + "Support.LifetimeSettled", Money(s.LifetimeSettled), tooltip: P + "Support.LifetimeSettled.Tip"));
            items.Add(Row("support.lifetimeCredited", P + "Support.LifetimeCredited", Money(s.LifetimeCredited)));
            items.Add(Row("support.lifetimeUnallocated", P + "Support.LifetimeUnallocated", Money(s.LifetimeUnallocated), tooltip: P + "Support.LifetimeUnallocated.Tip"));
            return items;
        }
        private static List<AreaPanelItem> Companies(IndustrialPanelModel model)
        {
            var items = new List<AreaPanelItem> { new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "company.tip", LabelKey = P + "Company.Tip" } };
            int count = 0;
            foreach (var company in model.Companies)
            {
                if (count++ >= IndustrialCompanyCandidateSet.Capacity) break;
                string id = "company." + company.Key;
                items.Add(Row(id + ".name", P + "Company.Name", company.Label, company.Label == null ? P + "Company.Unnamed" : null));
                items.Add(Row(id + ".status", P + "Company.Ownership", valueKey: P + "Company." + (company.Invalid ? "Invalid" : company.Manual ? "Manual" : "Automatic")));
                items.Add(Row(id + ".owner", P + "Company.Owner", company.Owner == 0 ? null : company.Owner.ToString(Invariant), company.Owner == 0 ? P + "Status.Unavailable" : null));
                items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = id + ".assign", LabelKey = P + "Company.Assign",
                    Action = IndustrialGameplayFacade.AssignCompanyHere, Value = company.Key, Disabled = !company.CanAssign(model.StableId) });
                if (company.HasClaimHere) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = id + ".clear", LabelKey = P + "Company.Clear",
                    Action = IndustrialGameplayFacade.ClearCompanyAssignment, Value = company.Key });
            }
            if (count == 0) items.Add(Row("company.empty", P + "Company.Empty"));
            return items;
        }
        private static List<AreaPanelItem> Comparison(IndustrialPanelModel model)
        {
            var items=new List<AreaPanelItem>{new AreaPanelItem{Kind=AreaPanelItemKind.Note,Id="comparison.note",LabelKey=P+"Comparison.Tip"}};
            foreach(var area in model.Comparison) {
                string prefix="comparison."+area.StableId.ToString(Invariant)+".";
                items.Add(Row(prefix+"name",P+"Comparison.Area",area.Name??area.StableId.ToString(Invariant)));
                if(area.Stale)items.Add(Warning(prefix+"stale",P+"Status.Stale"));
                void Add(string name,IndustrialMetric metric) {
                    bool value=metric.HasValue||metric.Status==IndustrialMetricStatus.Stale;
                    items.Add(Row(prefix+name,P+"Metric."+name,value?One(metric.Value):null,value?null:P+"Status."+metric.Status,area.Stale?P+"Status.Stale":metric.Status==IndustrialMetricStatus.Partial?P+"PartialData":null));
                }
                Add("Production",area.Production);Add("Logistics",area.Logistics);Add("Employment",area.Employment);Add("Efficiency",area.Efficiency);Add("Performance",area.Performance);
                items.Add(Row(prefix+"trend",P+"Comparison.Trend",area.PerformanceDelta.HasValue?(area.PerformanceDelta.Value>0?"+":"")+One(area.PerformanceDelta.Value):null,area.PerformanceDelta.HasValue?null:P+"Status.Unavailable"));
            }
            if(model.Comparison.Count==0)items.Add(Row("comparison.empty",P+"Comparison.Empty"));
            return items;
        }
        // Every module key the builder can emit (resource titles are vanilla "Resources.TITLE[...]" keys).
        public static IEnumerable<string> AllKeys()
        {
            foreach (var tab in new[] { "Production", "Logistics", "Employment", "Progress" }) yield return "CS2AS.Tab." + tab;
            yield return "CS2AS.Tab.Comparison";
            yield return "CS2AS.Tab.Companies";
            yield return "CS2AS.Tab.Support";
            foreach (var key in new[] { "Tip", "Unavailable", "PerCompany", "PerCompany.Tip", "Off", "Receivers", "Receivers.Tip", "DailyRequest", "DailyRequest.Tip", "Pending",
                "LifetimeRequested", "LifetimeSettled", "LifetimeSettled.Tip", "LifetimeCredited", "LifetimeUnallocated", "LifetimeUnallocated.Tip" }) yield return P + "Support." + key;
            foreach (var stage in SupportStages) yield return P + "Support.Stage." + stage;
            yield return "CS2AS.Tab.Contracts";
            foreach (var key in ContractKeys) yield return P + "Contracts." + key;
            foreach (var status in Enum.GetNames(typeof(IndustrialSupplyContractStatus))) yield return P + "Contracts.Status." + status;
            foreach (var key in new[] { "Tip", "Name", "Unnamed", "Ownership", "Owner", "Automatic", "Manual", "Invalid", "Assign", "Clear", "Empty" }) yield return P + "Company." + key;
            foreach(var key in new[]{"Tip","Area","Trend","Empty"})yield return P+"Comparison."+key;
            for (int level = IndustrialProgressionModel.MinLevel; level <= IndustrialProgressionModel.MaxLevel; level++) yield return P + "LevelName." + level;
            foreach (var c in Components) { yield return P + "Metric." + c; yield return P + "Metric." + c + ".Tip"; }
            foreach (var m in new[] { "Performance", "Activity", "ActiveShare", "Diversity", "InputContinuity", "OutputContinuity", "JobFill", "StaffingBalance", "BuildingEfficiency", "Profitability" })
            { yield return P + "Metric." + m; yield return P + "Metric." + m + ".Tip"; }
            foreach (var s in Statuses) yield return P + "Status." + s;
            foreach (var r in XpReasons) yield return P + "XpBlocked." + r;
            foreach (var r in DayReasons) yield return P + "Day." + r;
            foreach (var c in Checks) yield return P + "Check." + c;
            foreach (var w in Warnings) yield return P + "Warning." + w;
            foreach (var n in Notes) yield return P + "Note." + n;
            foreach (var b in IndustrialPerformanceModel.BottleneckKeys) { yield return P + "Bottleneck." + b; yield return P + "Bottleneck." + b + ".Tip"; yield return P + "Suggestion." + b; }
            for (int i = 0; i < 5; i++) yield return P + "Vacancy." + i;
            foreach (var k in new[] { "Level", "Experience", "Calculating", "PartialData", "ObservedHours", "ActiveFacilities", "EmployeesJobs", "LastUpdate", "LastUpdate.Tip",
                "OverlapMembers", "Extractors", "Processors", "Producers", "RawActivity", "RawActivity.Tip", "MeaningfulOutputs", "StorageFacilities", "StorageUtilization",
                "CargoFacilities", "Jobs", "Employees", "Vacancies", "ProducerJobs", "SupportJobs", "NextLevel", "MaxLevel", "HourlyXp", "HourlyXp.Tip", "DailyXp", "DailyXp.Tip",
                "StreakDays", "DayAverage", "DayHours", "LastDay", "LastDay.Tip", "Headquarters", "Hq.Valid", "Hq.Invalid", "Hq.Missing", "Resource.Tip", "Bottlenecks.None",
                "Summary.Active", "Summary.HeadquartersRequired", "Summary.HeadquartersInvalid",
                "Process.Input", "Process.Output", "Process.Companies", "Process.ActualPerDay", "Process.Tip", "FocusTarget", "FocusTarget.Tip" })
                yield return P + k;
            foreach (var t in RuntimeTrendPresentation.Keys) yield return t;
        }
        // ---- formatting
        private static string Number(double value) => Math.Round(value).ToString("N0", Invariant);
        private static string One(double value) => value.ToString("0.0", Invariant);
        private static string Hours(double value) => value.ToString("0.0", Invariant) + " h";
        private static string Percent(double value) => Math.Round(value).ToString("N0", Invariant) + "%";
        private static string Money(long value) => (value < 0 ? "-¢" : "¢") + Math.Abs(value).ToString("N0", Invariant);
        private static string Clock(double hours)
        {
            double h = Math.Max(0, hours);
            long day = (long)Math.Floor(h / 24.0);
            int minutes = Math.Min(24 * 60 - 1, (int)Math.Round((h - day * 24.0) * 60.0));
            return day.ToString(Invariant) + ", " + (minutes / 60).ToString("00", Invariant) + ":" + (minutes % 60).ToString("00", Invariant);
        }
        private static AreaPanelItem Row(string id, string label, string value = null, string valueKey = null, string tooltip = null) =>
            new AreaPanelItem { Kind = AreaPanelItemKind.Row, Id = id, LabelKey = label, Value = value, ValueKey = valueKey, TooltipKey = tooltip };
        private static AreaPanelItem Bar(string id, string label, double value, string text, string tooltip = null) =>
            new AreaPanelItem { Kind = AreaPanelItemKind.Bar, Id = id, LabelKey = label, NumericValue = Math.Round(IndustrialMetric.Clamp(value), 1), Min = 0, Max = 100, Value = text, TooltipKey = tooltip };
        private static AreaPanelItem Note(string id, string key) => new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = id, LabelKey = P + "Note." + key };
        private static AreaPanelItem Warning(string id, string key, string value = null, string tooltip = null) =>
            new AreaPanelItem { Kind = AreaPanelItemKind.Warning, Id = id, LabelKey = key, Value = value, TooltipKey = tooltip };
        // Runtime trend row directly after its headline metric, only once a sample exists (Wave A1).
        private static void Trend(List<AreaPanelItem> items, IndustrialPanelModel m, string id, Func<IndustrialTrendSnapshot, RuntimeTrendSeries> series, Func<double, string> magnitude = null)
        { if (m.Trends != null) RuntimeTrendPresentation.Add(items, id, series(m.Trends), magnitude ?? One); }
        private static bool Stale(IndustrialPanelModel m) => m.Current != null && (m.Paused || m.Current.Delayed || m.Lifecycle != AreaLifecycleState.Active);
        // A metric: bar when it has a value, otherwise its reason; partial coverage and warm-up progress are shown.
        // Stale values are shown as plain rows (never as a current score).
        private static void Metric(List<AreaPanelItem> items, IndustrialPanelModel m, string id, string name, IndustrialMetric metric)
        {
            string label = P + "Metric." + name, tip = label + ".Tip";
            if (m.Current == null) { items.Add(Row(id, label, valueKey: P + "Calculating", tooltip: tip)); return; }
            if (metric.HasValue && Stale(m)) { items.Add(Row(id, label, One(metric.Value), tooltip: P + "Status.Stale")); return; }
            if (metric.HasValue)
            {
                items.Add(Bar(id, label, metric.Value, One(metric.Value), tip));
                if (metric.Status == IndustrialMetricStatus.Partial) items.Add(Row(id + ".coverage", P + "PartialData", Number(metric.Observed) + " / " + Number(metric.Eligible)));
                return;
            }
            items.Add(Row(id, label, valueKey: P + "Status." + (metric.Status == IndustrialMetricStatus.Available || metric.Status == IndustrialMetricStatus.Partial ? "Unavailable" : metric.Status.ToString()), tooltip: tip));
            if (metric.Status == IndustrialMetricStatus.WarmingUp) items.Add(Row(id + ".coverage", P + "ObservedHours", One(metric.Observed) + " / " + Hours(metric.Eligible)));
        }
        private static AreaPanelItem ExperienceBar(IndustrialPanelModel m)
        {
            var target = IndustrialProgressionModel.GetTarget(m.Level);
            if (target == null) return Bar("experience", P + "Experience", 100, Number(m.Experience));
            return Bar("experience", P + "Experience", 100.0 * m.Experience / target.Experience, Number(m.Experience) + " / " + Number(target.Experience));
        }
        private static void StateWarnings(List<AreaPanelItem> items, IndustrialPanelModel m)
        {
            if (m.Headquarters == IndustrialHeadquartersState.Missing && m.Lifecycle != AreaLifecycleState.Active) items.Add(Warning("warning.hq", P + "Warning.HeadquartersRequired"));
            else if (m.Headquarters == IndustrialHeadquartersState.Invalid && m.Lifecycle != AreaLifecycleState.Active) items.Add(Warning("warning.hq", P + "Warning.HeadquartersInvalid"));
            if (m.Current != null && (m.Paused || m.Lifecycle != AreaLifecycleState.Active)) items.Add(Warning("warning.paused", P + "Warning.Paused"));
            else if (m.Current != null && m.Current.Delayed) items.Add(Warning("warning.delayed", P + "Warning.Delayed", Hours(m.Current.EndHours - m.Current.StartHours)));
            if (m.Current == null && m.Lifecycle == AreaLifecycleState.Active) items.Add(Warning("warning.observing", P + "Warning.Observing"));
            if (m.Current != null && m.Current.FailedReads > 0) items.Add(Warning("warning.readFailures", P + "Warning.ReadFailures", Number(m.Current.FailedReads)));
        }
        private static void Bottlenecks(List<AreaPanelItem> items, IReadOnlyList<IndustrialBottleneck> list, Func<string, bool> include)
        {
            if (list == null) return;
            foreach (var b in list)
            {
                if (!include(b.Key)) continue;
                items.Add(Warning("bottleneck." + b.Key, P + "Bottleneck." + b.Key,
                    "(" + Percent(b.EvidenceValue) + " · ×" + Number(b.AffectedFacilities) + " · " + Hours(b.ObservedHours) + ")", P + "Bottleneck." + b.Key + ".Tip"));
                items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "suggestion." + b.Key, LabelKey = P + "Suggestion." + b.Key });
                // Read-only camera focus on the representative facility; the key is re-validated when clicked.
                if (b.Target != Unity.Entities.Entity.Null)
                    items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Button, Id = "focus." + b.Key, LabelKey = P + "FocusTarget", TooltipKey = P + "FocusTarget.Tip",
                        Action = IndustrialGameplayFacade.FocusDiagnosticTarget, Value = AreaDiagnosticTargetKey.Format(b.Target) });
            }
        }
        // ---- tabs
        private static List<AreaPanelItem> Overview(IndustrialPanelModel m)
        {
            var r = m.Current;
            var items = new List<AreaPanelItem> { Row("level", P + "Level", valueKey: P + "LevelName." + m.Level), ExperienceBar(m),
                Row("headquarters", P + "Headquarters", valueKey: P + "Hq." + m.Headquarters) };
            StateWarnings(items, m);
            Metric(items, m, "performance", "Performance", r?.Performance ?? default);
            Trend(items, m, "performance", t => t.Performance);
            foreach (var c in Components)
            {
                Metric(items, m, c.ToLowerInvariant(), c, r == null ? default : c == "Production" ? r.Production : c == "Logistics" ? r.Logistics : c == "Employment" ? r.Employment : r.Efficiency);
                Trend(items, m, c.ToLowerInvariant(), t => c == "Production" ? t.Production : c == "Logistics" ? t.Logistics : c == "Employment" ? t.Employment : t.Efficiency);
            }
            if (r != null)
            {
                items.Add(Row("activeFacilities", P + "ActiveFacilities", Number(r.OperatingProducers) + " / " + Number(r.ProducerFacilities)));
                Trend(items, m, "activeFacilities", t => t.ProductiveFacilities, Number);
                items.Add(Row("employees", P + "EmployeesJobs", Number(r.Employees) + " / " + Number(r.Jobs)));
                if (r.Bottlenecks.Count == 0 && !Stale(m)) items.Add(Row("bottlenecks", P + "Bottlenecks.None", "—"));
                Bottlenecks(items, r.Bottlenecks, key => true);
                if (r.NonPrimaryMembers > 0) items.Add(Row("overlap", P + "OverlapMembers", Number(r.NonPrimaryMembers), tooltip: P + "Note.Overlap"));
                items.Add(Row("lastUpdate", P + "LastUpdate", Clock(r.EndHours), tooltip: P + "LastUpdate.Tip"));
            }
            if (m.Headquarters == IndustrialHeadquartersState.Valid) items.Add(Note("hqHealth", "HqHealth"));
            return items;
        }
        private static List<AreaPanelItem> Production(IndustrialPanelModel m)
        {
            var r = m.Current; var items = new List<AreaPanelItem>();
            if (r == null) { Metric(items, m, "production", "Production", default); return items; }
            items.Add(Row("extractors", P + "Extractors", Number(r.Extractors)));
            items.Add(Row("processors", P + "Processors", Number(r.Processors)));
            items.Add(Row("producers", P + "Producers", Number(r.OperatingProducers) + " / " + Number(r.ProducerFacilities)));
            Metric(items, m, "activeShare", "ActiveShare", r.ActiveShare);
            Metric(items, m, "activity", "Activity", r.Activity);
            if (r.RawActivity.HasValue) items.Add(Row("rawActivity", P + "RawActivity", One(r.RawActivity.Value) + "%", tooltip: P + "RawActivity.Tip"));
            Metric(items, m, "diversity", "Diversity", r.Diversity);
            items.Add(Row("meaningfulOutputs", P + "MeaningfulOutputs", Number(r.MeaningfulOutputs) + " / " + Number(r.RollingMeaningfulOutputs)));
            int shown = 0;
            foreach (var resource in r.Resources)
            {
                if (shown++ >= 5) break;
                var name = Enum.GetName(typeof(Resource), (Resource)resource.Resource);
                if (name == null) continue;
                items.Add(Row("resource." + name, "Resources.TITLE[" + name + "]", Number(resource.ActualPerDay) + " /d", tooltip: P + "Resource.Tip"));
            }
            Processes(items, m);
            Metric(items, m, "production", "Production", r.Production);
            items.Add(Note("sampled", "Sampled"));
            return items;
        }
        public const int MaxProcesses = 5;
        // Observed processes (Wave A4): per recipe, localized resource-title rows for its inputs and output, then its actual
        // daily output and company count. A stale result keeps the stale context; an unmeasured rate is never shown as 0.
        private static void Processes(List<AreaPanelItem> items, IndustrialPanelModel m)
        {
            var processes = m.Current?.Processes;
            if (processes == null) return;
            string tip = P + "Process.Tip";
            int shown = 0;
            foreach (var p in processes)
            {
                if (shown >= MaxProcesses) break;
                string output = ResourceName(p.Process.Output), input1 = ResourceName(p.Process.Input1), input2 = ResourceName(p.Process.Input2);
                if (output == null || (p.Process.Input1 != 0 && input1 == null) || (p.Process.Input2 != 0 && input2 == null)) continue;
                string id = "process." + shown++ + ".";
                int n = 0;
                foreach (var input in new[] { input1, input2 })
                    if (input != null) items.Add(Row(id + "input." + ++n, "Resources.TITLE[" + input + "]", valueKey: P + "Process.Input", tooltip: tip));
                items.Add(Row(id + "output", "Resources.TITLE[" + output + "]", valueKey: P + "Process.Output", tooltip: tip));
                items.Add(p.MeasuredCompanies > 0
                    ? Row(id + "actual", P + "Process.ActualPerDay", Number(p.ActualOutputPerDay) + " /d", tooltip: Stale(m) ? P + "Status.Stale" : tip)
                    : Row(id + "actual", P + "Process.ActualPerDay", valueKey: P + "Status.Unavailable", tooltip: tip));
                if (p.MeasuredCompanies > 0 && p.MeasuredCompanies < p.Companies)
                    items.Add(Row(id + "coverage", P + "PartialData", Number(p.MeasuredCompanies) + " / " + Number(p.Companies)));
                items.Add(Row(id + "companies", P + "Process.Companies", Number(p.Companies), tooltip: tip));
            }
        }
        private static string ResourceName(ulong resource) => resource == 0 ? null : Enum.GetName(typeof(Resource), (Resource)resource);
        private static List<AreaPanelItem> Logistics(IndustrialPanelModel m)
        {
            var r = m.Current; var items = new List<AreaPanelItem>();
            if (r == null) { Metric(items, m, "logistics", "Logistics", default); items.Add(Note("noPlacementBonus", "NoPlacementBonus")); return items; }
            items.Add(Row("storageFacilities", P + "StorageFacilities", Number(r.StorageFacilities)));
            items.Add(r.StorageObserved > 0 && r.StorageCapacity > 0
                ? Row("storageUtilization", P + "StorageUtilization", Number(r.StoredAmount) + " / " + Number(r.StorageCapacity) + " (" + Percent(100.0 * r.StoredAmount / r.StorageCapacity) + ")")
                : Row("storageUtilization", P + "StorageUtilization", valueKey: P + "Status." + (r.StorageFacilities > 0 ? "Unavailable" : "NotApplicable")));
            items.Add(Row("cargoFacilities", P + "CargoFacilities", Number(r.CargoOperational) + " / " + Number(r.CargoFacilities)));
            items.Add(Note("externalCargo", "ExternalCargo"));
            Metric(items, m, "inputContinuity", "InputContinuity", r.InputContinuity);
            if (r.InputContinuity.HasValue) items.Add(Row("inputHours", P + "ObservedHours", One(r.InputObservedHours) + " / " + Hours(IndustrialFlowWindow.WindowHours)));
            Metric(items, m, "outputContinuity", "OutputContinuity", r.OutputContinuity);
            if (r.OutputContinuity.HasValue) items.Add(Row("outputHours", P + "ObservedHours", One(r.OutputObservedHours) + " / " + Hours(IndustrialFlowWindow.WindowHours)));
            Metric(items, m, "logistics", "Logistics", r.Logistics);
            // All current flow diagnostics, not only those in the overall top three.
            Bottlenecks(items, r.AllBottlenecks, key => key == "InputStarvation" || key == "StorageSaturation" || key == "InsufficientCargo");
            items.Add(Note("noPlacementBonus", "NoPlacementBonus"));
            return items;
        }
        private static List<AreaPanelItem> Employment(IndustrialPanelModel m)
        {
            var r = m.Current; var items = new List<AreaPanelItem>();
            if (r == null) { Metric(items, m, "employment", "Employment", default); items.Add(Note("officesExcluded", "OfficesExcluded")); return items; }
            items.Add(Row("jobs", P + "Jobs", Number(r.Jobs)));
            items.Add(Row("employees", P + "Employees", Number(r.Employees)));
            items.Add(Row("vacancies", P + "Vacancies", Number(Math.Max(0, r.Jobs - r.Employees))));
            Metric(items, m, "jobFill", "JobFill", r.JobFill);
            Metric(items, m, "staffingBalance", "StaffingBalance", r.StaffingBalance);
            Metric(items, m, "employment", "Employment", r.Employment);
            items.Add(Row("producerJobs", P + "ProducerJobs", Number(r.ProducerEmployees) + " / " + Number(r.ProducerJobs)));
            items.Add(Row("supportJobs", P + "SupportJobs", Number(r.SupportEmployees) + " / " + Number(r.SupportJobs)));
            if (r.Jobs > 0)
                for (int level = 0; level < 5; level++) items.Add(Row("vacancy." + level, P + "Vacancy." + level, Number(r.VacanciesByLevel[level])));
            items.Add(Note("officesExcluded", "OfficesExcluded"));
            return items;
        }
        private static List<AreaPanelItem> Progress(IndustrialPanelModel m)
        {
            var items = new List<AreaPanelItem> { Row("level", P + "Level", valueKey: P + "LevelName." + m.Level), ExperienceBar(m) };
            var promotion = m.Promotion;
            if (promotion?.Target == null) items.Add(Row("maxLevel", P + "MaxLevel", valueKey: P + "LevelName." + IndustrialProgressionModel.MaxLevel));
            else
            {
                items.Add(Row("nextLevel", P + "NextLevel", valueKey: P + "LevelName." + (m.Level + 1)));
                foreach (var check in promotion.Checklist)
                {
                    string current = !check.Available ? "—" : check.Key == "LogisticsRoute" ? (check.Met ? "✓" : "—") : check.Key.StartsWith("Prestige", StringComparison.Ordinal) || check.Key == "Performance" || check.Key == "Logistics" || check.Key == "Production" ? One(check.Current) : Number(check.Current);
                    string required = check.Key == "LogisticsRoute" ? "✓" : Number(check.Required);
                    items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Check, Id = "check." + check.Key, LabelKey = P + "Check." + check.Key, Value = current + " / " + required,
                        NumericValue = check.Met ? 1 : 0, TooltipKey = check.Available ? null : P + "Status.Unavailable" });
                }
                if (promotion.Target.PrestigeRequired > 0) items.Add(Note("prestige", "Prestige"));
            }
            if (m.HasXp)
            {
                items.Add(Row("hourlyXp", P + "HourlyXp", Number(m.Xp.Award) + " / " + Number(IndustrialProgressionModel.MaxHourlyXp), tooltip: P + "HourlyXp.Tip"));
                if (m.Xp.BlockedReason != null) items.Add(Warning("xpBlocked", P + "XpBlocked." + m.Xp.BlockedReason));
            }
            else items.Add(Row("hourlyXp", P + "HourlyXp", valueKey: P + "Calculating", tooltip: P + "HourlyXp.Tip"));
            items.Add(Row("dailyXp", P + "DailyXp", Number(m.State.XpGrantedToday) + " / " + Number(IndustrialProgressionModel.DailyXpCap), tooltip: P + "DailyXp.Tip"));
            items.Add(Row("streakDays", P + "StreakDays", Number(m.State.HighThroughputStreakDays) + " / " + Number(IndustrialProgressionModel.StreakDays)));
            double hours = m.State.ActivityObservedHours;
            items.Add(Row("dayAverage", P + "DayAverage", hours > 0 ? One(m.State.ActivityWeightedSum / hours) + " / " + Number(IndustrialProgressionModel.StreakActivity) : null,
                hours > 0 ? null : P + "Status.WarmingUp"));
            items.Add(Row("dayHours", P + "DayHours", One(hours) + " / " + Hours(IndustrialProgressionModel.StreakHours)));
            if (m.LastCompletedDay.HasValue)
            {
                var d = m.LastCompletedDay.Value;
                items.Add(Row("lastDay", P + "LastDay", valueKey: P + "Day." + d.Reason, tooltip: P + "LastDay.Tip"));
            }
            items.Add(Note("noBuff", "NoBuff"));
            return items;
        }
    }
    // The selected-info section asks for the panel every rendered frame; rebuild a few times per second at most.
    public sealed class IndustrialPanelCache
    {
        private readonly long intervalMilliseconds;
        private readonly Func<long> clockMilliseconds;
        private Unity.Entities.Entity area;
        private long builtAt;
        private AreaPanelDocument document;
        public IndustrialPanelCache(long intervalMilliseconds, Func<long> clockMilliseconds) { this.intervalMilliseconds = intervalMilliseconds; this.clockMilliseconds = clockMilliseconds; }
        public void Invalidate() => document = null;
        public AreaPanelDocument Get(Unity.Entities.Entity key, Func<AreaPanelDocument> build)
        {
            long now = clockMilliseconds();
            if (document != null && key == area && now >= builtAt && now - builtAt < intervalMilliseconds) return document;
            document = build(); area = key; builtAt = now;
            return document;
        }
    }
}
