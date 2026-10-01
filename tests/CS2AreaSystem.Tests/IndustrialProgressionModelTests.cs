using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class IndustrialProgressionModelTests {
 private const double Tol = 1e-9;
 private static IndustrialMetric M(double v) => IndustrialMetric.Of(v);
 private static readonly IndustrialMetric U = IndustrialMetric.Unavailable();
 private static IndustrialProgressInputs All(double v, int facilities = 30, int employees = 2000) => new() {
  Production = M(v), Logistics = M(v), Employment = M(v), Efficiency = M(v), Performance = M(v),
  ActiveProductiveFacilities = facilities, EmployedWorkers = employees, Fresh = true };
 private static IndustrialXpResult Xp(in IndustrialProgressInputs inputs, bool active = true, int granted = 0) => IndustrialProgressionModel.ComputeHourlyXp(inputs, active, granted);
 // ---- promotion gates
 private static IndustrialPromotionCheck Check(IndustrialPromotionResult r, string key) => r.Checklist.Single(c => c.Key == key);
 [Test] public void Level1To2Gates() {
  var t = IndustrialProgressionModel.GetTarget(1)!;
  Assert.That((t.Experience, t.Performance, t.ActiveFacilities, t.Employees), Is.EqualTo((1_000L, 45.0, 3, 50)));
  var inputs = new IndustrialProgressInputs { Performance = M(45), ActiveProductiveFacilities = 3, EmployedWorkers = 50, Fresh = true };
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(1, 1_000, inputs).CanPromote, Is.True);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(1, 999, inputs).CanPromote, Is.False);
  var low = inputs; low.Performance = M(44.99);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(1, 1_000, low).CanPromote, Is.False);
  var few = inputs; few.ActiveProductiveFacilities = 2;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(1, 1_000, few).CanPromote, Is.False);
  var workers = inputs; workers.EmployedWorkers = 49;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(1, 1_000, workers).CanPromote, Is.False);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(1, 1_000, inputs).Checklist.Select(c => c.Key), Is.EqualTo(new[] { "Experience", "Performance", "ActiveFacilities", "Employees" }));
 }
 [Test] public void Level2To3Gates() {
  var inputs = new IndustrialProgressInputs { Performance = M(55), ActiveProductiveFacilities = 8, EmployedWorkers = 200, Logistics = M(45), Fresh = true };
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(2, 5_000, inputs).CanPromote, Is.True);
  var logistics = inputs; logistics.Logistics = M(44.9);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(2, 5_000, logistics).CanPromote, Is.False);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(2, 4_999, inputs).CanPromote, Is.False);
  var r = IndustrialProgressionModel.EvaluatePromotion(2, 5_000, inputs);
  Assert.That(Check(r, "ActiveFacilities").Required, Is.EqualTo(8)); Assert.That(Check(r, "Employees").Required, Is.EqualTo(200));
  Assert.That(Check(r, "Performance").Required, Is.EqualTo(55)); Assert.That(Check(r, "Logistics").Required, Is.EqualTo(45));
 }
 [Test] public void Level3To4NeedsProductionAndACargoOrLogisticsRoute() {
  var inputs = new IndustrialProgressInputs { Performance = M(65), ActiveProductiveFacilities = 20, EmployedWorkers = 600, Production = M(60), Logistics = M(60), FunctionalCargo = true, Fresh = true };
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 15_000, inputs).CanPromote, Is.True, "functional cargo infrastructure");
  var noCargo = inputs; noCargo.FunctionalCargo = false;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 15_000, noCargo).CanPromote, Is.False);
  noCargo.Logistics = M(70);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 15_000, noCargo).CanPromote, Is.True, "Logistics >= 70 without dedicated cargo");
  var production = inputs; production.Production = M(59.9);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 15_000, production).CanPromote, Is.False);
  var counts = inputs; counts.ActiveProductiveFacilities = 19;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 15_000, counts).CanPromote, Is.False);
  counts = inputs; counts.EmployedWorkers = 599;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 15_000, counts).CanPromote, Is.False);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 14_999, inputs).CanPromote, Is.False);
 }
 [Test] public void Level4To5NeedsAnyTwoPrestigeObjectives() {
  var basis = new IndustrialProgressInputs { Performance = M(75), EmployedWorkers = 1_500, Production = M(70), Logistics = M(10), Employment = M(10), Efficiency = M(10), Fresh = true };
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 35_000, basis).CanPromote, Is.False, "no prestige objective");
  var one = basis; one.Logistics = M(80);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 35_000, one).CanPromote, Is.False, "one objective");
  var objectives = new Func<IndustrialProgressInputs, IndustrialProgressInputs>[] {
   i => { i.Logistics = M(80); return i; }, i => { i.Employment = M(85); return i; }, i => { i.Efficiency = M(80); return i; },
   i => { i.MeaningfulOutputs = 3; return i; }, i => { i.StreakDays = 7; return i; } };
  for (int a = 0; a < objectives.Length; a++)
   for (int b = a + 1; b < objectives.Length; b++) {
    var pair = objectives[b](objectives[a](basis));
    var r = IndustrialProgressionModel.EvaluatePromotion(4, 35_000, pair);
    Assert.That(r.CanPromote, Is.True, $"objectives {a}+{b}");
    Assert.That(r.PrestigeMet, Is.EqualTo(2));
   }
  var two = objectives[3](objectives[4](basis));
  var weak = two; weak.Performance = M(74.9);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 35_000, weak).CanPromote, Is.False);
  weak = two; weak.EmployedWorkers = 1_499;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 35_000, weak).CanPromote, Is.False);
  weak = two; weak.Production = M(69.9);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 35_000, weak).CanPromote, Is.False);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 34_999, two).CanPromote, Is.False);
  var streak = IndustrialProgressionModel.EvaluatePromotion(4, 35_000, two).Checklist.Single(c => c.Key == "PrestigeStreak");
  Assert.That(streak.Required, Is.EqualTo(7)); Assert.That(streak.Prestige, Is.True);
  var six = basis; six.StreakDays = 6; six.MeaningfulOutputs = 3;
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(4, 35_000, six).CanPromote, Is.False, "six days are not seven");
 }
 [Test] public void MissingPromotionMetricStaysVisiblyUnmet() {
  var inputs = new IndustrialProgressInputs { Performance = U, ActiveProductiveFacilities = 50, EmployedWorkers = 500, Fresh = true };
  var r = IndustrialProgressionModel.EvaluatePromotion(1, 10_000, inputs);
  Assert.That(r.CanPromote, Is.False);
  var check = Check(r, "Performance");
  Assert.That(check.Met, Is.False); Assert.That(check.Available, Is.False);
  var production = new IndustrialProgressInputs { Performance = M(80), ActiveProductiveFacilities = 30, EmployedWorkers = 700, Production = U, Logistics = M(90), FunctionalCargo = true, Fresh = true };
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(3, 20_000, production).CanPromote, Is.False, "the one-input XP fallback never waives a Production gate");
 }
 [Test] public void MaxLevelHasNoTargetAndLevelNeverDemotes() {
  Assert.That(IndustrialProgressionModel.GetTarget(5), Is.Null);
  Assert.That(IndustrialProgressionModel.EvaluatePromotion(5, 0, default).CanPromote, Is.False);
  var state = IndustrialAreaState.CreateDefault(0);
  var progress = new IndustrialLevelProgress { Level = 4, Experience = 40_000 };
  var step = IndustrialProgressionModel.Step(ref progress, ref state, All(0, 0, 0), new IndustrialObservationStamp { HourBucket = 10, Active = true, Fresh = true });
  Assert.That(progress.Level, Is.EqualTo(4), "poor scores never demote"); Assert.That(step.Promoted, Is.False);
  var bogus = new IndustrialLevelProgress { Level = 0, Experience = 0 };
  IndustrialProgressionModel.Step(ref bogus, ref state, All(0), new IndustrialObservationStamp { HourBucket = 11, Active = true, Fresh = true });
  Assert.That(bogus.Level, Is.EqualTo(1));
 }
 [Test] public void PromotionAppliesAtMostOneLevelPerObservation() {
  var state = IndustrialAreaState.CreateDefault(0);
  var progress = new IndustrialLevelProgress { Level = 1, Experience = 1_000_000 };
  var step = IndustrialProgressionModel.Step(ref progress, ref state, All(100, 100, 5000) with { MeaningfulOutputs = 3, StreakDays = 7, FunctionalCargo = true },
   new IndustrialObservationStamp { HourBucket = 5, Active = true, Fresh = true });
  Assert.That(step.Promoted, Is.True); Assert.That(progress.Level, Is.EqualTo(2));
 }
 // ---- XP
 [Test] public void AllAvailable100GivesTenAndADayOfSlotsCapsAt240() {
  Assert.That(Xp(All(100)).Hourly, Is.EqualTo(10));
  var state = IndustrialAreaState.CreateDefault(3);
  var progress = new IndustrialLevelProgress { Level = 1 };
  for (int h = 0; h < 24; h++) IndustrialProgressionModel.Step(ref progress, ref state, All(100), new IndustrialObservationStamp { HourBucket = 3 * 24 + h, Active = true, Fresh = true });
  Assert.That(progress.Experience, Is.EqualTo(240)); Assert.That(state.XpGrantedToday, Is.EqualTo(240));
  Assert.That(Xp(All(100), true, 240).Award, Is.Zero); Assert.That(Xp(All(100), true, 240).BlockedReason, Is.EqualTo("DailyCap"));
  Assert.That(Xp(All(100), true, 235).Award, Is.EqualTo(5), "award = min(hourly, 240 - granted)");
 }
 [Test] public void TheCapIsPerGameCalendarDayAndResetsOnlyOnDayChange() {
  var state = IndustrialAreaState.CreateDefault(3);
  var progress = new IndustrialLevelProgress { Level = 1 };
  state.XpGrantedToday = 240; state.XpDay = 3;
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), new IndustrialObservationStamp { HourBucket = 3 * 24 + 23, Active = true, Fresh = true });
  Assert.That(progress.Experience, Is.Zero, "same day, cap exhausted");
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), new IndustrialObservationStamp { HourBucket = 4 * 24, Active = true, Fresh = true });
  Assert.That(progress.Experience, Is.EqualTo(10)); Assert.That(state.XpDay, Is.EqualTo(4u)); Assert.That(state.XpGrantedToday, Is.EqualTo(10));
 }
 [Test] public void MissingProductionReweightsToSeven() {
  var inputs = new IndustrialProgressInputs { Production = U, Employment = M(60), Logistics = M(80), Performance = M(80), ActiveProductiveFacilities = 1, Fresh = true };
  var xp = Xp(inputs);
  Assert.That(xp.OperatingScore, Is.EqualTo(47.0 / 0.65).Within(Tol)); Assert.That(xp.OperatingScore, Is.EqualTo(72.307692).Within(1e-6));
  Assert.That(xp.Hourly, Is.EqualTo(7));
  var noProducer = inputs; noProducer.ActiveProductiveFacilities = 0;
  Assert.That(Xp(noProducer).Award, Is.Zero, "independent active-producer confirmation still required");
  Assert.That(Xp(noProducer).BlockedReason, Is.EqualTo("NoOperatingProducer"));
 }
 [Test] public void EachSingleMissingInputRenormalizesTheRemainingOriginalWeights() {
  var full = new IndustrialProgressInputs { Production = M(90), Employment = M(70), Logistics = M(50), Performance = M(30), ActiveProductiveFacilities = 1, Fresh = true };
  var cases = new (Func<IndustrialProgressInputs, IndustrialProgressInputs> Drop, double Expected)[] {
   (i => { i.Production = U; return i; }, (0.25 * 70 + 0.2 * 50 + 0.2 * 30) / 0.65),
   (i => { i.Employment = U; return i; }, (0.35 * 90 + 0.2 * 50 + 0.2 * 30) / 0.75),
   (i => { i.Logistics = U; return i; }, (0.35 * 90 + 0.25 * 70 + 0.2 * 30) / 0.8),
   (i => { i.Performance = U; return i; }, (0.35 * 90 + 0.25 * 70 + 0.2 * 50) / 0.8) };
  foreach (var (drop, expected) in cases) Assert.That(Xp(drop(full)).OperatingScore, Is.EqualTo(expected).Within(Tol));
  Assert.That(Xp(full).OperatingScore, Is.EqualTo(0.35 * 90 + 0.25 * 70 + 0.2 * 50 + 0.2 * 30).Within(Tol));
 }
 [Test] public void TwoMissingInputsStaleSuspendedOrNoProducerGiveZero() {
  var two = new IndustrialProgressInputs { Production = U, Logistics = U, Employment = M(100), Performance = M(100), ActiveProductiveFacilities = 3, Fresh = true };
  Assert.That(Xp(two).Award, Is.Zero); Assert.That(Xp(two).BlockedReason, Is.EqualTo("InsufficientInputs")); Assert.That(Xp(two).OperatingScore, Is.Null);
  var stale = All(100); stale.Fresh = false;
  Assert.That(Xp(stale).Award, Is.Zero); Assert.That(Xp(stale).BlockedReason, Is.EqualTo("Stale"));
  Assert.That(Xp(All(100), active: false).Award, Is.Zero); Assert.That(Xp(All(100), active: false).BlockedReason, Is.EqualTo("Suspended"));
  var none = All(100); none.ActiveProductiveFacilities = 0;
  Assert.That(Xp(none).Award, Is.Zero);
 }
 [Test] public void SameHourAfterReloadOrRetryAwardsNothingAndTheNextHourCanEarn() {
  var state = IndustrialAreaState.CreateDefault(0);
  var progress = new IndustrialLevelProgress { Level = 1 };
  var stamp = new IndustrialObservationStamp { HourBucket = 7, Active = true, Fresh = true };
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), stamp);
  state = Support.MemorySerializer.RoundTrip(state); // save/load in the same hour
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), stamp);
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), stamp); // observation retry / HQ reassignment
  Assert.That(progress.Experience, Is.EqualTo(10)); Assert.That(state.LastSettledXpHour, Is.EqualTo(7));
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), stamp with { HourBucket = 8 });
  Assert.That(progress.Experience, Is.EqualTo(20));
  Assert.That(state.XpGrantedToday, Is.EqualTo(20));
 }
 [Test] public void MissingHoursAreNotBackfilled() {
  var state = IndustrialAreaState.CreateDefault(0);
  var progress = new IndustrialLevelProgress { Level = 1 };
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), new IndustrialObservationStamp { HourBucket = 1, Active = true, Fresh = true });
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), new IndustrialObservationStamp { HourBucket = 9, Active = true, Fresh = true });
  Assert.That(progress.Experience, Is.EqualTo(20), "hours 2..8 earn nothing");
 }
 [Test] public void AllZeroGivesZeroAndRoundingIsAwayFromZero() {
  Assert.That(Xp(All(0)).Hourly, Is.Zero); Assert.That(Xp(All(0)).Award, Is.Zero);
  Assert.That(Xp(All(45)).Hourly, Is.EqualTo(5), "4.5 rounds away from zero");
  Assert.That(Xp(All(44.99)).Hourly, Is.EqualTo(4));
  Assert.That(Xp(All(4)).Hourly, Is.Zero, "no unconditional minimum");
 }
 [Test] public void HealthySmallAndLargeEarnIdenticalXp() {
  Assert.That(Xp(All(90, 3, 60)).Award, Is.EqualTo(Xp(All(90, 300, 60_000)).Award), "no Area-size multiplier");
 }
 [Test] public void ExperienceSaturatesAtLongMaxValue() {
  var state = IndustrialAreaState.CreateDefault(0);
  var progress = new IndustrialLevelProgress { Level = 5, Experience = long.MaxValue - 3 };
  IndustrialProgressionModel.Step(ref progress, ref state, All(100), new IndustrialObservationStamp { HourBucket = 1, Active = true, Fresh = true });
  Assert.That(progress.Experience, Is.EqualTo(long.MaxValue));
 }
 [Test] public void PeakEmployeesUpdateOnlyFromActiveObservations() {
  var state = IndustrialAreaState.CreateDefault(0);
  var progress = new IndustrialLevelProgress { Level = 1 };
  IndustrialProgressionModel.Step(ref progress, ref state, All(50, 3, 400), new IndustrialObservationStamp { HourBucket = 1, Active = true, Fresh = true });
  IndustrialProgressionModel.Step(ref progress, ref state, All(50, 3, 900), new IndustrialObservationStamp { HourBucket = 2, Active = false, Fresh = true });
  IndustrialProgressionModel.Step(ref progress, ref state, All(50, 3, 100), new IndustrialObservationStamp { HourBucket = 3, Active = true, Fresh = true });
  Assert.That(state.LifetimePeakEmployees, Is.EqualTo(400));
 }
 // ---- daily productive-activity streak
 private static IndustrialDailyActivityObservation A(double hours, double activity, ulong generation = 1, bool valid = true) =>
  new() { Hours = hours, HourBucket = (long)Math.Floor(hours), Activity = activity, Generation = generation, Valid = valid };
 private static void Feed(ref IndustrialAreaState state, ref IndustrialActivitySample? baseline, IEnumerable<IndustrialDailyActivityObservation> samples) {
  foreach (var s in samples) IndustrialProgressionModel.AccumulateDailyActivity(s, ref state, ref baseline);
 }
 private static IEnumerable<IndustrialDailyActivityObservation> Hourly(double from, double to, Func<double, double> activity, double step = 1) {
  for (double h = from; h <= to + 1e-9; h += step) yield return A(h, activity(h));
 }
 [Test] public void BracketedIntervalsUseTheEarlierValueAndTheFirstSampleAddsNoCoverage() {
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  IndustrialProgressionModel.AccumulateDailyActivity(A(1, 100), ref state, ref baseline);
  Assert.That(state.ActivityObservedHours, Is.Zero);
  IndustrialProgressionModel.AccumulateDailyActivity(A(2, 0), ref state, ref baseline);
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1).Within(Tol)); Assert.That(state.ActivityWeightedSum, Is.EqualTo(100).Within(Tol), "earlier sample value");
  IndustrialProgressionModel.AccumulateDailyActivity(A(2.5, 50), ref state, ref baseline);
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1.5).Within(Tol)); Assert.That(state.ActivityWeightedSum, Is.EqualTo(100).Within(Tol));
  Assert.That(state.LastCreditedActivityHour, Is.EqualTo(2));
 }
 [Test] public void LongGapsUnavailableAndOtherGenerationsAddNoCoverage() {
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  Feed(ref state, ref baseline, new[] { A(1, 90), A(2.5, 90) });
  Assert.That(state.ActivityObservedHours, Is.Zero, "1.5 h gap");
  Feed(ref state, ref baseline, new[] { A(3.5, 90), A(4.5, 90, valid: false), A(5.5, 90) });
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1).Within(Tol), "unavailable/stale/suspended breaks the bracket");
  Feed(ref state, ref baseline, new[] { A(6.5, 90, generation: 2) });
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1).Within(Tol), "invalidated generation adds no coverage");
  Feed(ref state, ref baseline, new[] { A(7.5, 90, generation: 2) });
  Assert.That(state.ActivityObservedHours, Is.EqualTo(2).Within(Tol), "earned coverage preserved, new endpoints resume");
 }
 [Test] public void IntervalSpanningMidnightIsSplitBetweenDays() {
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  Feed(ref state, ref baseline, new[] { A(23.5, 80), A(24.25, 60) });
  Assert.That(state.ActivityDay, Is.EqualTo(1u));
  Assert.That(state.ActivityObservedHours, Is.EqualTo(0.25).Within(Tol)); Assert.That(state.ActivityWeightedSum, Is.EqualTo(80 * 0.25).Within(Tol));
  Assert.That(state.HighThroughputLastDay, Is.EqualTo(0u), "day 0 completed once when the next day started");
 }
 [Test] public void ExactlyEighteenHoursAtEightyQualifiesButLessDoesNot() {
  Assert.That(IndustrialProgressionModel.Qualifies(80 * 18, 18), Is.True);
  Assert.That(IndustrialProgressionModel.Qualifies(80 * 17.99, 17.99), Is.False, "17.99 covered hours");
  Assert.That(IndustrialProgressionModel.Qualifies(79.99 * 24, 24), Is.False, "mean 79.99");
  Assert.That(IndustrialProgressionModel.Qualifies(0, 0), Is.False);
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  Feed(ref state, ref baseline, Hourly(3, 21, h => 80));
  Assert.That(state.ActivityObservedHours, Is.EqualTo(18).Within(Tol));
  IndustrialProgressionModel.AdvanceToDay(1, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.EqualTo(1));
  state = IndustrialAreaState.CreateDefault(0); baseline = null;
  Feed(ref state, ref baseline, Hourly(3, 20.99, h => 100, step: 0.99));
  IndustrialProgressionModel.AdvanceToDay(1, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.Zero, "17.82 covered hours");
 }
 [Test] public void BriefSpikeCannotQualifyAndSampleDensityDoesNotChangeTheMean() {
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  Feed(ref state, ref baseline, Hourly(0, 23, h => h < 2 ? 100 : 40));
  IndustrialProgressionModel.AdvanceToDay(1, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.Zero);
  var coarse = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? b1 = null;
  Feed(ref coarse, ref b1, Hourly(0, 20, h => h < 10 ? 90 : 70));
  var dense = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? b2 = null;
  Feed(ref dense, ref b2, Hourly(0, 20, h => h < 10 ? 90 : 70, step: 0.25));
  Assert.That(dense.ActivityWeightedSum / dense.ActivityObservedHours, Is.EqualTo(coarse.ActivityWeightedSum / coarse.ActivityObservedHours).Within(1e-9));
  Assert.That(dense.ActivityObservedHours, Is.EqualTo(coarse.ActivityObservedHours).Within(1e-9));
 }
 [Test] public void SevenConsecutiveQualifyingDaysSatisfyAndAMissedDayResets() {
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  for (int d = 0; d < 7; d++) { baseline = null; Feed(ref state, ref baseline, Hourly(d * 24 + 2, d * 24 + 22, h => 95)); }
  IndustrialProgressionModel.AdvanceToDay(7, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.EqualTo(7));
  IndustrialProgressionModel.AdvanceToDay(7, ref state);
  IndustrialProgressionModel.FinalizeActivityDay(6, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.EqualTo(7), "each day is evaluated once");
  IndustrialProgressionModel.AdvanceToDay(9, ref state); // day 7 and 8 without coverage
  Assert.That(state.HighThroughputStreakDays, Is.Zero); Assert.That(state.HighThroughputLastDay, Is.EqualTo(8u));
 }
 [Test] public void UnqualifiedDayResetsAndSkippedEvaluationBreaksTheStreak() {
  var state = IndustrialAreaState.CreateDefault(0);
  IndustrialProgressionModel.UpdateHighThroughputStreak(0, true, ref state);
  IndustrialProgressionModel.UpdateHighThroughputStreak(1, true, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.EqualTo(2));
  IndustrialProgressionModel.UpdateHighThroughputStreak(3, true, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.EqualTo(1), "day 2 skipped");
  IndustrialProgressionModel.UpdateHighThroughputStreak(4, false, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.Zero);
  IndustrialProgressionModel.UpdateHighThroughputStreak(4, true, ref state);
  Assert.That(state.HighThroughputStreakDays, Is.Zero, "repeated evaluation of one day never changes it");
 }
 [Test] public void SameHourReloadCannotDuplicateCoverageAndLoadGapIsNotBridged() {
  var state = IndustrialAreaState.CreateDefault(0); IndustrialActivitySample? baseline = null;
  Feed(ref state, ref baseline, new[] { A(5, 90), A(6, 90) });
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1).Within(Tol));
  state = Support.MemorySerializer.RoundTrip(state); baseline = null; // load: interpolation baseline discarded
  Feed(ref state, ref baseline, new[] { A(6.4, 90) });
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1).Within(Tol), "first post-load sample is an endpoint only");
  IndustrialProgressionModel.AccumulateDailyActivity(A(6.4, 90), ref state, ref baseline);
  Assert.That(state.ActivityObservedHours, Is.EqualTo(1).Within(Tol), "a repeated endpoint adds nothing");
  Feed(ref state, ref baseline, new[] { A(7.4, 90) });
  Assert.That(state.ActivityObservedHours, Is.EqualTo(2).Within(Tol));
 }
 [Test] public void DayZeroAndMultiDayJumpsAreHandledWithoutSentinelCollisions() {
  var state = IndustrialAreaState.CreateDefault(0);
  Assert.That(state.HighThroughputLastDay, Is.EqualTo(IndustrialAreaState.NoDay));
  IndustrialProgressionModel.AdvanceToDay(1, ref state);
  Assert.That(state.HighThroughputLastDay, Is.EqualTo(0u), "day zero is a real completed day");
  IndustrialProgressionModel.AdvanceToDay(100_000, ref state);
  Assert.That(state.HighThroughputLastDay, Is.EqualTo(99_999u)); Assert.That(state.ActivityDay, Is.EqualTo(100_000u));
  Assert.That(state.ActivityObservedHours, Is.Zero); Assert.That(state.HighThroughputStreakDays, Is.Zero);
 }
}
