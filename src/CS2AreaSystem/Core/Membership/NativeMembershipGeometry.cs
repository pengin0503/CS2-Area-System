using Colossal.Mathematics;
using Unity.Mathematics;
namespace CS2AreaSystem.Core.Membership
{
    public static class NativeMembershipGeometry
    {
        public static bool Contains(Triangle2 triangle, float2 point) =>
            MathUtils.Intersect(triangle, new Circle2(0f, point));
    }
}
