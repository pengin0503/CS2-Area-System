using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class IndustrialAreaComparisonTests {
 private static IndustrialComparisonSource Source(ulong id,double? score=80,AreaLifecycleState state=AreaLifecycleState.Active) {
  var snapshot=new IndustrialRuntimeSnapshot();snapshot.Publish(new(){Active=true,HourBucket=9,EndHours=9.2,Performance=IndustrialMetric.Of(70)});
  snapshot.Publish(new(){Active=true,HourBucket=10,EndHours=10.2,Performance=score.HasValue?IndustrialMetric.Of(score.Value):IndustrialMetric.Unavailable(),Production=IndustrialMetric.Of(75)});
  return new(){StableId=id,TypeId=(int)AreaTypeId.Industrial,Name="Area "+id,Lifecycle=state,Snapshot=snapshot};
 }
 [Test] public void FiltersToLiveManagedIndustrialAreasAndSortsCurrentPerformanceThenStableId() {
  var excluded=Source(9,100);excluded.Quarantined=true;var deleted=Source(10,100);deleted.Deleted=true;var tourism=Source(11,100);tourism.TypeId=(int)AreaTypeId.Tourism;
  var rows=IndustrialAreaComparison.Build(new[]{Source(3),Source(2),Source(1,90),Source(4,null),excluded,deleted,tourism},11);
  Assert.That(rows.Select(r=>r.StableId),Is.EqualTo(new ulong[]{1,2,3,4}));
  Assert.That(rows[0].Performance.Value,Is.EqualTo(90));Assert.That(rows[0].PerformanceDelta,Is.EqualTo(20));
  Assert.That(rows[3].Performance.HasValue,Is.False);Assert.That(rows[3].PerformanceDelta,Is.Null);
 }
 [Test] public void SuspendedDelayedAndOldResultsRemainStaleRatherThanCurrentZeros() {
  var paused=Source(1,99,AreaLifecycleState.Suspended);var delayed=Source(2,98);delayed.Snapshot.Current.Delayed=true;
  var old=Source(3,97);old.Snapshot.Current.EndHours=5;
  var rows=IndustrialAreaComparison.Build(new[]{paused,delayed,old,Source(4,20)},11);
  Assert.That(rows.Select(r=>r.StableId),Is.EqualTo(new ulong[]{4,1,2,3}));
  foreach(var stale in rows.Skip(1)){Assert.That(stale.Stale,Is.True);Assert.That(stale.Performance.Status,Is.EqualTo(IndustrialMetricStatus.Stale));Assert.That(stale.Performance.Value,Is.GreaterThan(90));Assert.That(stale.PerformanceDelta,Is.Null);}
  Assert.That(IndustrialAreaComparison.Build(new[]{Source(5)},null)[0].Stale,Is.True);
 }
 [Test] public void ComparisonTabUsesExistingReadOnlyRowsAndMarksStaleData() {
  var model=new IndustrialPanelModel{Comparison=IndustrialAreaComparison.Build(new[]{Source(1),Source(2,90,AreaLifecycleState.Suspended)},11)};
  var tab=IndustrialPanelBuilder.Build(model).Tabs.Single(t=>t.Id=="comparison");
  Assert.That(tab.Items.Any(i=>i.Id=="comparison.1.Performance"),Is.True);
  Assert.That(tab.Items.Any(i=>i.Id=="comparison.2.stale"),Is.True);
  Assert.That(tab.Items.All(i=>i.Action==null),Is.True);
 }
}
