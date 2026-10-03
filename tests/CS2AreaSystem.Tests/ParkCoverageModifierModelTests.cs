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
 [TestCase(500u, 500u, true, true)] [TestCase(499u, 500u, true, false)] [TestCase(501u, 500u, true, false)] [TestCase(500u, 500u, false, false)] [TestCase(0u, 0u, true, false)]
 public void OnlyAChunkWrittenInTheNativeWritersLastUpdateIsAFreshBaseline(uint chunk, uint native, bool written, bool fresh) =>
  Assert.That(ParkCoverageModifierModel.IsFresh(chunk, native, written), Is.EqualTo(fresh));
 [Test] public void BuildingsThatLeftEveryParkAreRestoredWhileStillOurs() {
  var tracker = new ParkCoverageTracker();
  var kept = new Entity { Index = 1, Version = 1 }; var left = new Entity { Index = 2, Version = 1 }; var rewritten = new Entity { Index = 3, Version = 1 };
  tracker.OnFreshBaseline(kept, Value(10, 1), 1, 1.1f, out _);
  tracker.OnFreshBaseline(left, Value(20, 1), 1, 1.1f, out var leftWrite);
  tracker.OnFreshBaseline(rewritten, Value(30, 1), 1, 1.1f, out _);
  var restored = new List<Entity>();
  tracker.RestoreUnvisited(new HashSet<Entity> { kept }, e => e == left ? leftWrite : Value(31, 1), (e, v) => restored.Add(e));
  Assert.That(restored, Is.EqualTo(new[] { left }), "a natively rewritten building keeps its native value");
  Assert.That(tracker.IsTracked(kept), Is.True);
  Assert.That(tracker.Count, Is.EqualTo(1));
 }
}
// The C6 writer ships disabled: it is registered, ordered and restorable, but never enabled until the in-game gate passes.
public class ParkCoverageModifierSystemTests {
 static string Source(string path) => IndustrialSupportSourceGuardTests.Source(path);
 [Test] public void TheRuntimeGateIsClosed() => Assert.That(ParkCoverageModifierGate.RuntimeVerified, Is.False);
 [Test] public void TheWriterRunsOnTheNativeWritersInterval() =>
  Assert.That(new ParkCoverageModifierSystem().GetUpdateInterval(Game.SystemUpdatePhase.GameSimulation), Is.EqualTo(262144 / Game.Simulation.ParkAISystem.kUpdatesPerDay));
 [Test] public void TheWriterIsDisabledFromCreationUnlessTheGateIsOpen() {
  var system = Source("Modules/Park/ParkCoverageModifierSystem.cs");
  Assert.That(system, Does.Contain("Enabled = ParkCoverageModifierGate.RuntimeVerified;"));
  Assert.That(system.IndexOf("Enabled = ParkCoverageModifierGate.RuntimeVerified;"), Is.LessThan(system.IndexOf("protected override void OnUpdate()")));
 }
 [Test] public void TheWriterUsesTheVerifiedFreshnessSignalAfterCompletingTheNativeJob() {
  var system = Source("Modules/Park/ParkCoverageModifierSystem.cs");
  int complete = system.IndexOf("EntityManager.CompleteDependencyBeforeRO<ModifiedServiceCoverage>();");
  Assert.That(complete, Is.GreaterThan(0));
  Assert.That(system.IndexOf("GetChangeVersion(ref coverageHandle)"), Is.GreaterThan(complete));
  Assert.That(system, Does.Contain("parkAI.LastSystemVersion"));
  Assert.That(system, Does.Contain("ParkCoverageModifierModel.IsFresh("));
  Assert.That(system, Does.Contain("tracker.OnFreshBaseline("));
  Assert.That(system, Does.Not.Contain("m_Range"), "range is never written");
 }
 // A write stamps the whole chunk with the mod's version, so every building's freshness is read before the first write.
 [Test] public void FreshnessIsReadForEveryBuildingBeforeAnyWrite() {
  var system = Source("Modules/Park/ParkCoverageModifierSystem.cs");
  int update = system.IndexOf("protected override void OnUpdate()");
  int lastFresh = system.LastIndexOf("IsFresh(member, native)");
  int firstWrite = system.IndexOf("EntityManager.SetComponentData(", update);
  Assert.That(lastFresh, Is.GreaterThan(update));
  Assert.That(system.IndexOf("foreach (var candidate in candidates)", update), Is.GreaterThan(lastFresh));
  Assert.That(firstWrite, Is.GreaterThan(system.IndexOf("foreach (var candidate in candidates)", update)));
 }
 // A tracked building the native writer no longer refreshes (destroyed, prefab lost its data) or that left every Park
 // gets its baseline back instead of keeping a stale modified value.
 [Test] public void TrackedBuildingsWithoutAFreshPassAreRestored() =>
  Assert.That(Source("Modules/Park/ParkCoverageModifierSystem.cs"), Does.Contain("tracker.RestoreUnvisited(fresh, Read, Write);"));
 [Test] public void FailureDisableAndSaveRestoreTrackedValues() {
  var system = Source("Modules/Park/ParkCoverageModifierSystem.cs");
  Assert.That(system, Does.Contain("protected override void OnStopRunning()"));
  Assert.That(system, Does.Contain("public void Shutdown()"));
  Assert.That(system.Split("RestoreTracked()").Length - 1, Is.GreaterThanOrEqualTo(4), "declaration, failure, stop, shutdown");
  Assert.That(system, Does.Contain("tracker.Clear();"), "preload forgets the previous world's entities without writing");
  var save = Source("Modules/Park/ParkCoverageModifierSaveSystem.cs");
  Assert.That(save, Does.Contain("writer.RestoreTracked();"));
 }
}

