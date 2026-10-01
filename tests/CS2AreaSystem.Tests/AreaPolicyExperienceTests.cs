using CS2AreaSystem.Core.Policies;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaPolicyExperienceTests {
 private const long NoCap = long.MaxValue;
 [Test] public void FocusedManagementIsExactlyTenPercent() {
  Assert.That(AreaPolicyExperience.FocusedManagementMultiplier, Is.EqualTo(1.10));
  Assert.That(AreaPolicyRuntime.FocusedManagementMultiplier, Is.EqualTo(AreaPolicyExperience.FocusedManagementMultiplier));
 }
 [TestCase(10L, 11L)] [TestCase(5L, 6L)] [TestCase(4L, 4L)] [TestCase(2L, 2L)] [TestCase(15L, 17L)] [TestCase(1L, 1L)]
 public void TheMultipliedAwardRoundsHalfAwayFromZero(long baseAward, long expected) =>
  Assert.That(AreaPolicyExperience.Apply(baseAward, 1.10, NoCap), Is.EqualTo(expected));
 [Test] public void NeutralMultiplierKeepsTheBaseAward() => Assert.That(AreaPolicyExperience.Apply(7, 1.0, NoCap), Is.EqualTo(7));
 [TestCase(0L)] [TestCase(-4L)]
 public void ZeroOrNegativeBaseAwardsStayZero(long baseAward) => Assert.That(AreaPolicyExperience.Apply(baseAward, 1.10, NoCap), Is.Zero);
 [Test] public void TheRemainingCapIsAppliedAfterTheMultiplier() {
  Assert.That(AreaPolicyExperience.Apply(10, 1.10, 5), Is.EqualTo(5));
  Assert.That(AreaPolicyExperience.Apply(10, 1.10, 11), Is.EqualTo(11));
  Assert.That(AreaPolicyExperience.Apply(10, 1.10, 0), Is.Zero);
  Assert.That(AreaPolicyExperience.Apply(10, 1.10, -3), Is.Zero, "a negative remaining cap never grants XP");
 }
 [Test] public void HugeAwardsSaturateInsteadOfOverflowing() {
  Assert.That(AreaPolicyExperience.Apply(long.MaxValue, 1.10, NoCap), Is.EqualTo(long.MaxValue));
  Assert.That(AreaPolicyExperience.Apply(long.MaxValue / 2, 3.0, NoCap), Is.EqualTo(long.MaxValue));
 }
 [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1.1)]
 public void InvalidMultipliersFallBackToTheBaseAward(double multiplier) =>
  Assert.That(AreaPolicyExperience.Apply(10, multiplier, NoCap), Is.EqualTo(10));
}
