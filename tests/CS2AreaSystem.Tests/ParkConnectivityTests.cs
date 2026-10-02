using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Core.Modules;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkConnectivityTests
{
 [Test] public void PanelNeverOffersDisconnectedFocusForPartialResults() {
  var model=new ParkPanelModel{Runtime=new ParkRuntimeSnapshot()};
  model.Runtime.Connectivity=new(){Status=ParkConnectivityStatus.Partial,Total=2,DisconnectedTargets=new[]{E(101)}};
  var partial=ParkPanelBuilder.Build(model).Tabs.Single(t=>t.Id=="landscape").Items;
  Assert.That(partial.Any(i=>i.Id=="connectivity.status"),Is.True);
  Assert.That(partial.Any(i=>i.Id.StartsWith("focus.connectivity")),Is.False);
  model.Runtime.Connectivity.Status=ParkConnectivityStatus.Complete;
  Assert.That(ParkPanelBuilder.Build(model).Tabs.Single(t=>t.Id=="landscape").Items.Any(i=>i.Id.StartsWith("focus.connectivity")),Is.True);
 }
 private static Entity E(int i)=>new(){Index=i,Version=1};
 private static ParkConnectivityScan Graph(IEnumerable<Entity> seeds,int cap=20000) {
  var edges=new Dictionary<Entity,ParkConnectivityEdge>{[E(1)]=new(true,true,E(10),E(11)),[E(2)]=new(true,true,E(11),E(12)),[E(3)]=new(true,true,E(20),E(21)),[E(4)]=new(true,false,E(12),E(21))};
  var nodes=new Dictionary<Entity,Entity[]>{[E(10)]=new[]{E(1)},[E(11)]=new[]{E(1),E(2)},[E(12)]=new[]{E(2),E(4)},[E(20)]=new[]{E(3)},[E(21)]=new[]{E(3),E(4)}};
  return new(seeds,new[]{(E(100),E(2)),(E(101),E(3))},e=>edges.TryGetValue(e,out var v)?v:default,
   (node,index)=>nodes.TryGetValue(node,out var list)&&index<list.Length?list[index]:(Entity?)null,cap);
 }
 [Test] public void ConnectedTargetsAndDisconnectedTargetsAreDistinctAndNonpedestrianEdgesDoNotBridge() {
  var scan=Graph(new[]{E(1)});scan.Step(4096);var r=scan.Snapshot();
  Assert.That(r.Status,Is.EqualTo(ParkConnectivityStatus.Complete));Assert.That(r.Connected,Is.EqualTo(1));
  Assert.That(r.DisconnectedTargets,Is.EqualTo(new[]{E(101)}));Assert.That(scan.LastWork,Is.LessThanOrEqualTo(4096));
 }
 [Test] public void MultipleEntrancesReachIndependentNetworkComponents() {
  var scan=Graph(new[]{E(1),E(3)});scan.Step(4096);Assert.That(scan.Snapshot().Connected,Is.EqualTo(2));Assert.That(scan.Snapshot().DisconnectedTargets,Is.Empty);
 }
 [Test] public void SlicedAndCappedWorkNeverReportsUnvisitedTargetsAsDisconnected() {
  var scan=Graph(new[]{E(1)});scan.Step(1);Assert.That(scan.LastWork,Is.EqualTo(1));Assert.That(scan.Snapshot().Status,Is.EqualTo(ParkConnectivityStatus.Partial));Assert.That(scan.Snapshot().DisconnectedTargets,Is.Empty);
  var capped=Graph(new[]{E(1)},2);capped.Step(4096);Assert.That(capped.Snapshot().Status,Is.EqualTo(ParkConnectivityStatus.Partial));Assert.That(capped.Snapshot().DisconnectedTargets,Is.Empty);
 }
 [Test] public void NoSeedsAreUnknownAndDirtyRestartRejectsPartialResultsAndStaleFocus() {
  var empty=Graph(Array.Empty<Entity>());Assert.That(empty.Snapshot().Status,Is.EqualTo(ParkConnectivityStatus.Unknown));
  var epoch=new ParkConnectivityEpoch();long old=epoch.Revision;epoch.Invalidate();var scan=Graph(new[]{E(1)});scan.Step(4096);
  Assert.That(epoch.TryPublish(old,scan.Snapshot()),Is.False);Assert.That(epoch.TryPublish(epoch.Revision,scan.Snapshot()),Is.True);
  var focused=new List<Entity>();Assert.That(ParkConnectivityFocus.TryFocus(epoch.Current,AreaDiagnosticTargetKey.Format(E(101)),e=>true,focused.Add),Is.True);
  Assert.That(ParkConnectivityFocus.TryFocus(epoch.Current,AreaDiagnosticTargetKey.Format(E(101)),e=>false,focused.Add),Is.False);
  epoch.Invalidate();Assert.That(ParkConnectivityFocus.TryFocus(epoch.Current,AreaDiagnosticTargetKey.Format(E(101)),e=>true,focused.Add),Is.False);
 }
}
