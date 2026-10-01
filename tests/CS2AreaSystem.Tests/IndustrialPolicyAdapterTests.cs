using System.Reflection;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Industrial.Policies;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Industrial policy effects go through dedicated vanilla adapters (design §9.2, §12). The controller applies and removes
// them only on effective-state transitions, isolates failures per Area and policy, and never touches Industrial scores.
// No 1.6.2f1 adapter is verified (docs/verification/industrial-policy-effect-contracts.md), so the shipped set is empty.
public class IndustrialPolicyAdapterTests {
 private sealed class FakeAdapter : IIndustrialPolicyAdapter {
  public FakeAdapter(int policyId) { PolicyId = policyId; }
  public int PolicyId { get; }
  public readonly List<string> Calls = new();
  public bool Supported = true; public string Reason;
  public bool ThrowOnSupported, ThrowOnApply, ThrowOnRemove;
  public bool IsSupported(Entity area, in AreaPolicyContext context, out string reasonKey) {
   Calls.Add("supported:" + area.Index);
   if (ThrowOnSupported) throw new InvalidOperationException("supported");
   reasonKey = Reason; return Supported;
  }
  public void Apply(Entity area, in AreaPolicyContext context) { Calls.Add("apply:" + area.Index); if (ThrowOnApply) throw new InvalidOperationException("apply"); }
  public void Remove(Entity area, in AreaPolicyContext context) { Calls.Add("remove:" + area.Index); if (ThrowOnRemove) throw new InvalidOperationException("remove"); }
  public int Count(string call) => Calls.Count(c => c == call);
 }
 private static readonly Entity A = new() { Index = 1, Version = 1 }, B = new() { Index = 2, Version = 1 };
 private const ulong IdA = 101, IdB = 102;
 private const int Regulation = AreaPolicyIds.EnvironmentalRegulation, Hours = AreaPolicyIds.RestrictedOperatingHours;
 private readonly List<string> reports = new();
 private IndustrialPolicyEffectController Controller(params IIndustrialPolicyAdapter[] adapters) =>
  new(adapters, (error, key) => reports.Add(key));
 [SetUp] public void Clear() => reports.Clear();

 [Test] public void TheAdapterContractTakesOnlyTheAreaAndThePolicyContext() {
  var allowed = new[] { typeof(Entity), typeof(AreaPolicyContext), typeof(AreaPolicyContext).MakeByRefType(), typeof(string).MakeByRefType() };
  foreach (var method in typeof(IIndustrialPolicyAdapter).GetMethods())
   foreach (var parameter in method.GetParameters())
    Assert.That(allowed, Does.Contain(parameter.ParameterType), method.Name + "." + parameter.Name);
  Assert.That(typeof(IIndustrialPolicyAdapter).GetProperty("PolicyId")!.PropertyType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(IIndustrialPolicyAdapter).GetMethods().Select(m => m.Name), Is.EquivalentTo(new[] { "get_PolicyId", "IsSupported", "Apply", "Remove" }));
 }
 [Test] public void TheRegistryRejectsDuplicateNullAndForeignAdapters() {
  Assert.Throws<ArgumentException>(() => Controller(new FakeAdapter(Regulation), new FakeAdapter(Regulation)));
  Assert.Throws<ArgumentNullException>(() => Controller(new IIndustrialPolicyAdapter[] { null! }));
  Assert.Throws<ArgumentException>(() => Controller(new FakeAdapter(AreaPolicyIds.EnhancedMaintenance)), "a Park policy is not an Industrial adapter");
  Assert.Throws<ArgumentException>(() => Controller(new FakeAdapter(3999)));
  Assert.That(Controller(new FakeAdapter(Regulation), new FakeAdapter(Hours)).PolicyIds, Is.EquivalentTo(new[] { Regulation, Hours }));
 }
 [Test] public void EffectsApplyAndRemoveOnlyOnEffectiveStateTransitions() {
  var adapter = new FakeAdapter(Regulation); var controller = Controller(adapter);
  controller.Sync(A, IdA, Regulation, false, default);
  Assert.That(adapter.Calls, Is.Empty, "never applied, nothing to remove");
  controller.Sync(A, IdA, Regulation, true, default); controller.Sync(A, IdA, Regulation, true, default);
  Assert.That(adapter.Count("apply:1"), Is.EqualTo(1), "idempotent while effective"); Assert.That(controller.IsApplied(IdA, Regulation), Is.True);
  controller.Sync(A, IdA, Regulation, false, default); controller.Sync(A, IdA, Regulation, false, default);
  Assert.That(adapter.Count("remove:1"), Is.EqualTo(1), "idempotent while ineffective"); Assert.That(controller.IsApplied(IdA, Regulation), Is.False);
  controller.Sync(A, IdA, Regulation, true, default);
  Assert.That(adapter.Count("apply:1"), Is.EqualTo(2), "re-enabling applies again");
 }
 [Test] public void EachAreaHasItsOwnEffectState() {
  var adapter = new FakeAdapter(Regulation); var controller = Controller(adapter);
  controller.Sync(A, IdA, Regulation, true, default); controller.Sync(B, IdB, Regulation, false, default);
  Assert.That(controller.IsApplied(IdA, Regulation), Is.True); Assert.That(controller.IsApplied(IdB, Regulation), Is.False);
  Assert.That(adapter.Calls, Is.EqualTo(new[] { "apply:1" }));
 }
 [Test] public void PoliciesWithoutAnAdapterAreNeverSynced() {
  var adapter = new FakeAdapter(Regulation); var controller = Controller(adapter);
  controller.Sync(A, IdA, Hours, true, default);
  Assert.That(controller.IsApplied(IdA, Hours), Is.False); Assert.That(adapter.Calls, Is.Empty);
 }
 [Test] public void ReleasingAnAreaRemovesEveryAppliedEffectOnce() {
  var regulation = new FakeAdapter(Regulation); var hours = new FakeAdapter(Hours); var controller = Controller(regulation, hours);
  controller.Sync(A, IdA, Regulation, true, default); controller.Sync(A, IdA, Hours, true, default); controller.Sync(B, IdB, Hours, true, default);
  controller.Release(A, IdA, default); controller.Release(A, IdA, default);
  Assert.That(regulation.Count("remove:1"), Is.EqualTo(1)); Assert.That(hours.Count("remove:1"), Is.EqualTo(1));
  Assert.That(controller.IsApplied(IdA, Regulation) || controller.IsApplied(IdA, Hours), Is.False);
  Assert.That(controller.IsApplied(IdB, Hours), Is.True, "other Areas keep their effects");
 }
 [Test] public void ANewLoadForgetsTheOldWorldWithoutRemovingFromIt() {
  var adapter = new FakeAdapter(Regulation); var controller = Controller(adapter);
  controller.Sync(A, IdA, Regulation, true, default);
  controller.Reset();
  Assert.That(adapter.Count("remove:1"), Is.Zero, "the previous world is gone; its state is not touched");
  Assert.That(controller.IsApplied(IdA, Regulation), Is.False);
  controller.Sync(A, IdA, Regulation, true, default);
  Assert.That(adapter.Count("apply:1"), Is.EqualTo(2), "a rebuilt world gets the effect again");
 }
 [Test] public void AFailedApplyDisablesOnlyThatAreaAndPolicyForThisLoad() {
  var regulation = new FakeAdapter(Regulation) { ThrowOnApply = true }; var hours = new FakeAdapter(Hours); var controller = Controller(regulation, hours);
  controller.Sync(A, IdA, Regulation, true, default);
  Assert.That(regulation.Calls, Is.EqualTo(new[] { "apply:1", "remove:1" }), "a partial apply is cleaned up");
  Assert.That(controller.HasFailed(IdA, Regulation), Is.True); Assert.That(controller.IsApplied(IdA, Regulation), Is.False);
  controller.Sync(A, IdA, Regulation, true, default); controller.Sync(A, IdA, Regulation, false, default);
  Assert.That(regulation.Calls, Has.Count.EqualTo(2), "a failed effect is not retried in this load");
  Assert.That(controller.IsAvailable(A, IdA, Regulation, default, out var reason), Is.False);
  Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
  regulation.ThrowOnApply = false;
  controller.Sync(B, IdB, Regulation, true, default); controller.Sync(A, IdA, Hours, true, default);
  Assert.That(controller.IsApplied(IdB, Regulation), Is.True, "other Areas keep the policy"); Assert.That(controller.IsApplied(IdA, Hours), Is.True, "other policies keep working");
  Assert.That(reports, Is.EqualTo(new[] { "101:3002:apply" }));
  controller.Reset();
  Assert.That(controller.HasFailed(IdA, Regulation), Is.False, "a new load tries again");
 }
 [Test] public void AFailedRemoveIsReportedOnceAndNotRetried() {
  var adapter = new FakeAdapter(Regulation) { ThrowOnRemove = true }; var controller = Controller(adapter);
  controller.Sync(A, IdA, Regulation, true, default);
  controller.Sync(A, IdA, Regulation, false, default); controller.Sync(A, IdA, Regulation, false, default);
  controller.Release(A, IdA, default);
  Assert.That(adapter.Count("remove:1"), Is.EqualTo(1));
  Assert.That(controller.HasFailed(IdA, Regulation), Is.True); Assert.That(controller.IsApplied(IdA, Regulation), Is.False);
  Assert.That(reports, Is.EqualTo(new[] { "101:3002:remove" }));
 }
 [Test] public void AvailabilityFollowsTheAdapterAndIsolatesItsFailures() {
  var adapter = new FakeAdapter(Regulation); var controller = Controller(adapter);
  Assert.That(controller.IsAvailable(A, IdA, Regulation, default, out var reason), Is.True); Assert.That(reason, Is.Null);
  Assert.That(controller.IsAvailable(A, IdA, Hours, default, out reason), Is.False, "no adapter");
  Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
  adapter.Supported = false; adapter.Reason = "CS2AS.Policy.Reason.Custom";
  Assert.That(controller.IsAvailable(A, IdA, Regulation, default, out reason), Is.False); Assert.That(reason, Is.EqualTo("CS2AS.Policy.Reason.Custom"));
  adapter.Reason = null;
  Assert.That(controller.IsAvailable(A, IdA, Regulation, default, out reason), Is.False); Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
  adapter.Supported = true; adapter.ThrowOnSupported = true;
  Assert.That(controller.IsAvailable(A, IdA, Regulation, default, out reason), Is.False); Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
  Assert.That(controller.IsAvailable(A, IdA, Regulation, default, out _), Is.False);
  Assert.That(reports, Is.EqualTo(new[] { "101:3002:supported" }), "logged once per Area, policy and failure class");
  adapter.ThrowOnSupported = false;
  Assert.That(controller.IsAvailable(B, IdB, Regulation, default, out _), Is.True, "another Area is unaffected");
 }
 [Test] public void NoIndustrialAdapterIsVerifiedSoEveryIndustrialPolicyStaysUnavailable() {
  Assert.That(IndustrialPolicyAdapters.Create(), Is.Empty);
  IAreaPolicyProvider module = new IndustrialAreaModule(null);
  foreach (var definition in IndustrialPolicyCatalog.Definitions) {
   Assert.That(IndustrialPolicyCatalog.IsEffectAvailable(definition.PolicyId, out var reason), Is.False, definition.PolicyId.ToString());
   Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
   Assert.That(module.IsPolicyEffectAvailable(Entity.Null, definition.PolicyId, default, out reason), Is.False);
   Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
  }
 }
 // The policy layer cannot bypass observation: score producers have no policy input, and the adapter layer has no
 // access to Industrial scores. A verified adapter can change Industrial scores only by changing vanilla state that
 // IndustrialObservationSystem then observes.
 [Test] public void IndustrialScoreProducersHaveNoPolicyInput() {
  var producers = new[] { typeof(IndustrialPerformanceModel), typeof(IndustrialProductionModel), typeof(IndustrialLogisticsModel),
   typeof(IndustrialEmploymentModel), typeof(IndustrialObservationAggregator), typeof(IndustrialObservationSystem) };
  foreach (var producer in producers) {
   foreach (var type in Referenced(producer))
    Assert.That(type.Namespace, Is.Not.EqualTo(typeof(AreaPolicySystem).Namespace).And.Not.EqualTo(typeof(IIndustrialPolicyAdapter).Namespace),
     producer.Name + " references " + type.FullName);
  }
 }
 [Test] public void ThePolicyAdapterLayerHasNoAccessToIndustrialScores() {
  var scoreTypes = new[] { typeof(IndustrialAreaState), typeof(IndustrialRuntimeSnapshot), typeof(IndustrialObservationResult),
   typeof(IndustrialPerformanceModel), typeof(IndustrialObservationAggregator), typeof(IndustrialRuntimeSystem), typeof(IndustrialObservationSystem) };
  var layer = typeof(IIndustrialPolicyAdapter).Assembly.GetTypes().Where(t => t.Namespace == typeof(IIndustrialPolicyAdapter).Namespace).ToList();
  Assert.That(layer, Is.Not.Empty);
  foreach (var type in layer)
   foreach (var referenced in Referenced(type))
    Assert.That(scoreTypes, Does.Not.Contain(referenced), type.Name + " references " + referenced.Name);
 }
 private static IEnumerable<Type> Referenced(Type type) {
  const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
  static Type Element(Type t) => t.HasElementType ? Element(t.GetElementType()!) : t;
  IEnumerable<Type> Expand(Type t) { t = Element(t); yield return t; if (t.IsGenericType) foreach (var a in t.GetGenericArguments().SelectMany(Expand)) yield return a; }
  var direct = type.GetFields(all).Select(f => f.FieldType)
   .Concat(type.GetProperties(all).Select(p => p.PropertyType))
   .Concat(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
   .Concat(type.GetConstructors(all).SelectMany(c => c.GetParameters().Select(p => p.ParameterType)));
  return direct.SelectMany(Expand).Distinct();
 }
}
