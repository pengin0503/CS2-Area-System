using Colossal.Mathematics;
using CS2AreaSystem.Core.Membership;
using NUnit.Framework;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
public class NativeMembershipGeometryTests {
    [TestCase(1f, 1f, true)]
    [TestCase(0f, 0f, true)]
    [TestCase(9f, 9f, false)]
    public void NativeTriangleTestsCanonicalPoint(float x, float z, bool expected) {
        var triangle = new Triangle2(new float2(0,0),new float2(10,0),new float2(0,10));
        Assert.That(NativeMembershipGeometry.Contains(triangle, new float2(x,z)), Is.EqualTo(expected));
    }
}
