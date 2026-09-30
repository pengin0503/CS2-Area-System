using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkTransactionLedgerTests {
 private static readonly Entity area = new() { Index = 3, Version = 1 }, citizen = new() { Index = 9, Version = 1 }, other = new() { Index = 10, Version = 1 };
 [Test] public void AdmissionResultsRecordExactlyTheCollectedAmount() {
  var ledger = new ParkTransactionLedger();
  ledger.EnqueueAdmission(new ParkAdmissionRequest { Citizen = citizen, Area = area, ParkId = 7, Fee = 20 });
  ledger.EnqueueAdmission(new ParkAdmissionRequest { Citizen = other, Area = area, ParkId = 7, Fee = 20 });
  var batch = ledger.TakeBatch();
  Assert.That(batch.Admissions.Count, Is.EqualTo(2)); Assert.That(ledger.PendingAdmissions, Is.Zero);
  var outcomes = ParkTransactionLedger.Resolve(batch, new long[] { 20, 7 }, cityApplied: true);
  Assert.That(outcomes.Admissions.Select(o => o.Collected), Is.EqualTo(new long[] { 20, 7 }));
  Assert.That(outcomes.Admissions.Select(o => o.Uncollected), Is.EqualTo(new long[] { 0, 13 }));
 }
 [Test] public void CollectedAmountsAreClampedToTheRequestedFee() {
  var ledger = new ParkTransactionLedger();
  ledger.EnqueueAdmission(new ParkAdmissionRequest { Citizen = citizen, Area = area, ParkId = 7, Fee = 20 });
  var outcomes = ParkTransactionLedger.Resolve(ledger.TakeBatch(), new long[] { 999 }, true);
  Assert.That(outcomes.Admissions.Single().Collected, Is.EqualTo(20));
  Assert.That(ParkTransactionLedger.Resolve(ledger.TakeBatch(), new long[0], true).Admissions, Is.Empty);
 }
 [Test] public void ManagementExpenseIsRecordedOnlyWhenTheCityPaid() {
  var ledger = new ParkTransactionLedger();
  ledger.EnqueueManagement(area, 7, 120); ledger.EnqueueManagement(area, 8, 30); ledger.EnqueueManagement(area, 9, 0);
  var batch = ledger.TakeBatch();
  Assert.That(batch.CityDelta, Is.EqualTo(-150), "one aggregated city-money change per batch");
  Assert.That(ParkTransactionLedger.Resolve(batch, new long[0], cityApplied: true).Management.Select(m => m.Paid), Is.EqualTo(new long[] { 120, 30 }));
  Assert.That(ParkTransactionLedger.Resolve(batch, new long[0], cityApplied: false).Management, Is.Empty, "no city entity: nothing booked");
 }
}
public class ParkVisitTargetTests {
 private static ParkFacilityTraits Playground() => new() { IsBuilding = true, HasPark = true, HasLeisureProvider = true, HasLeisureData = true, LeisureEfficiency = 40 };
 [Test] public void RelevantBuildingsMapToTheirPrimaryActivePark() {
  var relations = new[] { new AreaMembership(9, 1, false), new AreaMembership(7, 1, true) };
  Assert.That(ParkVisitTargets.TryResolve(relations, 1, id => true, Playground(), out var target), Is.True);
  Assert.That(target.ParkId, Is.EqualTo(7ul)); Assert.That(target.Facility, Is.EqualTo(ParkVisitModel.FacilityExperience(true, 40)));
  Assert.That(ParkVisitTargets.TryResolve(relations, 1, id => id != 7, Playground(), out _), Is.False, "suspended primary owns nothing");
  Assert.That(ParkVisitTargets.TryResolve(relations, 1, id => true, new ParkFacilityTraits { IsBuilding = true, CommercialLeisureMask = 1 }, out _), Is.False);
  Assert.That(ParkVisitTargets.TryResolve(new[] { new AreaMembership(3, 3, true) }, 1, id => true, Playground(), out _), Is.False, "Tourism-only building");
 }
}
