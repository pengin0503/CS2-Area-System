using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Wave A3: each Industrial bottleneck carries at most one representative facility, collected during the existing
// observation cycle, and the focus action re-validates it against the current snapshot, existence and ownership.
public class IndustrialDiagnosticTargetTests {
 private const int Industrial = (int)AreaTypeId.Industrial;
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static IndustrialEntityObservation Producer(int index, double activity = 100, int jobs = 40, int employees = 40, double efficiency = 100, double profit = 80,
  IndustrialFlowState input = IndustrialFlowState.Healthy, IndustrialFlowState output = IndustrialFlowState.Healthy, bool available = true) => new() {
  Facility = E(index), Roles = IndustrialFacilityRole.Processor, IsProducer = true, ProducerTenants = 1, Available = available, IsOperatingProducer = activity > 0,
  ProductiveActivity = activity, Resources = new[] { new IndustrialResourceActivity { Resource = (ulong)Resource.Furniture, ActualPerDay = activity, CapacityPerDay = 100 } },
  Input = input, Output = output, Jobs = jobs, Employees = employees, VacanciesByLevel = new int[5], Efficiency = efficiency, Profitability = profit };
 private static IndustrialEntityObservation Cargo(int index, bool operational) => new() {
  Facility = E(index), Roles = IndustrialFacilityRole.Cargo | IndustrialFacilityRole.SupportingWorkplace, Available = true, CargoOperational = operational,
  Jobs = 30, Employees = 30, VacanciesByLevel = new int[5], Input = IndustrialFlowState.NotApplicable, Output = IndustrialFlowState.NotApplicable, Efficiency = 100 };
 private static IndustrialCycleAccumulator Apply(params IndustrialEntityObservation[] observations) {
  var acc = new IndustrialCycleAccumulator();
  foreach (var o in observations) IndustrialObservationAggregator.Apply(acc, o);
  return acc;
 }
 private static IndustrialObservationResult Hours(IndustrialRuntimeSnapshot snapshot, int hours, Func<int, IndustrialEntityObservation[]> at) {
  IndustrialObservationResult last = null!;
  for (int h = 0; h <= hours; h++) {
   var acc = Apply(at(h));
   last = IndustrialObservationAggregator.Finalize(acc, snapshot, new IndustrialCycleInfo { CycleId = h + 1, HourBucket = h, StartHours = h, EndHours = h + 0.1,
    MembershipToken = 1, StructureToken = 1, Active = true });
  }
  return last;
 }
 private static IndustrialBottleneck Find(IndustrialObservationResult r, string key) => r.AllBottlenecks.Single(b => b.Key == key);
 // ---- representative rules
 [Test] public void WorkforceShortageTargetsTheLowestFillAmongUnderstaffed() {
  var acc = Apply(Producer(1, jobs: 40, employees: 20), Producer(2, jobs: 40, employees: 10), Producer(3, jobs: 40, employees: 10), Producer(4, jobs: 40, employees: 40));
  Assert.That(acc.Targets.For("WorkforceShortage"), Is.EqualTo(E(2)), "lowest fill; the first observed wins a tie");
  Assert.That(Apply(Producer(1, jobs: 40, employees: 35)).Targets.For("WorkforceShortage"), Is.EqualTo(Entity.Null), "not understaffed");
 }
 [Test] public void EfficiencyAndProfitabilityTargetTheLowestObservedFacility() {
  var acc = Apply(Producer(1, efficiency: 60, profit: 30), Producer(2, efficiency: 40, profit: 50), Producer(3, efficiency: 40, profit: 10), Producer(4, efficiency: 90, profit: 10));
  Assert.That(acc.Targets.For("LowEfficiency"), Is.EqualTo(E(2)));
  Assert.That(acc.Targets.For("LowProfitability"), Is.EqualTo(E(3)));
 }
 [Test] public void FlowTargetsAreTheLargestAffectedProducer() {
  var acc = Apply(Producer(1, jobs: 10, employees: 10, input: IndustrialFlowState.Affected), Producer(2, jobs: 60, employees: 60, input: IndustrialFlowState.Affected),
   Producer(3, jobs: 60, employees: 60, input: IndustrialFlowState.Affected, output: IndustrialFlowState.Affected), Producer(4, output: IndustrialFlowState.Unavailable));
  Assert.That(acc.Targets.For("InputStarvation"), Is.EqualTo(E(2)), "largest weight; first observed on a tie");
  Assert.That(acc.Targets.For("StorageSaturation"), Is.EqualTo(E(3)));
 }
 [Test] public void InsufficientCargoPrefersANonOperationalCargoFacility() {
  Assert.That(Apply(Producer(1, output: IndustrialFlowState.Affected), Cargo(5, true), Cargo(6, false), Cargo(7, false)).Targets.For("InsufficientCargo"), Is.EqualTo(E(6)));
  Assert.That(Apply(Producer(1, output: IndustrialFlowState.Affected), Cargo(5, true)).Targets.For("InsufficientCargo"), Is.EqualTo(E(1)), "falls back to the affected-output producer");
 }
 [Test] public void LowActiveProducerShareTargetsAnIdleProducer() {
  var acc = Apply(Producer(1), Producer(2, activity: 0), Producer(3, activity: 0), Producer(4, available: false));
  Assert.That(acc.Targets.For("LowActiveProducerShare"), Is.EqualTo(E(2)));
 }
 [Test] public void UnknownKeyHasNoTarget() => Assert.That(new IndustrialCycleAccumulator().Targets.For("Nope"), Is.EqualTo(Entity.Null));
 [Test] public void FinalizeAttachesOnlyTheCurrentCyclesCandidate() {
  var snapshot = new IndustrialRuntimeSnapshot();
  var r = Hours(snapshot, 6, h => new[] { Producer(1, jobs: 40, employees: 10, efficiency: 20, profit: 10), Producer(2, activity: 0, jobs: 40, employees: 40) });
  Assert.That(Find(r, "WorkforceShortage").Target, Is.EqualTo(E(1)));
  Assert.That(Find(r, "LowEfficiency").Target, Is.EqualTo(E(1)));
  Assert.That(Find(r, "LowActiveProducerShare").Target, Is.EqualTo(E(2)));
  Assert.That(r.Bottlenecks.All(b => r.AllBottlenecks.Contains(b)), Is.True, "top three are the same objects");
  var next = Hours(snapshot, 0, h => new[] { Producer(3, jobs: 40, employees: 10), Producer(4, jobs: 40, employees: 40) });
  Assert.That(Find(next, "WorkforceShortage").Target, Is.EqualTo(E(3)), "never a facility from an earlier cycle");
 }
 // ---- focus action safety
 private sealed class Facts {
  public readonly AreaMembershipIndex Index = new();
  public readonly HashSet<Entity> Alive = new();
  public bool Exists(Entity e) => Alive.Contains(e);
 }
 private static (Facts World, IndustrialObservationResult Current) Setup() {
  var world = new Facts();
  var r = Hours(new IndustrialRuntimeSnapshot(), 6, h => new[] { Producer(1, jobs: 40, employees: 10), Producer(2) });
  world.Alive.Add(E(1)); world.Alive.Add(E(2));
  world.Index.ReplaceMembership(E(1), new[] { new AreaMembership(30, Industrial, true) });
  world.Index.ReplaceMembership(E(2), new[] { new AreaMembership(30, Industrial, true) });
  return (world, r);
 }
 private static bool Resolve(Facts world, IndustrialObservationResult current, string key, out Entity target) =>
  IndustrialGameplayFacade.TryResolveFocusTarget(current, key, 30, world.Exists, world.Index, out target);
 [Test] public void ValidCurrentTargetResolves() {
  var (world, current) = Setup();
  Assert.That(Resolve(world, current, AreaDiagnosticTargetKey.Format(E(1)), out var target), Is.True);
  Assert.That(target, Is.EqualTo(E(1)));
 }
 [Test] public void TargetMissingFromTheCurrentSnapshotIsRejected() {
  var (world, current) = Setup();
  Assert.That(Resolve(world, current, AreaDiagnosticTargetKey.Format(E(2)), out _), Is.False, "a member that is not a bottleneck target");
  var newer = Hours(new IndustrialRuntimeSnapshot(), 6, h => new[] { Producer(1), Producer(2) });
  Assert.That(Resolve(world, newer, AreaDiagnosticTargetKey.Format(E(1)), out _), Is.False, "the snapshot changed and the target disappeared");
  Assert.That(Resolve(world, null!, AreaDiagnosticTargetKey.Format(E(1)), out _), Is.False, "no snapshot (after load)");
 }
 [Test] public void DeletedTargetIsRejected() {
  var (world, current) = Setup();
  world.Alive.Remove(E(1));
  Assert.That(Resolve(world, current, AreaDiagnosticTargetKey.Format(E(1)), out _), Is.False);
 }
 [Test] public void TargetNoLongerPrimaryOwnedIsRejected() {
  var (world, current) = Setup();
  world.Index.ReplaceMembership(E(1), new[] { new AreaMembership(10, Industrial, true), new AreaMembership(30, Industrial, true) });
  Assert.That(Resolve(world, current, AreaDiagnosticTargetKey.Format(E(1)), out _), Is.False, "Area 10 is now the primary Industrial owner");
  world.Index.ReplaceMembership(E(1), Array.Empty<AreaMembership>());
  Assert.That(Resolve(world, current, AreaDiagnosticTargetKey.Format(E(1)), out _), Is.False, "moved out of the Area");
 }
 [Test] public void OtherActionsAreNotHandledAndTouchNothing() {
  int focused = 0;
  var context = new AreaPanelContext(null, default, _ => focused++);
  Assert.That(IndustrialGameplayFacade.HandleAction(E(30), "setBudget", "1.1", context), Is.False);
  Assert.That(IndustrialGameplayFacade.HandleAction(E(30), "", "1.1", context), Is.False);
  Assert.That(focused, Is.Zero);
  Assert.That(IndustrialGameplayFacade.FocusDiagnosticTarget, Is.EqualTo("focusDiagnosticTarget"));
 }
 [Test] public void ValidTargetFocusesExactlyOnce() {
  var (world, current) = Setup();
  var calls = new List<Entity>();
  Assert.That(IndustrialGameplayFacade.TryFocus(current, AreaDiagnosticTargetKey.Format(E(1)), 30, world.Exists, world.Index, calls.Add), Is.True);
  Assert.That(calls, Is.EqualTo(new[] { E(1) }));
  Assert.That(IndustrialGameplayFacade.TryFocus(current, AreaDiagnosticTargetKey.Format(E(2)), 30, world.Exists, world.Index, calls.Add), Is.False);
  Assert.That(IndustrialGameplayFacade.TryFocus(current, AreaDiagnosticTargetKey.Format(E(1)), 30, world.Exists, world.Index, null), Is.False, "no focus path, no action");
  Assert.That(calls, Has.Count.EqualTo(1));
 }
}
