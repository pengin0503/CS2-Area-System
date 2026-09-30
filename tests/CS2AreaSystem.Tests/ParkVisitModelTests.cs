using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkVisitModelTests {
 private const uint Grace = 2048;
 private static readonly Entity alice = new() { Index = 1, Version = 1 }, bob = new() { Index = 2, Version = 1 };
 private readonly List<ParkVisitEvent> events = new();
 private ParkVisitTracker Tracker() { events.Clear(); return new ParkVisitTracker(Grace); }
 private IEnumerable<ParkVisitEvent> Of(ParkVisitEventKind kind) => events.Where(e => e.Kind == kind);
 [Test] public void SatisfactionStartsAtANeutralLowConfidencePrior() {
  var state = new ParkSatisfactionState();
  Assert.That(state.Display, Is.EqualTo(50f)); Assert.That(state.Confidence, Is.Zero);
  state = state.Add(95f);
  Assert.That(state.Display, Is.InRange(50f, 65f), "one visit barely moves the prior");
  Assert.That(state.Confidence, Is.LessThan(0.5f));
 }
 [Test] public void SatisfactionConvergesWithCompletedVisits() {
  var state = new ParkSatisfactionState();
  for (int i = 0; i < 400; i++) state = state.Add(90f);
  Assert.That(state.Display, Is.EqualTo(90f).Within(1.5f)); Assert.That(state.Confidence, Is.GreaterThan(0.95f));
  for (int i = 0; i < 400; i++) state = state.Add(30f);
  Assert.That(state.Display, Is.EqualTo(30f).Within(3f), "rolling aggregate follows recent visits");
  Assert.That(state.Weight, Is.LessThanOrEqualTo(ParkSatisfactionState.MaxWeight));
 }
 [Test] public void VisitSampleUsesTheApprovedWeightsAndIsBounded() {
  var perfect = new ParkVisitExperience { Facility = 100, Landscape = 100, Comfort = 100, Maintenance = 100, ValueForMoney = 100, Accessibility = 100 };
  Assert.That(ParkVisitModel.ComputeVisitSample(perfect), Is.EqualTo(100f).Within(0.001));
  var landscapeOnly = new ParkVisitExperience { Landscape = 100 };
  Assert.That(ParkVisitModel.ComputeVisitSample(landscapeOnly), Is.EqualTo(20f).Within(0.001), "landscape alone cannot make a perfect visit");
  Assert.That(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { Facility = 100 }), Is.EqualTo(30f).Within(0.001));
  Assert.That(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { Comfort = 100 }), Is.EqualTo(20f).Within(0.001));
  Assert.That(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { Maintenance = 100 }), Is.EqualTo(15f).Within(0.001));
  Assert.That(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { ValueForMoney = 100 }), Is.EqualTo(10f).Within(0.001));
  Assert.That(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { Accessibility = 100 }), Is.EqualTo(5f).Within(0.001));
  Assert.That(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { Facility = 500, Landscape = -50 }), Is.InRange(0f, 100f));
 }
 [Test] public void FacilityExperiencePrefersProviderEfficiencyWithNeutralFallback() {
  Assert.That(ParkVisitModel.FacilityExperience(true, 60), Is.GreaterThan(ParkVisitModel.FacilityExperience(true, 10)));
  Assert.That(ParkVisitModel.FacilityExperience(false, 0), Is.EqualTo(ParkVisitModel.NeutralFacilityExperience));
  Assert.That(ParkVisitModel.FacilityExperience(true, 1000), Is.LessThanOrEqualTo(100f));
 }
 [Test] public void RelevantLeisureStartsASessionAndSameParkMovesContinueIt() {
  var t = Tracker();
  t.Observe(alice, 7, false, 80, 1000, events);
  t.Observe(alice, 7, false, 60, 2024, events);
  t.Observe(alice, 7, false, 70, 3048, events);
  Assert.That(Of(ParkVisitEventKind.Started).Count(), Is.EqualTo(1));
  Assert.That(Of(ParkVisitEventKind.Completed), Is.Empty);
  Assert.That(t.ActiveCount(7), Is.EqualTo(1));
 }
 [Test] public void BriefLeaveWithinGraceContinuesAndLongLeaveCompletesExactlyOnce() {
  var t = Tracker();
  t.Observe(alice, 7, false, 80, 1000, events);
  t.Observe(alice, 0, false, 0, 2024, events);   // walking between buildings
  t.Expire(2500, events);
  t.Observe(alice, 7, false, 60, 3048, events);
  Assert.That(Of(ParkVisitEventKind.Started).Count(), Is.EqualTo(1));
  t.Expire(3048 + Grace + 1, events);
  t.Expire(3048 + Grace * 3, events);
  var done = Of(ParkVisitEventKind.Completed).Single();
  Assert.That(done.ParkId, Is.EqualTo(7ul));
  Assert.That(done.Satisfaction, Is.EqualTo(70f).Within(0.01), "average experienced state of the visit");
  Assert.That(t.ActiveCount(7), Is.Zero);
 }
 [Test] public void NonParkPresenceNeverStartsAVisit() {
  var t = Tracker();
  t.Observe(bob, 0, false, 0, 1000, events);
  t.Expire(100000, events);
  Assert.That(events, Is.Empty);
 }
 [Test] public void MovingToAnotherParkCompletesTheFirstVisit() {
  var t = Tracker();
  t.Observe(alice, 7, false, 80, 1000, events);
  t.Observe(alice, 9, false, 40, 2024, events);
  Assert.That(Of(ParkVisitEventKind.Completed).Single().ParkId, Is.EqualTo(7ul));
  Assert.That(Of(ParkVisitEventKind.Started).Select(e => e.ParkId), Is.EqualTo(new ulong[] { 7, 9 }));
  Assert.That(t.ActiveCount(7), Is.Zero); Assert.That(t.ActiveCount(9), Is.EqualTo(1));
 }
 [Test] public void TouristAndResidentVisitsStaySeparate() {
  var t = Tracker();
  t.Observe(alice, 7, true, 80, 1000, events); t.Observe(bob, 7, false, 60, 1000, events);
  t.Expire(1000 + Grace + 1, events);
  var done = Of(ParkVisitEventKind.Completed).ToList();
  Assert.That(done.Count(e => e.Tourist), Is.EqualTo(1)); Assert.That(done.Count(e => !e.Tourist), Is.EqualTo(1));
  var state = ParkAreaState.CreateDefault();
  foreach (var e in done) state = ParkVisitModel.RecordCompletedVisit(state, e.Tourist, e.Satisfaction);
  Assert.That(state.LifetimeVisitors, Is.EqualTo(2)); Assert.That(state.LifetimeTouristVisitors, Is.EqualTo(1)); Assert.That(state.LifetimeResidentVisitors, Is.EqualTo(1));
  Assert.That(state.TouristSatisfaction.Mean, Is.EqualTo(80f)); Assert.That(state.ResidentSatisfaction.Mean, Is.EqualTo(60f));
  Assert.That(state.Satisfaction.Weight, Is.EqualTo(2f));
 }
 [Test] public void SameKindOverlapUsesOnlyThePrimaryPark() {
  // Two overlapping Parks (ids 7 and 9) contain the building; Core marks 7 primary.
  var relations = new[] { new CS2AreaSystem.Core.Membership.AreaMembership(9, 1, false), new CS2AreaSystem.Core.Membership.AreaMembership(7, 1, true),
   new CS2AreaSystem.Core.Membership.AreaMembership(3, 3, true) };
  Assert.That(ParkVisitModel.PrimaryPark(relations, parkTypeId: 1, isActive: id => true), Is.EqualTo(7ul));
  Assert.That(ParkVisitModel.PrimaryPark(relations, parkTypeId: 1, isActive: id => id != 7), Is.Zero, "a suspended primary is not replaced by the other Park");
  var t = Tracker();
  t.Observe(alice, ParkVisitModel.PrimaryPark(relations, 1, _ => true), false, 50, 1000, events);
  t.Expire(1000 + Grace + 1, events);
  Assert.That(Of(ParkVisitEventKind.Started).Single().ParkId, Is.EqualTo(7ul));
  Assert.That(Of(ParkVisitEventKind.Completed).Single().ParkId, Is.EqualTo(7ul));
  Assert.That(t.ActiveCount(9), Is.Zero, "one occupancy owner");
 }
 [Test] public void AdmissionCanBeAttemptedOnlyOncePerSession() {
  var t = Tracker();
  t.Observe(alice, 7, false, 50, 1000, events);
  Assert.That(Of(ParkVisitEventKind.Started).Single().FeeSatisfied, Is.False);
  Assert.That(t.TryBeginAdmission(alice, 7), Is.True);
  Assert.That(t.TryBeginAdmission(alice, 7), Is.False, "repeat frame or error cannot double-charge");
  t.Observe(alice, 7, false, 50, 2024, events);
  Assert.That(t.TryBeginAdmission(alice, 7), Is.False);
  Assert.That(t.TryBeginAdmission(bob, 7), Is.False, "no session, no charge");
 }
 [Test] public void LoadSeededVisitorsAreFeeSatisfiedUntilTheyStartANewVisit() {
  var t = Tracker();
  t.SeedPreExistingUntil(5000);
  t.Observe(alice, 7, false, 50, 1000, events);
  Assert.That(Of(ParkVisitEventKind.Started).Single().FeeSatisfied, Is.True);
  Assert.That(t.TryBeginAdmission(alice, 7), Is.False, "restored visitor is not charged again");
  t.Expire(1000 + Grace + 1, events);
  events.Clear();
  t.Observe(alice, 7, false, 50, 9000, events);
  Assert.That(Of(ParkVisitEventKind.Started).Single().FeeSatisfied, Is.False, "a genuinely new visit pays");
  Assert.That(t.TryBeginAdmission(alice, 7), Is.True);
 }
 [Test] public void RollingWindowKeepsOnlyRecentActivity() {
  var window = new ParkRollingWindow(bucketFrames: 100, buckets: 4);
  window.Add(0, 5); window.Add(150, 3); window.Add(399, 2);
  Assert.That(window.Sum(399), Is.EqualTo(10));
  Assert.That(window.Sum(450), Is.EqualTo(5), "bucket 0 expired");
  Assert.That(window.Sum(10_000), Is.Zero);
 }
}
public class ParkVisitCadenceTests {
 [Test] public void ObservationFollowsTheVanillaLeisureCadence() {
  Assert.That(ParkVisitCadence.UpdateInterval, Is.EqualTo(262144 / 4096), "same interval as vanilla LeisureSystem");
  Assert.That(ParkVisitCadence.Groups, Is.EqualTo(16));
  Assert.That(ParkVisitCadence.CycleFrames, Is.EqualTo(ParkVisitCadence.UpdateInterval * 16));
  Assert.That(ParkVisitCadence.GraceFrames, Is.GreaterThan(ParkVisitCadence.CycleFrames), "a present visitor is always re-observed before expiry");
  Assert.That(ParkVisitCadence.SeedFrames, Is.GreaterThanOrEqualTo(ParkVisitCadence.CycleFrames), "every restored visitor is seen once while seeding");
  Assert.That(ParkVisitCadence.GroupFor(0), Is.EqualTo(0u)); Assert.That(ParkVisitCadence.GroupFor(64), Is.EqualTo(1u)); Assert.That(ParkVisitCadence.GroupFor(1024), Is.EqualTo(0u));
 }
 [Test] public void NewParkComponentsStartAtLevelOneWithFullBudgetAndFreeAdmission() {
  var progress = ParkStateDefaults.Progress(); var settings = ParkStateDefaults.Settings(); var state = ParkAreaState.CreateDefault();
  Assert.That(progress.Level, Is.EqualTo(1)); Assert.That(progress.Experience, Is.Zero);
  Assert.That(settings.BudgetMultiplier, Is.EqualTo(1f)); Assert.That(state.AdmissionFee, Is.Zero);
 }
 [Test] public void CompletedVisitsUpdateRecentStatisticsForThePrimaryParkOnly() {
  var owner = new ParkRuntimeSnapshot { StableId = 7 }; var other = new ParkRuntimeSnapshot { StableId = 9 };
  ParkVisitRecorder.RecordRecent(owner, tourist: true, frame: 100);
  ParkVisitRecorder.RecordRecent(owner, tourist: false, frame: 100);
  Assert.That(owner.RecentVisitors.Sum(100), Is.EqualTo(2)); Assert.That(owner.RecentTouristVisitors.Sum(100), Is.EqualTo(1));
  Assert.That(other.RecentVisitors.Sum(100), Is.Zero);
  ParkVisitRecorder.RecordOccupancy(owner, 12); ParkVisitRecorder.RecordOccupancy(owner, 5);
  Assert.That(owner.PeakOccupancy, Is.EqualTo(12));
 }
}
public class ParkVisitSamplingReadinessTests {
 private static readonly Entity alice = new() { Index = 1, Version = 1 };
 [Test] public void NoSatisfactionSampleIsTakenBeforeTheParksFirstQualityEvaluation() {
  var snapshot = new ParkRuntimeSnapshot { StableId = 7 };
  Assert.That(snapshot.Experience.ValueForMoney, Is.EqualTo(50f), "neutral defaults, never an optimistic 100");
  Assert.That(snapshot.Experience.Comfort, Is.EqualTo(50f));
  Assert.That(ParkVisitModel.TrySample(snapshot, 80f, out _), Is.False);
  snapshot.Quality = new ParkQualitySnapshot { Experience = new ParkVisitExperience { Landscape = 60, Comfort = 90, Maintenance = 70, ValueForMoney = 20, Accessibility = 50 } };
  snapshot.Experience = snapshot.Quality.Experience;
  Assert.That(ParkVisitModel.TrySample(snapshot, 80f, out var sample), Is.True);
  Assert.That(sample, Is.EqualTo(ParkVisitModel.ComputeVisitSample(new ParkVisitExperience { Facility = 80, Landscape = 60, Comfort = 90, Maintenance = 70, ValueForMoney = 20, Accessibility = 50 })));
 }
 [Test] public void VisitsWithoutSamplesCountButDoNotMoveSatisfaction() {
  var events = new List<ParkVisitEvent>(); var t = new ParkVisitTracker(ParkVisitCadence.GraceFrames);
  t.Observe(alice, 7, false, float.NaN, 100, events);
  t.Expire(100 + ParkVisitCadence.GraceFrames + 1, events);
  var done = events.Single(e => e.Kind == ParkVisitEventKind.Completed);
  Assert.That(done.HasSatisfaction, Is.False);
  var state = ParkVisitModel.RecordCompletedVisit(ParkAreaState.CreateDefault(), done.Tourist, done.Satisfaction, done.HasSatisfaction);
  Assert.That(state.LifetimeVisitors, Is.EqualTo(1)); Assert.That(state.LifetimeResidentVisitors, Is.EqualTo(1));
  Assert.That(state.Satisfaction.Weight, Is.Zero); Assert.That(state.ResidentSatisfaction.Weight, Is.Zero);
 }
 [Test] public void UnevaluatedParksAreEvaluatedFirst() {
  var ids = new ulong[] { 1, 2, 3, 4, 5, 6 };
  var picked = ParkQualitySchedule.Pick(ids, id => id != 5 && id != 6, cursor: 0, count: 2, out var next);
  Assert.That(picked, Is.EqualTo(new ulong[] { 5, 6 }));
  Assert.That(next, Is.EqualTo(0), "round-robin cursor untouched while catching up");
  picked = ParkQualitySchedule.Pick(ids, id => true, cursor: 4, count: 4, out next);
  Assert.That(picked, Is.EqualTo(new ulong[] { 5, 6, 1, 2 })); Assert.That(next, Is.EqualTo(2));
  picked = ParkQualitySchedule.Pick(ids, id => id != 6, cursor: 0, count: 3, out next);
  Assert.That(picked, Is.EqualTo(new ulong[] { 6, 1, 2 }), "catch-up first, remaining budget round-robin");
 }
}
