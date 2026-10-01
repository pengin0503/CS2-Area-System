using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Tests.Support;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// The budget page shows the amounts Area finance actually applied to city money (design §15, plan Task 9).
public class AreaBudgetViewModelTests {
 private static void Apply(AreaFinanceRuntime runtime, ref PlayerMoney money, uint frame) {
  var batch = runtime.BeginApply(frame);
  var applied = AreaFinanceVanillaAdapter.Apply(ref money, batch.TotalExpense, batch.TotalIncome);
  runtime.CompleteApply(applied.Expense, applied.Income);
 }
 [Test] public void ValuesAreTheSettledAreaManagementAndServiceFees() {
  var runtime = new AreaFinanceRuntime(_ => { });
  runtime.Start(0);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 300);
  runtime.EnqueueExpense(2, AreaFinanceKind.ParkManagement, 120);
  var money = new PlayerMoney(-50_000);
  Apply(runtime, ref money, 256);
  runtime.RecordSettledIncome(300, 2, AreaFinanceKind.ParkAdmission, 45);
  var view = AreaBudgetViewModel.From(runtime, 512);
  Assert.That(view.AreaManagement, Is.EqualTo(420), "policy upkeep and Park management, both applied while the city was in debt");
  Assert.That(view.AreaServiceFees, Is.EqualTo(45));
 }
 [Test] public void QueuedButUnappliedAmountsAreNotShown() {
  var runtime = new AreaFinanceRuntime(_ => { });
  runtime.Start(0);
  runtime.EnqueueExpense(1, AreaFinanceKind.PolicyUpkeep, 300);
  runtime.EnqueueIncome(1, AreaFinanceKind.ParkAdmission, 80);
  Assert.That(AreaBudgetViewModel.From(runtime, 10), Is.EqualTo(new AreaBudgetViewModel(0, 0)));
  runtime.BeginApply(10);
  Assert.That(AreaBudgetViewModel.From(runtime, 11), Is.EqualTo(new AreaBudgetViewModel(0, 0)), "in flight is not settled");
 }
 [Test] public void OnlyTheAmountTheCityActuallyReceivedIsShown() {
  var runtime = new AreaFinanceRuntime(_ => { });
  runtime.Start(0);
  runtime.EnqueueIncome(1, AreaFinanceKind.ParkAdmission, 500);
  var money = new PlayerMoney(PlayerMoney.kMaxMoney - 200);
  Apply(runtime, ref money, 256);
  Assert.That(AreaBudgetViewModel.From(runtime, 300).AreaServiceFees, Is.EqualTo(200), "the native money cap limits what was credited");
 }
 [Test] public void AmountsLeaveTheTrailingDay() {
  var runtime = new AreaFinanceRuntime(_ => { });
  runtime.Start(0);
  runtime.RecordSettledIncome(100, 1, AreaFinanceKind.ParkAdmission, 45);
  Assert.That(AreaBudgetViewModel.From(runtime, 100 + AreaFinanceModel.FramesPerDay - 1).AreaServiceFees, Is.EqualTo(45));
  Assert.That(AreaBudgetViewModel.From(runtime, 100 + AreaFinanceModel.FramesPerDay).AreaServiceFees, Is.Zero);
 }
 [Test] public void NoRuntimeAndNegativeInputsShowNothing() {
  Assert.That(AreaBudgetViewModel.From(null, 0), Is.EqualTo(new AreaBudgetViewModel(0, 0)));
  Assert.That(new AreaBudgetViewModel(-5, -1), Is.EqualTo(new AreaBudgetViewModel(0, 0)));
 }
 [Test] public void PublishesBothValuesUnderStableNames() {
  var writer = new RecordingJsonWriter();
  new AreaBudgetViewModel(420, 45).Write(writer);
  var json = writer.Parse();
  Assert.That(json.GetProperty("areaManagement").GetInt64(), Is.EqualTo(420));
  Assert.That(json.GetProperty("areaServiceFees").GetInt64(), Is.EqualTo(45));
  Assert.That(AreaBudgetViewModel.AreaManagementSource, Is.EqualTo("CS2AS.AreaManagement"));
  Assert.That(AreaBudgetViewModel.AreaServiceFeesSource, Is.EqualTo("CS2AS.AreaServiceFees"));
 }
 [Test] public void TheBindingSystemPublishesButNeverMovesMoney() {
  var type = typeof(AreaBudgetBindingSystem);
  Assert.That(type.BaseType, Is.EqualTo(typeof(Game.UI.UISystemBase)));
  var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Core/Finance/AreaBudgetBindingSystem.cs"));
  Assert.That(source, Does.Contain("\"cs2AreaSystem\", \"areaBudget\""));
  Assert.That(source, Does.Not.Match(@"PlayerMoney|Enqueue(Expense|Income)|RecordSettledIncome|ComponentLookup"));
 }
}
