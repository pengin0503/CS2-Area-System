using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public sealed class AreaPanelTab
    {
        public string Id { get; }
        public string LabelKey { get; }
        public AreaPanelTab(string id, string labelKey) { Id = id; LabelKey = labelKey; }
    }
    public interface IAreaPanelProvider
    {
        IReadOnlyList<AreaPanelTab> Tabs { get; }
        string BuildSummary(Entity area);
        bool HandleAction(Entity area, string action);
    }
}
