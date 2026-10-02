using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Tourism;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaAnchorServiceTests {
    private sealed class Store : IAreaAnchorStore {
        public Dictionary<Entity,IReadOnlyList<AreaAnchorLink>> Values = new();
        public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area,out var links)?links:Array.Empty<AreaAnchorLink>();
        public void Write(Entity area,IReadOnlyList<AreaAnchorLink> links) => Values[area]=links.ToArray();
    }
    private readonly Entity area=new(){Index=10,Version=1}, other=new(){Index=20,Version=1}, hq=new(){Index=30,Version=1}, replacement=new(){Index=40,Version=1};
    [Test] public void HeadquartersLossSuspendsOnlyItsAreaAndReassignmentPreservesProgress() {
        var invalid=new HashSet<Entity>();
        var store=new Store(); var anchors=new AreaAnchorService(store,e=>!invalid.Contains(e),(a,e,r)=>true);
        var industrial=new IndustrialAreaModule(anchors); var tourism=new TourismAreaModule();
        AreaLifecycleState State(Entity a)=>AreaLifecyclePolicy.Resolve(true,industrial.ValidateActivation(a, default),false);
        var progress=new ManagedAreaProgress{Level=3,Experience=1000};
        Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Suspended));
        Assert.That(anchors.Assign(area,hq,AreaAnchorRole.Headquarters),Is.True);
        anchors.Assign(other,replacement,AreaAnchorRole.Headquarters);
        Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Active));
        invalid.Add(hq); Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Suspended)); Assert.That(State(other),Is.EqualTo(AreaLifecycleState.Active));
        anchors.Assign(area,replacement,AreaAnchorRole.Headquarters);
        Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Active));
        Assert.That(store.Read(area).Count,Is.EqualTo(1)); Assert.That(progress.Level,Is.EqualTo(3)); Assert.That(progress.Experience,Is.EqualTo(1000));
        Assert.That(tourism.ValidateActivation(area, default),Is.True);
    }
    [Test] public void ParkNeedsEntranceAndInvalidAssignmentCannotReplaceExistingLinks() {
        var store=new Store(); var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(a,e,r)=>r==AreaAnchorRole.Entrance);
        var park=new ParkAreaModule(anchors);
        Assert.That(park.ValidateActivation(area, default),Is.False);
        Assert.That(anchors.Assign(area,hq,AreaAnchorRole.Entrance),Is.True);
        Assert.That(park.ValidateActivation(area, default),Is.True);
        Assert.That(anchors.Assign(area,Entity.Null,AreaAnchorRole.Entrance),Is.False);
        Assert.That(anchors.Assign(area,replacement,AreaAnchorRole.Headquarters),Is.False);
        Assert.That(anchors.Remove(area,hq,AreaAnchorRole.Entrance),Is.True);
        Assert.That(park.ValidateActivation(area, default),Is.False);
    }
    [Test] public void AnchorUsesSerializerEntityOverloadAndReaderRemapping() {
        var io=MemorySerializer.Create();
        var link=new AreaAnchorLink{Anchor=hq,Role=AreaAnchorRole.Headquarters}; link.Serialize(io.Writer);
        Assert.That(io.Data.Tokens,Does.Contain(hq));
        io.Data.Remap=e=>e==hq?replacement:e;
        var loaded=new AreaAnchorLink();loaded.Deserialize(io.Reader);
        Assert.That(loaded.Anchor,Is.EqualTo(replacement));Assert.That(loaded.Role,Is.EqualTo(AreaAnchorRole.Headquarters));
    }
    [Test] public void ForgetAreaPrunesReverseIndexButQueuesFinalValidation() {
        var store=new Store();
        var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(a,e,r)=>true);
        Assert.That(anchors.Assign(area,hq,AreaAnchorRole.Headquarters),Is.True);
        anchors.DrainDirty();
        anchors.ForgetArea(area);
        Assert.That(anchors.DrainDirty(),Is.EquivalentTo(new[]{area}));
        anchors.InvalidateAnchor(hq);
        Assert.That(anchors.DrainDirty(),Is.Empty);
    }
    [Test] public void RepeatedDeletionPreservesOnlyTheSurvivingAreasSharedAnchorRelation() {
        var store=new Store();
        var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(a,e,r)=>true);
        anchors.Assign(other,hq,AreaAnchorRole.Headquarters);
        anchors.DrainDirty();
        for(int i=0;i<100;i++) {
            var deleted=new Entity {Index=100+i,Version=1};
            anchors.Assign(deleted,hq,AreaAnchorRole.Headquarters);
            anchors.DrainDirty();
            anchors.ForgetArea(deleted);
            Assert.That(anchors.DrainDirty(),Is.EquivalentTo(new[]{deleted}));
            anchors.InvalidateAnchor(hq);
            Assert.That(anchors.DrainDirty(),Is.EquivalentTo(new[]{other}));
        }
        Assert.That(store.Read(other).Single().Anchor,Is.EqualTo(hq));
    }
 [Test] public void ExposesTheCurrentValidAnchorForTheSectionOnly() {
  var invalid=new HashSet<Entity>();
  var store=new Store(); var anchors=new AreaAnchorService(store,e=>e!=Entity.Null&&!invalid.Contains(e),(a,e,r)=>true);
  Assert.That(anchors.TryGetValidAnchor(area,AreaAnchorRole.Headquarters,out var none),Is.False); Assert.That(none,Is.EqualTo(Entity.Null));
  anchors.Assign(area,hq,AreaAnchorRole.Headquarters);
  Assert.That(anchors.TryGetValidAnchor(area,AreaAnchorRole.Headquarters,out var found),Is.True); Assert.That(found,Is.EqualTo(hq));
  Assert.That(anchors.TryGetValidAnchor(area,AreaAnchorRole.Entrance,out _),Is.False,"other role");
  invalid.Add(hq);
  Assert.That(anchors.TryGetValidAnchor(area,AreaAnchorRole.Headquarters,out _),Is.False,"demolished anchor is not shown");
 }
}
public class AreaAnchorPolicyTests {
 private sealed class Store : IAreaAnchorStore {
  public Dictionary<Entity,IReadOnlyList<AreaAnchorLink>> Values = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area,out var links)?links:Array.Empty<AreaAnchorLink>();
  public void Write(Entity area,IReadOnlyList<AreaAnchorLink> links) => Values[area]=links.ToArray();
 }
 private static readonly Entity area=new(){Index=10,Version=1}, park=new(){Index=30,Version=1}, gate=new(){Index=31,Version=1}, office=new(){Index=40,Version=1}, hq=new(){Index=50,Version=1};
 private static ParkFacilityTraits Traits(Entity e) =>
  e==park ? new ParkFacilityTraits{IsBuilding=true,HasRoadAccess=true,HasPark=true,HasLeisureProvider=true,LeisureType=Game.Agents.LeisureType.CityPark,LeisureEfficiency=40} :
  e==gate ? new ParkFacilityTraits{IsBuilding=true,HasRoadAccess=true,Attractiveness=12} :
  new ParkFacilityTraits{IsBuilding=true,HasRoadAccess=true};
 [Test] public void ParkAcceptsOnlyPlausibleLeisureOrAttractionEntrances() {
  var module=new ParkAreaModule(new AreaAnchorService(new Store(),e=>true,(a,e,r)=>true),(e,_)=>Traits(e));
  IAreaAnchorPolicy policy=module;
  Assert.That(policy.AcceptsAnchor(area,park,AreaAnchorRole.Entrance,default),Is.True);
  Assert.That(policy.AcceptsAnchor(area,gate,AreaAnchorRole.Entrance,default),Is.True,"attraction building");
  Assert.That(policy.AcceptsAnchor(area,office,AreaAnchorRole.Entrance,default),Is.False,"unrelated building");
  Assert.That(policy.AcceptsAnchor(area,park,AreaAnchorRole.Headquarters,default),Is.False,"Park has no HQ role");
 }
 [Test] public void ParkEntranceRequiresRoadAccessWhenKnown() {
  var module=new ParkAreaModule(new AreaAnchorService(new Store(),e=>true,(a,e,r)=>true),(e,_)=>new ParkFacilityTraits{IsBuilding=true,HasPark=true,HasRoadAccess=false});
  Assert.That(((IAreaAnchorPolicy)module).AcceptsAnchor(area,park,AreaAnchorRole.Entrance,default),Is.False);
 }
 [Test] public void ParkPolicyFailureRejectsInsteadOfThrowing() {
  var module=new ParkAreaModule(new AreaAnchorService(new Store(),e=>true,(a,e,r)=>true),(e,_)=>throw new InvalidOperationException("reader"));
  Assert.That(((IAreaAnchorPolicy)module).AcceptsAnchor(area,park,AreaAnchorRole.Entrance,default),Is.False);
 }
 [Test] public void IndustrialRejectsOrdinaryOfficesAtAssignment() {
  IAreaAnchorPolicy industrial=new IndustrialAreaModule(new AreaAnchorService(new Store(),e=>true,(a,e,r)=>true),
   (e,_)=>new IndustrialFacilityTraits{IsBuilding=true,HasOfficeProperty=true,HasOfficeCompany=true,HasWorkplaces=true});
  Assert.That(industrial.AcceptsAnchor(area,office,AreaAnchorRole.Headquarters,default),Is.False);
 }
 [Test] public void AssignmentAndExistingLinkUseSeparatePredicates() {
  var invalid=new HashSet<Entity>();
  var healthy=new HashSet<Entity>{hq};
  var store=new Store();
  // Assignment requires current eligibility; an accepted link only needs to stay a valid member building.
  var service=new AreaAnchorService(store,e=>e!=Entity.Null&&!invalid.Contains(e),(a,e,r)=>healthy.Contains(e),(a,e,r)=>true);
  Assert.That(service.Assign(area,office,AreaAnchorRole.Headquarters),Is.False,"stricter initial eligibility rejects an office");
  Assert.That(service.Assign(area,hq,AreaAnchorRole.Headquarters),Is.True);
  healthy.Remove(hq); // vacancy, poor profitability or disabled production
  Assert.That(service.HasValidAnchor(area,AreaAnchorRole.Headquarters),Is.True,"existing HQ survives tenant/health changes");
  Assert.That(service.GetAnchorLinks(area,AreaAnchorRole.Headquarters).Single().Valid,Is.True);
  invalid.Add(hq); // deletion, destruction or leaving the boundary (Core validity/membership)
  Assert.That(service.HasValidAnchor(area,AreaAnchorRole.Headquarters),Is.False);
  Assert.That(service.GetAnchorLinks(area,AreaAnchorRole.Headquarters).Single().Valid,Is.False);
 }
 [Test] public void LegacyCallersAndParkUseOnePredicateForAssignmentAndValidation() {
  var eligible=new HashSet<Entity>{park};
  var store=new Store();
  var service=new AreaAnchorService(store,e=>e!=Entity.Null,(a,e,r)=>eligible.Contains(e));
  Assert.That(service.Assign(area,park,AreaAnchorRole.Entrance),Is.True);
  eligible.Remove(park);
  Assert.That(service.HasValidAnchor(area,AreaAnchorRole.Entrance),Is.False,"without an existing predicate the assignment rule still validates links");
  Assert.That((object)new ParkAreaModule(service) is IAreaExistingAnchorPolicy,Is.False,"Park does not opt in");
 }
 [Test] public void MultipleEntrancesAreEnumeratedAndHeadquartersStaysSingle() {
  var invalid=new HashSet<Entity>();
  var store=new Store();
  var service=new AreaAnchorService(store,e=>e!=Entity.Null&&!invalid.Contains(e),(a,e,r)=>true);
  Assert.That(service.Assign(area,park,AreaAnchorRole.Entrance),Is.True);
  Assert.That(service.Assign(area,gate,AreaAnchorRole.Entrance),Is.True);
  Assert.That(service.Assign(area,park,AreaAnchorRole.Entrance),Is.True,"reassigning is idempotent");
  Assert.That(service.GetValidAnchors(area,AreaAnchorRole.Entrance),Is.EquivalentTo(new[]{park,gate}));
  invalid.Add(park);
  Assert.That(service.GetValidAnchors(area,AreaAnchorRole.Entrance),Is.EqualTo(new[]{gate}));
  Assert.That(service.TryGetValidAnchor(area,AreaAnchorRole.Entrance,out var first),Is.True); Assert.That(first,Is.EqualTo(gate));
  service.Assign(area,hq,AreaAnchorRole.Headquarters); service.Assign(area,office,AreaAnchorRole.Headquarters);
  Assert.That(service.GetValidAnchors(area,AreaAnchorRole.Headquarters),Is.EqualTo(new[]{office}));
 }
 [Test] public void EligibilityChecksRunInGenericThenModuleOrder() {
  var consulted=new List<string>();
  bool Run(bool area,bool building,bool role,bool member,bool accepts) {
   consulted.Clear();
   return AreaAnchorEligibility.Evaluate(()=>{consulted.Add("area");return area;},()=>{consulted.Add("building");return building;},
    ()=>{consulted.Add("role");return role;},()=>{consulted.Add("member");return member;},()=>{consulted.Add("policy");return accepts;});
  }
  Assert.That(Run(true,true,true,true,true),Is.True); Assert.That(consulted,Is.EqualTo(new[]{"area","building","role","member","policy"}));
  Assert.That(Run(true,true,true,false,true),Is.False); Assert.That(consulted,Does.Not.Contain("policy"),"module policy only sees current members");
  Assert.That(Run(true,false,true,true,true),Is.False); Assert.That(consulted,Is.EqualTo(new[]{"area","building"}));
  Assert.That(Run(true,true,true,true,false),Is.False);
 }
}
public class AreaAnchorLinkListTests {
 private sealed class Store : IAreaAnchorStore {
  public Dictionary<Entity,IReadOnlyList<AreaAnchorLink>> Values = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area,out var links)?links:Array.Empty<AreaAnchorLink>();
  public void Write(Entity area,IReadOnlyList<AreaAnchorLink> links) => Values[area]=links.ToArray();
 }
 private static readonly Entity area=new(){Index=10,Version=1}, house=new(){Index=30,Version=1}, gate=new(){Index=31,Version=1}, gone=new(){Index=32,Version=1};
 [Test] public void LinksListIncludesInvalidEntriesSoTheyCanBeShownAndRemoved() {
  var store=new Store();
  store.Values[area]=new[]{ new AreaAnchorLink{Anchor=house,Role=AreaAnchorRole.Entrance}, new AreaAnchorLink{Anchor=gate,Role=AreaAnchorRole.Entrance},
   new AreaAnchorLink{Anchor=gone,Role=AreaAnchorRole.Entrance}, new AreaAnchorLink{Anchor=gate,Role=AreaAnchorRole.Entrance} };
  var service=new AreaAnchorService(store,e=>e!=gone,(a,e,r)=>e==gate);
  var links=service.GetAnchorLinks(area,AreaAnchorRole.Entrance);
  Assert.That(links.Select(l=>l.Anchor),Is.EqualTo(new[]{house,gate,gone}),"link order, no duplicates");
  Assert.That(links.Select(l=>l.Valid),Is.EqualTo(new[]{false,true,false}));
  Assert.That(links.Select(l=>l.Exists),Is.EqualTo(new[]{true,true,false}));
  Assert.That(service.HasLinks(area,AreaAnchorRole.Entrance),Is.True);
  Assert.That(service.Remove(area,house,AreaAnchorRole.Entrance),Is.True,"an ineligible link can be removed");
  Assert.That(service.GetAnchorLinks(area,AreaAnchorRole.Entrance).Any(l=>l.Anchor==house),Is.False);
 }
 [Test] public void ParkExplainsWhyItIsSuspendedWhenOnlyIneligibleEntrancesRemain() {
  var store=new Store();
  var service=new AreaAnchorService(store,e=>true,(a,e,r)=>e==gate);
  var park=new ParkAreaModule(service);
  Assert.That(park.BuildSummary(area,default),Is.EqualTo("CS2AS.Park.Summary.EntranceRequired"));
  store.Values[area]=new[]{ new AreaAnchorLink{Anchor=house,Role=AreaAnchorRole.Entrance} };
  Assert.That(park.BuildSummary(area,default),Is.EqualTo("CS2AS.Park.Summary.EntranceInvalid"));
  store.Values[area]=new[]{ new AreaAnchorLink{Anchor=house,Role=AreaAnchorRole.Entrance}, new AreaAnchorLink{Anchor=gate,Role=AreaAnchorRole.Entrance} };
  Assert.That(park.BuildSummary(area,default),Is.EqualTo("CS2AS.Park.Summary.Active"));
 }
}
