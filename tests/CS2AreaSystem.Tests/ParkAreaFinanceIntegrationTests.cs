using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Park;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Park finance on the shared Area path, with the real PlayerMoney arithmetic and the production ledgers. The ECS jobs
// cannot run outside Unity; each step below is the code those jobs and systems call, in their order.
public class ParkAreaFinanceIntegrationTests {
 private sealed class Park : IAreaFinanceListener {
  public ParkAreaState State = ParkAreaState.CreateDefault();
  public long RecentExpense;
  public void OnAreaFinanceSettled(AreaFinanceBatch settled, uint frame) {
   foreach (var charge in ParkAreaFinance.AppliedManagement(settled)) {
    if (charge.AreaStableId != 7) continue;
    State = ParkFinanceModel.RecordManagementExpense(State, charge.Amount); RecentExpense += charge.Amount;
   }
  }
 }
 private static void ApplyAreaFinance(AreaFinanceRuntime finance, ref PlayerMoney money, uint frame) {
  var batch = finance.BeginApply(frame);
  if (batch == null) return;
  var applied = AreaFinanceVanillaAdapter.Apply(ref money, batch.TotalExpense, batch.TotalIncome);
  finance.CompleteApply(applied.Expense, applied.Income);
 }
 // What ParkFinanceSystem's transfer job and RecordResults do for one admission.
 private static long Admit(AreaFinanceRuntime finance, Park park, ref PlayerMoney money, ref long household, long fee) {
  long collected = AreaFinanceHouseholdTransfer.Transfer(ref money, household, fee);
  household -= collected;
  park.State = ParkFinanceModel.RecordAdmission(park.State, collected);
  finance.RecordSettledIncome(1, 7, AreaFinanceKind.ParkAdmission, collected);
  return collected;
 }
 [Test] public void ManagementIsBookedAsAreaManagementAndRecordedAtTheAppliedAmount() {
  var finance = new AreaFinanceRuntime(_ => { }); var park = new Park(); finance.AddListener(park); finance.Start(0);
  var money = new PlayerMoney(-50_000);
  double carry = 0;
  long settled = ParkFinanceModel.Settle(ParkFinanceModel.ComputeManagementCostPerDay(640, 1f, 1.0), ParkFinanceModel.FramesPerDay / 64, ref carry);
  Assert.That(settled, Is.EqualTo(10));
  Assert.That(finance.EnqueueExpense(7, AreaFinanceKind.ParkManagement, settled), Is.True);
  Assert.That(park.State.LifetimeManagementExpense, Is.Zero, "nothing is recorded before the city is charged");
  ApplyAreaFinance(finance, ref money, 4096);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(-50_010));
  Assert.That(park.State.LifetimeManagementExpense, Is.EqualTo(10)); Assert.That(park.RecentExpense, Is.EqualTo(10));
  Assert.That(finance.AreaManagement(4100), Is.EqualTo(10));
 }
 [Test] public void ManagementAtTheNativeLowerBoundRecordsOnlyWhatPlayerMoneyCouldRepresent() {
  var finance = new AreaFinanceRuntime(_ => { }); var park = new Park(); finance.AddListener(park);
  var money = new PlayerMoney(-1_999_999_990);
  finance.EnqueueExpense(7, AreaFinanceKind.ParkManagement, 60);
  ApplyAreaFinance(finance, ref money, 1);
  Assert.That(park.State.LifetimeManagementExpense, Is.EqualTo(10));
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(-2_000_000_000));
 }
 [Test] public void AdmissionNearTheUpperBoundDebitsTheHouseholdExactlyWhatTheCityReceived() {
  var finance = new AreaFinanceRuntime(_ => { }); var park = new Park();
  var money = new PlayerMoney(1_999_999_990); long household = 1000;
  long collected = Admit(finance, park, ref money, ref household, 60);
  Assert.That(collected, Is.EqualTo(10));
  Assert.That(household, Is.EqualTo(990));
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(2_000_000_000));
  Assert.That(park.State.LifetimeAdmissionRevenue, Is.EqualTo(10));
  Assert.That(finance.AreaServiceFees(5), Is.EqualTo(10));
  ApplyAreaFinance(finance, ref money, 2);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(2_000_000_000), "recording the income never credits again");
 }
 [Test] public void AHouseholdWithoutEnoughMoneyPaysOnlyWhatItHasAndTheCityGetsTheSame() {
  var finance = new AreaFinanceRuntime(_ => { }); var park = new Park();
  var money = new PlayerMoney(-3_000); long household = 7;
  Assert.That(Admit(finance, park, ref money, ref household, 20), Is.EqualTo(7));
  Assert.That(household, Is.Zero);
  Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(-2_993), "a negative city balance still receives admission");
  Assert.That(finance.AreaServiceFees(1), Is.EqualTo(7));
 }
 [Test] public void AdmissionIsAnAreaServiceFeeAndNeverAnExpenseOrVanillaFee() {
  var finance = new AreaFinanceRuntime(_ => { });
  Assert.That(finance.EnqueueExpense(7, AreaFinanceKind.ParkAdmission, 10), Is.False);
  Assert.That(finance.RecordSettledIncome(1, 7, AreaFinanceKind.ParkAdmission, 10), Is.True);
  Assert.That(finance.AreaManagement(2), Is.Zero);
  Assert.That(finance.AreaServiceFees(2), Is.EqualTo(10));
  Assert.That(Enum.GetNames(typeof(AreaFinanceKind)).Where(n => n.Contains("Parking") || n.Contains("Transport")), Is.Empty);
 }
 [Test] public void AppliedManagementIgnoresOtherKinds() {
  var ledger = new AreaFinanceLedger();
  ledger.EnqueueExpense(7, AreaFinanceKind.PolicyUpkeep, 5);
  ledger.EnqueueExpense(7, AreaFinanceKind.ParkManagement, 6);
  ledger.EnqueueIncome(7, AreaFinanceKind.ParkAdmission, 9);
  var settled = ledger.TakeBatch().Resolve(11, 9);
  Assert.That(ParkAreaFinance.AppliedManagement(settled), Is.EqualTo(new[] { new AreaFinanceEntry(7, AreaFinanceKind.ParkManagement, 6) }));
  Assert.That(ParkAreaFinance.AppliedManagement(null), Is.Empty);
 }
}
