using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 2 C5 Task 2: a festival starts only after its Area Finance expense actually settles; zero/aborted outcomes clear
// the pending request without history, charge or retry.
public class ParkEventFinanceTests {
 static Entity E(int id) => new() { Index = id, Version = 1 };
 internal sealed class Harness : IAreaFinanceRequestListener {
  public readonly AreaFinanceRuntime Finance = new(_ => { });
  public readonly ParkEventLedger Ledger = new();
  public readonly List<ParkEventSettlement> Paid = new();
  public ParkEventState State = ParkEventState.CreateDefault();
  public readonly List<ParkEventHistoryEntry> History = new();
  public bool ParkActive = true, Exists = true;
  public uint Frame = 10_000;
  public int Generation = 1;
  public double DailyCost = 1000;
  public bool CostKnown = true, Available = true;
  public Harness() { Finance.Start(0); Finance.AddRequestListener(this); }
  public void OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame) {
   Paid.Clear();
   ParkEventFinance.OnCompleted(Ledger, Generation, requested, settled, Paid);
   foreach (var paid in Paid)
    ParkEventFinance.Apply(paid, ParkEventFinance.EligibleToStart(Exists, ParkActive, true, State, Frame), Frame, ref State, History);
  }
  public ParkEventRequestInputs Inputs(int eventId = ParkEventCatalog.ParkFestival) => new() {
   Available = Available, ParkActive = ParkActive, HasState = true, State = State, Frame = Frame, EventId = eventId,
   HasPending = Ledger.HasPending(9), CostKnown = CostKnown, DailyManagementCost = DailyCost };
  public ParkEventRequestOutcome Request(int eventId = ParkEventCatalog.ParkFestival) =>
   ParkEventFinance.TryRequest(Inputs(eventId), Generation, 9, E(9), amount => Finance.EnqueueExpense(9, AreaFinanceKind.ParkEvent, amount), Ledger);
  public void Settle(long applied) { Finance.BeginApply(Frame); Finance.CompleteApply(applied, 0); }
 }
 [Test] public void StartIsRequestedOnceAtHalfTheDailyManagementCost() {
  var h = new Harness { DailyCost = 1001 };
  Assert.That(h.Request(), Is.EqualTo(ParkEventRequestOutcome.Request));
  Assert.That(h.Ledger.Get(9).Requested, Is.EqualTo(500), "round-to-even of 500.5");
  Assert.That(h.Request(), Is.EqualTo(ParkEventRequestOutcome.Pending), "second click while payment is pending");
  var batch = h.Finance.BeginApply(1);
  Assert.That(batch.Total(AreaFinanceKind.ParkEvent), Is.EqualTo(500), "exactly one request");
 }
 [Test] public void FullSettlementStartsAFullDayAndRecordsTheCostOnce() {
  var h = new Harness();
  h.Request();
  h.Settle(500);
  Assert.That(ParkEventModel.IsActive(h.State, h.Frame), Is.True);
  Assert.That((h.State.StartFrame, h.State.EndFrameExclusive), Is.EqualTo((10_000u, 10_000u + ParkEventCatalog.DurationFrames)));
  Assert.That((h.State.LifetimeEvents, h.State.LifetimeEventExpense), Is.EqualTo((1L, 500L)));
  Assert.That(h.History.Single().SettledExpense, Is.EqualTo(500));
  Assert.That(h.Ledger.HasPending(9), Is.False);
 }
 [Test] public void PartialPositiveSettlementStartsAndRecordsTheActualPaidCost() {
  var h = new Harness();
  h.Request();
  h.Settle(120);
  Assert.That(ParkEventModel.IsActive(h.State, h.Frame), Is.True);
  Assert.That(h.State.LifetimeEventExpense, Is.EqualTo(120));
  Assert.That(h.History.Single().SettledExpense, Is.EqualTo(120));
 }
 [Test] public void ZeroSettlementAtTheNativeBoundClearsPendingWithoutStartHistoryOrRetry() {
  var h = new Harness();
  h.Request();
  h.Settle(0);
  Assert.That(h.State.ActiveEventId, Is.Zero);
  Assert.That((h.State.LifetimeEvents, h.State.LifetimeEventExpense), Is.EqualTo((0L, 0L)));
  Assert.That(h.History, Is.Empty);
  Assert.That(h.Ledger.HasPending(9), Is.False);
  Assert.That(h.Finance.HasQueued, Is.False, "never re-queued automatically");
 }
 [Test] public void AbortedOrCancelledRequestsClearPendingWithoutCharge() {
  var h = new Harness();
  h.Request();
  h.Finance.BeginApply(1); h.Finance.AbortApply();
  Assert.That(h.Ledger.HasPending(9), Is.False);
  Assert.That(h.Request(), Is.EqualTo(ParkEventRequestOutcome.Request), "the player may start again");
  h.Finance.Reset();
  Assert.That(h.Ledger.HasPending(9), Is.False, "preload/teardown cancels the unapplied request");
  Assert.That(h.History, Is.Empty);
 }
 [Test] public void EligibilityLostBeforeSettlementAccountsTheExpenseWithoutEffectsOrHistory() {
  var h = new Harness();
  h.Request();
  h.ParkActive = false;
  h.Settle(500);
  Assert.That(h.State.ActiveEventId, Is.Zero);
  Assert.That((h.State.LifetimeEvents, h.State.LifetimeEventExpense), Is.EqualTo((0L, 500L)));
  Assert.That(h.History, Is.Empty);
 }
 [Test] public void DeletedAreaGetsNoStateWrite() {
  Assert.That(ParkEventFinance.EligibleToStart(false, true, true, ParkEventState.CreateDefault(), 1), Is.False);
  Assert.That(ParkEventFinance.EligibleToStart(true, true, false, ParkEventState.CreateDefault(), 1), Is.False);
  var active = ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 0, 1);
  Assert.That(ParkEventFinance.EligibleToStart(true, true, true, active, 5), Is.False, "one active event per Park");
 }
 [TestCase(false, true, true, ParkEventCatalog.ParkFestival, ParkEventRequestOutcome.Unavailable)]
 [TestCase(true, false, true, ParkEventCatalog.ParkFestival, ParkEventRequestOutcome.Inactive)]
 [TestCase(true, true, false, ParkEventCatalog.ParkFestival, ParkEventRequestOutcome.NoCost)]
 [TestCase(true, true, true, 2, ParkEventRequestOutcome.UnknownEvent)]
 public void StartIsRejectedUntilEligible(bool available, bool parkActive, bool costKnown, int eventId, ParkEventRequestOutcome expected) {
  var h = new Harness { Available = available, ParkActive = parkActive, CostKnown = costKnown };
  Assert.That(h.Request(eventId), Is.EqualTo(expected));
  Assert.That(h.Finance.HasQueued, Is.False);
  Assert.That(h.Ledger.HasPending(9), Is.False);
 }
 [Test] public void AnActiveEventCannotBeStartedAgainUntilItEnds() {
  var h = new Harness();
  h.Request(); h.Settle(500);
  Assert.That(h.Request(), Is.EqualTo(ParkEventRequestOutcome.Active));
  h.Frame += ParkEventCatalog.DurationFrames;
  Assert.That(h.Request(), Is.EqualTo(ParkEventRequestOutcome.Request), "no automatic recurrence; the player starts the next one");
 }
 [Test] public void RejectedEnqueueLeavesNothingPending() {
  var h = new Harness();
  Assert.That(ParkEventFinance.TryRequest(h.Inputs(), 1, 9, E(9), _ => false, h.Ledger), Is.EqualTo(ParkEventRequestOutcome.Unavailable));
  Assert.That(h.Ledger.HasPending(9), Is.False);
 }
 [Test] public void OtherGenerationsAndKindsAreIgnored() {
  var h = new Harness();
  h.Request();
  h.Generation = 2;
  h.Settle(500);
  Assert.That(h.State.ActiveEventId, Is.Zero, "an old-generation pending entry is never started in a new city");
  var other = new Harness();
  other.Request();
  other.Finance.EnqueueExpense(9, AreaFinanceKind.ParkManagement, 30);
  var ledger = other.Ledger;
  var batch = new AreaFinanceRuntime(_ => { }); batch.EnqueueExpense(9, AreaFinanceKind.ParkManagement, 30); var only = batch.BeginApply(1);
  var paid = new List<ParkEventSettlement>();
  ParkEventFinance.OnCompleted(ledger, other.Generation, only, only, paid);
  Assert.That(paid, Is.Empty); Assert.That(ledger.HasPending(9), Is.True, "a management-only completion does not consume the event request");
 }
 [Test] public void ParkManagementListenersNeverCountTheEventExpense() {
  var runtime = new AreaFinanceRuntime(_ => { }); runtime.Start(0);
  runtime.EnqueueExpense(9, AreaFinanceKind.ParkManagement, 30); runtime.EnqueueExpense(9, AreaFinanceKind.ParkEvent, 500);
  runtime.BeginApply(1); var settled = runtime.CompleteApply(530, 0);
  Assert.That(ParkAreaFinance.AppliedManagement(settled).Sum(e => e.Amount), Is.EqualTo(30));
  Assert.That(runtime.AreaManagement(1), Is.EqualTo(530), "Area Management totals include the event once");
 }
}
// Save/load during pending or active events.
public class ParkEventSaveBoundaryTests {
 [Test] public void SaveFlushSettlesAndStartsThePendingEventOnceBeforeSerialization() {
  var h = new ParkEventFinanceTests.Harness();
  h.Request();
  void Flush() => AreaFinanceSaveBoundary.Flush(true, 1, 1, () => { }, () => h.Finance.HasQueued, () => { }, () => h.Settle(500));
  Flush(); Flush();
  Assert.That(h.State.LifetimeEvents, Is.EqualTo(1));
  Assert.That(h.State.LifetimeEventExpense, Is.EqualTo(500), "no second charge");
  Assert.That(h.History, Has.Count.EqualTo(1));
  Assert.That(h.Ledger.Count, Is.Zero, "nothing pending is serialized");
 }
 [Test] public void ActiveEventSurvivesLoadWithItsEndFrameAndPaidAmount() {
  var state = ParkEventModel.Start(ParkEventState.CreateDefault(), 1, 400, 500);
  var io = Support.MemorySerializer.Create(); state.Serialize(io.Writer);
  var loaded = new ParkEventState(); loaded.Deserialize(io.Reader);
  Assert.That((loaded.EndFrameExclusive, loaded.LifetimeEventExpense), Is.EqualTo((400u + ParkEventCatalog.DurationFrames, 500L)));
  Assert.That(ParkEventModel.IsActive(loaded, 1000), Is.True);
 }
}
// The ECS wiring cannot run here; these guards pin the parts the pure tests rely on.
public class ParkEventSystemSourceGuardTests {
 static string Source(string path) => IndustrialSupportSourceGuardTests.Source(path);
 [Test] public void StartsAreRequestedOnlyThroughTheAcceptedEnqueuePathAfterFinanceStarted() {
  var system = Source("Modules/Park/ParkEventSystem.cs");
  Assert.That(system, Does.Contain("IAreaFinanceRequestListener"));
  Assert.That(system, Does.Contain("ParkEventFinance.TryRequest("));
  Assert.That(system, Does.Contain("finance.EnqueueExpense(parkId, AreaFinanceKind.ParkEvent, amount)"));
  Assert.That(system, Does.Contain("Available = Enabled && finance.Started && runtime.Ready"));
  Assert.That(system, Does.Not.Contain("snapshot.ManagementCostKnown"), "a settled snapshot does not prove current budget/policy inputs");
  Assert.That(system, Does.Contain("parkFinance.ReadManagementCost(area, snapshot, frame)"));
  Assert.That(system, Does.Contain("CostKnown = estimate.Known, DailyManagementCost = estimate.CostPerDay"));
 }
 [Test] public void CompletionRecordsTheEventExpenseInParkRecentExpenseOnce() {
  var system = Source("Modules/Park/ParkEventSystem.cs");
  Assert.That(system.Split("RecentExpense.Add(").Length - 1, Is.EqualTo(1));
  Assert.That(system, Does.Contain("ParkEventFinance.OnCompleted(ledger, finance.LoadGeneration, requested, settled, paid)"));
  Assert.That(Source("Modules/Park/ParkFinanceSystem.cs"), Does.Contain("snapshot.ManagementCostKnown = true;"));
 }
 [Test] public void AStartedFestivalRecomputesParkQualityImmediately() {
  var system = Source("Modules/Park/ParkEventSystem.cs");
  int started = system.IndexOf("if (outcome == ParkEventApplyOutcome.Started)");
  Assert.That(started, Is.GreaterThan(0));
  Assert.That(system.IndexOf("quality.Recompute(new ParkAreaRef(area, payment.AreaId,", started), Is.GreaterThan(started), "capacity/occupancy reflect the festival at once");
 }
 [Test] public void EventStateIsAddedWithParkStateInTheModificationPhase() {
  var runtime = Source("Modules/Park/ParkRuntimeSystem.cs");
  Assert.That(runtime, Does.Contain("EntityManager.AddComponentData(park.Area, ParkEventState.CreateDefault())"));
  Assert.That(runtime, Does.Contain("EntityManager.AddBuffer<ParkEventHistoryEntry>(park.Area)"));
  Assert.That(Source("Modules/Park/ParkEventSystem.cs"), Does.Not.Contain("AddComponent"), "no structural change in GameSimulation");
 }
}
