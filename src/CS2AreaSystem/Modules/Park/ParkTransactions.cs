using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public struct ParkAdmissionRequest
    {
        public Entity Citizen, Area;
        public ulong ParkId;
        public long Fee;
    }
    public struct ParkAdmissionOutcome
    {
        public ParkAdmissionRequest Request;
        public long Collected;
        public long Uncollected => Request.Fee - Collected;
    }
    public sealed class ParkTransactionBatch
    {
        public List<ParkAdmissionRequest> Admissions = new List<ParkAdmissionRequest>();
    }
    public sealed class ParkTransactionOutcomes
    {
        public List<ParkAdmissionOutcome> Admissions = new List<ParkAdmissionOutcome>();
    }
    // Admissions are household-to-city transfers made by a scheduled job (no main-thread sync on Resources/PlayerMoney);
    // this ledger holds the requests until then and turns the job's results into exactly-collected records afterwards.
    // Park management is not here: it is an Area Management expense queued in the shared Area finance.
    public sealed class ParkTransactionLedger
    {
        private ParkTransactionBatch pending = new ParkTransactionBatch();
        public int PendingAdmissions => pending.Admissions.Count;
        public bool HasPending => pending.Admissions.Count > 0;
        public void EnqueueAdmission(ParkAdmissionRequest request) { if (request.Fee > 0) pending.Admissions.Add(request); }
        public ParkTransactionBatch TakeBatch() { var batch = pending; pending = new ParkTransactionBatch(); return batch; }
        public void Clear() => pending = new ParkTransactionBatch();
        public static ParkTransactionOutcomes Resolve(ParkTransactionBatch batch, IReadOnlyList<long> collected)
        {
            var outcomes = new ParkTransactionOutcomes();
            int count = Math.Min(batch.Admissions.Count, collected.Count);
            for (int i = 0; i < count; i++)
            {
                var request = batch.Admissions[i];
                outcomes.Admissions.Add(new ParkAdmissionOutcome { Request = request, Collected = Math.Max(0, Math.Min(request.Fee, collected[i])) });
            }
            return outcomes;
        }
    }
}
