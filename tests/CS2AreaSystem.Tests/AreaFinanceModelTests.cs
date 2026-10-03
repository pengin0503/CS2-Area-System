using CS2AreaSystem.Core.Finance;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaFinanceModelTests {
 private const uint Day = AreaFinanceModel.FramesPerDay;
 [Test] public void FramesPerDayMatchesTheVanillaSimulationDay() => Assert.That(Day, Is.EqualTo(262144u));
 [Test] public void AFullDaySettlesTheDailyAmountExactly() {
  double carry = 0;
  Assert.That(AreaFinanceModel.SettleDaily(1024, Day, ref carry), Is.EqualTo(1024));
  Assert.That(carry, Is.Zero);
 }
 [Test] public void PartialDaysAreProratedBySimulationFrames() {
  double carry = 0;
  Assert.That(AreaFinanceModel.SettleDaily(300, Day / 4, ref carry), Is.EqualTo(75));
  Assert.That(carry, Is.Zero);
 }
 [Test] public void FractionalCarryLosesNothingAcrossManySmallSettlements() {
  double carry = 0; long total = 0;
  for (int i = 0; i < 1024; i++) total += AreaFinanceModel.SettleDaily(777.7, Day / 1024, ref carry);
  Assert.That(total, Is.EqualTo(777), "settled in 256-frame steps, as BudgetApplySystem does");
  Assert.That(carry, Is.EqualTo(0.7).Within(1e-6));
  for (int i = 0; i < 1024; i++) total += AreaFinanceModel.SettleDaily(777.7, Day / 1024, ref carry);
  Assert.That(total, Is.EqualTo(1555), "the carried fraction is paid on the next day");
 }
 [Test] public void ZeroFramesSettleNothingAndKeepTheCarry() {
  double carry = 0.5;
  Assert.That(AreaFinanceModel.SettleDaily(1000, 0, ref carry), Is.Zero);
  Assert.That(carry, Is.EqualTo(0.5));
 }
 [TestCase(double.NaN)] [TestCase(-1d)] [TestCase(double.NegativeInfinity)] [TestCase(double.PositiveInfinity)]
 public void InvalidDailyAmountsSettleNothing(double perDay) {
  double carry = 0.25;
  Assert.That(AreaFinanceModel.SettleDaily(perDay, Day, ref carry), Is.Zero);
  Assert.That(carry, Is.EqualTo(0.25), "an invalid rate does not consume or grow the carry");
 }
 [TestCase(double.NaN)] [TestCase(-0.5)] [TestCase(double.PositiveInfinity)] [TestCase(1d)] [TestCase(42d)]
 public void InvalidCarryIsDerivedStateAndResets(double invalid) {
  double carry = invalid;
  Assert.That(AreaFinanceModel.SettleDaily(100, Day / 2, ref carry), Is.EqualTo(50), "a corrupt carry never becomes money");
  Assert.That(carry, Is.Zero);
 }
 [Test] public void VeryLargeFiniteRatesSaturateInsteadOfOverflowing() {
  double carry = 0;
  Assert.That(AreaFinanceModel.SettleDaily(1e15, Day, ref carry), Is.EqualTo(1_000_000_000_000_000L));
  Assert.That(AreaFinanceModel.SettleDaily(double.MaxValue, Day, ref carry), Is.EqualTo(long.MaxValue));
  Assert.That(carry, Is.Zero);
  Assert.That(AreaFinanceModel.SettleDaily(double.MaxValue, 1, ref carry), Is.EqualTo(long.MaxValue));
 }
 [Test] public void ConflictSwapAtABoundaryChargesEachPolicyOnlyForItsOwnFrames() {
  double oldCarry = 0, newCarry = 0;
  long old = AreaFinanceModel.SettleDaily(500, Day / 2, ref oldCarry);
  long replacement = AreaFinanceModel.SettleDaily(500, Day / 2, ref newCarry);
  Assert.That(old + replacement, Is.EqualTo(500), "no overlap: the pair costs what one continuous policy costs");
 }
 [Test] public void ZeroLengthConflictTransitionChargesTheReplacedPolicyNothing() {
  double oldCarry = 0, newCarry = 0;
  Assert.That(AreaFinanceModel.SettleDaily(500, 0, ref oldCarry), Is.Zero);
  Assert.That(AreaFinanceModel.SettleDaily(800, Day, ref newCarry), Is.EqualTo(800));
 }
 [Test] public void SaturatingAddNeverWraps() {
  Assert.That(AreaFinanceModel.SaturatingAdd(long.MaxValue, 1), Is.EqualTo(long.MaxValue));
  Assert.That(AreaFinanceModel.SaturatingAdd(long.MaxValue - 5, 3), Is.EqualTo(long.MaxValue - 2));
  Assert.That(AreaFinanceModel.SaturatingAdd(7, 8), Is.EqualTo(15));
 }
 [Test] public void KindsHaveOneFixedDirectionAndBudgetGroup() {
  Assert.That(Enum.GetNames(typeof(AreaFinanceKind)), Is.EqualTo(new[] { "PolicyUpkeep", "ParkManagement", "ParkAdmission", "IndustrialSupport", "ParkEvent" }));
  // Existing numeric values are persisted nowhere but stay fixed.
  Assert.That(new[] { (byte)AreaFinanceKind.PolicyUpkeep, (byte)AreaFinanceKind.ParkManagement, (byte)AreaFinanceKind.ParkAdmission, (byte)AreaFinanceKind.IndustrialSupport, (byte)AreaFinanceKind.ParkEvent },
   Is.EqualTo(new byte[] { 0, 1, 2, 3, 4 }));
  Assert.That(AreaFinanceKinds.IsExpense(AreaFinanceKind.ParkEvent), Is.True);
  Assert.That(AreaFinanceKinds.IsIncome(AreaFinanceKind.ParkEvent), Is.False);
  Assert.That(Enum.GetUnderlyingType(typeof(AreaFinanceKind)), Is.EqualTo(typeof(byte)));
  Assert.That(AreaFinanceKinds.IsExpense(AreaFinanceKind.PolicyUpkeep), Is.True);
  Assert.That(AreaFinanceKinds.IsExpense(AreaFinanceKind.ParkManagement), Is.True);
  Assert.That(AreaFinanceKinds.IsIncome(AreaFinanceKind.ParkAdmission), Is.True);
  Assert.That(AreaFinanceKinds.IsIncome(AreaFinanceKind.PolicyUpkeep), Is.False);
  Assert.That(AreaFinanceKinds.IsExpense(AreaFinanceKind.ParkAdmission), Is.False);
  Assert.That(AreaFinanceKinds.IsExpense((AreaFinanceKind)200) || AreaFinanceKinds.IsIncome((AreaFinanceKind)200), Is.False);
 }
}
