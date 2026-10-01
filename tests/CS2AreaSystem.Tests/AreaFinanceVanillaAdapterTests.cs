using CS2AreaSystem.Core.Finance;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Runs the real 1.6.2f1 PlayerMoney struct (its Add/clamp is pure managed code); only the ECS job wiring is untested here.
public class AreaFinanceVanillaAdapterTests {
 private static long Stored(PlayerMoney money) => AreaFinanceVanillaAdapter.StoredCityMoney(money);
 [Test] public void ExpenseIsDebitedInFullWhileTheCityBalanceIsNegative() {
  var money = new PlayerMoney(-1_000_000);
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, 5000), Is.EqualTo(5000));
  Assert.That(Stored(money), Is.EqualTo(-1_005_000));
 }
 [Test] public void ExpenseStopsOnlyAtTheNativeLowerBound() {
  var money = new PlayerMoney(-1_999_999_990);
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, 60), Is.EqualTo(10), "PlayerMoney cannot represent less than -kMaxMoney");
  Assert.That(Stored(money), Is.EqualTo(-2_000_000_000));
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, 60), Is.Zero);
 }
 [TestCase(false)] [TestCase(true)]
 public void UnlimitedModeIsPreservedAndTheStoredBalanceMoves(bool unlimited) {
  var money = new PlayerMoney(500) { m_Unlimited = unlimited };
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, 800), Is.EqualTo(800));
  Assert.That(AreaFinanceVanillaAdapter.ApplyIncome(ref money, 100), Is.EqualTo(100));
  Assert.That(Stored(money), Is.EqualTo(-200));
  Assert.That(money.m_Unlimited, Is.EqualTo(unlimited));
 }
 [Test] public void LargeAmountsCrossTheWholeRangeWithoutIntOverflow() {
  var money = new PlayerMoney(2_000_000_000);
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, long.MaxValue), Is.EqualTo(4_000_000_000L));
  Assert.That(Stored(money), Is.EqualTo(-2_000_000_000));
  money = new PlayerMoney(1_900_000_000);
  Assert.That(AreaFinanceVanillaAdapter.ApplyIncome(ref money, int.MaxValue), Is.EqualTo(100_000_000), "PlayerMoney.Add(int.MaxValue) would wrap");
  Assert.That(Stored(money), Is.EqualTo(2_000_000_000));
 }
 [Test] public void NonpositiveAmountsApplyNothing() {
  var money = new PlayerMoney(100);
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, 0), Is.Zero);
  Assert.That(AreaFinanceVanillaAdapter.ApplyExpense(ref money, -10), Is.Zero);
  Assert.That(AreaFinanceVanillaAdapter.ApplyIncome(ref money, -10), Is.Zero);
  Assert.That(Stored(money), Is.EqualTo(100));
 }
 [Test] public void ABatchIsAppliedAsOneNetChangeLikeBudgetApplySystem() {
  var money = new PlayerMoney(-2_000_000_000);
  var applied = AreaFinanceVanillaAdapter.Apply(ref money, expense: 100, income: 100);
  Assert.That(applied.Expense, Is.EqualTo(100)); Assert.That(applied.Income, Is.EqualTo(100));
  Assert.That(Stored(money), Is.EqualTo(-2_000_000_000));
  money = new PlayerMoney(1_999_999_950);
  applied = AreaFinanceVanillaAdapter.Apply(ref money, expense: 10, income: 100);
  Assert.That(applied.Expense, Is.EqualTo(10), "expense is never shortened at the upper bound");
  Assert.That(applied.Income, Is.EqualTo(60));
  Assert.That(Stored(money) - 1_999_999_950, Is.EqualTo(applied.Income - applied.Expense));
  money = new PlayerMoney(-1_999_999_950);
  applied = AreaFinanceVanillaAdapter.Apply(ref money, expense: 100, income: 10);
  Assert.That(applied.Income, Is.EqualTo(10), "income is never shortened at the lower bound");
  Assert.That(applied.Expense, Is.EqualTo(60));
  Assert.That(Stored(money), Is.EqualTo(-2_000_000_000));
 }
 [Test] public void ExpenseStatisticIsServiceUpkeepInMoneyUnits() {
  var stat = AreaFinanceVanillaAdapter.ExpenseStatistic(1234);
  Assert.That(stat.m_Statistic, Is.EqualTo(StatisticType.Expense));
  Assert.That(stat.m_Parameter, Is.EqualTo((int)ExpenseSource.ServiceUpkeep));
  Assert.That(stat.m_Change, Is.EqualTo(1234f));
 }
}
public class AreaFinanceHouseholdTransferTests {
 private static long Stored(PlayerMoney money) => AreaFinanceVanillaAdapter.StoredCityMoney(money);
 [Test] public void PlanIsLimitedByFeeHouseholdMoneyAndCityHeadroom() {
  Assert.That(AreaFinanceHouseholdTransfer.Plan(60, 1000, 0), Is.EqualTo(60));
  Assert.That(AreaFinanceHouseholdTransfer.Plan(60, 25, 0), Is.EqualTo(25), "never below zero household money");
  Assert.That(AreaFinanceHouseholdTransfer.Plan(60, 1000, 1_999_999_990), Is.EqualTo(10), "never more than the city can receive");
  Assert.That(AreaFinanceHouseholdTransfer.Plan(60, 0, 0), Is.Zero);
  Assert.That(AreaFinanceHouseholdTransfer.Plan(60, -40, 0), Is.Zero);
  Assert.That(AreaFinanceHouseholdTransfer.Plan(0, 1000, 0), Is.Zero);
  Assert.That(AreaFinanceHouseholdTransfer.Plan(-3, 1000, 0), Is.Zero);
  Assert.That(AreaFinanceHouseholdTransfer.Plan(60, 1000, 2_000_000_000), Is.Zero);
 }
 [TestCase(false)] [TestCase(true)]
 public void HouseholdDebitEqualsTheCityCreditAtTheUpperBound(bool unlimited) {
  var money = new PlayerMoney(1_999_999_990) { m_Unlimited = unlimited };
  long household = 1000, before = Stored(money);
  long debit = AreaFinanceHouseholdTransfer.Transfer(ref money, household, 60);
  Assert.That(debit, Is.EqualTo(10));
  Assert.That(Stored(money) - before, Is.EqualTo(debit), "household debit == city credit");
  Assert.That(money.m_Unlimited, Is.EqualTo(unlimited));
  Assert.That(AreaFinanceHouseholdTransfer.Transfer(ref money, household - debit, 60), Is.Zero, "sequential admissions share the headroom");
 }
 [Test] public void ANegativeCityBalanceStillReceivesTheFullAdmission() {
  var money = new PlayerMoney(-5_000_000);
  Assert.That(AreaFinanceHouseholdTransfer.Transfer(ref money, 1000, 60), Is.EqualTo(60));
  Assert.That(Stored(money), Is.EqualTo(-4_999_940));
 }
 [Test] public void APoorHouseholdPaysOnlyWhatItHas() {
  var money = new PlayerMoney(0);
  Assert.That(AreaFinanceHouseholdTransfer.Transfer(ref money, 25, 60), Is.EqualTo(25));
  Assert.That(Stored(money), Is.EqualTo(25));
 }
}
