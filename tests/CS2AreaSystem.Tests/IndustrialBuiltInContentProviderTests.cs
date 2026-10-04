using CS2AreaSystem.Core.Semantics;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 D2 Task 2: the reference factory is optional built-in content. The shared C2 provider registers it as an
// IndustrialProducer only when the loaded prefab still matches the process contract; its absence changes nothing.
public class IndustrialBuiltInContentProviderTests
{
 private static Entity E(int index, int version = 1) => new() { Index = index, Version = version };
 private static AreaBuiltInBuildingFacts Matching() => new() { HasProperty = true, Manufactured = (ulong)Resource.Furniture, Signature = true, IndustrialZone = true };
 private static AreaBuiltInPrefabCandidate Factory(AreaBuiltInBuildingFacts? facts, int version = 1, string name = IndustrialReferenceFactoryContract.PrefabName) =>
  new(E(40, version), AreaBuiltInAssetContract.PrefabType, name, true, false, "AS_FurnitureFactory.cok", facts);
 private static AreaBuiltInPrefabCandidate[] RequiredThree() => new[] {
  new AreaBuiltInPrefabCandidate(E(1), "BuildingPrefab", "AS_ParkHeadquarters", true, false, "AS_ParkHeadquarters.cok"),
  new AreaBuiltInPrefabCandidate(E(2), "BuildingPrefab", "AS_IndustrialHeadquarters", true, false, "AS_IndustrialHeadquarters.cok"),
  new AreaBuiltInPrefabCandidate(E(3), "BuildingPrefab", "AS_ParkGate", true, false, "AS_ParkHeadquarters.cok") };
 private static bool Producer(AreaAssetSemanticRegistry registry, Entity prefab) =>
  registry.HasProviderAllow(AreaBuiltInAssetContract.ProviderId, prefab, AreaAssetSemantic.IndustrialProducer);

 [Test] public void TheOptionalEntryMatchesTheIndustrialContract() {
  var factory = AreaBuiltInAssetContract.Optional.Single();
  Assert.That(factory.PrefabName, Is.EqualTo(IndustrialReferenceFactoryContract.PrefabName));
  Assert.That(factory.Semantic, Is.EqualTo(AreaAssetSemantic.IndustrialProducer));
  Assert.That(factory.RequiredManufactured, Is.EqualTo((ulong)IndustrialReferenceFactoryContract.Output));
  Assert.That(AreaBuiltInAssetContract.Required.All(a => a.RequiredManufactured == 0), Is.True, "C2 anchors keep their identity-only rule");
  Assert.That(AreaBuiltInAssetContract.RequiredSemantics & AreaAssetSemantic.IndustrialProducer, Is.EqualTo(AreaAssetSemantic.None), "the factory is never required");
 }
 [Test] public void PresentMatchingFactoryRegistersOnlyTheProducerRole() {
  var registry = new AreaAssetSemanticRegistry();
  var result = new AreaBuiltInContentSemanticProvider().Refresh(registry, RequiredThree().Append(Factory(Matching())));
  Assert.That(Producer(registry, E(40)), Is.True);
  Assert.That(result.Missing, Is.EqualTo(AreaAssetSemantic.None));
  Assert.That(result.Rejected, Is.EqualTo(0));
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(40), AreaAssetSemantic.IndustrialHeadquarters), Is.False, "never an anchor");
  // A vacant factory is still a producer facility through the generic classifier; no factory-specific branch.
  var traits = new IndustrialFacilityTraits { IsBuilding = true, Prefab = E(40) };
  Assert.That(IndustrialFacilityClassifier.Classify(traits, registry) & IndustrialFacilityRole.Processor, Is.EqualTo(IndustrialFacilityRole.Processor));
 }
 [Test] public void AbsentFactoryChangesNothing() {
  var registry = new AreaAssetSemanticRegistry();
  var result = new AreaBuiltInContentSemanticProvider().Refresh(registry, RequiredThree());
  Assert.That(result.Missing, Is.EqualTo(AreaAssetSemantic.None), "optional content is never missing");
  Assert.That(result.Rejected, Is.EqualTo(0));
  Assert.That(registry.HasRegisteredAllow(AreaAssetSemantic.IndustrialProducer), Is.False);
 }
 private static IEnumerable<TestCaseData> Mismatches() {
  AreaBuiltInBuildingFacts With(Action<AreaBuiltInBuildingFacts> change) { var f = Matching(); change(f); return f; }
  yield return new TestCaseData(null).SetName("NoFacts");
  yield return new TestCaseData(With(f => f.HasProperty = false)).SetName("NoBuildingProperties");
  yield return new TestCaseData(With(f => f.Manufactured = (ulong)Resource.Paper)).SetName("OtherResource");
  yield return new TestCaseData(With(f => f.Manufactured = (ulong)(Resource.Furniture | Resource.Paper))).SetName("ExtraResource");
  yield return new TestCaseData(With(f => f.Sold = (ulong)Resource.Furniture)).SetName("Sells");
  yield return new TestCaseData(With(f => f.Stored = (ulong)Resource.Timber)).SetName("Stores");
  yield return new TestCaseData(With(f => f.Residential = 4)).SetName("Residential");
  yield return new TestCaseData(With(f => f.Signature = false)).SetName("NotSignature");
  yield return new TestCaseData(With(f => f.IndustrialZone = false)).SetName("NotIndustrialZone");
  yield return new TestCaseData(With(f => f.OfficeZone = true)).SetName("OfficeZone");
 }
 [TestCaseSource(nameof(Mismatches))]
 public void AMismatchedImportIsRejected(AreaBuiltInBuildingFacts? facts) {
  var registry = new AreaAssetSemanticRegistry();
  var result = new AreaBuiltInContentSemanticProvider().Refresh(registry, RequiredThree().Append(Factory(facts)));
  Assert.That(Producer(registry, E(40)), Is.False);
  Assert.That(result.Rejected, Is.EqualTo(1));
  Assert.That(result.Missing, Is.EqualTo(AreaAssetSemantic.None), "the anchors are unaffected");
 }
 [Test] public void TheFactoryFollowsTheOwnedPackageAndExactNameRules() {
  var registry = new AreaAssetSemanticRegistry();
  new AreaBuiltInContentSemanticProvider().Refresh(registry, new[] {
   new AreaBuiltInPrefabCandidate(E(41), "BuildingPrefab", IndustrialReferenceFactoryContract.PrefabName, false, false, null, Matching()),
   new AreaBuiltInPrefabCandidate(E(42), "BuildingPrefab", IndustrialReferenceFactoryContract.PrefabName, true, false, "Other.cok", Matching()),
   Factory(Matching(), name: "AS_FurnitureFactory2") });
  Assert.That(registry.HasRegisteredAllow(AreaAssetSemantic.IndustrialProducer), Is.False);
 }
 [Test] public void ReloadRebindsTheFactoryToTheCurrentPrefabEntity() {
  var registry = new AreaAssetSemanticRegistry(); var provider = new AreaBuiltInContentSemanticProvider();
  provider.Refresh(registry, new[] { Factory(Matching(), version: 1) });
  provider.Refresh(registry, new[] { Factory(Matching(), version: 2) });
  Assert.That(Producer(registry, E(40, 1)), Is.False);
  Assert.That(Producer(registry, E(40, 2)), Is.True);
  provider.Refresh(registry, Array.Empty<AreaBuiltInPrefabCandidate>());
  Assert.That(registry.HasRegisteredAllow(AreaAssetSemantic.IndustrialProducer), Is.False, "removed content disappears on the next load");
 }
}
