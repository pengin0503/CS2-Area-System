using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Wave A4: observed input -> output processes are grouped by exact recipe inside the existing observation cycle.
public class IndustrialProcessDiagnosticsTests {
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static IndustrialProcessKey Key(Resource in1, int a1, Resource in2, int a2, Resource output, int amount = 1) =>
  new((ulong)in1, a1, (ulong)in2, a2, (ulong)output, amount);
 private static IndustrialEntityObservation Producer(int index, IndustrialProcessKey process, double actual, bool measured = true, bool available = true,
  IndustrialFacilityRole roles = IndustrialFacilityRole.Processor) => new() {
  Facility = E(index), Roles = roles, IsProducer = true, ProducerTenants = 1, Available = available, IsOperatingProducer = actual > 0,
  ProductiveActivity = measured ? actual : null, Resources = measured ? new[] { new IndustrialResourceActivity { Resource = process.Output, ActualPerDay = actual, CapacityPerDay = 100 } } : Array.Empty<IndustrialResourceActivity>(),
  Processes = new[] { new IndustrialProcessActivity(process, measured, actual) },
  Input = IndustrialFlowState.Healthy, Output = IndustrialFlowState.Healthy, Jobs = 10, Employees = 10, VacanciesByLevel = new int[5] };
 private static IndustrialObservationResult Run(params IndustrialEntityObservation[] observations) {
  var acc = new IndustrialCycleAccumulator();
  foreach (var o in observations) IndustrialObservationAggregator.Apply(acc, o);
  return IndustrialObservationAggregator.Finalize(acc, new IndustrialRuntimeSnapshot(),
   new IndustrialCycleInfo { CycleId = 1, HourBucket = 3, StartHours = 3, EndHours = 3.1, MembershipToken = 1, StructureToken = 1, Active = true });
 }
 private static readonly IndustrialProcessKey furnitureFromWood = Key(Resource.Wood, 2, Resource.NoResource, 0, Resource.Furniture);
 private static readonly IndustrialProcessKey furnitureFromPlastic = Key(Resource.Plastics, 1, Resource.Wood, 1, Resource.Furniture);
 [Test] public void SameRecipeAggregatesCompanyCountAndOutputRate() {
  var r = Run(Producer(1, furnitureFromWood, 30), Producer(2, furnitureFromWood, 12.5));
  var p = r.Processes.Single();
  Assert.That(p.Process, Is.EqualTo(furnitureFromWood));
  Assert.That(p.Companies, Is.EqualTo(2)); Assert.That(p.MeasuredCompanies, Is.EqualTo(2));
  Assert.That(p.ActualOutputPerDay, Is.EqualTo(42.5).Within(1e-9));
 }
 [Test] public void SameOutputWithDifferentInputsStaysSeparate() {
  var r = Run(Producer(1, furnitureFromWood, 30), Producer(2, furnitureFromPlastic, 50));
  Assert.That(r.Processes, Has.Count.EqualTo(2), "never merged solely because they share an output resource");
  Assert.That(r.Processes[0].Process, Is.EqualTo(furnitureFromPlastic), "ordered by actual output");
  Assert.That(Key(Resource.Wood, 2, Resource.NoResource, 0, Resource.Furniture, 1), Is.Not.EqualTo(Key(Resource.Wood, 3, Resource.NoResource, 0, Resource.Furniture, 1)),
   "recipe amounts are part of the process identity");
 }
 [Test] public void ExtractorWithoutMaterialInputIsAnOutputOnlyProcess() {
  var wood = Key(Resource.NoResource, 0, Resource.NoResource, 0, Resource.Wood);
  var r = Run(Producer(1, wood, 80, roles: IndustrialFacilityRole.Extractor));
  Assert.That(r.Processes.Single().Process.InputCount, Is.Zero);
  Assert.That(r.Processes[0].Process.Output, Is.EqualTo((ulong)Resource.Wood));
 }
 [Test] public void UnmeasuredOrUnusableCompaniesAreCountedWithoutInventingOutput() {
  var r = Run(Producer(1, furnitureFromWood, 30), Producer(2, furnitureFromWood, 0, measured: false), Producer(3, furnitureFromWood, 40, available: false));
  var p = r.Processes.Single();
  Assert.That(p.Companies, Is.EqualTo(3)); Assert.That(p.MeasuredCompanies, Is.EqualTo(2), "the unmeasured company is partial coverage");
  Assert.That(p.ActualOutputPerDay, Is.EqualTo(30).Within(1e-9), "an unusable facility produces nothing");
 }
 [Test] public void OfficeAndCommercialTenantsProduceNoIndustrialProcess() {
  var office = new IndustrialEntityObservation { Facility = E(9), Roles = IndustrialFacilityRole.None, Available = true, Jobs = 500, Employees = 500,
   Processes = new[] { new IndustrialProcessActivity(furnitureFromWood, true, 99) } };
  var mapped = IndustrialObservationMapper.ToObservation(new IndustrialBuildingRead { Building = E(9), Exists = true, Available = true,
   Traits = new IndustrialFacilityTraits { IsBuilding = true, HasOfficeProperty = true, HasOfficeCompany = true } }, Array.Empty<IndustrialCompanyRead>());
  Assert.That(mapped.Processes, Is.Null.Or.Empty);
  Assert.That(Run(office).Processes, Is.Empty, "an ineligible facility never contributes a process");
 }
 [Test] public void OrderingIsDeterministicForEqualOutput() {
  var a = Key(Resource.Wood, 1, Resource.NoResource, 0, Resource.Paper);
  var b = Key(Resource.Wood, 1, Resource.NoResource, 0, Resource.Furniture);
  var first = Run(Producer(1, a, 10), Producer(2, b, 10)).Processes.Select(p => p.Process).ToList();
  var second = Run(Producer(2, b, 10), Producer(1, a, 10)).Processes.Select(p => p.Process).ToList();
  Assert.That(first, Is.EqualTo(second));
 }
}
