using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialCompanyAssignmentTests {
 static Entity E(int id,int version=1)=>new(){Index=id,Version=version};
 [Test] public void AssignmentRoundTripRemapsBothEntityReferences() {
  var io=MemorySerializer.Create();
  new IndustrialCompanyAssignment{Company=E(7),Building=E(8)}.Serialize(io.Writer);
  Assert.That(io.Data.Tokens,Is.EqualTo(new object[]{(ushort)1,E(7),E(8)}));
  io.Data.Remap=e=>E(e.Index+100);
  var value=new IndustrialCompanyAssignment();value.Deserialize(io.Reader);
  Assert.That(value.Company,Is.EqualTo(E(107)));Assert.That(value.Building,Is.EqualTo(E(108)));
 }
 [Test] public void UnknownSchemaNeverReadsOrOverwritesEntityPayload() {
  var io=MemorySerializer.Create();io.Data.Tokens.Add((ushort)2);io.Data.Tokens.Add(E(1));
  Assert.Throws<System.IO.InvalidDataException>(()=>new IndustrialCompanyAssignment().Deserialize(io.Reader));
  Assert.That(io.Data.Position,Is.EqualTo(1));
  Assert.Throws<System.IO.InvalidDataException>(()=>new IndustrialCompanyAssignment{SchemaVersion=2}.Serialize(io.Writer));
 }
 [Test] public void DuplicateClaimsUseLowestStableIdRegardlessOfOrder() {
  var index=new IndustrialCompanyAssignmentIndex();
  index.Rebuild(new[]{new IndustrialCompanyClaim(20,E(1),E(2)),new IndustrialCompanyClaim(9,E(1),E(2))});
  Assert.That(index.TryGetOwner(E(1),out var owner),Is.True);Assert.That(owner,Is.EqualTo(9UL));
  var revision=index.Revision;
  index.Rebuild(new[]{new IndustrialCompanyClaim(9,E(1),E(2)),new IndustrialCompanyClaim(20,E(1),E(2))});
  Assert.That(index.Revision,Is.EqualTo(revision),"unchanged effective index must not restart every update");
 }
 [Test] public void LoadClearDropsPreviousGenerationAndReuseCannotClaimOwner() {
  var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(new[]{new IndustrialCompanyClaim(9,E(1),E(2))});
  var revision=index.Revision;index.Clear();
  Assert.That(index.Revision,Is.Not.EqualTo(revision));Assert.That(index.TryGetOwner(E(1),out _),Is.False);
  index.Rebuild(new[]{new IndustrialCompanyClaim(8,E(1,2),E(4))});
  Assert.That(index.TryGetOwner(E(1),out _),Is.False);Assert.That(index.TryGetOwner(E(1,2),out var id),Is.True);Assert.That(id,Is.EqualTo(8UL));
 }
 [Test] public void InvalidNullClaimsNeverDisplaceValidClaim() {
  var index=new IndustrialCompanyAssignmentIndex();index.Rebuild(new[]{new IndustrialCompanyClaim(0,E(1),E(2)),new IndustrialCompanyClaim(3,Entity.Null,E(2)),new IndustrialCompanyClaim(4,E(1),Entity.Null),new IndustrialCompanyClaim(7,E(1),E(2))});
  Assert.That(index.TryGetOwner(E(1),out var id),Is.True);Assert.That(id,Is.EqualTo(7UL));
 }
 [Test] public void OwnerTransfersAndFallbackInvalidateOnlyFormerAndNewResolvedAreas() {
  var index=new IndustrialCompanyAssignmentIndex();var claim=new IndustrialCompanyClaim(7,E(1),E(100));
  index.Rebuild(new[]{claim},fallbackOwner:(_,_)=>2);
  Assert.That(index.ChangedAreas,Is.EquivalentTo(new ulong[]{2,7}));
  var primary=index.GetAreaRevision(2);var manual=index.GetAreaRevision(7);var unrelated=index.GetAreaRevision(40);
  index.Rebuild(new[]{new IndustrialCompanyClaim(9,E(1),E(100))},fallbackOwner:(_,_)=>2);
  Assert.That(index.ChangedAreas,Is.EquivalentTo(new ulong[]{7,9}));
  Assert.That(index.GetAreaRevision(2),Is.EqualTo(primary));Assert.That(index.GetAreaRevision(7),Is.Not.EqualTo(manual));Assert.That(index.GetAreaRevision(40),Is.EqualTo(unrelated));
  index.Rebuild(Array.Empty<IndustrialCompanyClaim>(),fallbackOwner:(_,_)=>20);
  Assert.That(index.ChangedAreas,Is.EquivalentTo(new ulong[]{9,20}),"moved renter falls back at its current Building");
 }
 [Test] public void LosingDuplicateRemovalAndUnchangedRebuildPreserveCoverageGenerations() {
  var index=new IndustrialCompanyAssignmentIndex();var winner=new IndustrialCompanyClaim(7,E(1),E(100));
  index.Rebuild(new[]{winner,new IndustrialCompanyClaim(9,E(1),E(100))},fallbackOwner:(_,_)=>2);
  var revision=index.Revision;var area=index.GetAreaRevision(7);
  index.Rebuild(new[]{winner},fallbackOwner:(_,_)=>2);
  Assert.That(index.ChangedAreas,Is.Empty);Assert.That(index.Revision,Is.EqualTo(revision));Assert.That(index.GetAreaRevision(7),Is.EqualTo(area));
  index.Clear();Assert.That(index.GetAreaRevision(7),Is.Zero);Assert.That(index.ChangedAreas,Is.Empty);
 }
 [Test] public void ScopedRuntimeInvalidationKeepsUnrelatedFlowAndProgressionEndpoint() {
  var store=new IndustrialRuntimeStore();var affected=store.GetOrCreate(7,E(7));var unrelated=store.GetOrCreate(40,E(40));
  foreach(var row in store.All){row.Publish(new IndustrialObservationResult{CycleId=17,HourBucket=5,Active=true});row.InputWindow.Add(0,4,0,1);row.HasFlowBaseline=true;}
  store.InvalidateOwnership(new ulong[]{7,9});
  Assert.That(affected.Paused,Is.True);Assert.That(affected.LastConsumedCycle,Is.EqualTo(17));Assert.That(affected.InputWindow.CoveredHours(4),Is.Zero);
  Assert.That(unrelated.Paused,Is.False);Assert.That(unrelated.LastConsumedCycle,Is.EqualTo(-1));Assert.That(unrelated.HasFlowBaseline,Is.True);Assert.That(unrelated.InputWindow.CoveredHours(4),Is.EqualTo(4));
 }

}
