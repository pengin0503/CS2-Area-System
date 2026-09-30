using CS2AreaSystem.Core.UI;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaListViewModelTests {
 private static AreaListEntry Entry(ulong id,string name="Area",string state="CS2AS.State.Active")=>new(){StableId=id,Name=name,TypeKey="CS2AS.Type.Park",State=state};
 private static readonly Entity a=new(){Index=1,Version=1}, b=new(){Index=2,Version=1};
 [Test] public void EntriesAreOrderedByStableIdAndComparedByContent() {
  var sorted=AreaListViewModel.Sort(new[]{Entry(9),Entry(2),Entry(5)});
  Assert.That(sorted.Select(e=>e.StableId),Is.EqualTo(new ulong[]{2,5,9}));
  Assert.That(AreaListViewModel.Equivalent(sorted,AreaListViewModel.Sort(new[]{Entry(5),Entry(9),Entry(2)})),Is.True);
  Assert.That(AreaListViewModel.Equivalent(sorted,AreaListViewModel.Sort(new[]{Entry(2),Entry(5,"Renamed"),Entry(9)})),Is.False);
  Assert.That(AreaListViewModel.Equivalent(sorted,AreaListViewModel.Sort(new[]{Entry(2),Entry(5,state:"CS2AS.State.Suspended"),Entry(9)})),Is.False);
  Assert.That(AreaListViewModel.Equivalent(sorted,AreaListViewModel.Sort(new[]{Entry(2),Entry(5)})),Is.False);
 }
 [Test] public void SelectionResolvesOnlyAnExactUniqueStableId() {
  var areas=new[]{new KeyValuePair<Entity,ulong>(a,7),new KeyValuePair<Entity,ulong>(b,9007199254740993)};
  Assert.That(AreaListViewModel.TryFind(areas,"9007199254740993",out var found),Is.True);
  Assert.That(found,Is.EqualTo(b));
  foreach(var invalid in new[]{"8","0","-7"," 7","7.0","abc",""})
   Assert.That(AreaListViewModel.TryFind(areas,invalid,out _),Is.False,invalid);
  var duplicate=new[]{new KeyValuePair<Entity,ulong>(a,7),new KeyValuePair<Entity,ulong>(b,7)};
  Assert.That(AreaListViewModel.TryFind(duplicate,"7",out var ambiguous),Is.False);
  Assert.That(ambiguous,Is.EqualTo(Entity.Null));
 }
}
