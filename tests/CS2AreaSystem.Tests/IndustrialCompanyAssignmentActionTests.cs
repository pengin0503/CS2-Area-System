using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialCompanyAssignmentActionTests {
 static Entity E(int id,int v=1)=>new(){Index=id,Version=v};
 class Store:IIndustrialCompanyAssignmentStore {
  public List<IndustrialCompanyClaim> Claims=new(){new(2,E(1),E(100)),new(2,E(2),E(100))};
  public bool CanWrite(ulong area)=>area==2||area==7;
  public bool HasClaim(ulong area,Entity company,Entity building)=>Claims.Any(c=>c.AreaId==area&&c.Company==company&&c.Building==building);
  public void Assign(ulong area,Entity company,Entity building){Claims.RemoveAll(c=>c.Company==company);Claims.Add(new(area,company,building));}
  public void Remove(ulong area,Entity company,Entity building)=>Claims.RemoveAll(c=>c.AreaId==area&&c.Company==company&&c.Building==building);
 }
 static IndustrialCompanyLive Live(Entity company)=>new(company,E(100),new(){Live=true,Industrial=true,BuildingEligible=true});
 static AreaMembershipIndex Membership(){var m=new AreaMembershipIndex();m.ReplaceMembership(E(100),new[]{new AreaMembership(2,(int)AreaTypeId.Industrial,true),new AreaMembership(7,(int)AreaTypeId.Industrial,false)});return m;}
 static IndustrialCompanyCandidateSet Candidates(IndustrialCompanyAssignmentIndex index,bool claim=false,bool invalid=false) {
  var set=new IndustrialCompanyCandidateSet();set.Begin(7,1,5,index.Revision);
  set.Record(E(1),E(100),new IndustrialCompanyOwnerResolution(2,true),"Steelworks",claim,invalid);return set;
 }
 [Test] public void AssignTransfersOnlyOneCompanyAndClearRestoresAutomaticFallback() {
  var store=new Store();var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(store.Claims);var set=Candidates(index);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",set.Rows[0].Key,7,set,index,Live,Membership(),store),Is.True);
  Assert.That(store.Claims.Single(c=>c.Company==E(1)).AreaId,Is.EqualTo(7UL));Assert.That(store.Claims.Single(c=>c.Company==E(2)).AreaId,Is.EqualTo(2UL));
  index.Rebuild(store.Claims);set=Candidates(index,true);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("clearCompanyAssignment",set.Rows[0].Key,7,set,index,Live,Membership(),store),Is.True);
  Assert.That(store.Claims.Any(c=>c.Company==E(1)),Is.False);Assert.That(store.Claims,Has.Count.EqualTo(1));
 }
 [Test] public void StaleAreaSnapshotRevisionAndReusedEntityNeverMutateClaims() {
  var store=new Store();var index=new IndustrialCompanyAssignmentIndex();var set=Candidates(index);var key=set.Rows[0].Key;
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",key,8,set,index,Live,Membership(),store),Is.False);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",key,7,set,index,c=>new IndustrialCompanyLive(E(1,2),E(100),Live(c).Eligibility),Membership(),store),Is.False);
  set.Begin(7,2,5,index.Revision);Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",key,7,set,index,Live,Membership(),store),Is.False);
  Assert.That(store.Claims,Has.Count.EqualTo(2));
 }
 [Test] public void MovedOrIneligibleOrOutsideAreaCandidateIsRejectedAtActionTime() {
  var store=new Store();var index=new IndustrialCompanyAssignmentIndex();var set=Candidates(index);
  foreach(var live in new[]{new IndustrialCompanyLive(E(1),E(101),Live(E(1)).Eligibility),new IndustrialCompanyLive(E(1),E(100),default)})
   Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",set.Rows[0].Key,7,set,index,_=>live,Membership(),store),Is.False);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",set.Rows[0].Key,7,set,index,Live,new AreaMembershipIndex(),store),Is.False);
  Assert.That(store.Claims,Has.Count.EqualTo(2));
 }
 [Test] public void AssignmentRevisionAndUnknownActionInvalidateRenderedKey() {
  var store=new Store();var index=new IndustrialCompanyAssignmentIndex();var set=Candidates(index);index.Rebuild(store.Claims);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",set.Rows[0].Key,7,set,index,Live,Membership(),store),Is.False);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("setTax",set.Rows[0].Key,7,set,index,Live,Membership(),store),Is.False);
 }
 [Test] public void CandidatesAreBoundedAndOldKeysCannotResolveAfterNewHour() {
  var set=new IndustrialCompanyCandidateSet();set.Begin(7,1,5,0);
  for(int i=1;i<=200;i++)set.Record(E(i),E(100),new IndustrialCompanyOwnerResolution(2,false),"Factory");
  Assert.That(set.Rows,Has.Count.EqualTo(64));var key=set.Rows[0].Key;
  set.Begin(7,1,6,0);set.Record(E(1),E(100),new IndustrialCompanyOwnerResolution(2,false),"Factory");
  Assert.That(set.TryResolve(key,out _),Is.False);
 }
 [Test] public void ClearingDuplicateLoserLeavesEffectiveOwnerClaimUntouched() {
  var store=new Store();store.Claims.Add(new(7,E(1),E(100)));var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(store.Claims);var set=Candidates(index,true,true);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("clearCompanyAssignment",set.Rows[0].Key,7,set,index,Live,Membership(),store),Is.True);
  Assert.That(store.Claims.Count(c=>c.Company==E(1)),Is.EqualTo(1));Assert.That(store.Claims.Single(c=>c.Company==E(1)).AreaId,Is.EqualTo(2UL));
 }
 [Test] public void OwnershipPanelDistinguishesAutomaticManualAndInvalidClaims() {
  var set=new IndustrialCompanyCandidateSet();set.Begin(7,1,5,0);
  set.Record(E(1),E(100),new IndustrialCompanyOwnerResolution(2,false),"Auto");
  set.Record(E(2),E(100),new IndustrialCompanyOwnerResolution(7,true),"Manual",true);
  set.Record(E(3),E(100),new IndustrialCompanyOwnerResolution(2,true),"Duplicate",true,true);
  var tab=IndustrialPanelBuilder.Build(new IndustrialPanelModel{StableId=7,Companies=set.Rows}).Tabs.Single(t=>t.Id=="companies");
  Assert.That(tab.Items.Where(i=>i.Id.EndsWith(".status")).Select(i=>i.ValueKey),Is.EqualTo(new[]{"CS2AS.Industrial.Company.Automatic","CS2AS.Industrial.Company.Manual","CS2AS.Industrial.Company.Invalid"}));
  Assert.That(tab.Items.Where(i=>i.Action=="assignCompanyHere").Select(i=>i.Disabled),Is.EqualTo(new[]{false,true,true}));
  Assert.That(tab.Items.Count(i=>i.Action=="clearCompanyAssignment"),Is.EqualTo(2));
 }
 [Test] public void ObservingAgainDoesNotEraseSavedClaimRemovalAndInvalidClaimsRemainVisibleAtCapacity() {
  var set=new IndustrialCompanyCandidateSet();set.Begin(7,1,5,0);
  set.Record(E(1),E(100),new IndustrialCompanyOwnerResolution(7,true),"Manual",true);
  set.Record(E(1),E(100),new IndustrialCompanyOwnerResolution(7,true),"Manual");
  Assert.That(set.Rows[0].HasClaimHere,Is.True);
  for(int i=2;i<=64;i++)set.Record(E(i),E(100),new IndustrialCompanyOwnerResolution(2,false),"Auto");
  set.Record(E(200),E(101),default,"Invalid",true,true);
  Assert.That(set.Rows,Has.Count.EqualTo(64));Assert.That(set.Rows.Any(c=>c.Company==E(200)&&c.Invalid&&c.HasClaimHere),Is.True);
 }
 [TestCase(false)] [TestCase(true)] public void NullCompanySavedClaimIsVisibleRemovableAndNeverAssignable(bool missingBuilding) {
  var building=missingBuilding?Entity.Null:E(100);var store=new Store();store.Claims.Add(new(7,Entity.Null,building));store.Claims.Add(new(2,Entity.Null,building));
  var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(store.Claims);var set=new IndustrialCompanyCandidateSet();set.Begin(7,1,5,index.Revision);
  set.Record(Entity.Null,building,default,null);Assert.That(set.Rows,Is.Empty,"ordinary Null observations are not candidates");
  set.Record(Entity.Null,building,default,null,hasClaimHere:true);
  Assert.That(set.Rows,Has.Count.EqualTo(1),"saved missing reference must remain cleanable");var row=set.Rows.Single();
  Assert.That(row.Invalid,Is.True);Assert.That(row.CanAssign(7),Is.False);
  IndustrialCompanyLive Missing(Entity company)=>new(company,Entity.Null,default);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("assignCompanyHere",row.Key,7,set,index,Missing,Membership(),store),Is.False);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("clearCompanyAssignment",row.Key,2,set,index,Missing,Membership(),store),Is.False);
  Assert.That(IndustrialCompanyAssignmentActions.TryApply("clearCompanyAssignment",row.Key,7,set,index,Missing,Membership(),store),Is.True);
  Assert.That(store.HasClaim(7,Entity.Null,building),Is.False);Assert.That(store.HasClaim(2,Entity.Null,building),Is.True);Assert.That(store.Claims.Count(c=>c.Company!=Entity.Null),Is.EqualTo(2));
 }

}
