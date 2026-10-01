using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Park
{
    // Where an admission outcome is recorded after its household-to-city transfer already ran. No member credits money.
    public interface IParkAdmissionRecordSink
    {
        void RecordRecent(in ParkAdmissionOutcome outcome);
        void RecordServiceFee(in ParkAdmissionOutcome outcome);
        void RecordLifetimeRevenue(in ParkAdmissionOutcome outcome);
    }
    // #75: every outcome, and every record of an outcome, is isolated: a failing record is reported and skipped, the other
    // records of the batch are still written, and the transfer is never repeated (the money already moved in the job).
    public static class ParkAdmissionRecording
    {
        public static int RecordAll(IReadOnlyList<ParkAdmissionOutcome> outcomes, IParkAdmissionRecordSink sink, Action<Exception, ParkAdmissionOutcome, string> report)
        {
            int failures = 0;
            foreach (var outcome in outcomes)
            {
                if (!Try(() => sink.RecordRecent(outcome), outcome, "recent admission statistics", report)) failures++;
                if (outcome.Collected <= 0) continue;
                if (!Try(() => sink.RecordServiceFee(outcome), outcome, "Area Service Fees", report)) failures++;
                if (!Try(() => sink.RecordLifetimeRevenue(outcome), outcome, "lifetime admission revenue", report)) failures++;
            }
            return failures;
        }
        private static bool Try(Action record, ParkAdmissionOutcome outcome, string what, Action<Exception, ParkAdmissionOutcome, string> report)
        {
            try { record(); return true; }
            catch (Exception error) { report(error, outcome, what); return false; }
        }
    }
}
