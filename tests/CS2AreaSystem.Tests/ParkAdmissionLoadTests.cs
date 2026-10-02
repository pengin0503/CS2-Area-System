using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Modules.Park;
using Game.City;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;

public class ParkAdmissionLoadTests {
    private sealed class Sink : IParkAdmissionRecordSink {
        public readonly AreaFinanceRuntime Accounts = new(_ => { });
        public readonly Dictionary<ulong, ParkAreaState> States = new();
        public readonly List<ulong> Recent = new();
        public bool FailFirst;
        public void RecordRecent(in ParkAdmissionOutcome o) => Recent.Add(o.Request.ParkId);
        public void RecordServiceFee(in ParkAdmissionOutcome o) => Accounts.RecordSettledIncome(1, o.Request.ParkId, AreaFinanceKind.ParkAdmission, o.Collected);
        public void RecordLifetimeRevenue(in ParkAdmissionOutcome o) {
            if (FailFirst && o.Request.ParkId == 7) throw new InvalidOperationException("unreadable old Area");
            States[o.Request.ParkId] = ParkFinanceModel.RecordAdmission(States[o.Request.ParkId], o.Collected);
        }
    }
    private static int RecordLoad(IReadOnlyList<ParkAdmissionOutcome> outcomes, Sink sink, Action<Exception, ParkAdmissionOutcome, string> report) {
        // The missing contract must fail as an assertion on the old main, rather than preventing the test build.
        var method = typeof(ParkAdmissionRecording).GetMethods().SingleOrDefault(m => m.Name == "RecordAll" && m.GetParameters().Length == 4);
        Assert.That(method, Is.Not.Null, "load recording must exclude accounts which another preload subscriber already reset");
        return (int)method!.Invoke(null, new object[] { outcomes, sink, report, true })!;
    }
    [Test] public void PendingTransferIsRecordedIntoOldCountersWithoutCreditingCityOrRepopulatingNewAccounts() {
        var ledger = new ParkTransactionLedger();
        ledger.EnqueueAdmission(new ParkAdmissionRequest { ParkId = 7, Fee = 20 });
        var inFlight = ledger.TakeBatch();
        ledger.EnqueueAdmission(new ParkAdmissionRequest { ParkId = 8, Fee = 30 });
        var money = new PlayerMoney(1000); long household = 100;
        // Completing the pending job runs this production transfer exactly once, before its old-world result is recorded.
        long collected = AreaFinanceHouseholdTransfer.Transfer(ref money, household, 20); household -= collected;
        var sink = new Sink(); sink.States[7] = ParkAreaState.CreateDefault();
        sink.Accounts.Reset(); // Area finance can receive preload before Park finance.
        var outcomes = ParkTransactionLedger.Resolve(inFlight, new[] { collected });
        Assert.That(RecordLoad(outcomes.Admissions, sink, (_, _, _) => Assert.Fail()), Is.Zero);
        inFlight = null!; ledger.Clear();
        Assert.That(sink.States[7].LifetimeAdmissionRevenue, Is.EqualTo(20));
        Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(money), Is.EqualTo(1020));
        Assert.That(household, Is.EqualTo(80)); Assert.That(ledger.HasPending, Is.False);
        Assert.That(sink.Recent, Is.Empty); Assert.That(sink.Accounts.AreaServiceFees(2), Is.Zero);
        var newCity = new PlayerMoney(500); var newState = ParkAreaState.CreateDefault();
        Assert.That(newState.LifetimeAdmissionRevenue, Is.Zero); Assert.That(AreaFinanceVanillaAdapter.StoredCityMoney(newCity), Is.EqualTo(500));
    }
    [Test] public void LoadRecordingIsolatesOneFailedLifetimeRecordAndSkipsZeroCollections() {
        var sink = new Sink { FailFirst = true };
        sink.States[7] = ParkAreaState.CreateDefault(); sink.States[8] = ParkAreaState.CreateDefault();
        var outcomes = new[] {
            new ParkAdmissionOutcome { Request = new ParkAdmissionRequest { ParkId = 7, Fee = 20 }, Collected = 20 },
            new ParkAdmissionOutcome { Request = new ParkAdmissionRequest { ParkId = 8, Fee = 30 }, Collected = 12 },
            new ParkAdmissionOutcome { Request = new ParkAdmissionRequest { ParkId = 9, Fee = 30 }, Collected = 0 }
        };
        var reports = new List<ulong>();
        Assert.That(RecordLoad(outcomes, sink, (_, o, _) => reports.Add(o.Request.ParkId)), Is.EqualTo(1));
        Assert.That(reports, Is.EqualTo(new ulong[] { 7 }));
        Assert.That(sink.States[8].LifetimeAdmissionRevenue, Is.EqualTo(12));
        Assert.That(sink.Recent, Is.Empty); Assert.That(sink.Accounts.AreaServiceFees(2), Is.Zero);
    }
}
