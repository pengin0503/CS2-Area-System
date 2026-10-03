using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 2 C4 Task 2: the support roster comes from the existing bounded observation cycle; one request per Area/day is
// queued only from complete, current evidence, and the day is committed only after Area Finance accepts the request.
public class IndustrialSupportSystemTests {
 static Entity E(int id) => new() { Index = id, Version = 1 };
 static IndustrialCompanyEligibility Eligible(IndustrialCompanyRead _) => new() { Live = true, Industrial = true, BuildingEligible = true };
 static AreaMembershipIndex Membership(params int[] buildings) {
  var m = new AreaMembershipIndex();
  foreach (var b in buildings) m.ReplaceMembership(E(b), new[] { new AreaMembership(2, (int)AreaTypeId.Industrial, true), new AreaMembership(7, (int)AreaTypeId.Industrial, false) });
  return m;
 }
 static IndustrialBuildingRead Building(int id, ulong area) => new() { Building = E(id), AreaId = area, Exists = true, Available = true, Traits = new() { IsBuilding = true, ManufacturesGoods = true } };
 static IndustrialCompanyRead Company(int id, int building) => new() { Company = E(id), Building = E(building), Producer = true };
 static IndustrialObservationResult Publish(IndustrialCycleAccumulator acc, ulong membership = 11, ulong structure = 22, bool active = true) =>
  IndustrialObservationAggregator.Finalize(acc, new IndustrialRuntimeSnapshot(), new IndustrialCycleInfo { CycleId = 1, HourBucket = 1, StartHours = 1, EndHours = 1.2,
   MembershipToken = membership, StructureToken = structure, Active = active });

 [Test] public void OwnedObservationCarriesOnlyCompaniesResolvedToTheReadArea() {
  var m = Membership(100);
  var assignments = new IndustrialCompanyAssignmentIndex(); assignments.Rebuild(new[] { new IndustrialCompanyClaim(7, E(1), E(100)) });
  var companies = new[] { Company(1, 100), Company(2, 100) };
  Assert.That(IndustrialObservationMapper.ToOwnedObservation(Building(100, 7), companies, m, assignments, Eligible).OwnedCompanies, Is.EqualTo(new[] { E(1) }), "manual owner");
  Assert.That(IndustrialObservationMapper.ToOwnedObservation(Building(100, 2), companies, m, assignments, Eligible).OwnedCompanies, Is.EqualTo(new[] { E(2) }), "geometric owner keeps the rest");
 }
 [Test] public void CandidateOnlyReadsCarryNoReceivers() {
  var obs = IndustrialObservationMapper.ToOwnedObservation(Building(100, 7), Array.Empty<IndustrialCompanyRead>(), Membership(100), new IndustrialCompanyAssignmentIndex(), Eligible);
  Assert.That(obs.CandidateOnly, Is.True);
  Assert.That(obs.OwnedCompanies, Is.Empty);
 }
 [Test] public void RosterCoversEveryOwnedCompanyBeyondTheUiCandidateLimitDeduplicatedAndSorted() {
  var acc = new IndustrialCycleAccumulator();
  var m = Membership(Enumerable.Range(100, 40).ToArray());
  var assignments = new IndustrialCompanyAssignmentIndex();
  // 40 buildings × 3 companies = 120 receivers, read in descending order and with one building read twice.
  foreach (var b in Enumerable.Range(100, 40).Reverse().Append(120)) {
   var companies = new[] { Company(b * 10 + 2, b), Company(b * 10 + 1, b), Company(b * 10, b) };
   IndustrialObservationAggregator.Apply(acc, IndustrialObservationMapper.ToOwnedObservation(Building(b, 2), companies, m, assignments, Eligible));
  }
  var result = Publish(acc);
  Assert.That(result.SupportRoster, Has.Count.EqualTo(120));
  Assert.That(result.SupportRoster.Select(e => e.Index), Is.Ordered);
  Assert.That(result.SupportRoster.Distinct().Count(), Is.EqualTo(120));
  Assert.That(result.SupportRosterComplete, Is.True);
 }
 [Test] public void ACycleWithAFailedReadPublishesAnIncompleteRoster() {
  var acc = new IndustrialCycleAccumulator();
  acc.FailedReads = 1;
  Assert.That(Publish(acc).SupportRosterComplete, Is.False);
 }

 static IndustrialSupportScheduleInputs Inputs(IndustrialObservationResult roster, int setting = 1000, long day = 5, bool active = true, bool outstanding = false,
  ulong membership = 11, ulong structure = 22, bool paused = false, Func<Entity, bool> owned = null) {
  var state = IndustrialSupportState.CreateDefault(); state.DailySupportPerCompany = setting;
  return new IndustrialSupportScheduleInputs { Active = active, State = state, Day = day, HasOutstanding = outstanding, Roster = roster,
   MembershipToken = membership, StructureToken = structure, SnapshotPaused = paused, StillOwned = owned ?? (_ => true) };
 }
 static IndustrialObservationResult Roster(params int[] companies) {
  var acc = new IndustrialCycleAccumulator();
  var m = Membership(100); var assignments = new IndustrialCompanyAssignmentIndex();
  IndustrialObservationAggregator.Apply(acc, IndustrialObservationMapper.ToOwnedObservation(Building(100, 2), companies.Select(c => Company(c, 100)).ToArray(), m, assignments, Eligible));
  return Publish(acc);
 }
 [Test] public void CompleteCurrentRosterRequestsReceiversTimesTheSnapshottedSetting() {
  var decision = IndustrialSupportScheduling.Decide(Inputs(Roster(3, 1, 2)));
  Assert.That(decision.Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Request));
  Assert.That(decision.Receivers, Is.EqualTo(new[] { E(1), E(2), E(3) }));
  Assert.That(decision.PerCompany, Is.EqualTo(1000));
  Assert.That(decision.Requested, Is.EqualTo(3000));
 }
 [Test] public void NothingIsQueuedWithoutAPositiveSettingAnActiveAreaOrBeforeTheNextDay() {
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1), setting: 0)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Disabled));
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1), active: false)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Inactive));
  var inputs = Inputs(Roster(1)); inputs.State = IndustrialSupportModel.MarkScheduled(inputs.State, 5);
  Assert.That(IndustrialSupportScheduling.Decide(inputs).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.AlreadyScheduled), "same-day load keeps the saved day");
  inputs.Day = 6;
  Assert.That(IndustrialSupportScheduling.Decide(inputs).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Request));
 }
 [Test] public void AnOutstandingRequestBlocksTheNextDay() =>
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1), outstanding: true)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Outstanding));
 [Test] public void MissingPartialOrStaleEvidenceQueuesNothing() {
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(null)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.NoEvidence));
  var partial = Roster(1); partial.FailedReads = 1;
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(partial)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Partial));
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1), membership: 12)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Stale));
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1), structure: 23)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Stale), "an assignment revision changes the structure token");
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1), paused: true)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Stale));
  var inactiveCycle = Roster(1); inactiveCycle.Active = false;
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(inactiveCycle)).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Stale));
 }
 [Test] public void AReceiverNoLongerResolvedHereMakesTheRosterStale() =>
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster(1, 2), owned: c => c != E(2))).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.Stale),
   "B2 owner changes are followed by the next roster, never paid to the old owner");
 [Test] public void AnEmptyRosterQueuesNothing() =>
  Assert.That(IndustrialSupportScheduling.Decide(Inputs(Roster())).Outcome, Is.EqualTo(IndustrialSupportScheduleOutcome.NoReceivers));

 [Test] public void TheDayIsCommittedOnlyAfterAreaFinanceAcceptsTheRequest() {
  var ledger = new IndustrialSupportLedger();
  var decision = IndustrialSupportScheduling.Decide(Inputs(Roster(1, 2)));
  var state = Inputs(Roster(1)).State;
  Assert.That(IndustrialSupportScheduling.TryRequest(decision, 3, 9, E(9), 5, _ => false, ref state, ledger), Is.False);
  Assert.That(IndustrialSupportModel.AlreadyScheduled(state, 5), Is.False, "a rejected enqueue may be retried later the same day");
  Assert.That(ledger.HasOutstanding(9), Is.False);
  long queued = 0;
  Assert.That(IndustrialSupportScheduling.TryRequest(decision, 3, 9, E(9), 5, amount => { queued = amount; return true; }, ref state, ledger), Is.True);
  Assert.That(queued, Is.EqualTo(2000));
  Assert.That(IndustrialSupportModel.AlreadyScheduled(state, 5), Is.True);
  Assert.That(ledger.HasOutstanding(9), Is.True);
  Assert.That(ledger.Get(9).Receivers, Is.EqualTo(new[] { E(1), E(2) }));
  Assert.That(ledger.Get(9).PerCompany, Is.EqualTo(1000));
  Assert.That(ledger.Get(9).Generation, Is.EqualTo(3));
 }
 [Test] public void AtMostOneOutstandingRequestPerArea() {
  var ledger = new IndustrialSupportLedger();
  Assert.That(ledger.TryBegin(1, 9, E(9), 1000, new[] { E(1) }, 1000), Is.True);
  Assert.That(ledger.TryBegin(1, 9, E(9), 1000, new[] { E(1) }, 1000), Is.False);
  Assert.That(ledger.TryBegin(1, 10, E(10), 1000, new[] { E(1) }, 1000), Is.True, "other Areas are independent");
  ledger.Clear();
  Assert.That(ledger.HasOutstanding(9), Is.False);
 }
}
public class IndustrialSupportSavedStateTests {
 static CS2AreaSystem.Core.Persistence.AreaSavedStateReport Validate(IndustrialSupportState state) {
  var r = new CS2AreaSystem.Core.Persistence.AreaSavedStateReport(); IndustrialSavedStateValidator.ValidateSupport(state, r); return r;
 }
 [Test] public void DefaultAndScheduledStatesAreValid() {
  Assert.That(Validate(IndustrialSupportState.CreateDefault()).IsInvalid, Is.False);
  var state = IndustrialSupportModel.MarkScheduled(IndustrialSupportState.CreateDefault(), 0);
  state = IndustrialSupportModel.RecordCompletion(state, 100, 80, 60);
  Assert.That(Validate(state).IsInvalid, Is.False);
 }
 [Test] public void ImpossibleCountersOrDaysAreInvalid() {
  var state = IndustrialSupportState.CreateDefault(); state.LifetimeCredited = -1;
  Assert.That(Validate(state).IsInvalid, Is.True);
  state = IndustrialSupportState.CreateDefault(); state.LastScheduledDay = -2;
  Assert.That(Validate(state).IsInvalid, Is.True);
  state = IndustrialSupportState.CreateDefault(); state.LifetimeSettled = 10; state.LifetimeCredited = 4; state.LifetimeUnallocated = 5;
  Assert.That(Validate(state).IsInvalid, Is.True, "settled = credited + unallocated");
 }
}
public class IndustrialSupportSourceGuardTests {
 internal static string Source(string path) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/" + path));
 [Test] public void SupportSchedulingUsesTheObservationRosterAndNoWorldQuery() {
  var system = Source("Modules/Industrial/IndustrialSupportSystem.cs");
  Assert.That(system, Does.Not.Contain("GetEntityQuery"), "no second whole-world company scan");
  Assert.That(system, Does.Not.Contain("OwnershipCandidates"), "the 64-row UI candidate list is not a payment roster");
  Assert.That(system, Does.Contain("Roster = snapshot?.Current"));
  Assert.That(system, Does.Contain("IndustrialSupportScheduling.TryRequest("), "the day is committed only through the accepted-enqueue path");
  Assert.That(system.Split("LastScheduledDay").Length - 1, Is.Zero, "the system never writes the scheduled day directly");
 }
}
