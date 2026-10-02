using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Tourism
{
    public sealed class TourismAreaModule : AnchorAreaModule
    {
        public TourismAreaModule() : base(AreaTypeId.Tourism, null) { }
        public override string BuildSummary(Entity area, in AreaPanelContext context) => "CS2AS.Summary.TourismBoundaryOnly";
    }
}
