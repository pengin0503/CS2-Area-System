using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkLiveManagementCostTests {
 // The shared calculator must ignore the previous settlement's estimate and read all current runtime inputs.
 // Reflection keeps this RED test compilable before the shared production API exists.
 static (bool Known, float Need, float Cost) Read(ParkRuntimeSnapshot snapshot, uint frame, float budget, ParkPolicyEffects effects) {
  var method = typeof(ParkFinanceModel).GetMethod("ComputeLiveManagementCost");
  Assert.That(method, Is.Not.Null, "settlement and Festival need one live input calculation");
  object value = method.Invoke(null, new object[] { snapshot, frame, budget, effects });
  var type = value.GetType();
  return ((bool)type.GetField("Known").GetValue(value), (float)type.GetField("NeedPerDay").GetValue(value), (float)type.GetField("CostPerDay").GetValue(value));
 }
 [TestCase(1.5f, false, 648L)]
 [TestCase(1f, true, 540L)]
 [TestCase(1.5f, true, 810L)]
 public void FestivalBeforeNextSettlementUsesChangedBudgetAndEffectivePolicy(float budget, bool enhanced, long expectedCost) {
  var snapshot = new ParkRuntimeSnapshot { AreaSquareMeters = 10_000, Facilities = new ParkFacilitySnapshot { RelevantFacilities = 2 } };
  snapshot.Landscape.Available = true; snapshot.Landscape.Result = new ParkLandscapeResult { ObjectCount = 4 };
  snapshot.RecentVisitors.Add(100, 10);
  // Hand-calculated need = 150 + 350 + 15 + 16 + 80 = 611, settled at 100% without policy.
  // More visits arrived since that settlement, so current need = 864 (611 + 253.5, represented as 864.5 below).
  snapshot.ManagementNeedPerDay = 611; snapshot.ManagementCostPerDay = 611; snapshot.ManagementCostKnown = true;
  snapshot.RecentVisitors.Add(110, 169); // current need = 864.5
  var estimate = Read(snapshot, 120, budget, new ParkPolicyEffects(enhanced, false, false));
  Assert.That(estimate.Known, Is.True); Assert.That(estimate.Need, Is.EqualTo(864.5f));
  var inputs = new ParkEventRequestInputs { Available = true, ParkActive = true, HasState = true, State = ParkEventState.CreateDefault(),
   Frame = 120, EventId = 1, CostKnown = estimate.Known, DailyManagementCost = estimate.Cost };
  Assert.That(ParkEventFinance.Decide(inputs, out var displayed), Is.EqualTo(ParkEventRequestOutcome.Request));
  Assert.That(displayed, Is.EqualTo(expectedCost));
  var finance = new AreaFinanceRuntime(_ => { }); finance.Start(0); var ledger = new ParkEventLedger();
  Assert.That(ParkEventFinance.TryRequest(inputs, 1, 9, new Entity { Index = 9, Version = 1 },
   amount => finance.EnqueueExpense(9, AreaFinanceKind.ParkEvent, amount), ledger), Is.EqualTo(ParkEventRequestOutcome.Request));
  Assert.That(ledger.Get(9).Requested, Is.EqualTo(displayed), "UI and action charge the same current cost");
  Assert.That(finance.BeginApply(120).Total(AreaFinanceKind.ParkEvent), Is.EqualTo(expectedCost));
  Assert.That(snapshot.ManagementCostPerDay, Is.EqualTo(611), "estimation does not perform another settlement");
 }
 [Test] public void UnknownLandscapeAndExpiredVisitorsUseCurrentAvailableInputs() {
  var snapshot = new ParkRuntimeSnapshot { ManagementCostKnown = true, ManagementCostPerDay = 9000, ManagementNeedPerDay = 9000 };
  snapshot.Landscape.Available = false; snapshot.Landscape.Result = new ParkLandscapeResult { ObjectCount = 100 };
  snapshot.RecentVisitors.Add(1, 100);
  var estimate = Read(snapshot, ParkFinanceModel.FramesPerDay * 2, 1f, ParkPolicyEffects.None);
  Assert.That(estimate.Need, Is.EqualTo(150)); Assert.That(estimate.Cost, Is.EqualTo(150));
  Assert.That(Read(null, 1, 1f, ParkPolicyEffects.None).Known, Is.False);
 }
 [Test] public void SettlementAndFestivalUseTheSameLiveCalculation() {
  var source = IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkFinanceSystem.cs");
  Assert.That(source, Does.Contain("var estimate = ReadManagementCost(park.Area, snapshot, frame)"));
  Assert.That(source, Does.Contain("ParkFinanceModel.ComputeLiveManagementCost("));
  Assert.That(IndustrialSupportSourceGuardTests.Source("Modules/Park/ParkGameplayFacade.cs"), Does.Contain("StartInputs("));
 }
}
