using CS2AreaSystem.Core.Semantics;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaSemanticAdapterTests
{
 private static Entity P=>new(){Index=9,Version=1};
 [Test] public void FullQualityReaderUsesTheSamePrefabFactsAsVisitsAndAnchors() {
  // EntityManager requires Unity native runtime; inspect the compiled integration seam and
  // execute the semantic aggregator below. F07 covers the live ECS path in the game.
  var calls=CS2AreaSystem.Tests.Support.IlCallScanner.Find(typeof(ParkFacilityReader).GetMethod(nameof(ParkFacilityReader.Read))!,m=>m.DeclaringType==typeof(ParkFacilityReader)&&m.Name==nameof(ParkFacilityReader.ReadCore));
  Assert.That(calls,Has.Count.EqualTo(1),"quality must retain the prefab identity used by visits/anchors");
 }
 [Test] public void QualityAggregationHonorsRegisteredAllowDenyAndRemoval() {
  var registry=new AreaAssetSemanticRegistry();var traits=new ParkFacilityTraits{IsBuilding=true,Prefab=P,Attractiveness=20};
  registry.Register("a",P,new(AreaAssetSemantic.ParkAttraction,AreaAssetSemantic.None,0));
  Assert.That(ParkFacilitySnapshot.Build(new[]{traits},registry).Attractions,Is.EqualTo(1));
  registry.Register("a",P,new(AreaAssetSemantic.None,AreaAssetSemantic.ParkAttraction,0));
  Assert.That(ParkFacilitySnapshot.Build(new[]{traits},registry).Attractions,Is.Zero);
  registry.UnregisterProvider("a");Assert.That(ParkFacilitySnapshot.Build(new[]{traits},registry).Attractions,Is.EqualTo(1));
 }
 // Phase 3 C2: Headquarters come only from the built-in provider; removing it leaves no typed-fact fallback.
 [Test] public void HeadquartersRequireTheBuiltInProviderAndRemovalLeavesNoFallback() {
  var r=new AreaAssetSemanticRegistry();var park=new ParkFacilityTraits{IsBuilding=true,Prefab=P,HasParkMaintenance=true};
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(park,r),Is.False);
  r.Register(AreaBuiltInAssetContract.ProviderId,P,new(AreaAssetSemantic.ParkHeadquarters|AreaAssetSemantic.IndustrialHeadquarters,AreaAssetSemantic.None,0));
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(park,r),Is.True);
  r.Register("a",P,new(AreaAssetSemantic.None,AreaAssetSemantic.ParkHeadquarters,1));Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(park,r),Is.False,"higher-priority Deny wins");
  r.UnregisterProvider("a");Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(park,r),Is.True);
  var industrial=new IndustrialFacilityTraits{IsBuilding=true,Prefab=P,HasOfficeProperty=true};
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(industrial,r),Is.True);
  r.UnregisterProvider(AreaBuiltInAssetContract.ProviderId);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(park,r),Is.False,"no Park Maintenance fallback");
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(industrial,r),Is.False,"no office fallback");
  industrial.IsBuilding=false;Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(industrial,r),Is.False);
 }
 [Test] public void SemanticLeisureAttractionAndIndustrialHintsArePrefabScoped() {
  var r=new AreaAssetSemanticRegistry();r.Register("a",P,new(AreaAssetSemantic.ParkLeisure|AreaAssetSemantic.ParkAttraction|AreaAssetSemantic.IndustrialStorage,AreaAssetSemantic.None,0));
  Assert.That(ParkFacilityClassifier.Classify(new ParkFacilityTraits{IsBuilding=true,Prefab=P},r),Is.EqualTo(ParkFacilityRoles.Leisure|ParkFacilityRoles.Attraction));
  Assert.That(IndustrialFacilityClassifier.Classify(new IndustrialFacilityTraits{IsBuilding=true,Prefab=P},r),Is.EqualTo(IndustrialFacilityRole.Storage));
  Assert.That(ParkFacilityClassifier.Classify(new ParkFacilityTraits{IsBuilding=true},r),Is.EqualTo(ParkFacilityRoles.None));
 }
 private sealed class Store:IAreaAnchorStore {public List<AreaAnchorLink> Links=new();public IReadOnlyList<AreaAnchorLink> Read(Entity a)=>Links;public void Write(Entity a,IReadOnlyList<AreaAnchorLink> v)=>Links=v.ToList();}
 [Test] public void GateProviderRemovalInvalidatesTheEntranceAndSuspendsThePark() {
  var r=new AreaAssetSemanticRegistry();var store=new Store();var a=new Entity{Index=1,Version=1};var h=new Entity{Index=2,Version=1};var gate=new Entity{Index=3,Version=1};
  var hqPrefab=new Entity{Index=8,Version=1};
  ParkAreaModule m=null;var service=new AreaAnchorService(store,e=>e!=Entity.Null,(x,e,role)=>m.AcceptsAnchor(x,e,role,default));
  m=new(service,(e,_)=>new ParkFacilityTraits{IsBuilding=true,Prefab=e==h?hqPrefab:P},r);
  r.Register(AreaBuiltInAssetContract.ProviderId,hqPrefab,new(AreaAssetSemantic.ParkHeadquarters,AreaAssetSemantic.None,0));
  Assert.That(service.Assign(a,h,AreaAnchorRole.Headquarters),Is.True);
  Assert.That(service.Assign(a,gate,AreaAnchorRole.Entrance),Is.False,"no gate content registered yet");
  r.Register(AreaBuiltInAssetContract.ProviderId,P,new(AreaAssetSemantic.ParkEntrance,AreaAssetSemantic.None,0));
  Assert.That(service.Assign(a,gate,AreaAnchorRole.Entrance),Is.True);Assert.That(m.ValidateActivation(a,default),Is.True);
  r.Unregister(AreaBuiltInAssetContract.ProviderId,P);
  Assert.That(service.GetAnchorLinks(a,AreaAnchorRole.Entrance).Single().Valid,Is.False);
  Assert.That(m.ValidateActivation(a,default),Is.False,"no path-derived fallback survives");
  Assert.That(service.GetAnchorLinks(a,AreaAnchorRole.Entrance),Has.Count.EqualTo(1),"the saved link stays visible and removable");
 }
}
