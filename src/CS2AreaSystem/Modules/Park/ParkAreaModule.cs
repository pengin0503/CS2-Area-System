using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
namespace CS2AreaSystem.Modules.Park
{
    public sealed class ParkAreaModule : AnchorAreaModule
    {
        public ParkAreaModule(AreaAnchorService anchors) : base(AreaTypeId.Park, anchors, AreaAnchorRole.Entrance) { }
    }
}
