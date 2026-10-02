using Colossal.Mathematics;
using CS2AreaSystem.Core.Labels;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
// Camera looking straight down, camera right = +x: the label plane is horizontal, label x = world x, label y = world z.
public class AreaLabelPickingTests {
 private static readonly float3 Right=new(1,0,0);
 private static readonly Bounds2 Line1=new(new float2(-100,-20),new float2(100,20)), Line2=new(new float2(-50,-70),new float2(50,-30));
 private static readonly Entity a=new(){Index=1,Version=1}, b=new(){Index=2,Version=1};
 private static Line3.Segment Down(float x,float height,float z=0)=>new(new float3(x,height,z),new float3(x,-height,z));
 private static (Entity,float3,IReadOnlyList<Bounds2>) Label(Entity e,float3 p)=>(e,p,new[]{Line1});
 [Test] public void RayThroughLabelCentreHits() {
  Assert.That(AreaLabelPicking.TryHit(Down(0,1000),Right,float3.zero,new[]{Line1},out var distance),Is.True);
  Assert.That(distance,Is.EqualTo(0.5f-40f/2000f).Within(1e-4f),"t minus the line height scaled like RaycastLabelsJob");
 }
 [Test] public void RayBesideLabelMisses() {
  Assert.That(AreaLabelPicking.TryHit(Down(150,1000),Right,float3.zero,new[]{Line1},out _),Is.False);
  Assert.That(AreaLabelPicking.TryHit(Down(0,1000,30),Right,float3.zero,new[]{Line1},out _),Is.False);
  Assert.That(AreaLabelPicking.TryHit(Down(0,1000),Right,float3.zero,Array.Empty<Bounds2>(),out _),Is.False);
 }
 [Test] public void EveryLabelLineIsClickable() {
  Assert.That(AreaLabelPicking.TryHit(Down(0,1000,-50),Right,float3.zero,new[]{Line1},out _),Is.False);
  Assert.That(AreaLabelPicking.TryHit(Down(0,1000,-50),Right,float3.zero,new[]{Line1,Line2},out _),Is.True);
 }
 [Test] public void LabelScaleGrowsWithCameraDistance() {
  Assert.That(AreaLabelPicking.TryHit(Down(150,1000),Right,float3.zero,new[]{Line1},out _),Is.False);
  Assert.That(AreaLabelPicking.TryHit(Down(150,4000),Right,float3.zero,new[]{Line1},out _),Is.True,"scale sqrt(4000 * 0.001) = 2");
 }
 [Test] public void NearestLabelWinsAndTiesAreStable() {
  Assert.That(AreaLabelPicking.TryPick(Down(0,1000),Right,new[]{Label(a,float3.zero),Label(b,new float3(0,100,0))},out var area,out var distance),Is.True);
  Assert.That(area,Is.EqualTo(b),"the label nearer the camera");
  Assert.That(distance,Is.LessThan(0.5f));
  Assert.That(AreaLabelPicking.TryPick(Down(0,1000),Right,new[]{Label(b,float3.zero),Label(a,float3.zero)},out area,out _),Is.True);
  Assert.That(area,Is.EqualTo(a),"equal distance picks the lower entity index");
  Assert.That(AreaLabelPicking.TryPick(Down(500,1000),Right,new[]{Label(a,float3.zero)},out area,out _),Is.False);
  Assert.That(area,Is.EqualTo(Entity.Null));
 }
 [Test] public void NoSelectionWithoutWorldClickInDefaultTool() {
  Assert.That(AreaLabelPicking.ResolveClick(false,true,true,true,a,0.4f,false,0),Is.EqualTo(Entity.Null),"no press");
  Assert.That(AreaLabelPicking.ResolveClick(true,false,true,true,a,0.4f,false,0),Is.EqualTo(Entity.Null),"pointer over UI");
  Assert.That(AreaLabelPicking.ResolveClick(true,true,false,true,a,0.4f,false,0),Is.EqualTo(Entity.Null),"another tool");
  Assert.That(AreaLabelPicking.ResolveClick(true,true,true,false,Entity.Null,0,false,0),Is.EqualTo(Entity.Null),"no label");
 }
 [Test] public void LabelNearerThanVanillaHitOverridesSelection() =>
  Assert.That(AreaLabelPicking.ResolveClick(true,true,true,true,a,0.40f,true,0.45f),Is.EqualTo(a));
 [Test] public void VanillaHitNearerKeepsVanillaSelection() {
  Assert.That(AreaLabelPicking.ResolveClick(true,true,true,true,a,0.45f,true,0.40f),Is.EqualTo(Entity.Null));
  Assert.That(AreaLabelPicking.ResolveClick(true,true,true,true,a,0.40f,true,0.40f),Is.EqualTo(Entity.Null),"a tie keeps vanilla");
 }
 [Test] public void NoVanillaHitSelectsLabel() =>
  Assert.That(AreaLabelPicking.ResolveClick(true,true,true,true,a,0.9f,false,0),Is.EqualTo(a));
}
