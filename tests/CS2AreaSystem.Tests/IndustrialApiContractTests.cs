using System.Reflection;
using CS2AreaSystem.Modules.Industrial;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Game.UI.InGame;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
// Pins the exact 1.6.2f1 public members the Industrial observation adapter reads (see
// docs/verification/industrial-observation-contracts.md). Shape is proven by compilation/reflection; the executable
// checks call pure vanilla functions to pin the normalizations. Neither proves live in-game semantics.
public class IndustrialApiContractTests {
 private static void Field<T>(string name, Type type) {
  var field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.Instance);
  Assert.That(field, Is.Not.Null, typeof(T).FullName + "." + name);
  Assert.That(field!.FieldType, Is.EqualTo(type), typeof(T).FullName + "." + name);
 }
 [Test] public void SelectedTypesExistInTheSuppliedReferences() {
  var types = new[] { typeof(IndustrialCompany), typeof(Game.Companies.ProcessingCompany), typeof(Game.Companies.ExtractorCompany), typeof(Game.Companies.StorageCompany), typeof(OfficeCompany),
   typeof(CompanyData), typeof(WorkProvider), typeof(Employee), typeof(Profitability), typeof(CompanyStatisticData),
   typeof(Game.Buildings.ExtractorFacility), typeof(StorageProperty), typeof(Game.Buildings.CargoTransportStation), typeof(Game.Buildings.BuildingEfficiency),
   typeof(Efficiency), typeof(Renter), typeof(PropertyRenter), typeof(Resources), typeof(IndustrialProperty), typeof(OfficeProperty), typeof(ExtractorProperty) };
  foreach (var type in types) Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game"), type.FullName);
 }
 [Test] public void TenantResolutionUsesRenterAndPropertyRenter() {
  Field<Renter>("m_Renter", typeof(Entity));
  Field<PropertyRenter>("m_Property", typeof(Entity));
  Assert.That(typeof(CompanyData).IsValueType, Is.True, "a renter is a company when it carries CompanyData");
 }
 [Test] public void JobsAndEmployeesUseWorkProviderAndEmployeeBuffer() {
  Field<WorkProvider>("m_MaxWorkers", typeof(int));
  Field<Employee>("m_Worker", typeof(Entity));
  Field<Employee>("m_Level", typeof(byte));
  Field<WorkplaceData>("m_Complexity", typeof(WorkplaceComplexity));
  Field<SpawnableBuildingData>("m_Level", typeof(byte));
  Assert.That(typeof(IBufferElementData).IsAssignableFrom(typeof(Employee)), Is.True);
  // EmployeesSection reports max employees as EmploymentData.GetWorkplacesData(maxWorkers, level, complexity).total.
  foreach (WorkplaceComplexity complexity in Enum.GetValues(typeof(WorkplaceComplexity)))
   for (int level = 1; level <= 5; level++)
    foreach (var max in new[] { 0, 1, 7, 40, 133 })
     Assert.That(EmploymentData.GetWorkplacesData(max, level, complexity).total, Is.EqualTo(max), $"{complexity} L{level} {max}");
 }
 [Test] public void BuildingEfficiencyIsAnEmptyTagAndTheValueIsTheEfficiencyBufferProduct() {
  Assert.That(typeof(Game.Buildings.BuildingEfficiency).GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty, "no value member to read");
  Field<Efficiency>("m_Factor", typeof(EfficiencyFactor));
  Field<Efficiency>("m_Efficiency", typeof(float));
  Assert.That(Enum.IsDefined(typeof(EfficiencyFactor), EfficiencyFactor.LackResources), Is.True);
  // Product of max(0, factor) over the buffer, rounded to 0.01 (decompiled 1.6.2f1); EfficiencySection and the production
  // systems read this overload. The Span overload binds to the game's mscorlib and cannot be executed under net8.
  Assert.That(typeof(BuildingUtils).GetMethod(nameof(BuildingUtils.GetEfficiency), new[] { typeof(DynamicBuffer<Efficiency>) })?.ReturnType, Is.EqualTo(typeof(float)));
  Assert.That(typeof(BuildingUtils).GetMethod(nameof(BuildingUtils.GetEfficiency), new[] { typeof(Entity), typeof(BufferLookup<Efficiency>).MakeByRefType() })?.ReturnType, Is.EqualTo(typeof(float)),
   "the overload the job reader calls (same product)");
  Assert.That(typeof(BuildingUtils).GetMethod(nameof(BuildingUtils.GetEfficiencyExcludingFactor), new[] { typeof(DynamicBuffer<Efficiency>), typeof(EfficiencyFactor) }), Is.Not.Null);
  Assert.That(typeof(BuildingUtils).GetMethod(nameof(BuildingUtils.GetEfficiencyFactor), new[] { typeof(DynamicBuffer<Efficiency>), typeof(EfficiencyFactor) }), Is.Not.Null);
  Assert.That(IndustrialSourceNormalization.EfficiencyPercent(1f), Is.EqualTo(100).Within(1e-9));
  Assert.That(IndustrialSourceNormalization.EfficiencyPercent(1.2f), Is.EqualTo(120).Within(1e-4), "raw value kept; only scoring clamps");
  Assert.That(IndustrialSourceNormalization.EfficiencyPercent(float.NaN), Is.Null);
 }
 [Test] public void ProfitabilityMapsEveryVanillaByteIntoItsVanillaTierBand() {
  Field<Profitability>("m_Profitability", typeof(byte));
  for (int raw = 0; raw <= 255; raw++) {
   int index = IndustrialSourceNormalization.ProfitIndex((byte)raw);
   Assert.That(index, Is.EqualTo((int)Math.Round(raw * 2f - 255f)), "ProfitabilitySection.OnProcess for one company");
   double v = IndustrialSourceNormalization.ProfitViability(index);
   var key = CompanyUIUtils.GetProfitabilityKey(index);
   var (low, high) = key switch {
    CompanyProfitabilityKey.Bankrupt => (0.0, 20.0), CompanyProfitabilityKey.LosingMoney => (20.0, 40.0),
    CompanyProfitabilityKey.BreakingEven => (40.0, 60.0), CompanyProfitabilityKey.GettingBy => (60.0, 80.0), _ => (80.0, 100.0) };
   Assert.That(v, Is.InRange(low, high), $"raw {raw} index {index} {key}");
   if (key != CompanyProfitabilityKey.Bankrupt) Assert.That(v, Is.GreaterThan(low));
  }
  Assert.That(IndustrialSourceNormalization.ProfitViability(-255), Is.EqualTo(0));
  Assert.That(IndustrialSourceNormalization.ProfitViability(255), Is.EqualTo(100));
 }
 [Test] public void StorageUsesStorageLimitDataAndWeightedResources() {
  Field<StorageLimitData>("m_Limit", typeof(int));
  Field<Resources>("m_Resource", typeof(Resource));
  Field<Resources>("m_Amount", typeof(int));
  Field<ResourceData>("m_Weight", typeof(float));
  Field<BuildingPropertyData>("m_AllowedStored", typeof(Resource));
  Field<BuildingPropertyData>("m_AllowedManufactured", typeof(Resource));
  Field<Game.Objects.Attached>("m_Parent", typeof(Entity));
  var warehouse = new StorageLimitData { m_Limit = 1000 }.GetAdjustedLimitForWarehouse(new SpawnableBuildingData { m_Level = 2 }, new BuildingData { m_LotSize = new int2(3, 4) });
  Assert.That(warehouse, Is.EqualTo(1000 * 2 * 3 * 4), "StorageSection warehouse capacity");
 }
 [Test] public void CargoUsesTheStationComponentAndPrefabData() {
  Field<Game.Buildings.CargoTransportStation>("m_WorkAmount", typeof(float));
  Field<CargoTransportStationData>("m_WorkMultiplier", typeof(float));
  Assert.That(Enum.IsDefined(typeof(BuildingOption), BuildingOption.Inactive), Is.True);
  Assert.That(typeof(BuildingUtils).GetMethod(nameof(BuildingUtils.CheckOption), new[] { typeof(Building), typeof(BuildingOption) }), Is.Not.Null);
  Assert.That(typeof(UpgradeUtils).GetMethods().Any(m => m.Name == nameof(UpgradeUtils.TryGetCombinedComponent) && m.GetParameters().Length == 5), Is.True,
   "lookup overload used for station storage and extractor upgrade limits");
 }
 [Test] public void StructureChangeEvidenceUsesVanillaTenantEvents() {
  Field<RentersUpdated>("m_Property", typeof(Entity));
  Assert.That(typeof(IComponentData).IsAssignableFrom(typeof(Game.Common.Event)), Is.True, "RentersUpdated is emitted on Event entities");
  Assert.That(typeof(Game.Common.Destroyed).IsValueType && typeof(Abandoned).IsValueType, Is.True);
 }
 [Test] public void JobReaderUsesPublicPrefabAndEconomyAccessors() {
  Assert.That(typeof(ResourceSystem).GetMethod(nameof(ResourceSystem.GetPrefabs))?.ReturnType, Is.EqualTo(typeof(ResourcePrefabs)));
  Assert.That(typeof(ResourceSystem).GetMethod(nameof(ResourceSystem.AddPrefabsReader)), Is.Not.Null);
  Field<EconomyParameterData>("m_IndustrialEfficiency", typeof(float));
  Field<EconomyParameterData>("m_ExtractorProductionEfficiency", typeof(float));
  Assert.That(typeof(EconomyUtils).GetMethod(nameof(EconomyUtils.GetTotalStorageUsed)), Is.Not.Null);
  Assert.That(typeof(EconomyUtils).GetMethod(nameof(EconomyUtils.GetWeight), new[] { typeof(Resource), typeof(ResourcePrefabs), typeof(ComponentLookup<ResourceData>).MakeByRefType() }), Is.Not.Null);
 }
 [Test] public void ProductiveActivityUsesVanillaProductionAndCapacityFunctions() {
  Field<CompanyStatisticData>("m_LastUpdateProduce", typeof(int));
  Field<IndustrialProcessData>("m_Input1", typeof(ResourceStack));
  Field<IndustrialProcessData>("m_Output", typeof(ResourceStack));
  Field<ResourceStack>("m_Amount", typeof(int));
  Assert.That(typeof(EconomyUtils).GetMethod(nameof(EconomyUtils.GetCompanyProductionPerDay), new[] { typeof(float), typeof(bool), typeof(DynamicBuffer<Employee>),
   typeof(IndustrialProcessData), typeof(ResourcePrefabs), typeof(ComponentLookup<ResourceData>).MakeByRefType(), typeof(ComponentLookup<Game.Citizens.Citizen>).MakeByRefType(),
   typeof(EconomyParameterData).MakeByRefType(), typeof(ServiceAvailable), typeof(ServiceCompanyData) }), Is.Not.Null, "CompanySection current production");
  // Capacity reference: the same formula vanilla uses for a company's maximum (GetCompanyMaxProfitPerDay), efficiency 1, full staffing.
  var process = new IndustrialProcessData { m_Output = new ResourceStack { m_Resource = Resource.Furniture, m_Amount = 1 } };
  var resource = new ResourceData { m_NeededWorkPerUnit = new int2(80, 80), m_Weight = 1 };
  var economy = new EconomyParameterData { m_IndustrialEfficiency = 1, m_ExtractorProductionEfficiency = 1 };
  var workplace = new WorkplaceData { m_Complexity = WorkplaceComplexity.Simple };
  int full = EconomyUtils.GetCompanyProductionPerDay(1f, 40, 1, true, workplace, process, resource, ref economy);
  int half = EconomyUtils.GetCompanyProductionPerDay(0.5f, 40, 1, true, workplace, process, resource, ref economy);
  int none = EconomyUtils.GetCompanyProductionPerDay(1f, 0, 1, true, workplace, process, resource, ref economy);
  Assert.That(full, Is.GreaterThan(0)); Assert.That(none, Is.Zero);
  Assert.That(half, Is.InRange(full / 2 - 1, full / 2 + 1), "linear in building efficiency");
  Assert.That(EconomyUtils.kCompanyUpdatesPerDay, Is.EqualTo(256), "m_LastUpdateProduce = last update amount * kCompanyUpdatesPerDay");
  Assert.That(IndustrialSourceNormalization.ActivityPercent(full / 2.0, full), Is.EqualTo(50).Within(1e-9));
  Assert.That(IndustrialSourceNormalization.ActivityPercent(5, 0), Is.Null, "invalid denominator is unavailable, not zero");
  Assert.That(IndustrialSourceNormalization.ActivityPercent(double.NaN, 10), Is.Null);
  Assert.That(IndustrialSourceNormalization.ActivityPercent(-1, 10), Is.Null);
 }
 [Test] public void GameCalendarMatchesTimeSystemGetDay() {
  Assert.That(TimeSystem.kTicksPerDay, Is.EqualTo(262144));
  Field<TimeData>("m_FirstFrame", typeof(uint));
  var data = new TimeData { m_FirstFrame = 1000, m_StartingHour = 7, m_StartingMinutes = 0 };
  var clock = IndustrialGameClock.From(data);
  foreach (var frame in new uint[] { 1000, 50_000, 190_000, 250_000, 1_000_000, 9_000_000 })
   Assert.That(clock.Day(frame), Is.EqualTo((uint)TimeSystem.GetDay(frame, data)), "frame " + frame);
  // 7:00 start: the first day ends 17 game hours later.
  Assert.That(clock.HourBucket(1000), Is.EqualTo(7));
  Assert.That(clock.Day(1000 + (uint)(17.5 * TimeSystem.kTicksPerDay / 24)), Is.EqualTo(1u));
 }
}
