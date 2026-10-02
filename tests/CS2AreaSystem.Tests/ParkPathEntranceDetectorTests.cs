using CS2AreaSystem.Modules.Park;
using Colossal.Mathematics;
using Unity.Mathematics;
using Unity.Entities;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkPathEntranceDetectorTests
{
 private static Entity E(int i)=>new(){Index=i,Version=1};
 private static Bezier4x3 Line(float a,float b)=>new(new float3(a,0,0),new float3(a+(b-a)/3,0,0),new float3(a+2*(b-a)/3,0,0),new float3(b,0,0));
 [TestCase(true,-20,20,1)][TestCase(false,-20,20,0)][TestCase(true,10,20,0)][TestCase(true,-20,-10,0)]
 public void OnlyDedicatedPathsThatCrossTheBoundaryCount(bool dedicated,float a,float b,int count) {
  var points=ParkPathEntranceDetector.Detect(E(1),dedicated,Line(a,b),p=>p.x>=0);
  Assert.That(points.Count,Is.EqualTo(count));if(count>0)Assert.That(points[0].X,Is.EqualTo(0).Within(0.02));
 }
 [Test] public void TwoCrossingsAndNearbyDuplicatesUseLowestEdgeDeterministically() {
  var points=ParkPathEntranceDetector.Detect(E(2),true,Line(-40,40),p=>p.x>=-15&&p.x<=15);
  Assert.That(points.Count,Is.EqualTo(2));
  var duplicates=points.Concat(new[]{new ParkEntrancePoint(-14,0,E(1))});
  var a=ParkPathEntranceDetector.Merge(duplicates);var b=ParkPathEntranceDetector.Merge(duplicates.Reverse());
  Assert.That(a.Count,Is.EqualTo(2));Assert.That(a[0].Edge,Is.EqualTo(E(1)));
  Assert.That(b.Select(p=>p.Edge),Is.EqualTo(a.Select(p=>p.Edge)));
 }
 [Test] public void SlicedScanStaysBoundedAndBoundaryEditsDropPartialAndOldEvidence() {
  var store=new ParkEntranceEvidenceStore();var lifecycle=new ParkEntranceScanCoordinator(store);
  lifecycle.Track(7);var scan=lifecycle.Begin(7,new Bounds2(new float2(-100),new float2(100)));
  var edges=Enumerable.Range(1,6000).Select(E).ToArray();var supplied=0;
  ParkNetPage Read(Bounds2 box,int limit) {var page=edges.Skip(supplied).Take(limit).ToArray();supplied+=page.Length;return new ParkNetPage(page,false);}
  scan.Step(Read,e=>new[]{new ParkEntrancePoint(e.Index*10,0,e)},2048);
  Assert.That(scan.LastWork,Is.LessThanOrEqualTo(2048));Assert.That(store.TryGet(7,out var initial),Is.True);Assert.That(initial.Status,Is.EqualTo(ParkEntranceEvidenceStatus.Unknown));
  lifecycle.BoundaryChanged(7);Assert.That(lifecycle.TryPublish(scan),Is.False);
  Assert.That(store.TryGet(7,out var unknown),Is.True);Assert.That(unknown.Status,Is.EqualTo(ParkEntranceEvidenceStatus.Unknown));
  lifecycle.Clear();Assert.That(store.TryGet(7,out _),Is.False);
 }
 [Test] public void NetRefreshKeepsCompleteEvidenceUntilAtomicReplacement() {
  var store=new ParkEntranceEvidenceStore();var c=new ParkEntranceScanCoordinator(store);c.Track(7);
  var old=c.Begin(7,new Bounds2(new float2(0),new float2(10)));
  old.Step((b,l)=>new ParkNetPage(new[]{E(1)},false),e=>new[]{new ParkEntrancePoint(0,0,e)},2048);
  Assert.That(c.TryPublish(old),Is.True);c.NetChanged(7);
  Assert.That(store.TryGet(7,out var retained),Is.True);Assert.That(retained.Count,Is.EqualTo(1));
  var next=c.Begin(7,new Bounds2(new float2(0),new float2(10)));
  next.Step((b,l)=>new ParkNetPage(Array.Empty<Entity>(),false),e=>Array.Empty<ParkEntrancePoint>(),2048);
  Assert.That(c.TryPublish(next),Is.True);Assert.That(store.TryGet(7,out var replaced),Is.True);Assert.That(replaced.Count,Is.Zero);
 }
 [Test] public void ContinuallyOverflowingBoundsTerminateWithoutPublishingAndReleaseNextPark() {
  var store=new ParkEntranceEvidenceStore();var coordinator=new ParkEntranceScanCoordinator(store);
  var scan=coordinator.Begin(7,new Bounds2(new float2(-100),new float2(100)));
  int queries=0,maxQueued=0;
  for(int update=0;update<1000&&!scan.Done;update++) {
   scan.Step((b,limit)=>{queries++;return new ParkNetPage(Enumerable.Range(1,limit).Select(E).ToArray(),true);},_=>Array.Empty<ParkEntrancePoint>(),2048);
   Assert.That(scan.LastWork,Is.LessThanOrEqualTo(2048));
   var pending=(System.Collections.ICollection)typeof(ParkEntranceScan).GetField("tiles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(scan)!;
   maxQueued=Math.Max(maxQueued,pending.Count);
  }
  Assert.That(scan.Done,Is.True);Assert.That(scan.Complete,Is.False);
  Assert.That(queries,Is.LessThanOrEqualTo(4096));Assert.That(maxQueued,Is.LessThanOrEqualTo(4096));
  Assert.That(coordinator.TryPublish(scan),Is.False);
  var next=coordinator.Begin(8,new Bounds2(new float2(0),new float2(10)));
  next.Step((b,l)=>new ParkNetPage(new[]{E(1)},false),e=>new[]{new ParkEntrancePoint(0,0,e)},2048);
  Assert.That(coordinator.TryPublish(next),Is.True);
 }
}
