using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Semantics;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkActivationTests
{
 private sealed class Store : IAreaAnchorStore {
  public List<AreaAnchorLink> Links=new();public IReadOnlyList<AreaAnchorLink> Read(Entity a)=>Links;
  public void Write(Entity a,IReadOnlyList<AreaAnchorLink> v)=>Links=v.ToList();
 }
 // Phase 3 C2: Headquarters AND an explicit Entrance anchor; there is no derived path entrance any more.
 [TestCase(false,false,false)][TestCase(true,false,false)][TestCase(false,true,false)][TestCase(true,true,true)]
 public void ActivationRequiresHeadquartersAndAnAssignedEntrance(bool hq,bool entrance,bool active) {
  var a=new Entity{Index=1,Version=1};var building=new Entity{Index=2,Version=1};var gate=new Entity{Index=3,Version=1};var store=new Store();
  var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(x,y,r)=>true);
  if(hq)anchors.Assign(a,building,AreaAnchorRole.Headquarters);
  if(entrance)anchors.Assign(a,gate,AreaAnchorRole.Entrance);
  var module=new ParkAreaModule(anchors);
  Assert.That(module.ValidateActivation(a,default),Is.EqualTo(active));
  Assert.That(module.AnchorRoles,Is.EqualTo(new[]{AreaAnchorRole.Headquarters,AreaAnchorRole.Entrance}));
  Assert.That(module.CanAssignAnchor(a,AreaAnchorRole.Entrance,default),Is.True);
  Assert.That(module.CanAssignAnchor(a,AreaAnchorRole.Headquarters,default),Is.True);
 }
 [Test] public void OnlyTheDedicatedParkHeadquartersQualifies() {
  var prefab=new Entity{Index=9,Version=1};var registry=new AreaAssetSemanticRegistry();
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{IsBuilding=true,HasParkMaintenance=true,Prefab=prefab},registry),Is.False,"Park Maintenance is retired");
  new AreaBuiltInContentSemanticProvider().Refresh(registry,new[]{new AreaBuiltInPrefabCandidate(prefab,"BuildingPrefab","AS_ParkHeadquarters",true,false,"AS_ParkHeadquarters.cok")});
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{IsBuilding=true,Prefab=prefab},registry),Is.True);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{Prefab=prefab},registry),Is.False,"not a building");
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{IsBuilding=true,HasPark=true,HasLeisureProvider=true,Attractiveness=40},registry),Is.False);
 }
}
