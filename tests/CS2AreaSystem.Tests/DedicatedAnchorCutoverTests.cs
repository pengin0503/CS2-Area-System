using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Semantics;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 C2 Task 4: only the three registered AS_ prefabs satisfy Headquarters/Entrance; the Foundation office,
// Park Maintenance and derived dedicated-Path sources are retired with no fallback.
public class DedicatedAnchorCutoverTests
{
 private sealed class Store : IAreaAnchorStore {
  public Dictionary<Entity, IReadOnlyList<AreaAnchorLink>> Values = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area, out var links) ? links : Array.Empty<AreaAnchorLink>();
  public void Write(Entity area, IReadOnlyList<AreaAnchorLink> links) => Values[area] = links.ToArray();
 }
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static readonly Entity Area = E(1), ParkHqPrefab = E(101), IndustrialHqPrefab = E(102), GatePrefab = E(103), OtherPrefab = E(104);
 private static AreaAssetSemanticRegistry Registry(bool content = true) {
  var registry = new AreaAssetSemanticRegistry();
  if (content) new AreaBuiltInContentSemanticProvider().Refresh(registry, new[] {
   new AreaBuiltInPrefabCandidate(ParkHqPrefab, "BuildingPrefab", "AS_ParkHeadquarters", true, false, "AS_ParkHeadquarters.cok"),
   new AreaBuiltInPrefabCandidate(IndustrialHqPrefab, "BuildingPrefab", "AS_IndustrialHeadquarters", true, false, "AS_IndustrialHeadquarters.cok"),
   new AreaBuiltInPrefabCandidate(GatePrefab, "BuildingPrefab", "AS_ParkGate", true, false, "AS_ParkHeadquarters.cok") });
  return registry;
 }
 [Test] public void OrdinaryOfficeIsNoLongerAnIndustrialHeadquarters() {
  var office = new IndustrialFacilityTraits { IsBuilding = true, HasOfficeProperty = true, HasOfficeCompany = true, HasWorkplaces = true, Prefab = OtherPrefab };
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(office, Registry()), Is.False);
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(office), Is.False, "no registry, no fallback");
 }
 [Test] public void ParkMaintenanceIsNoLongerAParkHeadquarters() {
  var maintenance = new ParkFacilityTraits { IsBuilding = true, HasParkMaintenance = true, Prefab = OtherPrefab };
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(maintenance, Registry()), Is.False);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(maintenance), Is.False);
 }
 [Test] public void TheExactRegisteredPrefabsQualifyOnlyForTheirOwnRole() {
  var registry = Registry();
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits { IsBuilding = true, Prefab = ParkHqPrefab }, registry), Is.True);
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(new IndustrialFacilityTraits { IsBuilding = true, Prefab = IndustrialHqPrefab }, registry), Is.True);
  Assert.That(ParkFacilityClassifier.IsEntranceEligible(new ParkFacilityTraits { IsBuilding = true, Prefab = GatePrefab }, registry), Is.True);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits { IsBuilding = true, Prefab = GatePrefab }, registry), Is.False);
  Assert.That(ParkFacilityClassifier.IsEntranceEligible(new ParkFacilityTraits { IsBuilding = true, Prefab = ParkHqPrefab }, registry), Is.False);
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(new IndustrialFacilityTraits { IsBuilding = true, Prefab = ParkHqPrefab }, registry), Is.False);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits { IsBuilding = false, Prefab = ParkHqPrefab }, registry), Is.False);
 }
 // A same-looking building (same traits, unregistered prefab) or one another mod claims cannot qualify.
 [Test] public void SameLookingOrForeignRegisteredBuildingsDoNotQualify() {
  var registry = Registry();
  registry.Register("Other.Mod", OtherPrefab, new AreaAssetSemanticPatch(AreaAssetSemantic.ParkHeadquarters | AreaAssetSemantic.ParkEntrance | AreaAssetSemantic.IndustrialHeadquarters, AreaAssetSemantic.None, 99));
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits { IsBuilding = true, HasParkMaintenance = true, Prefab = OtherPrefab }, registry), Is.False);
  Assert.That(ParkFacilityClassifier.IsEntranceEligible(new ParkFacilityTraits { IsBuilding = true, HasPark = true, HasRoadAccess = true, Prefab = OtherPrefab }, registry), Is.False);
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(new IndustrialFacilityTraits { IsBuilding = true, HasOfficeProperty = true, Prefab = OtherPrefab }, registry), Is.False);
 }
 [Test] public void MissingContentLeavesTheRoleUnsatisfiable() {
  var registry = Registry(content: false);
  Assert.That(ParkFacilityClassifier.IsHeadquartersEligible(new ParkFacilityTraits { IsBuilding = true, Prefab = ParkHqPrefab }, registry), Is.False);
  Assert.That(ParkFacilityClassifier.IsEntranceEligible(new ParkFacilityTraits { IsBuilding = true, Prefab = GatePrefab }, registry), Is.False);
 }
 [TestCase(false, false, false)] [TestCase(true, false, false)] [TestCase(false, true, false)] [TestCase(true, true, true)]
 public void ParkActivatesOnlyWithDedicatedHeadquartersAndGate(bool hq, bool gate, bool active) {
  var registry = Registry(); var store = new Store(); ParkAreaModule park = null;
  var prefabs = new Dictionary<Entity, Entity> { [E(10)] = ParkHqPrefab, [E(11)] = GatePrefab };
  var anchors = new AreaAnchorService(store, e => e != Entity.Null, (a, e, r) => park.AcceptsAnchor(a, e, r, default));
  park = new ParkAreaModule(anchors, (e, _) => new ParkFacilityTraits { IsBuilding = true, HasRoadAccess = true, Prefab = prefabs.TryGetValue(e, out var p) ? p : OtherPrefab }, registry);
  Assert.That(park.CanAssignAnchor(Area, AreaAnchorRole.Headquarters, default), Is.True);
  Assert.That(park.CanAssignAnchor(Area, AreaAnchorRole.Entrance, default), Is.True);
  if (hq) Assert.That(anchors.Assign(Area, E(10), AreaAnchorRole.Headquarters), Is.True);
  if (gate) Assert.That(anchors.Assign(Area, E(11), AreaAnchorRole.Entrance), Is.True);
  Assert.That(anchors.Assign(Area, E(12), AreaAnchorRole.Entrance), Is.False, "a non-gate building is never an entrance");
  Assert.That(park.ValidateActivation(Area, default), Is.EqualTo(active));
 }
 // The derived dedicated-Path entrance pipeline is removed, so a Path crossing without a gate cannot activate a Park.
 [Test] public void DerivedPathEntranceIsRetiredFromRuntimeAndActivation() {
  var assembly = typeof(ParkAreaModule).Assembly;
  foreach (var retired in new[] { "ParkEntranceSystem", "ParkPathEntranceDetector", "ParkEntranceEvidence", "ParkEntranceEvidenceStore", "ParkEntranceScan", "ParkEntranceAccess" })
   Assert.That(assembly.GetType("CS2AreaSystem.Modules.Park." + retired), Is.Null, retired);
  Assert.That(typeof(ParkAreaModule).GetInterfaces(), Does.Not.Contain(typeof(IAreaDerivedAnchorPolicy)));
  foreach (var source in new[] { "Modules/Park/ParkAreaModule.cs", "Modules/Park/ParkQualitySystem.cs", "Modules/Park/ParkConnectivitySystem.cs", "Modules/Park/ParkRuntimeSystem.cs",
   "Modules/Park/ParkRuntimeSnapshot.cs", "Modules/Park/ParkGameplayFacade.cs", "Modules/Park/ParkPanelBuilder.cs", "Mod.cs" })
   Assert.That(IndustrialSupportSourceGuardTests.Source(source), Does.Not.Match(@"ParkEntrance(System|Evidence|Access|Scan)|PathEntrance|PathwayData"), source);
  Assert.That(IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkFacilityClassifier.cs"), Does.Not.Contain("traits.HasParkMaintenance)"), "Park Maintenance is no HQ default");
  Assert.That(IndustrialSupportSourceGuardTests.Source("Modules/Industrial/IndustrialFacilityClassifier.cs"), Does.Not.Contain("traits.HasOfficeProperty)"), "office is no HQ default");
 }
 [Test] public void IndustrialActivatesOnlyWithTheDedicatedHeadquarters() {
  var registry = Registry(); var store = new Store(); IndustrialAreaModule industrial = null;
  var anchors = new AreaAnchorService(store, e => e != Entity.Null, (a, e, r) => industrial.AcceptsAnchor(a, e, r, default));
  industrial = new IndustrialAreaModule(anchors, (e, _) => e == E(20) ? new IndustrialFacilityTraits { IsBuilding = true, Prefab = IndustrialHqPrefab }
   : new IndustrialFacilityTraits { IsBuilding = true, HasOfficeProperty = true, HasOfficeCompany = true, Prefab = OtherPrefab }, registry);
  Assert.That(anchors.Assign(Area, E(21), AreaAnchorRole.Headquarters), Is.False, "office");
  Assert.That(industrial.ValidateActivation(Area, default), Is.False);
  Assert.That(anchors.Assign(Area, E(20), AreaAnchorRole.Headquarters), Is.True);
  Assert.That(industrial.ValidateActivation(Area, default), Is.True);
 }
}
