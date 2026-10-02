using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialManualObservationTests {
 static Entity E(int id)=>new(){Index=id,Version=1};
 static IndustrialCompanyEligibility Eligible(IndustrialCompanyRead _)=>new(){Live=true,Industrial=true,BuildingEligible=true};
 static AreaMembershipIndex Membership(int count=1) {
  var m=new AreaMembershipIndex();for(int i=0;i<count;i++)m.ReplaceMembership(E(100+i),new[]{new AreaMembership(2,(int)AreaTypeId.Industrial,true),new AreaMembership(7,(int)AreaTypeId.Industrial,false)});return m;
 }
 [Test] public void NonPrimaryCycleIncludesOnlyExplicitBuildingsAndPreservesCorePrimary() {
  var m=Membership(2);var a=new IndustrialCompanyAssignmentIndex();a.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(100))});
  var source=new IndustrialMembershipCycleSource(m,new IndustrialStructureTracker(),a);
  Assert.That(source.PrimaryMembers(7,out var other),Is.EqualTo(new[]{E(100)}));Assert.That(other,Is.EqualTo(1));
  Assert.That(source.PrimaryMembers(2,out _),Has.Count.EqualTo(2));Assert.That(m.GetAreas(E(100))[0].AreaStableId,Is.EqualTo(2UL));
 }
 [Test] public void OverlappingReadAttributesJobsAndProductionOnlyToResolvedCompanyOwner() {
  var m=Membership();var a=new IndustrialCompanyAssignmentIndex();a.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(100))});
  var b=new IndustrialBuildingRead{Building=E(100),AreaId=7,Exists=true,Available=true,HasBuildingWorkplace=true,BuildingJobs=500,Traits=new(){IsBuilding=true,ManufacturesGoods=true}};
  var companies=new[]{new IndustrialCompanyRead{Company=E(1),Building=E(100),Producer=true,HasJobs=true,Jobs=10,Employees=8,HasActivity=true,ActualPerDay=40,CapacityPerDay=100},new IndustrialCompanyRead{Company=E(2),Building=E(100),Producer=true,HasJobs=true,Jobs=20,Employees=20,HasActivity=true,ActualPerDay=90,CapacityPerDay=100}};
  var manual=IndustrialObservationMapper.ToOwnedObservation(b,companies,m,a,Eligible);
  Assert.That(manual.Jobs,Is.EqualTo(10));Assert.That(manual.ProducerTenants,Is.EqualTo(1));Assert.That(manual.ProductiveActivity,Is.EqualTo(40));
  b.AreaId=2;b.HasBuildingWorkplace=false;
  var auto=IndustrialObservationMapper.ToOwnedObservation(b,companies,m,a,Eligible);
  Assert.That(auto.Jobs,Is.EqualTo(20));Assert.That(auto.ProducerTenants,Is.EqualTo(1));Assert.That(auto.ProductiveActivity,Is.EqualTo(90));
 }
 [Test] public void ManualOwnerGetsCompanyProfitAndActivityButBuildingEfficiencyStaysPrimary() {
  var m=Membership();var assignments=new IndustrialCompanyAssignmentIndex();assignments.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(100))});
  var building=new IndustrialBuildingRead{Building=E(100),AreaId=7,Exists=true,Available=true,HasEfficiency=true,Efficiency=0.25f,LackResourcesZero=true,Traits=new(){IsBuilding=true,ManufacturesGoods=true}};
  var companies=new[]{new IndustrialCompanyRead{Company=E(1),Building=E(100),Producer=true,HasProfit=true,ProfitIndex=100,HasActivity=true,ActualPerDay=40,CapacityPerDay=100},new IndustrialCompanyRead{Company=E(2),Building=E(100),Producer=true}};
  var manual=IndustrialObservationMapper.ToOwnedObservation(building,companies,m,assignments,Eligible);
  Assert.That(manual.Efficiency,Is.Null);Assert.That(manual.LackResourcesFactor,Is.False);
  Assert.That(manual.Profitability,Is.Not.Null);Assert.That(manual.ProductiveActivity,Is.EqualTo(40));
  building.AreaId=2;var primary=IndustrialObservationMapper.ToOwnedObservation(building,companies,m,assignments,Eligible);
  Assert.That(primary.Efficiency,Is.EqualTo(25));Assert.That(primary.LackResourcesFactor,Is.True);
 }
 [Test] public void AssignmentRevisionRestartsInFlightAndAlreadyCompletedHour() {
  var m=Membership();var a=new IndustrialCompanyAssignmentIndex();var source=new IndustrialMembershipCycleSource(m,new IndustrialStructureTracker(),a);
  var scheduler=new IndustrialObservationScheduler();var areas=new[]{new IndustrialAreaRef(E(7),7,AreaLifecycleState.Active)};
  scheduler.Sync(areas,1,0,1,source);var old=scheduler.Cycle(7).CycleId;
  a.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(100))});scheduler.Sync(areas,1,1,1,source);
  Assert.That(scheduler.Cycle(7).CycleId,Is.Not.EqualTo(old));
  var requests=new List<IndustrialReadRequest>();scheduler.NextReads(requests);foreach(var r in requests)scheduler.Complete(r,new IndustrialEntityObservation{Facility=r.Building});
  Assert.That(scheduler.TakeFinished(source,1,2,1),Has.Count.EqualTo(1));
  a.Clear();scheduler.Sync(areas,1,3,1,source);Assert.That(scheduler.Cycle(7),Is.Not.Null);
 }
 [Test] public void ManyManualBuildingsStillShareTheExisting128ReadBudgetAndCachedLists() {
  var m=Membership(300);var a=new IndustrialCompanyAssignmentIndex();a.Rebuild(Enumerable.Range(0,300).Select(i=>new IndustrialCompanyClaim(7,E(i+1000),E(i+100))));
  var source=new IndustrialMembershipCycleSource(m,new IndustrialStructureTracker(),a);var members=source.PrimaryMembers(7,out _);
  Assert.That(source.PrimaryMembers(7,out _),Is.SameAs(members));
  var scheduler=new IndustrialObservationScheduler();scheduler.Sync(new[]{new IndustrialAreaRef(E(2),2,AreaLifecycleState.Active),new IndustrialAreaRef(E(7),7,AreaLifecycleState.Active)},1,0,1,source);
  var requests=new List<IndustrialReadRequest>();scheduler.NextReads(requests);Assert.That(requests,Has.Count.EqualTo(128));
 }
 [Test] public void SameBuildingResultsForTwoAreasHaveSeparateCompanyBatchIdentity() {
  var row=new IndustrialCompanyRead{Company=E(1),Building=E(100),AreaId=2,CycleId=4};
  Assert.That(IndustrialObservationMapper.Matches(row,new IndustrialBuildingRead{Building=E(100),AreaId=7,CycleId=5}),Is.False);
  Assert.That(IndustrialObservationMapper.Matches(row,new IndustrialBuildingRead{Building=E(100),AreaId=2,CycleId=4}),Is.True);
 }
 [Test] public void OwnershipCandidateReadsExistEvenWhenPrimaryAreaIsSuspended() {
  var m=Membership();var source=new IndustrialMembershipCycleSource(m,new IndustrialStructureTracker(),new IndustrialCompanyAssignmentIndex(),true);
  var scheduler=new IndustrialObservationScheduler();scheduler.Sync(new[]{new IndustrialAreaRef(E(2),2,AreaLifecycleState.Suspended),new IndustrialAreaRef(E(7),7,AreaLifecycleState.Active)},1,0,1,source);
  var reads=new List<IndustrialReadRequest>();scheduler.NextReads(reads);
  Assert.That(reads.Select(r=>r.Building),Is.EqualTo(new[]{E(100)}));Assert.That(reads[0].AreaId,Is.EqualTo(7UL));
 }
 [Test] public void CandidateOnlyOverlapReadDoesNotInflateMemberCounts() {
  var b=new IndustrialBuildingRead{Building=E(100),AreaId=7,Exists=true,Available=true};
  var obs=IndustrialObservationMapper.ToOwnedObservation(b,Array.Empty<IndustrialCompanyRead>(),Membership(),new IndustrialCompanyAssignmentIndex(),Eligible);
  var acc=new IndustrialCycleAccumulator();IndustrialObservationAggregator.Apply(acc,obs);
  Assert.That(acc.Members,Is.Zero);Assert.That(acc.Ignored,Is.Zero);
 }
 [Test] public void OwnershipInvalidationSkipsUnconsumedOldCycleAndClearsOnlyDerivedCoverage() {
  var snapshot=new IndustrialRuntimeSnapshot();snapshot.Publish(new IndustrialObservationResult{CycleId=17,HourBucket=5,Active=true});
  snapshot.InputWindow.Add(0,4,0,1);snapshot.HasFlowBaseline=true;
  snapshot.InvalidateOwnership();
  Assert.That(snapshot.LastConsumedCycle,Is.EqualTo(17));Assert.That(snapshot.Paused,Is.True);
  Assert.That(snapshot.HasFlowBaseline,Is.False);Assert.That(snapshot.InputWindow.CoveredHours(4),Is.Zero);
  Assert.That(snapshot.Current.CycleId,Is.EqualTo(17),"last observation remains visible as stale");
 }
}
