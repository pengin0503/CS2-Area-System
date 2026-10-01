using CS2AreaSystem.Modules.Park;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkCityMoneyTransferTests {
 [TestCase(false)] [TestCase(true)]
 public void AdmissionDebitsOnlyWhatTheStoredCityBalanceCanReceive(bool unlimited) {
  var money = new PlayerMoney(1_999_999_990) { m_Unlimited = unlimited };
  long household = 1000, initial = ParkFinanceModel.StoredCityMoney(money);
  long collected = ParkFinanceModel.CollectAdmission(ref money, 60, household); household -= collected;
  Assert.That(collected, Is.EqualTo(10)); Assert.That(household, Is.EqualTo(990));
  Assert.That((long)ParkFinanceModel.StoredCityMoney(money) - initial, Is.EqualTo(collected));
  Assert.That(money.m_Unlimited, Is.EqualTo(unlimited));
  Assert.That(ParkFinanceModel.CollectAdmission(ref money, 60, household), Is.Zero, "sequential admissions share capacity");
 }
 [TestCase(false)] [TestCase(true)]
 public void ManagementRecordsOnlyTheExpenseAppliedAboveTheLowerBound(bool unlimited) {
  var money = new PlayerMoney(-1_999_999_990) { m_Unlimited = unlimited };
  long initial = ParkFinanceModel.StoredCityMoney(money);
  long paid = ParkFinanceModel.PayManagement(ref money, 60);
  Assert.That(paid, Is.EqualTo(10)); Assert.That(initial - ParkFinanceModel.StoredCityMoney(money), Is.EqualTo(paid));
  Assert.That(money.m_Unlimited, Is.EqualTo(unlimited));
  Assert.That(ParkFinanceModel.PayManagement(ref money, 60), Is.Zero);
 }
 [Test] public void ManagementCanSpanTheWholeBalanceRangeWithoutIntOverflow() {
  var money = new PlayerMoney(2_000_000_000);
  Assert.That(ParkFinanceModel.PayManagement(ref money, long.MaxValue), Is.EqualTo(4_000_000_000L));
  Assert.That(ParkFinanceModel.StoredCityMoney(money), Is.EqualTo(-2_000_000_000));
 }
 [Test] public void EmptyHouseholdAndNonpositiveRequestsTransferNothing() {
  var money = new PlayerMoney(100);
  Assert.That(ParkFinanceModel.CollectAdmission(ref money, 60, 0), Is.Zero);
  Assert.That(ParkFinanceModel.CollectAdmission(ref money, -1, 100), Is.Zero);
  Assert.That(ParkFinanceModel.PayManagement(ref money, -1), Is.Zero);
  Assert.That(ParkFinanceModel.StoredCityMoney(money), Is.EqualTo(100));
 }
 [Test] public void PartialManagementPaymentIsAllocatedExactlyOnceInRequestOrder() {
  var ledger = new ParkTransactionLedger();
  ledger.EnqueueManagement(default, 7, 60); ledger.EnqueueManagement(default, 8, 60);
  var batch = ledger.TakeBatch();
  var money = new PlayerMoney(-1_999_999_930);
  var outcomes = ParkTransactionLedger.Resolve(batch, Array.Empty<long>(), managementPaid: ParkFinanceModel.PayManagement(ref money, -batch.CityDelta));
  Assert.That(outcomes.Management.Select(c => c.Paid), Is.EqualTo(new long[] { 60, 10 }));
  Assert.That(outcomes.Management.Select(c => c.ParkId), Is.EqualTo(new ulong[] { 7, 8 }));
  Assert.That(outcomes.Management.Sum(c => c.Paid), Is.EqualTo(70));
  Assert.That(ledger.HasPending, Is.False);
 }
}
