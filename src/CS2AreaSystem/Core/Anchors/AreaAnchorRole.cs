namespace CS2AreaSystem.Core.Anchors
{
    public enum AreaAnchorRole : byte { Entrance = 1, Headquarters = 2 }
    public interface IAreaAnchorPolicy { AreaAnchorRole? RequiredAnchorRole { get; } }
}
