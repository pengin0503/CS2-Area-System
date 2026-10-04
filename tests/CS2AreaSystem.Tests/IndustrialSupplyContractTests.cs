using CS2AreaSystem.Core.Persistence;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using Game.Economy;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 1: a source Industrial Area persists only the configuration of its advisory supply contracts.
public class IndustrialSupplyContractTests
{
 private const ulong Source = 11, Destination = 22, Other = 33;
 private static IndustrialSupplyContract Contract(int id = 3, ulong destination = Destination, Resource resource = Resource.Timber, int target = 1500, bool enabled = true) =>
  new() { SchemaVersion = IndustrialSupplyContract.CurrentSchemaVersion, ContractId = id, DestinationStableId = destination, Resource = (ulong)resource, TargetPerDay = target, Enabled = enabled };

 [Test] public void ContractSchemaV1WritesExactlyTheConfiguration() {
  var io = MemorySerializer.Create();
  Contract().Serialize(io.Writer);
  Assert.That(io.Data.Tokens, Is.EqualTo(new object[] { (ushort)1, 3, Destination, (ulong)Resource.Timber, 1500, true }));
  var copy = new IndustrialSupplyContract(); copy.Deserialize(io.Reader);
  Assert.That(io.Data.Position, Is.EqualTo(io.Data.Tokens.Count));
  Assert.That((copy.ContractId, copy.DestinationStableId, copy.Resource, copy.TargetPerDay, copy.Enabled), Is.EqualTo((3, Destination, (ulong)Resource.Timber, 1500, true)));
 }
 [Test] public void StateSchemaV1WritesOnlyTheNextContractId() {
  var io = MemorySerializer.Create();
  new IndustrialSupplyContractState { SchemaVersion = 1, NextContractId = 7 }.Serialize(io.Writer);
  Assert.That(io.Data.Tokens, Is.EqualTo(new object[] { (ushort)1, 7 }));
  Assert.That(MemorySerializer.RoundTrip(new IndustrialSupplyContractState { NextContractId = 7 }).NextContractId, Is.EqualTo(7));
  Assert.That(IndustrialSupplyContractState.CreateDefault().NextContractId, Is.EqualTo(1));
 }
 [Test] public void UnsupportedFutureSchemasAreRejectedBeforeReadingPayload() {
  foreach (var value in new Colossal.Serialization.Entities.ISerializable[] { new IndustrialSupplyContract(), new IndustrialSupplyContractState() }) {
   var io = MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(99);
   Assert.Throws<System.IO.InvalidDataException>(() => value.Deserialize(io.Reader));
   Assert.That(io.Data.Position, Is.EqualTo(1));
  }
 }
 [Test] public void LoadNormalizesAnOutOfRangeTarget() {
  var io = MemorySerializer.Create();
  io.Data.Tokens.AddRange(new object[] { (ushort)1, 3, Destination, (ulong)Resource.Timber, 999_999_999, true });
  var copy = new IndustrialSupplyContract(); copy.Deserialize(io.Reader);
  Assert.That(copy.TargetPerDay, Is.EqualTo(IndustrialSupplyContractModel.MaxTargetPerDay));
 }
 [TestCase(Resource.Grain, true)] [TestCase(Resource.Fish, true)] [TestCase(Resource.Timber, true)]
 [TestCase(Resource.NoResource, false)] [TestCase(Resource.Money, false)] [TestCase(Resource.Last, false)] [TestCase(Resource.All, false)]
 public void OnlyOneExistingNonMoneyVanillaResourceIsValid(Resource resource, bool valid) =>
  Assert.That(IndustrialSupplyContractModel.IsValidResource((ulong)resource), Is.EqualTo(valid));
 [Test] public void CombinedOrUndefinedResourceBitsAreInvalid() {
  Assert.That(IndustrialSupplyContractModel.IsValidResource((ulong)(Resource.Grain | Resource.Wood)), Is.False);
  Assert.That(IndustrialSupplyContractModel.IsValidResource((ulong)Resource.Last << 1), Is.False);
 }
 [TestCase(0, 50)] [TestCase(74, 50)] [TestCase(75, 100)] [TestCase(1020, 1000)] [TestCase(int.MaxValue, 50000)] [TestCase(-5, 50)]
 public void TargetIsClampedAndSnappedToTheStep(int raw, int normalized) =>
  Assert.That(IndustrialSupplyContractModel.NormalizeTarget(raw), Is.EqualTo(normalized));
 [Test] public void UpsertCreatesWithMonotonicIdsAndUpdatesTheSameDestinationAndResource() {
  var list = new List<IndustrialSupplyContract>(); var state = IndustrialSupplyContractState.CreateDefault();
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Destination, (ulong)Resource.Timber, 1000, out var first), Is.EqualTo(IndustrialSupplyContractEdit.Created));
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Other, (ulong)Resource.Timber, 1000, out var second), Is.EqualTo(IndustrialSupplyContractEdit.Created));
  Assert.That((first, second, state.NextContractId), Is.EqualTo((1, 2, 3)));
  list[0] = list[0] with { Enabled = false };
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Destination, (ulong)Resource.Timber, 2020, out var updated), Is.EqualTo(IndustrialSupplyContractEdit.Updated));
  Assert.That((updated, list.Count, list[0].TargetPerDay, list[0].Enabled), Is.EqualTo((1, 2, 2000, true)), "no ambiguous duplicate; the update re-enables it");
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Destination, (ulong)Resource.Timber, 2000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Unchanged));
  Assert.That(IndustrialSupplyContractModel.Remove(list, 1), Is.True);
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Destination, (ulong)Resource.Timber, 1000, out var recreated), Is.EqualTo(IndustrialSupplyContractEdit.Created));
  Assert.That(recreated, Is.EqualTo(3), "a removed id is never reused");
 }
 [Test] public void UpsertRejectsInvalidInputsAndTheCap() {
  var list = new List<IndustrialSupplyContract>(); var state = IndustrialSupplyContractState.CreateDefault();
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Source, (ulong)Resource.Timber, 1000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Rejected), "self");
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, 0, (ulong)Resource.Timber, 1000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Rejected), "no destination");
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, Destination, (ulong)Resource.Money, 1000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Rejected), "money");
  Assert.That(list, Is.Empty); Assert.That(state.NextContractId, Is.EqualTo(1));
  for (int i = 0; i < IndustrialSupplyContractModel.MaxContracts; i++)
   Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, 100 + (ulong)i, (ulong)Resource.Timber, 1000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Created));
  Assert.That(IndustrialSupplyContractModel.MaxContracts, Is.EqualTo(32));
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, 999, (ulong)Resource.Timber, 1000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Rejected), "cap");
  Assert.That(IndustrialSupplyContractModel.Upsert(list, ref state, Source, 100, (ulong)Resource.Timber, 3000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Updated), "updates still work at the cap");
  var exhausted = new IndustrialSupplyContractState { SchemaVersion = 1, NextContractId = int.MaxValue };
  Assert.That(IndustrialSupplyContractModel.Upsert(new List<IndustrialSupplyContract>(), ref exhausted, Source, Destination, (ulong)Resource.Timber, 1000, out _), Is.EqualTo(IndustrialSupplyContractEdit.Rejected), "ids never wrap");
 }
 [Test] public void EditsAddressContractsOnlyByStableId() {
  var list = new List<IndustrialSupplyContract> { Contract(id: 4), Contract(id: 9, destination: Other) };
  Assert.That(IndustrialSupplyContractModel.SetTarget(list, 9, 2510), Is.True); Assert.That(list[1].TargetPerDay, Is.EqualTo(2500));
  Assert.That(IndustrialSupplyContractModel.SetTarget(list, 9, 2500), Is.False, "unchanged");
  Assert.That(IndustrialSupplyContractModel.Toggle(list, 4), Is.True); Assert.That(list[0].Enabled, Is.False);
  Assert.That(IndustrialSupplyContractModel.Toggle(list, 5), Is.False, "unknown id");
  Assert.That(IndustrialSupplyContractModel.Remove(list, 5), Is.False);
  Assert.That(IndustrialSupplyContractModel.Remove(list, 4), Is.True); Assert.That(list.Single().ContractId, Is.EqualTo(9));
 }
 [Test] public void SavedContractsWithImpossibleValuesWarnWithoutQuarantiningTheArea() {
  var report = new AreaSavedStateReport();
  IndustrialSavedStateValidator.ValidateContracts(new[] { Contract(id: 4), Contract(id: 4, destination: Other), Contract(id: 8, resource: Resource.Money), Contract(id: 9, destination: Source) },
   new IndustrialSupplyContractState { SchemaVersion = 1, NextContractId = 5 }, Source, report);
  Assert.That(report.IsInvalid, Is.False);
  Assert.That(report.Issues.Select(i => i.Field), Is.SupersetOf(new[] { "IndustrialSupplyContract.ContractId", "IndustrialSupplyContract.Resource", "IndustrialSupplyContract.DestinationStableId", "IndustrialSupplyContractState.NextContractId" }));
  var clean = new AreaSavedStateReport();
  IndustrialSavedStateValidator.ValidateContracts(new[] { Contract(id: 4) }, new IndustrialSupplyContractState { SchemaVersion = 1, NextContractId = 5 }, Source, clean);
  Assert.That(clean.Issues, Is.Empty);
 }
}
