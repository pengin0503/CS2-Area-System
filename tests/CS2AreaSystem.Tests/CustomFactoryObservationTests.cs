using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Semantics;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 D2 Task 3: the reference factory is observed through the same reader/mapper/aggregator contracts as a vanilla
// Furniture factory. Identical company reads give identical process rows, resource totals, bottlenecks and comparison
// metrics; the only difference is the prefab identity, which no scoring path reads.
public class CustomFactoryObservationTests
{
 private static Entity E(int index) => new() { Index = index, Version = 1 };
 private static readonly Entity FactoryPrefab = E(900), VanillaPrefab = E(901);
 private const ulong Area = 2, ManualArea = 7;
 private static AreaAssetSemanticRegistry Registry() {
  var registry = new AreaAssetSemanticRegistry();
  new AreaBuiltInContentSemanticProvider().Refresh(registry, new[] { new AreaBuiltInPrefabCandidate(FactoryPrefab, "BuildingPrefab", IndustrialReferenceFactoryContract.PrefabName, true, false,
   "AS_FurnitureFactory.cok", new AreaBuiltInBuildingFacts { HasProperty = true, Input = (ulong)EconomyUtils.GetAllResources(), Manufactured = (ulong)Resource.Furniture, Signature = true, IndustrialZone = true }) });
  return registry;
 }
 private static AreaMembershipIndex Membership(Entity building) {
  var m = new AreaMembershipIndex();
  m.ReplaceMembership(building, new[] { new AreaMembership(Area, (int)AreaTypeId.Industrial, true), new AreaMembership(ManualArea, (int)AreaTypeId.Industrial, false) });
  return m;
 }
 private static IndustrialBuildingRead Building(Entity building, Entity prefab, ulong area) => new() {
  Building = building, AreaId = area, CycleId = 1, Exists = true, Available = true, HasEfficiency = true, Efficiency = 0.8f,
  Traits = new IndustrialFacilityTraits { IsBuilding = true, Prefab = prefab, ManufacturesGoods = true, HasWorkplaces = true } };
 // A vanilla Furniture company as the game would place it: its own recipe, short of workers and of Timber.
 private static IndustrialCompanyRead Company(Entity company, Entity building, ulong area) => new() {
  Company = company, Building = building, AreaId = area, CycleId = 1, Producer = true, HasJobs = true, Jobs = 20, Employees = 6,
  HasProfit = true, ProfitIndex = 40, HasActivity = true, OutputResource = (ulong)Resource.Furniture, ActualPerDay = 30, CapacityPerDay = 100,
  Input = IndustrialFlowState.Affected, Output = IndustrialFlowState.Healthy,
  HasProcess = true, ProcessInput1 = (ulong)Resource.Timber, ProcessInput1Amount = 2, ProcessOutput = (ulong)Resource.Furniture, ProcessOutputAmount = 1 };
 private static IndustrialEntityObservation Observe(Entity prefab, ulong area, IndustrialCompanyAssignmentIndex? assignments = null) {
  var building = E(100);
  return IndustrialObservationMapper.ToOwnedObservation(Building(building, prefab, area), new[] { Company(E(1), building, area) }, Membership(building),
   assignments ?? new IndustrialCompanyAssignmentIndex(), _ => new IndustrialCompanyEligibility { Live = true, Industrial = true, BuildingEligible = true }, Registry());
 }
 private static IndustrialObservationResult Run(IndustrialEntityObservation observation, IndustrialRuntimeSnapshot snapshot) {
  var acc = new IndustrialCycleAccumulator();
  IndustrialObservationAggregator.Apply(acc, observation);
  var result = IndustrialObservationAggregator.Finalize(acc, snapshot,
   new IndustrialCycleInfo { CycleId = 1, HourBucket = 3, StartHours = 3, EndHours = 3.1, MembershipToken = 1, StructureToken = 1, Active = true });
  snapshot.Publish(result);
  return result;
 }
 [Test] public void TheFactoryIsObservedExactlyLikeAVanillaFurnitureFactory() {
  var factorySnapshot = new IndustrialRuntimeSnapshot(); var vanillaSnapshot = new IndustrialRuntimeSnapshot();
  var factory = Run(Observe(FactoryPrefab, Area), factorySnapshot);
  var vanilla = Run(Observe(VanillaPrefab, Area), vanillaSnapshot);
  var process = factory.Processes.Single();
  Assert.That(process.Process, Is.EqualTo(new IndustrialProcessKey((ulong)Resource.Timber, 2, 0, 0, (ulong)Resource.Furniture, 1)), "the vanilla company's recipe, read as is");
  Assert.That((process.Companies, process.MeasuredCompanies, process.ActualOutputPerDay), Is.EqualTo((1, 1, 30d)));
  Assert.That(factory.Resources.Select(r => (r.Resource, r.ActualPerDay)), Is.EqualTo(new[] { ((ulong)Resource.Furniture, 30d) }));
  Assert.That(factory.Processes.Select(p => (p.Process, p.Companies, p.MeasuredCompanies, p.ActualOutputPerDay)),
   Is.EqualTo(vanilla.Processes.Select(p => (p.Process, p.Companies, p.MeasuredCompanies, p.ActualOutputPerDay))));
  Assert.That(factory.Resources.Select(r => (r.Resource, r.ActualPerDay, r.Producers)), Is.EqualTo(vanilla.Resources.Select(r => (r.Resource, r.ActualPerDay, r.Producers))));
  Assert.That(factory.AllBottlenecks.Select(b => b.Key), Is.Not.Empty);
  Assert.That(factory.AllBottlenecks.Select(b => (b.Key, b.EvidenceValue)), Is.EqualTo(vanilla.AllBottlenecks.Select(b => (b.Key, b.EvidenceValue))));
  foreach (var metric in new Func<IndustrialObservationResult, IndustrialMetric>[] { r => r.Production, r => r.Logistics, r => r.Employment, r => r.Efficiency, r => r.Performance })
   Assert.That((metric(factory).Status, metric(factory).Value), Is.EqualTo((metric(vanilla).Status, metric(vanilla).Value)));
  // Comparison (C3) sees the two Areas as equal.
  var rows = IndustrialAreaComparison.Build(new[] {
   new IndustrialComparisonSource { StableId = 2, TypeId = (int)AreaTypeId.Industrial, Name = "Factory", Lifecycle = AreaLifecycleState.Active, Snapshot = factorySnapshot },
   new IndustrialComparisonSource { StableId = 3, TypeId = (int)AreaTypeId.Industrial, Name = "Vanilla", Lifecycle = AreaLifecycleState.Active, Snapshot = vanillaSnapshot } }, 3.1);
  Assert.That(rows, Has.Count.EqualTo(2));
  Assert.That((rows[0].Performance.Value, rows[0].Production.Value), Is.EqualTo((rows[1].Performance.Value, rows[1].Production.Value)));
 }
 [Test] public void AVacantFactoryIsAProducerFacilityWithoutInventedOutput() {
  var building = E(100);
  var observation = IndustrialObservationMapper.ToObservation(Building(building, FactoryPrefab, Area), Array.Empty<IndustrialCompanyRead>(), Registry());
  Assert.That(observation.IsProducer, Is.True);
  var result = Run(observation, new IndustrialRuntimeSnapshot());
  Assert.That(result.Processes, Is.Empty); Assert.That(result.Resources, Is.Empty, "no company yet, no production");
 }
 [Test] public void ManualOwnershipMovesTheFactoryCompanyLikeAnyOther() {
  var assignments = new IndustrialCompanyAssignmentIndex();
  assignments.Rebuild(new[] { new IndustrialCompanyClaim(ManualArea, E(1), E(100)) });
  var manual = Run(Observe(FactoryPrefab, ManualArea, assignments), new IndustrialRuntimeSnapshot());
  var primary = Run(Observe(FactoryPrefab, Area, assignments), new IndustrialRuntimeSnapshot());
  Assert.That(manual.Processes.Single().ActualOutputPerDay, Is.EqualTo(30), "the manual owner gets the company's production");
  Assert.That(primary.Processes, Is.Empty, "the geometric primary no longer counts it");
 }
}
