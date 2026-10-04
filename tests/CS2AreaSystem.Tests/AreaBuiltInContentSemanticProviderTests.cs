using System.Linq;
using CS2AreaSystem.Core.Semantics;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 C2 Task 3: the built-in provider registers only exact, owned AS_ prefabs and rebuilds every load generation.
public class AreaBuiltInContentSemanticProviderTests
{
 private static Entity E(int index, int version = 1) => new() { Index = index, Version = version };
 private static AreaBuiltInPrefabCandidate Owned(int index, string name, int version = 1) =>
  new(E(index, version), AreaBuiltInAssetContract.PrefabType, name, packaged: true, builtIn: false, packageName: "AS_ParkHeadquarters.cok");
 private static AreaBuiltInPrefabCandidate[] AllThree(int version = 1) => new[] {
  Owned(1, "AS_ParkHeadquarters", version), Owned(2, "AS_IndustrialHeadquarters", version), Owned(3, "AS_ParkGate", version) };
 [Test] public void PresentContentRegistersEachRoleOnItsExactPrefab() {
  var registry = new AreaAssetSemanticRegistry();
  var result = new AreaBuiltInContentSemanticProvider().Refresh(registry, AllThree());
  Assert.That(result.Registered, Is.EqualTo(AreaBuiltInAssetContract.RequiredSemantics));
  Assert.That(result.Missing, Is.EqualTo(AreaAssetSemantic.None));
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(1), AreaAssetSemantic.ParkHeadquarters), Is.True);
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(2), AreaAssetSemantic.IndustrialHeadquarters), Is.True);
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(3), AreaAssetSemantic.ParkEntrance), Is.True);
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(1), AreaAssetSemantic.ParkEntrance), Is.False, "one semantic per prefab");
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(3), AreaAssetSemantic.ParkHeadquarters), Is.False);
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(registry, AreaAssetSemantic.ParkEntrance), Is.True);
 }
 [Test] public void AbsentContentRegistersNothingForThatRoleAndFabricatesNoFallback() {
  var registry = new AreaAssetSemanticRegistry();
  var result = new AreaBuiltInContentSemanticProvider().Refresh(registry, new[] { Owned(1, "AS_ParkHeadquarters") });
  Assert.That(result.Registered, Is.EqualTo(AreaAssetSemantic.ParkHeadquarters));
  Assert.That(result.Missing, Is.EqualTo(AreaAssetSemantic.IndustrialHeadquarters | AreaAssetSemantic.ParkEntrance));
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(registry, AreaAssetSemantic.ParkEntrance), Is.False);
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(registry, AreaAssetSemantic.IndustrialHeadquarters), Is.False);
  Assert.That(registry.HasRegisteredAllow(AreaAssetSemantic.ParkEntrance), Is.False);
 }
 [Test] public void LookAlikeUnownedAndWrongTypePrefabsAreRejected() {
  var registry = new AreaAssetSemanticRegistry();
  var result = new AreaBuiltInContentSemanticProvider().Refresh(registry, new[] {
   new AreaBuiltInPrefabCandidate(E(10), "BuildingPrefab", "AS_ParkGate", false, false, null),                 // unpackaged local copy
   new AreaBuiltInPrefabCandidate(E(11), "BuildingPrefab", "AS_ParkGate", true, true, "AS_ParkGate.cok"),      // base game / DLC database
   new AreaBuiltInPrefabCandidate(E(12), "BuildingPrefab", "AS_ParkGate", true, false, "OtherMod.cok"),        // foreign package
   new AreaBuiltInPrefabCandidate(E(13), "StaticObjectPrefab", "AS_ParkGate", true, false, "AS_ParkGate.cok"), // not a building prefab
   new AreaBuiltInPrefabCandidate(E(14), "BuildingPrefab", "AS_ParkGate01", true, false, "AS_ParkGate.cok"),   // look-alike name
   new AreaBuiltInPrefabCandidate(E(15), "BuildingPrefab", "ParkGate01_2x1", true, false, "AS_ParkGate.cok"),  // authoring name
   new AreaBuiltInPrefabCandidate(Entity.Null, "BuildingPrefab", "AS_ParkGate", true, false, "AS_ParkGate.cok") });
  Assert.That(result.Registered, Is.EqualTo(AreaAssetSemantic.None));
  Assert.That(result.Rejected, Is.EqualTo(5), "exact-name candidates that failed ownership/type/entity checks are reported");
  for (int i = 10; i <= 15; i++) Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(i), AreaAssetSemantic.ParkEntrance), Is.False);
 }
 [Test] public void ReloadUnregistersTheStaleGenerationAndRegistersCurrentEntities() {
  var registry = new AreaAssetSemanticRegistry(); var provider = new AreaBuiltInContentSemanticProvider();
  provider.Refresh(registry, AllThree(version: 1));
  long before = registry.Revision;
  var result = provider.Refresh(registry, AllThree(version: 2));
  Assert.That(registry.Revision, Is.GreaterThan(before), "activation re-validates every Area after a rebuild");
  Assert.That(result.Registered, Is.EqualTo(AreaBuiltInAssetContract.RequiredSemantics));
  foreach (var index in new[] { 1, 2, 3 }) {
   Assert.That(AreaBuiltInAssetContract.Required.Any(a => AreaBuiltInAssetContract.Qualifies(registry, E(index, 1), a.Semantic)), Is.False, "stale prefab entity");
   Assert.That(AreaBuiltInAssetContract.Required.Any(a => AreaBuiltInAssetContract.Qualifies(registry, E(index, 2), a.Semantic)), Is.True);
  }
  provider.Refresh(registry, new AreaBuiltInPrefabCandidate[0]);
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(registry, AreaAssetSemantic.ParkHeadquarters), Is.False, "removed content disappears on the next load");
 }
 // Only the built-in provider makes a dedicated anchor; another provider's Allow cannot create one, but registry Deny still wins.
 [Test] public void ForeignProvidersCannotGrantDedicatedRolesButRegistryPrecedenceIsKept() {
  var registry = new AreaAssetSemanticRegistry();
  registry.Register("Other.Mod", E(20), new AreaAssetSemanticPatch(AreaAssetSemantic.ParkHeadquarters | AreaAssetSemantic.ParkEntrance | AreaAssetSemantic.IndustrialHeadquarters, AreaAssetSemantic.None, 100));
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(20), AreaAssetSemantic.ParkHeadquarters), Is.False);
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(registry, AreaAssetSemantic.ParkEntrance), Is.False);
  new AreaBuiltInContentSemanticProvider().Refresh(registry, AllThree());
  registry.Register("Other.Mod", E(3), new AreaAssetSemanticPatch(AreaAssetSemantic.None, AreaAssetSemantic.ParkEntrance, 100));
  Assert.That(AreaBuiltInAssetContract.Qualifies(registry, E(3), AreaAssetSemantic.ParkEntrance), Is.False);
  Assert.That(AreaBuiltInAssetContract.Qualifies(null, E(3), AreaAssetSemantic.ParkEntrance), Is.False, "no registry, no dedicated anchor");
 }
 [Test] public void RefreshTouchesOnlyItsOwnProviderRegistrations() {
  var registry = new AreaAssetSemanticRegistry();
  registry.Register("Other.Mod", E(30), new AreaAssetSemanticPatch(AreaAssetSemantic.ParkLeisure, AreaAssetSemantic.None, 0));
  new AreaBuiltInContentSemanticProvider().Refresh(registry, AllThree());
  new AreaBuiltInContentSemanticProvider().Refresh(registry, new AreaBuiltInPrefabCandidate[0]);
  Assert.That(registry.Resolve(E(30), AreaAssetSemantic.ParkLeisure, false), Is.True);
 }
}
