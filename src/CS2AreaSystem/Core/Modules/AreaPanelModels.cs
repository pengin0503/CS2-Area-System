using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
namespace CS2AreaSystem.Core.Modules
{
    // Module-neutral structured panel content. Modules emit data; the UI renders items by Kind. No module-named fields.
    public static class AreaPanelItemKind
    {
        public const string Row = "row", Bar = "bar", Warning = "warning", Note = "note", Slider = "slider", Button = "button";
        // Requirement row: Value is "current / required", NumericValue 1 when met and 0 when not.
        public const string Check = "check";
    }
    public sealed class AreaPanelItem
    {
        public string Kind { get; set; } = AreaPanelItemKind.Row;
        public string Id { get; set; }
        public string LabelKey { get; set; }
        // Value is preformatted text; ValueKey is a localization key and wins when both are set.
        public string Value { get; set; }
        public string ValueKey { get; set; }
        public double? NumericValue { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Step { get; set; }
        public bool Disabled { get; set; }
        public string TooltipKey { get; set; }
        // Module action sent through the guarded moduleAction trigger (sliders send their new value).
        public string Action { get; set; }
        internal static bool Equivalent(AreaPanelItem a, AreaPanelItem b) =>
            ReferenceEquals(a, b) || (a != null && b != null && a.Kind == b.Kind && a.Id == b.Id && a.LabelKey == b.LabelKey &&
            a.Value == b.Value && a.ValueKey == b.ValueKey && a.NumericValue == b.NumericValue && a.Min == b.Min && a.Max == b.Max &&
            a.Step == b.Step && a.Disabled == b.Disabled && a.TooltipKey == b.TooltipKey && a.Action == b.Action);
        internal void Write(IJsonWriter writer)
        {
            writer.TypeBegin("CS2AreaSystem.AreaPanelItem");
            writer.PropertyName("kind"); writer.Write(Kind);
            writer.PropertyName("id"); writer.Write(Id);
            writer.PropertyName("labelKey"); writer.Write(LabelKey);
            writer.PropertyName("value"); WriteString(writer, Value);
            writer.PropertyName("valueKey"); WriteString(writer, ValueKey);
            writer.PropertyName("numericValue"); WriteNumber(writer, NumericValue);
            writer.PropertyName("min"); WriteNumber(writer, Min);
            writer.PropertyName("max"); WriteNumber(writer, Max);
            writer.PropertyName("step"); WriteNumber(writer, Step);
            writer.PropertyName("disabled"); writer.Write(Disabled);
            writer.PropertyName("tooltipKey"); WriteString(writer, TooltipKey);
            writer.PropertyName("action"); WriteString(writer, Action);
            writer.TypeEnd();
        }
        private static void WriteString(IJsonWriter writer, string value) { if (value == null) writer.WriteNull(); else writer.Write(value); }
        private static void WriteNumber(IJsonWriter writer, double? value) { if (value.HasValue) writer.Write(value.Value); else writer.WriteNull(); }
    }
    public sealed class AreaPanelTabData
    {
        public string Id { get; set; }
        public string LabelKey { get; set; }
        public IReadOnlyList<AreaPanelItem> Items { get; set; } = Array.Empty<AreaPanelItem>();
    }
    public sealed class AreaPanelDocument
    {
        public IReadOnlyList<AreaPanelTabData> Tabs { get; set; } = Array.Empty<AreaPanelTabData>();
        // True when the module's own tabs present level/experience, so Core omits its numeric rows.
        public bool ShowsProgress { get; set; }
        public static AreaPanelDocument Overview() =>
            new AreaPanelDocument { Tabs = new[] { new AreaPanelTabData { Id = "overview", LabelKey = "CS2AS.Tab.Overview" } } };
        public static bool Equivalent(AreaPanelDocument a, AreaPanelDocument b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.ShowsProgress != b.ShowsProgress) return false;
            var left = a.Tabs ?? Array.Empty<AreaPanelTabData>();
            var right = b.Tabs ?? Array.Empty<AreaPanelTabData>();
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (left[i]?.Id != right[i]?.Id || left[i]?.LabelKey != right[i]?.LabelKey) return false;
                var li = left[i]?.Items ?? Array.Empty<AreaPanelItem>();
                var ri = right[i]?.Items ?? Array.Empty<AreaPanelItem>();
                if (li.Count != ri.Count) return false;
                for (int j = 0; j < li.Count; j++) if (!AreaPanelItem.Equivalent(li[j], ri[j])) return false;
            }
            return true;
        }
        // Writes the tabs array; null tabs/items become empty so declared sizes always match.
        public void Write(IJsonWriter writer)
        {
            var tabs = Tabs ?? Array.Empty<AreaPanelTabData>();
            writer.ArrayBegin((uint)tabs.Count);
            foreach (var tab in tabs)
            {
                var items = tab?.Items ?? Array.Empty<AreaPanelItem>();
                writer.TypeBegin("CS2AreaSystem.AreaPanelTab");
                writer.PropertyName("id"); writer.Write(tab?.Id ?? "");
                writer.PropertyName("labelKey"); writer.Write(tab?.LabelKey ?? "");
                writer.PropertyName("items"); writer.ArrayBegin((uint)items.Count);
                foreach (var item in items) item.Write(writer);
                writer.ArrayEnd();
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }
    }
    public static class AreaModuleActionDispatcher
    {
        // guardMatches must come from AreaUIActionGuard (stable ID + live selection); module failures stay isolated.
        public static bool Dispatch(bool guardMatches, IAreaPanelProvider provider, Unity.Entities.Entity area, string action, string value,
            in AreaPanelContext context, Action<Exception> report)
        {
            if (!guardMatches || provider == null || string.IsNullOrEmpty(action)) return false;
            try { return provider.HandleAction(area, action, value, context); }
            catch (Exception error) { report(error); return false; }
        }
    }
}
