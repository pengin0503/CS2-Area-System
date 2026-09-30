using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public interface IAreaPanelProvider
    {
        string BuildSummary(Entity area, in AreaPanelContext context);
        AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context);
        bool HandleAction(Entity area, string action, string value, in AreaPanelContext context);
    }
}
