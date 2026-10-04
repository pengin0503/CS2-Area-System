using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialFacilityClassifierTests {
 private static IndustrialFacilityTraits Building() => new() { IsBuilding = true };
 private static IndustrialFacilityTraits Extractor() { var t = Building(); t.HasExtractorProperty = true; t.HasExtractorCompany = true; t.HasWorkplaces = true; return t; }
 private static IndustrialFacilityTraits Factory() { var t = Building(); t.ManufacturesGoods = true; t.HasProcessorCompany = true; t.HasWorkplaces = true; return t; }
 private static IndustrialFacilityTraits Warehouse() { var t = Building(); t.HasStorageProperty = true; t.HasStorageCompany = true; t.HasWorkplaces = true; return t; }
 private static IndustrialFacilityTraits CargoStation() { var t = Building(); t.HasCargoStation = true; t.HasWorkplaces = true; return t; }
 private static IndustrialFacilityTraits Office() { var t = Building(); t.HasOfficeProperty = true; t.HasOfficeCompany = true; t.HasWorkplaces = true; return t; }
 [Test] public void ExtractorProcessorStorageAndCargoGetTheirRoles() {
  Assert.That(IndustrialFacilityClassifier.Classify(Extractor()), Is.EqualTo(IndustrialFacilityRole.Extractor));
  Assert.That(IndustrialFacilityClassifier.Classify(Factory()), Is.EqualTo(IndustrialFacilityRole.Processor));
  Assert.That(IndustrialFacilityClassifier.Classify(Warehouse()), Is.EqualTo(IndustrialFacilityRole.Storage | IndustrialFacilityRole.SupportingWorkplace));
  Assert.That(IndustrialFacilityClassifier.Classify(CargoStation()), Is.EqualTo(IndustrialFacilityRole.Cargo | IndustrialFacilityRole.SupportingWorkplace));
 }
 [Test] public void CargoCapabilityClassifiesEvenWhenTheStationIsDisabledOrUnstaffed() {
  var disabled = Building(); disabled.HasCargoStation = true;
  Assert.That(IndustrialFacilityClassifier.Classify(disabled), Is.EqualTo(IndustrialFacilityRole.Cargo), "operating status is a separate observation");
  Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(disabled), Is.False);
 }
 [Test] public void SupportingWorkplaceIsOnlyDescriptiveInsideEligibleFacilities() {
  Assert.That(IndustrialFacilityClassifier.Classify(Office()), Is.EqualTo(IndustrialFacilityRole.None), "ordinary office-only workplace");
  var jobsOnly = Building(); jobsOnly.HasWorkplaces = true;
  Assert.That(IndustrialFacilityClassifier.Classify(jobsOnly), Is.EqualTo(IndustrialFacilityRole.None), "jobs alone never make a building eligible");
  Assert.That(IndustrialFacilityClassifier.Classify(Factory()) & IndustrialFacilityRole.SupportingWorkplace, Is.EqualTo(IndustrialFacilityRole.None), "producer jobs are not support jobs");
 }
 [Test] public void TemporaryVacancyKeepsStaticIndustrialCapability() {
  var vacantFactory = Building(); vacantFactory.ManufacturesGoods = true;
  var vacantExtractor = Building(); vacantExtractor.HasExtractorProperty = true;
  var vacantWarehouse = Building(); vacantWarehouse.HasStorageProperty = true;
  Assert.That(IndustrialFacilityClassifier.Classify(vacantFactory), Is.EqualTo(IndustrialFacilityRole.Processor));
  Assert.That(IndustrialFacilityClassifier.Classify(vacantExtractor), Is.EqualTo(IndustrialFacilityRole.Extractor));
  Assert.That(IndustrialFacilityClassifier.Classify(vacantWarehouse), Is.EqualTo(IndustrialFacilityRole.Storage));
  foreach (var t in new[] { vacantFactory, vacantExtractor, vacantWarehouse }) Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(t), Is.False);
 }
 [Test] public void OneFacilityMayExposeSeveralRoles() {
  var t = CargoStation(); t.HasStorageCompany = true;
  Assert.That(IndustrialFacilityClassifier.Classify(t), Is.EqualTo(IndustrialFacilityRole.Cargo | IndustrialFacilityRole.Storage | IndustrialFacilityRole.SupportingWorkplace));
  var hub = Extractor(); hub.ManufacturesGoods = true;
  Assert.That(IndustrialFacilityClassifier.Classify(hub), Is.EqualTo(IndustrialFacilityRole.Extractor | IndustrialFacilityRole.Processor));
 }
 [Test] public void UnrelatedBuildingsHaveNoRoleAndAreNotHeadquartersEligible() {
  var residential = Building(); var park = Building(); var civic = Building(); civic.HasWorkplaces = true;
  foreach (var t in new[] { residential, park, civic, new IndustrialFacilityTraits() }) {
   Assert.That(IndustrialFacilityClassifier.Classify(t), Is.EqualTo(IndustrialFacilityRole.None));
   Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(t), Is.False);
  }
  var notBuilding = Extractor(); notBuilding.IsBuilding = false;
  Assert.That(IndustrialFacilityClassifier.Classify(notBuilding), Is.EqualTo(IndustrialFacilityRole.None), "unknown/non-building traits are safely None");
 }
 [Test] public void ProducerRolesAreNotHeadquartersEligible() {
  foreach (var t in new[] { Extractor(), Factory(), Warehouse(), CargoStation() }) Assert.That(IndustrialFacilityClassifier.IsHeadquartersEligible(t), Is.False);
 }
 [Test] public void ModdedBuildingsWithTheSameTraitsAreTreatedIdentically() {
  // Traits carry only component/prefab facts; there is no asset name or origin to distinguish a modded building.
  var modded = Factory();
  Assert.That(IndustrialFacilityClassifier.Classify(modded), Is.EqualTo(IndustrialFacilityClassifier.Classify(Factory())));
  Assert.That(typeof(IndustrialFacilityTraits).GetFields().Select(f => f.FieldType).Distinct(), Is.EquivalentTo(new[] { typeof(bool), typeof(Entity) }), "no names or strings");
 }
 [Test] public void ManufacturedGoodsExcludeOfficeAndRawExtractorResources() {
  Assert.That(IndustrialFacilityReader.ManufacturesGoods(Resource.Furniture | Resource.Paper), Is.True);
  Assert.That(IndustrialFacilityReader.ManufacturesGoods(Resource.Software | Resource.Financial), Is.False, "office output");
  Assert.That(IndustrialFacilityReader.ManufacturesGoods(Resource.Grain), Is.False, "raw extractor output is the Extractor role");
  Assert.That(IndustrialFacilityReader.ManufacturesGoods(Resource.Software | Resource.Steel), Is.True);
  Assert.That(IndustrialFacilityReader.ManufacturesGoods(Resource.NoResource), Is.False);
 }
}
public class IndustrialHeadquartersPolicyTests {
 private sealed class Store : IAreaAnchorStore {
  public Dictionary<Entity, IReadOnlyList<AreaAnchorLink>> Values = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area, out var links) ? links : Array.Empty<AreaAnchorLink>();
  public void Write(Entity area, IReadOnlyList<AreaAnchorLink> links) => Values[area] = links.ToArray();
 }
 private static readonly Entity area = new() { Index = 10, Version = 1 }, factory = new() { Index = 30, Version = 1 }, office = new() { Index = 40, Version = 1 };
 private static readonly Entity hqPrefab = new() { Index = 90, Version = 1 }, hq = new() { Index = 50, Version = 1 };
 private static CS2AreaSystem.Core.Semantics.AreaAssetSemanticRegistry Registry() {
  var r = new CS2AreaSystem.Core.Semantics.AreaAssetSemanticRegistry();
  new CS2AreaSystem.Core.Semantics.AreaBuiltInContentSemanticProvider().Refresh(r, new[] { new CS2AreaSystem.Core.Semantics.AreaBuiltInPrefabCandidate(hqPrefab, "BuildingPrefab", "AS_IndustrialHeadquarters", true, false, "AS_IndustrialHeadquarters.cok") });
  return r;
 }
 // Phase 3 C2: initial assignment accepts only AS_IndustrialHeadquarters; offices, legacy industrial buildings and reader failures are rejected.
 [Test] public void InitialAssignmentAcceptsOnlyTheDedicatedHeadquarters() {
  var traits = new Dictionary<Entity, IndustrialFacilityTraits> { [factory] = new IndustrialFacilityTraits { IsBuilding = true, ManufacturesGoods = true },
   [office] = new IndustrialFacilityTraits { IsBuilding = true, HasOfficeProperty = true, HasOfficeCompany = true, HasWorkplaces = true },
   [hq] = new IndustrialFacilityTraits { IsBuilding = true, Prefab = hqPrefab } };
  IAreaAnchorPolicy policy = new IndustrialAreaModule(new AreaAnchorService(new Store(), e => true, (a, e, r) => true), (e, _) => traits[e], Registry());
  Assert.That(policy.AcceptsAnchor(area, factory, AreaAnchorRole.Headquarters, default), Is.False);
  Assert.That(policy.AcceptsAnchor(area, office, AreaAnchorRole.Headquarters, default), Is.False, "ordinary office is retired");
  Assert.That(policy.AcceptsAnchor(area, hq, AreaAnchorRole.Headquarters, default), Is.True);
  Assert.That(policy.AcceptsAnchor(area, hq, AreaAnchorRole.Entrance, default), Is.False, "Industrial has no Entrance role");
  IAreaAnchorPolicy failing = new IndustrialAreaModule(new AreaAnchorService(new Store(), e => true, (a, e, r) => true), (e, _) => throw new InvalidOperationException("reader"), Registry());
  Assert.That(failing.AcceptsAnchor(area, hq, AreaAnchorRole.Headquarters, default), Is.False);
 }
 // A saved Foundation office link stays visible but invalid; the dedicated HQ stays valid through vacancy (static prefab identity).
 [Test] public void SavedOfficeLinkBecomesInvalidWhileTheDedicatedHeadquartersSurvivesVacancy() {
  int reads = 0;
  var module = new IndustrialAreaModule(new AreaAnchorService(new Store(), e => true, (a, e, r) => true), (e, _) => { reads++; return new IndustrialFacilityTraits{IsBuilding=true,HasOfficeProperty=e==office,Prefab=e==hq?hqPrefab:Entity.Null}; }, Registry());
  IAreaExistingAnchorPolicy existing = module;
  Assert.That(existing.AcceptsExistingAnchor(area, factory, AreaAnchorRole.Headquarters, default), Is.False);
  Assert.That(existing.AcceptsExistingAnchor(area, factory, AreaAnchorRole.Entrance, default), Is.False);
  Assert.That(existing.AcceptsExistingAnchor(area, office, AreaAnchorRole.Headquarters, default), Is.False, "Foundation office HQ is not converted");
  Assert.That(existing.AcceptsExistingAnchor(area, hq, AreaAnchorRole.Headquarters, default), Is.True);
  Assert.That(reads, Is.EqualTo(3), "prefab identity is revalidated without requiring a tenant");
 }
}
