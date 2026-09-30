using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public enum AreaMemberRole : byte { None, Member }
    public interface IAreaMemberClassifier { AreaMemberRole Classify(Entity member, Entity area); }
}
