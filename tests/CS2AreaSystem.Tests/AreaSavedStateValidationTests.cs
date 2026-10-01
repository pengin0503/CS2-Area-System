using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Persistence;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// #69: semantic validation of saved Area components after load: invalid values quarantine, read-time-clamped ones warn.
public class AreaSavedStateValidationTests {
    private static AreaSavedStateReport Progress(int level, long xp) { var r = new AreaSavedStateReport(); CoreSavedStateValidator.ValidateProgress(new ManagedAreaProgress { SchemaVersion = 1, Level = level, Experience = xp }, r); return r; }
    private static AreaSavedStateReport Budget(float budget) { var r = new AreaSavedStateReport(); CoreSavedStateValidator.ValidateSettings(new ManagedAreaSettings { SchemaVersion = 1, BudgetMultiplier = budget }, r); return r; }
    private static AreaSavedStateReport Park(ParkAreaState state) { var r = new AreaSavedStateReport(); ParkSavedStateValidator.Validate(state, r); return r; }
    private static AreaSavedStateReport Industrial(IndustrialAreaState state) { var r = new AreaSavedStateReport(); IndustrialSavedStateValidator.Validate(state, r); return r; }
    [TestCase(1, 0L)] [TestCase(5, long.MaxValue)] [TestCase(3, 12L)]
    public void ProgressInRangeIsValid(int level, long xp) { var r = Progress(level, xp); Assert.That(r.IsInvalid || r.HasWarnings, Is.False); }
    [TestCase(0, 0L)] [TestCase(6, 0L)] [TestCase(-1, 0L)] [TestCase(int.MaxValue, 0L)] [TestCase(2, -1L)] [TestCase(2, long.MinValue)]
    public void ProgressOutOfRangeIsInvalid(int level, long xp) => Assert.That(Progress(level, xp).IsInvalid, Is.True);
    [TestCase(0.5f)] [TestCase(1f)] [TestCase(1.5f)]
    public void BudgetInRangeIsValid(float budget) { var r = Budget(budget); Assert.That(r.IsInvalid || r.HasWarnings, Is.False); }
    [TestCase(0f)] [TestCase(-2f)] [TestCase(0.49f)] [TestCase(1.51f)] [TestCase(float.MaxValue)]
    public void FiniteBudgetOutsideRangeOnlyWarnsBecauseEveryReaderClampsIt(float budget) {
        var r = Budget(budget);
        Assert.That(r.IsInvalid, Is.False); Assert.That(r.HasWarnings, Is.True);
        Assert.That(ParkFinanceModel.ClampBudget(budget), Is.InRange(ParkFinanceModel.MinBudget, ParkFinanceModel.MaxBudget));
        Assert.That(CoreSavedStateValidator.MinBudget, Is.EqualTo(ParkFinanceModel.MinBudget)); Assert.That(CoreSavedStateValidator.MaxBudget, Is.EqualTo(ParkFinanceModel.MaxBudget));
    }
    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
    public void NonFiniteBudgetIsInvalid(float budget) => Assert.That(Budget(budget).IsInvalid, Is.True);

    [Test] public void DefaultAndTypicalParkStatesAreValid() {
        Assert.That(Park(ParkAreaState.CreateDefault()).Issues, Is.Empty);
        var s = ParkAreaState.CreateDefault();
        s.AdmissionFee = 60; s.LifetimeVisitors = 10; s.LifetimeAdmissionRevenue = 100; s.PassiveXpToday = 5;
        s.Satisfaction = new ParkSatisfactionState { Mean = 100, Weight = ParkSatisfactionState.MaxWeight };
        Assert.That(Park(s).Issues, Is.Empty);
    }
    [Test] public void ParkFeeOutsideRangeOnlyWarns() {
        foreach (var fee in new[] { -1, 61, int.MaxValue }) {
            var s = ParkAreaState.CreateDefault(); s.AdmissionFee = fee;
            var r = Park(s);
            Assert.That(r.IsInvalid, Is.False, fee.ToString()); Assert.That(r.HasWarnings, Is.True);
            Assert.That(ParkFinanceModel.EffectiveFee(5, fee), Is.InRange(0, ParkFinanceModel.MaxAdmissionFee));
        }
    }
    [Test] public void NegativeParkCountersAreInvalid() {
        var cases = new System.Action<ParkAreaState[]>[] {
            a => a[0].LifetimeVisitors = -1, a => a[0].LifetimeResidentVisitors = -1, a => a[0].LifetimeTouristVisitors = -1,
            a => a[0].LifetimeAdmissionRevenue = -1, a => a[0].LifetimeManagementExpense = -1, a => a[0].PassiveXpToday = -1 };
        foreach (var mutate in cases) { var s = new[] { ParkAreaState.CreateDefault() }; mutate(s); Assert.That(Park(s[0]).IsInvalid, Is.True); }
    }
    [TestCase(float.NaN, 1f)] [TestCase(50f, float.NaN)] [TestCase(float.PositiveInfinity, 1f)] [TestCase(-0.1f, 1f)] [TestCase(100.1f, 1f)]
    [TestCase(50f, -1f)] [TestCase(50f, 100.5f)]
    public void ParkSatisfactionOutsideItsBoundsIsInvalid(float mean, float weight) {
        foreach (int which in new[] { 0, 1, 2 }) {
            var s = ParkAreaState.CreateDefault();
            var bad = new ParkSatisfactionState { Mean = mean, Weight = weight };
            if (which == 0) s.Satisfaction = bad; else if (which == 1) s.ResidentSatisfaction = bad; else s.TouristSatisfaction = bad;
            Assert.That(Park(s).IsInvalid, Is.True, which.ToString());
        }
    }
    [Test] public void DefaultIndustrialStateIsValid() {
        Assert.That(Industrial(IndustrialAreaState.CreateDefault(3)).Issues, Is.Empty);
        var s = IndustrialAreaState.CreateDefault(3);
        s.LastSettledXpHour = 100; s.LastCreditedActivityHour = 100; s.ActivityWeightedSum = 3.5; s.ActivityObservedHours = 24; s.HighThroughputStreakDays = 7; s.HighThroughputLastDay = 2;
        Assert.That(Industrial(s).Issues, Is.Empty);
    }
    [Test] public void InvalidIndustrialValuesAreInvalid() {
        var cases = new System.Action<IndustrialAreaState[]>[] {
            a => a[0].LifetimePeakEmployees = -1, a => a[0].XpGrantedToday = -1, a => a[0].HighThroughputStreakDays = -1,
            a => a[0].LastSettledXpHour = -2, a => a[0].LastCreditedActivityHour = long.MinValue,
            a => a[0].ActivityWeightedSum = double.NaN, a => a[0].ActivityWeightedSum = -0.5, a => a[0].ActivityWeightedSum = double.PositiveInfinity,
            a => a[0].ActivityObservedHours = double.NaN, a => a[0].ActivityObservedHours = -1, a => a[0].ActivityObservedHours = double.NegativeInfinity };
        foreach (var mutate in cases) { var s = new[] { IndustrialAreaState.CreateDefault(1) }; mutate(s); Assert.That(Industrial(s[0]).IsInvalid, Is.True); }
    }
    [Test] public void ReportDescribesFieldsBySeverity() {
        var r = new AreaSavedStateReport();
        r.Invalid("A.B", -1); r.Warning("C.D", 2.5f);
        Assert.That(r.Describe(AreaSavedValueSeverity.Invalid), Is.EqualTo("A.B=-1"));
        Assert.That(r.Describe(AreaSavedValueSeverity.Warning), Is.EqualTo("C.D=2.5"));
        r.Clear(); Assert.That(r.IsInvalid || r.HasWarnings, Is.False); Assert.That(r.Issues, Is.Empty);
    }
}
