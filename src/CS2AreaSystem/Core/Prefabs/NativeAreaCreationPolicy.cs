using Game.Areas;
namespace CS2AreaSystem.Core.Prefabs
{
    // CS2 1.6.2f1 AreaToolSystem.OnUpdate gameplay eligibility, checked before toolbar exposure.
    public static class NativeAreaCreationPolicy
    {
        public static bool CanCreate(bool editor, AreaType type, GeometryFlags flags, bool rendered)
        {
            if (type == AreaType.Lot && !editor) return false;
            if (type == AreaType.Surface && !rendered && (flags & GeometryFlags.ClipTerrain) == 0) return false;
            return true;
        }
    }
}
