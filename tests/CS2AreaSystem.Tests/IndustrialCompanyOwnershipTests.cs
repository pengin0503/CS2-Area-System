using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialCompanyOwnershipTests {
 static Entity E(int id)=>new(){Index=id,Version=1};
 static IndustrialCompanyEligibility Eligible=>new(){Live=true,Industrial=true,BuildingEligible=true};
 static AreaMembership[] Relations=>new[]{new AreaMembership(2,(int)AreaTypeId.Industrial,true),new AreaMembership(7,(int)AreaTypeId.Industrial,false)};
 [Test] public void ManualNonPrimaryOwnerOverridesForOnlyTheAssignedCompany() {
  var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(3))});
  var owner=IndustrialCompanyOwnership.Resolve(E(1),E(3),Relations,index,Eligible);
  Assert.That(owner.Valid,Is.True);Assert.That(owner.Manual,Is.True);Assert.That(owner.AreaStableId,Is.EqualTo(7UL));
  owner=IndustrialCompanyOwnership.Resolve(E(4),E(3),Relations,index,Eligible);
  Assert.That(owner.AreaStableId,Is.EqualTo(2UL));Assert.That(owner.Manual,Is.False);
 }
 [TestCase(true,false)] [TestCase(false,true)]
 public void OfficeOrCommercialCompanyNeverHasIndustrialOwner(bool office,bool commercial) {
  var facts=Eligible;facts.Office=office;facts.Commercial=commercial;
  var owner=IndustrialCompanyOwnership.Resolve(E(1),E(3),Relations,new IndustrialCompanyAssignmentIndex(),facts);
  Assert.That(owner.Valid,Is.False);Assert.That(owner.AreaStableId,Is.Zero);
 }
 [Test] public void MovingToAnotherBuildingInvalidatesManualEvenInsideSameArea() {
  var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(3))});
  var owner=IndustrialCompanyOwnership.Resolve(E(1),E(5),Relations,index,Eligible);
  Assert.That(owner.Manual,Is.False);Assert.That(owner.AreaStableId,Is.EqualTo(2UL));
  Assert.That(owner.InvalidReason,Is.EqualTo("Moved"));
 }
 [Test] public void RemovedGeometricRelationFallsBackToCurrentPrimary() {
  var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(new[]{new IndustrialCompanyClaim(7,E(1),E(3))});
  var owner=IndustrialCompanyOwnership.Resolve(E(1),E(3),new[]{Relations[0]},index,Eligible);
  Assert.That(owner.Manual,Is.False);Assert.That(owner.AreaStableId,Is.EqualTo(2UL));
 }
 [Test] public void DeletedIneligibleAndNoIndustrialGeometryResolveToNoOwner() {
  foreach(var facts in new[]{default(IndustrialCompanyEligibility),new IndustrialCompanyEligibility{Live=true,BuildingEligible=true}})
   Assert.That(IndustrialCompanyOwnership.Resolve(E(1),E(3),Relations,null,facts).Valid,Is.False);
  Assert.That(IndustrialCompanyOwnership.Resolve(E(1),E(3),new[]{new AreaMembership(2,(int)AreaTypeId.Park,true)},null,Eligible).Valid,Is.False);
 }
 [Test] public void InvalidLowerClaimDoesNotHideValidHigherClaimDuringRebuild() {
  var index=new IndustrialCompanyAssignmentIndex();
  index.Rebuild(new[]{new IndustrialCompanyClaim(2,E(1),E(8)),new IndustrialCompanyClaim(7,E(1),E(3))},c=>IndustrialCompanyOwnership.IsValidClaim(c,E(3),Relations,Eligible));
  var owner=IndustrialCompanyOwnership.Resolve(E(1),E(3),Relations,index,Eligible);
  Assert.That(owner.Manual,Is.True);Assert.That(owner.AreaStableId,Is.EqualTo(7UL));
 }
}
