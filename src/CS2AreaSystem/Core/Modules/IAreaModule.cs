using CS2AreaSystem.Core.Areas;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public interface IAreaModule
    {
        AreaTypeDescriptor DescribeType();
        bool ValidateActivation(Entity area);
        IAreaMemberClassifier MemberClassifier { get; }
        IAreaPanelProvider PanelProvider { get; }
        bool AllowsOverlapWith(int otherTypeId);
        void OnAreaCreated(Entity area);
        void OnAreaActivated(Entity area);
        void OnAreaSuspended(Entity area);
        void OnAreaDeleted(Entity area);
    }
}
