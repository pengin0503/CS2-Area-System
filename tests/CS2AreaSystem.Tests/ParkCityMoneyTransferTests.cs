using System.Reflection;
using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Park;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Park money reaches the city only through the shared Area finance (design §15.4). These tests pin that Park keeps no
// city-money primitive of its own and that the old "paid only down to the lower bound" management semantics are gone.
public class ParkCityMoneyTransferTests {
 [Test] public void ParkFinanceHasNoDirectCityMoneyPrimitives() {
  foreach (var name in new[] { "CollectAdmission", "PayManagement", "StoredCityMoney", "ApplyMoneyDelta" })
   Assert.That(typeof(ParkFinanceModel).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), Is.Null, name);
  Assert.That(typeof(ParkFinanceModel).GetField("CityMoneyLimit"), Is.Null);
  var parkMethods = typeof(ParkFinanceModel).Assembly.GetTypes().Where(t => t.Namespace == typeof(ParkFinanceModel).Namespace)
   .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
   .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(PlayerMoney) || p.ParameterType == typeof(PlayerMoney).MakeByRefType()))
   .Select(m => m.DeclaringType!.Name + "." + m.Name);
  Assert.That(parkMethods, Is.Empty, "no Park method takes the city balance");
 }
 [Test] public void ManagementSettlementUsesTheSharedRoundingRule() {
  foreach (var perDay in new[] { 0.6, 1000.0, 777.7, double.NaN, -5.0 })
   foreach (var frames in new uint[] { 0, 1, 4096, ParkFinanceModel.FramesPerDay }) {
    double parkCarry = 0.25, coreCarry = 0.25;
    Assert.That(ParkFinanceModel.Settle(perDay, frames, ref parkCarry), Is.EqualTo(AreaFinanceModel.SettleDaily(perDay, frames, ref coreCarry)), $"{perDay}/{frames}");
    Assert.That(parkCarry, Is.EqualTo(coreCarry));
   }
  Assert.That(ParkFinanceModel.FramesPerDay, Is.EqualTo(AreaFinanceModel.FramesPerDay));
 }
 [TestCase(false)] [TestCase(true)]
 public void ANegativeCityBalanceStillIncursTheFullManagementExpense(bool unlimited) {
  var runtime = new AreaFinanceRuntime(_ => { });
  var money = new PlayerMoney(-1_000_000) { m_Unlimited = unlimited };
  runtime.EnqueueExpense(7, AreaFinanceKind.ParkManagement, 60);
  var batch = runtime.BeginApply(1);
  var applied = AreaFinanceVanillaAdapter.Apply(ref money, batch.TotalExpense, batch.TotalIncome);
  var settled = runtime.CompleteApply(applied.Expense, applied.Income);
  Assert.That(ParkAreaFinance.AppliedManagement(settled).Single().Amount, Is.EqualTo(60));
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(-1_000_060));
  Assert.That(money.m_Unlimited, Is.EqualTo(unlimited));
 }
}
