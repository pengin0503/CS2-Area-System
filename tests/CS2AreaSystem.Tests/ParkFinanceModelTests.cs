using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkFinanceModelTests {
 [Test] public void AdmissionIsLockedAtLevelOneAndAvailableFromLevelTwo() {
  Assert.That(ParkFinanceModel.IsAdmissionUnlocked(1), Is.False); Assert.That(ParkFinanceModel.IsAdmissionUnlocked(2), Is.True);
  Assert.That(ParkFinanceModel.EffectiveFee(1, 30), Is.Zero, "a stored fee is never collected at Level 1");
  Assert.That(ParkFinanceModel.EffectiveFee(2, 30), Is.EqualTo(30));
  Assert.That(ParkFinanceModel.ClampFee(2, -5), Is.Zero); Assert.That(ParkFinanceModel.ClampFee(2, 10_000), Is.EqualTo(ParkFinanceModel.MaxAdmissionFee));
  Assert.That(ParkFinanceModel.TryParseFee(1, "20", out _), Is.False, "Level 1 fee control is unavailable");
  Assert.That(ParkFinanceModel.TryParseFee(2, "20", out var fee), Is.True); Assert.That(fee, Is.EqualTo(20));
  Assert.That(ParkFinanceModel.TryParseFee(2, "abc", out _), Is.False);
 }
 [TestCase(20, 100, 20)] [TestCase(20, 20, 20)] [TestCase(20, 7, 7)] [TestCase(20, 0, 0)] [TestCase(20, -50, 0)] [TestCase(0, 100, 0)] [TestCase(-3, 100, 0)]
 public void CollectibleAdmissionNeverDrivesMoneyBelowZero(long fee, long available, long expected) {
  long collected = ParkFinanceModel.ComputeCollectibleAdmission(fee, available);
  Assert.That(collected, Is.EqualTo(expected));
  Assert.That(Math.Max(0, available) - collected, Is.GreaterThanOrEqualTo(0));
 }
 [Test] public void ValueForMoneyIsNeutralWhenFreeAndDegradesFasterForMediocreParks() {
  Assert.That(ParkFinanceModel.ComputeValueForMoney(0, 10), Is.EqualTo(100));
  Assert.That(ParkFinanceModel.ComputeValueForMoney(40, 50), Is.LessThan(ParkFinanceModel.ComputeValueForMoney(10, 50)));
  Assert.That(ParkFinanceModel.ComputeValueForMoney(30, 90), Is.GreaterThan(ParkFinanceModel.ComputeValueForMoney(30, 40)), "high quality tolerates a higher fee");
  Assert.That(ParkFinanceModel.ComputeValueForMoney(ParkFinanceModel.MaxAdmissionFee, 0), Is.GreaterThanOrEqualTo(0));
 }
 [Test] public void BudgetUsesTheServiceStyleRangeAndScalesManagementCost() {
  Assert.That(ParkFinanceModel.MinBudget, Is.EqualTo(0.5f)); Assert.That(ParkFinanceModel.MaxBudget, Is.EqualTo(1.5f));
  Assert.That(ParkFinanceModel.ClampBudget(3f), Is.EqualTo(1.5f)); Assert.That(ParkFinanceModel.ClampBudget(0.1f), Is.EqualTo(0.5f));
  Assert.That(ParkFinanceModel.ClampBudget(0f), Is.EqualTo(1f), "missing/zero settings mean the default 100%");
  Assert.That(ParkFinanceModel.ClampBudget(float.NaN), Is.EqualTo(1f));
  var need = ParkFinanceModel.ComputeManagementNeedPerDay(new ParkManagementInputs { AreaSquareMeters = 10_000, VisitorsPerDay = 100, LandscapeObjects = 200, ManagedFacilities = 3 });
  Assert.That(need, Is.GreaterThan(0));
  Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(need, 1.5f), Is.GreaterThan(ParkFinanceModel.ComputeManagementCostPerDay(need, 1f)));
  Assert.That(ParkFinanceModel.ComputeManagementCostPerDay(need, 1f), Is.EqualTo(need).Within(0.001), "100% funds the estimated need");
  var bigger = ParkFinanceModel.ComputeManagementNeedPerDay(new ParkManagementInputs { AreaSquareMeters = 40_000, VisitorsPerDay = 100, LandscapeObjects = 200, ManagedFacilities = 3 });
  Assert.That(bigger, Is.GreaterThan(need)); Assert.That(bigger, Is.LessThan(need * 4));
 }
 [Test] public void ManagementNeedExcludesVanillaUpkeepInputs() {
  Assert.That(typeof(ParkManagementInputs).GetFields().Select(f => f.Name), Has.None.Contains("Upkeep").And.None.Contains("Revenue"));
 }
 [Test] public void SettlementSplitsDailyCostWithoutLosingFractions() {
  double carry = 0; long total = 0;
  for (int i = 0; i < 16; i++) total += ParkFinanceModel.Settle(1000.0, ParkFinanceModel.FramesPerDay / 16, ref carry);
  Assert.That(total, Is.EqualTo(1000));
  carry = 0; Assert.That(ParkFinanceModel.Settle(0.6, ParkFinanceModel.FramesPerDay, ref carry), Is.Zero);
  Assert.That(ParkFinanceModel.Settle(0.6, ParkFinanceModel.FramesPerDay, ref carry), Is.EqualTo(1), "fractions carry over");
 }
 [Test] public void RevenueRecordsExactlyTheCollectedAmount() {
  var state = ParkAreaState.CreateDefault();
  state = ParkFinanceModel.RecordAdmission(state, 7);
  state = ParkFinanceModel.RecordAdmission(state, 0);
  state = ParkFinanceModel.RecordManagementExpense(state, 120);
  Assert.That(state.LifetimeAdmissionRevenue, Is.EqualTo(7)); Assert.That(state.LifetimeManagementExpense, Is.EqualTo(120));
 }
}
