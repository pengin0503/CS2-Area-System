using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Modules;
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
 [TestCase(false,false,false)][TestCase(true,false,false)][TestCase(false,true,false)][TestCase(true,true,true)]
 public void ActivationRequiresHeadquartersAndCompleteEntrance(bool hq,bool entrance,bool active) {
  var a=new Entity{Index=1,Version=1};var building=new Entity{Index=2,Version=1};var store=new Store();
  var evidence=new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Complete,1,entrance?new[]{new ParkEntrancePoint(0,0,building)}:Array.Empty<ParkEntrancePoint>());
  var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(x,y,r)=>r==AreaAnchorRole.Headquarters);
  if(hq)anchors.Assign(a,building,AreaAnchorRole.Headquarters);
  var module=new ParkAreaModule(anchors,null,(e,c)=>evidence);
  Assert.That(module.ValidateActivation(a,default),Is.EqualTo(active));
  Assert.That(module.AnchorRoles,Is.EqualTo(new[]{AreaAnchorRole.Headquarters,AreaAnchorRole.Entrance}));
  Assert.That(module.CanAssignAnchor(a,AreaAnchorRole.Entrance,default),Is.False);
  Assert.That(module.CanAssignAnchor(a,AreaAnchorRole.Headquarters,default),Is.True);
 }
 [Test] public void PendingEvidenceCannotFabricateAnEntranceAndPointsAreBoundedAndCopied() {
  var points=Enumerable.Range(0,1000).Select(i=>new ParkEntrancePoint(i,0,Entity.Null)).ToArray();
  var complete=new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Complete,2,points);
  Assert.That(complete.Count,Is.EqualTo(ParkEntranceEvidence.MaxPoints));points[0]=new ParkEntrancePoint(999,0,Entity.Null);
  Assert.That(complete.Points[0].X,Is.Zero);
  Assert.That(new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Unknown,3,points).Count,Is.Zero);
 }
 [Test] public void OnlyParkMaintenanceBuildingsQualifyAsHeadquarters() {
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{IsBuilding=true,HasParkMaintenance=true}),Is.True);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{HasParkMaintenance=true}),Is.False);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits{IsBuilding=true,HasPark=true,HasLeisureProvider=true,Attractiveness=40}),Is.False);
 }
}
