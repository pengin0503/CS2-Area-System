using System.Reflection;
using Colossal.Serialization.Entities;
using Game.City;
using Game.Economy;
using Game.Simulation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
namespace CS2AreaSystem.Tests;
// Pins the exact 1.6.2f1 members the shared Area finance adapter uses (docs/verification/area-finance-contracts.md).
// Shape is proven by reflection; the executable checks call pure vanilla functions. Neither proves in-game semantics.
public class AreaFinanceApiContractTests {
 private static void Field<T>(string name, Type type, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance) {
  var field = typeof(T).GetField(name, flags);
  Assert.That(field, Is.Not.Null, typeof(T).FullName + "." + name);
  Assert.That(field!.FieldType, Is.EqualTo(type), typeof(T).FullName + "." + name);
 }
 [Test] public void ExpenseSourceHasTheGenericServiceUpkeepSource() {
  Assert.That((int)ExpenseSource.ServiceUpkeep, Is.EqualTo(5));
  Assert.That((int)ExpenseSource.Count, Is.EqualTo(15), "BudgetApplySystem iterates 15 expense sources");
  Assert.That(Enum.GetUnderlyingType(typeof(ExpenseSource)), Is.EqualTo(typeof(int)));
 }
 [Test] public void IncomeSourceHasNoGenericServiceFeeSource() {
  Assert.That(Enum.GetNames(typeof(IncomeSource)), Is.EqualTo(new[] { "TaxResidential", "TaxCommercial", "TaxIndustrial", "FeeHealthcare", "FeeElectricity",
   "GovernmentSubsidy", "FeeEducation", "ExportElectricity", "ExportWater", "FeeParking", "FeePublicTransport", "TaxOffice", "FeeGarbage", "FeeWater", "Count" }));
  Assert.That((int)IncomeSource.Count, Is.EqualTo(14), "BudgetApplySystem iterates 14 income sources");
 }
 [Test] public void PlayerResourceHasNoParkOrAdmissionResource() {
  Assert.That(Enum.GetNames(typeof(PlayerResource)), Is.EqualTo(new[] { "Electricity", "Healthcare", "BasicEducation", "SecondaryEducation", "HigherEducation",
   "Garbage", "Water", "Mail", "PublicTransport", "FireResponse", "Police", "Sewage", "Parking", "Count" }));
 }
 [Test] public void UnsupportedPlayerResourcesHaveNoIncomeSource() {
  foreach (var resource in new[] { PlayerResource.Mail, PlayerResource.FireResponse, PlayerResource.Police, PlayerResource.Count })
   Assert.That(EconomyUtils.GetIncomeSource(resource), Is.EqualTo(IncomeSource.Count), resource.ToString());
  // The only mappings are the fixed resource fees; routing admission through one of them would mislabel it.
  Assert.That(EconomyUtils.GetIncomeSource(PlayerResource.Parking), Is.EqualTo(IncomeSource.FeeParking));
  Assert.That(EconomyUtils.GetIncomeSource(PlayerResource.PublicTransport), Is.EqualTo(IncomeSource.FeePublicTransport));
 }
 [Test] public void PlayerMoneyStoresAClampedIntWithAPublicUnlimitedFlag() {
  Assert.That(PlayerMoney.kMaxMoney, Is.EqualTo(2_000_000_000));
  Field<PlayerMoney>("m_Money", typeof(int), BindingFlags.NonPublic | BindingFlags.Instance);
  Field<PlayerMoney>("m_Unlimited", typeof(bool));
  Assert.That(typeof(PlayerMoney).GetProperty("money")?.PropertyType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(PlayerMoney).GetMethod(nameof(PlayerMoney.Add), new[] { typeof(int) }), Is.Not.Null);
  Assert.That(typeof(ISerializable).IsAssignableFrom(typeof(PlayerMoney)), Is.True, "the balance is saved with the city");
  var money = new PlayerMoney(1_999_999_990);
  money.Add(60);
  Assert.That(money.money, Is.EqualTo(2_000_000_000), "Add clamps to kMaxMoney");
  money = new PlayerMoney(-1_999_999_990);
  money.Add(-60);
  Assert.That(money.money, Is.EqualTo(-2_000_000_000), "Add clamps to -kMaxMoney; there is no other lower bound");
  money.m_Unlimited = true;
  Assert.That(money.money, Is.EqualTo(2_000_000_000), "the getter hides the stored balance in unlimited mode");
 }
 [Test] public void StatisticsEventsCarryTypeParameterAndFloatChange() {
  Field<StatisticsEvent>("m_Statistic", typeof(StatisticType));
  Field<StatisticsEvent>("m_Parameter", typeof(int));
  Field<StatisticsEvent>("m_Change", typeof(float));
  Assert.That(Enum.IsDefined(typeof(StatisticType), StatisticType.Expense), Is.True);
  Assert.That(Enum.IsDefined(typeof(StatisticType), StatisticType.Income), Is.True);
 }
 [Test] public void CityStatisticsExposeAGuardedQueueAndWriterRegistration() {
  var safe = typeof(CityStatisticsSystem).GetMethod(nameof(CityStatisticsSystem.GetSafeStatisticsQueue), new[] { typeof(JobHandle).MakeByRefType() });
  Assert.That(safe?.ReturnType, Is.EqualTo(typeof(CityStatisticsSystem.SafeStatisticQueue)));
  Assert.That(typeof(CityStatisticsSystem.SafeStatisticQueue).GetMethod(nameof(CityStatisticsSystem.SafeStatisticQueue.Enqueue), new[] { typeof(StatisticsEvent) }), Is.Not.Null);
  Assert.That(typeof(CityStatisticsSystem).GetMethod(nameof(CityStatisticsSystem.AddWriter), new[] { typeof(JobHandle) }), Is.Not.Null);
 }
 [Test] public void BudgetApplyCadenceAndBudgetReadersMatchTheDocumentedContract() {
  var perDay = typeof(BudgetApplySystem).GetField("kUpdatesPerDay", BindingFlags.Public | BindingFlags.Static);
  Assert.That(perDay?.FieldType, Is.EqualTo(typeof(int)));
  Assert.That(perDay!.IsInitOnly, Is.True);
  Assert.That(typeof(CityServiceBudgetSystem).GetMethod(nameof(CityServiceBudgetSystem.GetExpense), new[] { typeof(ExpenseSource), typeof(NativeArray<int>) })?.ReturnType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(CityServiceBudgetSystem).GetMethod(nameof(CityServiceBudgetSystem.GetExpense), new[] { typeof(ExpenseSource) })?.ReturnType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(CityServiceBudgetSystem).GetMethod(nameof(CityServiceBudgetSystem.GetTotalExpenses), Type.EmptyTypes)?.ReturnType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(CityServiceBudgetSystem).GetMethod(nameof(CityServiceBudgetSystem.GetTotalIncome), Type.EmptyTypes)?.ReturnType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(CitySystem).GetProperty(nameof(CitySystem.City))?.PropertyType, Is.EqualTo(typeof(Entity)));
 }
 [Test] public void HouseholdMoneyIsTheMoneyResourceInTheResourcesBuffer() {
  Assert.That(typeof(EconomyUtils).GetMethod(nameof(EconomyUtils.GetResources), new[] { typeof(Resource), typeof(DynamicBuffer<Resources>) })?.ReturnType, Is.EqualTo(typeof(int)));
  Assert.That(typeof(EconomyUtils).GetMethod(nameof(EconomyUtils.AddResources), new[] { typeof(Resource), typeof(int), typeof(DynamicBuffer<Resources>) })?.ReturnType, Is.EqualTo(typeof(int)));
  Assert.That(Enum.IsDefined(typeof(Resource), Resource.Money), Is.True);
 }
 [Test] public void ServiceFeeAndUpkeepCollectorsAreResourceKeyed() {
  Field<CollectedCityServiceFeeData>("m_PlayerResource", typeof(int));
  Field<CollectedCityServiceUpkeepData>("m_Resource", typeof(Resource));
  Field<CollectedCityServiceBudgetData>("m_BaseCost", typeof(int));
  Field<Game.Prefabs.ServiceData>("m_Service", typeof(CityService));
  Assert.That(typeof(ServiceFeeSystem).GetMethod(nameof(ServiceFeeSystem.GetServiceFeeIncomeEstimate), new[] { typeof(PlayerResource), typeof(float) })?.ReturnType, Is.EqualTo(typeof(int)));
 }
}
