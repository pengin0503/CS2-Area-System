using CS2AreaSystem.Modules.Park;
using Game.Buildings;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkCoverageModifierModelTests {
 static ModifiedServiceCoverage Value(float capacity, float magnitude, float range = 300) => new ModifiedServiceCoverage { m_Capacity = capacity, m_Magnitude = magnitude, m_Range = range };
 [TestCase(0.5f, 0.90f)] [TestCase(0.75f, 0.95f)] [TestCase(1f, 1f)] [TestCase(1.25f, 1.05f)] [TestCase(1.5f, 1.10f)]
 public void BudgetInterpolatesLinearlyBetweenTheBounds(float budget, float factor) =>
  Assert.That(ParkCoverageModifierModel.Factor(budget, 1.0), Is.EqualTo(factor).Within(1e-6));
 [Test] public void EnhancedMaintenanceRaisesTheInputButTheFactorStaysAtMost110Percent() {
  Assert.That(ParkCoverageModifierModel.Factor(1f, ParkPolicyEffects.EnhancedMaintenanceFundingMultiplier), Is.EqualTo(1.05f).Within(1e-6));
  Assert.That(ParkCoverageModifierModel.Factor(1.5f, ParkPolicyEffects.EnhancedMaintenanceFundingMultiplier), Is.EqualTo(1.10f).Within(1e-6));
 }
 [TestCase(float.NaN)] [TestCase(0f)] [TestCase(-1f)]
 public void InvalidBudgetUsesTheDefaultBudget(float budget) => Assert.That(ParkCoverageModifierModel.Factor(budget, 1.0), Is.EqualTo(1f));
 [TestCase(double.NaN)] [TestCase(0.0)] [TestCase(double.PositiveInfinity)]
 public void InvalidPolicyMultiplierIsNeutral(double funding) => Assert.That(ParkCoverageModifierModel.Factor(1.5f, funding), Is.EqualTo(1.10f).Within(1e-6));
 [Test] public void ApplyScalesCapacityAndMagnitudeButNeverRange() {
  var applied = ParkCoverageModifierModel.Apply(Value(1000, 2), 1.1f);
  Assert.That((applied.m_Capacity, applied.m_Magnitude, applied.m_Range), Is.EqualTo((1100f, 2.2f, 300f)).Within(1e-3));
 }
 [Test] public void AFreshNativeBaselineIsModifiedFromTheBaselineNotFromThePreviousWrite() {
  var tracker = new ParkCoverageTracker(); var e = new Entity { Index = 7, Version = 1 };
  Assert.That(tracker.OnFreshBaseline(e, Value(1000, 2), 42, 1.1f, out var first), Is.True);
  Assert.That(first.m_Capacity, Is.EqualTo(1100f).Within(1e-3));
  // The next native pass recomputes the same baseline from prefab data; the write must not compound.
  Assert.That(tracker.OnFreshBaseline(e, Value(1000, 2), 42, 1.1f, out var second), Is.True);
  Assert.That(second.m_Capacity, Is.EqualTo(1100f).Within(1e-3));
  Assert.That(tracker.Count, Is.EqualTo(1));
 }
 [Test] public void NeutralFactorOrNoOwnerWritesNothingAndForgetsTheBuilding() {
  var tracker = new ParkCoverageTracker(); var e = new Entity { Index = 7, Version = 1 };
  tracker.OnFreshBaseline(e, Value(1000, 2), 42, 1.1f, out _);
  Assert.That(tracker.OnFreshBaseline(e, Value(1000, 2), 0, 1.1f, out _), Is.False, "no longer primary-owned: the native value stays");
  Assert.That(tracker.Count, Is.Zero);
  Assert.That(tracker.OnFreshBaseline(e, Value(1000, 2), 42, 1f, out _), Is.False);
  Assert.That(tracker.Count, Is.Zero);
 }
 [Test] public void RestoreWritesTheBaselineOnlyWhileTheValueIsStillTheModifiersWrite() {
  var tracker = new ParkCoverageTracker(); var e = new Entity { Index = 7, Version = 1 };
  tracker.OnFreshBaseline(e, Value(1000, 2), 42, 0.9f, out var written);
  Assert.That(tracker.TryRestore(e, written, out var restored), Is.True);
  Assert.That((restored.m_Capacity, restored.m_Magnitude, restored.m_Range), Is.EqualTo((1000f, 2f, 300f)));
  Assert.That(tracker.Count, Is.Zero);
  Assert.That(tracker.TryRestore(e, written, out _), Is.False, "restored once");
 }
 [Test] public void AnExternalOrNativeRewriteIsNeverOverwrittenWithTheStoredBaseline() {
  var tracker = new ParkCoverageTracker(); var e = new Entity { Index = 7, Version = 1 };
  tracker.OnFreshBaseline(e, Value(1000, 2), 42, 1.1f, out _);
  Assert.That(tracker.TryRestore(e, Value(1000, 1.9f), out _), Is.False);
  Assert.That(tracker.Count, Is.Zero, "the building is no longer ours");
 }
 [Test] public void RestoreAllVisitsEveryTrackedBuildingOnce() {
  var tracker = new ParkCoverageTracker();
  var a = new Entity { Index = 1, Version = 1 }; var b = new Entity { Index = 2, Version = 1 };
  tracker.OnFreshBaseline(a, Value(10, 1), 1, 1.1f, out var wa);
  tracker.OnFreshBaseline(b, Value(20, 1), 2, 0.9f, out _);
  var restored = new List<(Entity, ModifiedServiceCoverage)>();
  tracker.RestoreAll(e => e == a ? wa : (ModifiedServiceCoverage?)null, (e, v) => restored.Add((e, v)));
  Assert.That(restored.Select(r => (r.Item1, r.Item2.m_Capacity)), Is.EqualTo(new[] { (a, 10f) }), "b no longer exists");
  Assert.That(tracker.Count, Is.Zero);
 }
}
