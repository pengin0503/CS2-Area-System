using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
namespace CS2AreaSystem.Modules.Industrial
{
    public sealed class IndustrialAreaModule : AnchorAreaModule
    {
        public IndustrialAreaModule(AreaAnchorService anchors) : base(AreaTypeId.Industrial, anchors, AreaAnchorRole.Headquarters) { }
    }
}
