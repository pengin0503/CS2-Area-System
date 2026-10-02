using System;
using System.Collections.Generic;
using System.Globalization;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Diagnostics;
using CS2AreaSystem.Core.Modules;
namespace CS2AreaSystem.Modules.Park
{
    // Builds the five Park tabs (design §19) through the module-neutral panel contract. Pure: no ECS access.
    public static class ParkPanelBuilder
    {
        private const string P = "CS2AS.Park.";
        public static readonly string[] Metrics = { "Satisfaction", "Attractiveness", "Accessibility", "Variety", "Maintenance", "Comfort" };
        public static readonly string[] Warnings = { "NoEntrance", "NotPrimary", "LowConfidence", "NoFacilities", "Crowded", "LowMaintenance", "NoPaths", "Cluttered", "PoorValue" };
        public static readonly string[] Actions = { "Entrance", "Attractiveness", "Accessibility", "Variety", "Maintenance", "Comfort", "Satisfaction", "Wait" };
        public static readonly string[] Checks = { "Experience", "Quality", "Visitors", "Variety", "Landscape", "Satisfaction", "Maintenance", "PrestigeLandscape", "PrestigeVariety", "PrestigeTourists", "Hold" };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static AreaPanelDocument Build(ParkPanelModel model)
        {
            return new AreaPanelDocument { ShowsProgress = true, Tabs = new[] {
                new AreaPanelTabData { Id = "overview", LabelKey = "CS2AS.Tab.Overview", Items = Overview(model) },
                new AreaPanelTabData { Id = "visitors", LabelKey = "CS2AS.Tab.Visitors", Items = Visitors(model) },
                new AreaPanelTabData { Id = "finances", LabelKey = "CS2AS.Tab.Finances", Items = Finances(model) },
                new AreaPanelTabData { Id = "landscape", LabelKey = "CS2AS.Tab.Landscape", Items = Landscape(model) },
                new AreaPanelTabData { Id = "progress", LabelKey = "CS2AS.Tab.Progress", Items = Progress(model) } } };
        }
        // Every key the builder can emit, including the dynamic ones (for localization coverage).
        public static IEnumerable<string> AllKeys()
        {
            for (int level = ParkProgressionModel.MinLevel; level <= ParkProgressionModel.MaxLevel; level++) yield return P + "LevelName." + level;
            foreach (var m in Metrics) yield return P + "Metric." + m;
            foreach (var w in Warnings) yield return P + "Warning." + w;
            foreach (var a in Actions) yield return P + "Action." + a;
            foreach (var c in Checks) yield return P + "Check." + c;
            foreach (var t in RuntimeTrendPresentation.Keys) yield return t;
        }
        private static AreaPanelItem Row(string id, string label, string value = null, string valueKey = null, string tooltip = null) =>
            new AreaPanelItem { Kind = AreaPanelItemKind.Row, Id = id, LabelKey = label, Value = value, ValueKey = valueKey, TooltipKey = tooltip };
        private static AreaPanelItem Bar(string id, string label, float value, string text = null, string tooltip = null) =>
            new AreaPanelItem { Kind = AreaPanelItemKind.Bar, Id = id, LabelKey = label, NumericValue = Math.Round(Clamp(value), 1), Min = 0, Max = 100,
                Value = text ?? Number(value), TooltipKey = tooltip };
        private static float Clamp(float v) => float.IsNaN(v) ? 0 : Math.Min(100, Math.Max(0, v));
        private static string Number(double value) => Math.Round(value).ToString("N0", Invariant);
        private static string Money(long value) => (value < 0 ? "-¢" : "¢") + Math.Abs(value).ToString("N0", Invariant);
        private static string Percent(double ratio) => Math.Round(ratio * 100).ToString("N0", Invariant) + "%";
        private static string OneDecimal(float value) => value.ToString("0.0", Invariant);
        private static string OneDecimal(double value) => value.ToString("0.0", Invariant);
        private static string MoneyAmount(double value) => Money((long)Math.Round(value));
        // Runtime trend row directly after its headline metric, only once a sample exists (Wave A1).
        private static void Trend(List<AreaPanelItem> items, string id, ParkRuntimeSnapshot runtime, Func<ParkTrendSnapshot, RuntimeTrendSeries> series, Func<double, string> magnitude)
        { if (runtime != null) RuntimeTrendPresentation.Add(items, id, series(runtime.Trends), magnitude); }
        private static long Recent(ParkRuntimeSnapshot runtime, Func<ParkRuntimeSnapshot, ParkRollingWindow> window, uint frame) => runtime == null ? 0 : window(runtime).Sum(frame);
        private static AreaPanelItem Metric(string name, ParkMetric metric) =>
            metric.Available ? Bar(name.ToLowerInvariant(), P + "Metric." + name, metric.Value, tooltip: P + "Metric." + name + ".Tip")
                             : Row(name.ToLowerInvariant(), P + "Metric." + name, valueKey: P + "Unavailable", tooltip: P + "Metric." + name + ".Tip");
        private static AreaPanelItem ExperienceBar(ParkPanelModel model)
        {
            var target = ParkProgressionModel.GetTarget(model.Level);
            if (target == null) return Bar("experience", P + "Experience", 100, Number(model.Experience));
            return Bar("experience", P + "Experience", (float)(100.0 * model.Experience / target.Experience), Number(model.Experience) + " / " + Number(target.Experience));
        }
        private static List<AreaPanelItem> Overview(ParkPanelModel model)
        {
            var q = model.Runtime?.Quality;
            var items = new List<AreaPanelItem> {
                Row("level", P + "Level", valueKey: P + "LevelName." + model.Level),
                ExperienceBar(model),
                q != null ? Bar("quality", P + "Quality", q.Quality, tooltip: P + "Quality.Tip") : Row("quality", P + "Quality", valueKey: P + "Calculating") };
            Trend(items, "quality", model.Runtime, t => t.Quality, OneDecimal);
            if (q != null) { items.Add(Metric("Satisfaction", q.Satisfaction)); items.Add(Metric("Attractiveness", q.Attractiveness)); items.Add(Metric("Accessibility", q.Accessibility));
                items.Add(Metric("Variety", q.Variety)); items.Add(Metric("Maintenance", q.Maintenance)); items.Add(Metric("Comfort", q.Comfort)); }
            items.Add(Row("activeVisitors", P + "ActiveVisitors", Number(model.Runtime?.ActiveVisitors ?? 0)));
            items.Add(Row("recentVisitors", P + "RecentVisitors", Number(Recent(model.Runtime, r => r.RecentVisitors, model.Frame)), tooltip: P + "Recent.Tip"));
            items.Add(Row("entrances", P + "Entrances", Number(model.Runtime?.EntranceCount ?? 0)));
            var warnings = q?.Warnings ?? new List<string>();
            if (q == null && model.Lifecycle != AreaLifecycleState.Active) warnings = new List<string> { P + "Warning.NoEntrance" };
            foreach (var warning in warnings) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Warning, Id = "warning." + warning, LabelKey = warning });
            if (q?.NextActionKey != null) items.Add(Row("nextAction", P + "NextAction", valueKey: q.NextActionKey));
            return items;
        }
        private static List<AreaPanelItem> Visitors(ParkPanelModel model)
        {
            var s = model.State; var q = model.Runtime?.Quality; uint frame = model.Frame;
            var items = new List<AreaPanelItem> {
                Row("activeVisitors", P + "ActiveVisitors", Number(model.Runtime?.ActiveVisitors ?? 0)),
                Row("recentVisitors", P + "RecentVisitors", Number(Recent(model.Runtime, r => r.RecentVisitors, frame)), tooltip: P + "Recent.Tip") };
            Trend(items, "recentVisitors", model.Runtime, t => t.Visitors, v => Number(v));
            items.Add(Row("lifetimeVisitors", P + "LifetimeVisitors", Number(s.LifetimeVisitors)));
            items.Add(Row("residentVisitors", P + "ResidentVisitors", Number(s.LifetimeResidentVisitors)));
            items.Add(Row("touristVisitors", P + "TouristVisitors", Number(s.LifetimeTouristVisitors)));
            items.Add(Bar("satisfaction", P + "Metric.Satisfaction", s.Satisfaction.Display));
            Trend(items, "satisfaction", model.Runtime, t => t.Satisfaction, OneDecimal);
            items.Add(Bar("residentSatisfaction", P + "ResidentSatisfaction", s.ResidentSatisfaction.Display));
            items.Add(Bar("touristSatisfaction", P + "TouristSatisfaction", s.TouristSatisfaction.Display));
            if (s.Satisfaction.Confidence < 0.5f) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "satisfactionPrior", LabelKey = P + "Note.SatisfactionPrior" });
            items.Add(Row("capacity", P + "Capacity", q != null ? Number(q.ComfortableCapacity) : null, q == null ? P + "Calculating" : null, P + "Capacity.Tip"));
            items.Add(Row("occupancy", P + "Occupancy", q != null ? Percent(q.OccupancyRatio) : null, q == null ? P + "Calculating" : null));
            Trend(items, "occupancy", model.Runtime, t => t.Occupancy, OneDecimal);
            items.Add(Row("peakOccupancy", P + "PeakOccupancy", Number(model.Runtime?.PeakOccupancy ?? 0)));
            items.Add(Row("tourismContribution", P + "TourismContribution", q != null ? OneDecimal(q.TourismContribution) : null, q == null ? P + "Calculating" : null, P + "TourismContribution.Tip"));
            return items;
        }
        private static List<AreaPanelItem> Finances(ParkPanelModel model)
        {
            var s = model.State; uint frame = model.Frame;
            bool unlocked = ParkFinanceModel.IsAdmissionUnlocked(model.Level);
            int fee = ParkFinanceModel.EffectiveFee(model.Level, s.AdmissionFee);
            long income = Recent(model.Runtime, r => r.RecentAdmission, frame), expense = Recent(model.Runtime, r => r.RecentExpense, frame);
            var items = new List<AreaPanelItem> {
                new AreaPanelItem { Kind = AreaPanelItemKind.Slider, Id = "budget", LabelKey = P + "Budget", Action = ParkGameplayFacade.SetBudget,
                    NumericValue = Math.Round(model.Budget * 100, 1), Min = ParkFinanceModel.MinBudget * 100, Max = ParkFinanceModel.MaxBudget * 100,
                    Step = Math.Round(ParkFinanceModel.BudgetStep * 100, 3), Value = Percent(model.Budget), TooltipKey = P + "Budget.Tip" },
                new AreaPanelItem { Kind = AreaPanelItemKind.Slider, Id = "fee", LabelKey = P + "Fee", Action = ParkGameplayFacade.SetFee,
                    NumericValue = fee, Min = 0, Max = ParkFinanceModel.MaxAdmissionFee, Step = ParkFinanceModel.AdmissionFeeStep,
                    Value = fee > 0 ? Money(fee) : null, ValueKey = fee > 0 ? null : P + "Free", Disabled = !unlocked,
                    TooltipKey = unlocked ? P + "Fee.Tip" : P + "Fee.Locked" } };
            if (!unlocked) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "feeLocked", LabelKey = P + "Fee.Locked" });
            items.Add(Row("recentAdmission", P + "RecentAdmission", Money(income), tooltip: P + "Recent.Tip"));
            Trend(items, "recentAdmission", model.Runtime, t => t.AdmissionIncome, MoneyAmount);
            items.Add(Row("lifetimeAdmission", P + "LifetimeAdmission", Money(s.LifetimeAdmissionRevenue)));
            items.Add(Row("recentUncollected", P + "RecentUncollected", Money(Recent(model.Runtime, r => r.RecentUncollected, frame)), tooltip: P + "RecentUncollected.Tip"));
            items.Add(Row("managementCost", P + "ManagementCost", Money((long)Math.Round(model.Runtime?.ManagementCostPerDay ?? 0)), tooltip: P + "ManagementCost.Tip"));
            items.Add(Row("recentExpense", P + "RecentExpense", Money(expense), tooltip: P + "Recent.Tip"));
            Trend(items, "recentExpense", model.Runtime, t => t.ManagementExpense, MoneyAmount);
            items.Add(Row("lifetimeExpense", P + "LifetimeExpense", Money(s.LifetimeManagementExpense)));
            items.Add(Row("recentNet", P + "RecentNet", Money(income - expense), tooltip: P + "RecentNet.Tip"));
            items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "vanillaSeparate", LabelKey = P + "Note.VanillaSeparate" });
            return items;
        }
        private static List<AreaPanelItem> Landscape(ParkPanelModel model)
        {
            var l = model.Runtime?.Landscape;
            var items = new List<AreaPanelItem>();
            if (l == null || !l.Available) { items.Add(Row("landscape", P + "Landscape", valueKey: P + "Calculating")); return items; }
            // The breakdown Quality used: the scan with this Park's policy effects (Landscape Protection).
            var r = model.Runtime.Quality?.EffectiveLandscape ?? l.Result;
            string Part(float value, float weight) => OneDecimal(value) + " / " + weight.ToString("0", Invariant);
            items.Add(Bar("landscape", P + "Landscape", r.Score, tooltip: P + "Landscape.Tip"));
            items.Add(Row("vegetation", P + "Vegetation", Part(r.Vegetation, ParkLandscapeModel.VegetationWeight)));
            items.Add(Row("amenities", P + "Amenities", Part(r.Amenities, ParkLandscapeModel.AmenitiesWeight)));
            items.Add(Row("decoration", P + "Decoration", Part(r.Decoration, ParkLandscapeModel.DecorationWeight)));
            items.Add(Row("lighting", P + "Lighting", Part(r.Lighting, ParkLandscapeModel.LightingWeight)));
            items.Add(Row("paths", P + "Paths", Part(r.Paths, ParkLandscapeModel.PathWeight)));
            items.Add(Row("diversity", P + "Diversity", Part(r.Diversity, ParkLandscapeModel.DiversityWeight)));
            if (r.Cluttered) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Warning, Id = "clutter", LabelKey = P + "Warning.Cluttered", Value = "-" + OneDecimal(r.ClutterPenalty) });
            items.Add(Row("pathLength", P + "PathLength", Number(l.Inputs.PathLength) + " m"));
            items.Add(Row("transit", P + "TransitStops", Number(l.TransitPoints)));
            items.Add(Row("facilityCategories", P + "FacilityCategories", Number(model.Runtime?.Facilities?.DistinctCategories ?? 0)));
            return items;
        }
        private static List<AreaPanelItem> Progress(ParkPanelModel model)
        {
            var items = new List<AreaPanelItem> { Row("level", P + "Level", valueKey: P + "LevelName." + model.Level), ExperienceBar(model) };
            var promotion = model.Promotion;
            if (promotion?.Target == null) { items.Add(Row("maxLevel", P + "MaxLevel", valueKey: P + "LevelName." + ParkProgressionModel.MaxLevel)); return items; }
            items.Add(Row("nextLevel", P + "NextLevel", valueKey: P + "LevelName." + (model.Level + 1)));
            items.Add(Row("nextQuality", P + "NextQuality", Number(promotion.Target.Quality)));
            foreach (var check in promotion.Checklist)
            {
                string text = check.Key == "Hold"
                    ? Hours(check.Current) + " / " + Hours(check.Required)
                    : Number(check.Current) + " / " + Number(check.Required);
                items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Check, Id = "check." + check.Key, LabelKey = P + "Check." + check.Key, Value = text, NumericValue = check.Met ? 1 : 0 });
            }
            if (promotion.Target.PrestigeRequired > 0) items.Add(new AreaPanelItem { Kind = AreaPanelItemKind.Note, Id = "prestige", LabelKey = P + "Note.Prestige" });
            return items;
        }
        private static string Hours(double frames) => Math.Floor(frames / (ParkProgressionModel.FramesPerDay / 24.0)).ToString("0", Invariant) + " h";
    }
}
namespace CS2AreaSystem.Modules.Park
{
    // The selected-info section asks for the panel every rendered frame, while Park numbers change every simulation
    // tick. Rebuilding (and rebinding the vanilla panel) a few times per second is enough; actions invalidate at once.
    public sealed class ParkPanelCache
    {
        private readonly long intervalMilliseconds;
        private readonly Func<long> clockMilliseconds;
        private Unity.Entities.Entity area;
        private long builtAt;
        private AreaPanelDocument document;
        public ParkPanelCache(long intervalMilliseconds, Func<long> clockMilliseconds)
        { this.intervalMilliseconds = intervalMilliseconds; this.clockMilliseconds = clockMilliseconds; }
        public AreaPanelDocument Get(Unity.Entities.Entity key, Func<AreaPanelDocument> build)
        {
            long now = clockMilliseconds();
            if (document != null && key == area && now >= builtAt && now - builtAt < intervalMilliseconds) return document;
            document = build(); area = key; builtAt = now;
            return document;
        }
        public void Invalidate() => document = null;
    }
}
