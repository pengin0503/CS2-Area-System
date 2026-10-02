using CS2AreaSystem.Core.Semantics;
using Unity.Entities;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaAssetSemanticRegistryTests
{
 private static Entity Prefab=>new(){Index=10,Version=1};
 [Test] public void UnregisteredAssetsUseTypedDefaultsAndReplacementInvalidatesCachedResolutions() {
  var r=new AreaAssetSemanticRegistry();Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkHeadquarters,true),Is.True);
  Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,false),Is.False);
  r.Register("mod.a",Prefab,new(AreaAssetSemantic.ParkEntrance,AreaAssetSemantic.None,0));long rev=r.Revision;
  Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,false),Is.True);
  r.Register("mod.a",Prefab,new(AreaAssetSemantic.None,AreaAssetSemantic.ParkEntrance,0));
  Assert.That(r.Revision,Is.GreaterThan(rev));Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,false),Is.False);
  Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkHeadquarters,true),Is.True);
 }
 [Test] public void HighestPriorityThenOrdinalProviderWinsEachBitRegardlessOfRegistrationOrder() {
  foreach(bool reversed in new[]{false,true}){
   var r=new AreaAssetSemanticRegistry();var ids=reversed?new[]{"b","a"}:new[]{"a","b"};
   foreach(var id in ids)r.Register(id,Prefab,id=="a"?new(AreaAssetSemantic.ParkEntrance,AreaAssetSemantic.None,2):new(AreaAssetSemantic.None,AreaAssetSemantic.ParkEntrance,2));
   Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,false),Is.True);
   r.Register("z",Prefab,new(AreaAssetSemantic.None,AreaAssetSemantic.ParkEntrance,3));Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,true),Is.False);
   Assert.That(r.UnregisterProvider("z"),Is.EqualTo(1));Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,false),Is.True);
   Assert.That(r.Unregister("a",Prefab),Is.True);Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,true),Is.False);
   r.UnregisterProvider("b");Assert.That(r.Resolve(Prefab,AreaAssetSemantic.ParkEntrance,true),Is.True);
  }
 }
 [Test] public void InvalidRegistrationIsIsolatedAndRemovalRestoresVanillaImmediately() {
  var r=new AreaAssetSemanticRegistry();r.Register("good",Prefab,new(AreaAssetSemantic.IndustrialHeadquarters,AreaAssetSemantic.None,0));
  long rev=r.Revision;
  Assert.That(r.Register("",Prefab,new()),Is.False);Assert.That(r.Register("bad",Entity.Null,new()),Is.False);
  Assert.That(r.Register("bad",Prefab,new(AreaAssetSemantic.ParkEntrance,AreaAssetSemantic.ParkEntrance,99)),Is.False);
  Assert.That(r.Revision,Is.EqualTo(rev));Assert.That(r.Resolve(Prefab,AreaAssetSemantic.IndustrialHeadquarters,false),Is.True);
  Assert.That(r.HasRegisteredAllow(AreaAssetSemantic.IndustrialHeadquarters),Is.True);
  r.UnregisterProvider("good");Assert.That(r.HasRegisteredAllow(AreaAssetSemantic.IndustrialHeadquarters),Is.False);
  Assert.That(r.Resolve(Prefab,AreaAssetSemantic.IndustrialHeadquarters,false),Is.False);
 }
}
