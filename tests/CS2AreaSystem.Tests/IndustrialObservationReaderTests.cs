using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using Game.Prefabs;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Wave A4: the reader copies the tenant's vanilla IndustrialProcessData into the blittable company read, and the
// mapper carries it to the observation without changing activity or flow semantics.
public class IndustrialObservationReaderTests {
 private static readonly Entity factory = new() { Index = 100, Version = 1 };
 private static IndustrialProcessData Recipe(Resource in1, int a1, Resource in2, int a2, Resource output, int amount) => new() {
  m_Input1 = new ResourceStack { m_Resource = in1, m_Amount = a1 }, m_Input2 = new ResourceStack { m_Resource = in2, m_Amount = a2 },
  m_Output = new ResourceStack { m_Resource = output, m_Amount = amount } };
 private static IndustrialBuildingRead Factory() => new() { Building = factory, Exists = true, Available = true,
  Traits = new IndustrialFacilityTraits { IsBuilding = true, ManufacturesGoods = true, HasProcessorCompany = true } };
 [Test] public void BothInputsOutputAndRecipeAmountsAreCopied() {
  var c = new IndustrialCompanyRead { Producer = true };
  Assert.That(IndustrialProcessDiagnostics.TryCopy(Recipe(Resource.Wood, 2, Resource.Plastics, 1, Resource.Furniture, 3), ref c), Is.True);
  Assert.That(c.HasProcess, Is.True);
  var key = IndustrialProcessDiagnostics.KeyOf(c);
  Assert.That(key.Input1, Is.EqualTo((ulong)Resource.Wood)); Assert.That(key.Input1Amount, Is.EqualTo(2));
  Assert.That(key.Input2, Is.EqualTo((ulong)Resource.Plastics)); Assert.That(key.Input2Amount, Is.EqualTo(1));
  Assert.That(key.Output, Is.EqualTo((ulong)Resource.Furniture)); Assert.That(key.OutputAmount, Is.EqualTo(3));
 }
 [Test] public void MissingOrEmptyInputsAreNormalizedAndNoOutputIsNoProcess() {
  var c = new IndustrialCompanyRead { Producer = true };
  Assert.That(IndustrialProcessDiagnostics.TryCopy(Recipe(Resource.NoResource, 0, Resource.Grain, 0, Resource.Wood, 1), ref c), Is.True);
  var key = IndustrialProcessDiagnostics.KeyOf(c);
  Assert.That(key.InputCount, Is.Zero, "an input with no amount is not a material input");
  Assert.That(key.Input2, Is.Zero); Assert.That(key.Input2Amount, Is.Zero);
  var none = new IndustrialCompanyRead { Producer = true };
  Assert.That(IndustrialProcessDiagnostics.TryCopy(Recipe(Resource.Wood, 1, Resource.NoResource, 0, Resource.NoResource, 0), ref none), Is.False);
  Assert.That(none.HasProcess, Is.False);
 }
 [Test] public void MapperCarriesProcessWithoutChangingActivityOrFlow() {
  var with = new IndustrialCompanyRead { Producer = true, HasJobs = true, Jobs = 20, Employees = 20, HasActivity = true, ActualPerDay = 30, CapacityPerDay = 60,
   OutputResource = (ulong)Resource.Furniture, Input = IndustrialFlowState.Affected, Output = IndustrialFlowState.Healthy };
  var without = with;
  IndustrialProcessDiagnostics.TryCopy(Recipe(Resource.Wood, 2, Resource.Plastics, 1, Resource.Furniture, 3), ref with);
  var a = IndustrialObservationMapper.ToObservation(Factory(), new[] { with });
  var b = IndustrialObservationMapper.ToObservation(Factory(), new[] { without });
  Assert.That(a.ProductiveActivity, Is.EqualTo(b.ProductiveActivity)); Assert.That(a.Input, Is.EqualTo(b.Input)); Assert.That(a.Output, Is.EqualTo(b.Output));
  Assert.That(a.IsOperatingProducer, Is.EqualTo(b.IsOperatingProducer)); Assert.That(a.Resources, Is.EqualTo(b.Resources));
  Assert.That(a.Processes, Has.Length.EqualTo(1));
  Assert.That(a.Processes![0].Process, Is.EqualTo(IndustrialProcessDiagnostics.KeyOf(with)));
  Assert.That(a.Processes[0].Measured, Is.True); Assert.That(a.Processes[0].ActualPerDay, Is.EqualTo(30));
  Assert.That(b.Processes, Is.Empty, "no valid process data, no relation");
 }
 [Test] public void UnmeasuredCompanyKeepsItsRecipeWithoutAnInventedAmount() {
  var c = new IndustrialCompanyRead { Producer = true, HasJobs = true, Jobs = 20, Employees = 0 };
  IndustrialProcessDiagnostics.TryCopy(Recipe(Resource.Wood, 2, Resource.NoResource, 0, Resource.Furniture, 1), ref c);
  var obs = IndustrialObservationMapper.ToObservation(Factory(), new[] { c });
  Assert.That(obs.Processes![0].Measured, Is.False);
  Assert.That(obs.Processes[0].ActualPerDay, Is.Zero);
 }
 [Test] public void NonProducerTenantsCarryNoProcess() {
  var storage = new IndustrialCompanyRead { Storage = true };
  IndustrialProcessDiagnostics.TryCopy(Recipe(Resource.Wood, 1, Resource.NoResource, 0, Resource.Furniture, 1), ref storage);
  var building = new IndustrialBuildingRead { Building = factory, Exists = true, Available = true,
   Traits = new IndustrialFacilityTraits { IsBuilding = true, HasStorageProperty = true, HasStorageCompany = true } };
  Assert.That(IndustrialObservationMapper.ToObservation(building, new[] { storage }).Processes, Is.Empty);
 }
}
