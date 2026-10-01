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
    public struct ParkManagementCharge
    {
        public Entity Area;
        public ulong ParkId;
        public long Paid;
    }
    public sealed class ParkTransactionBatch
    {
        public List<ParkAdmissionRequest> Admissions = new List<ParkAdmissionRequest>();
        public List<ParkManagementCharge> Management = new List<ParkManagementCharge>();
        // One aggregated PlayerMoney change for all management charges of the batch (negative = expense).
        public long CityDelta;
    }
    public sealed class ParkTransactionOutcomes
    {
        public List<ParkAdmissionOutcome> Admissions = new List<ParkAdmissionOutcome>();
        public List<ParkManagementCharge> Management = new List<ParkManagementCharge>();
    }
    // Money is moved by a scheduled job (no main-thread sync on Resources/PlayerMoney); this ledger holds the
    // requests until then and turns the job's results into exactly-collected records afterwards.
    public sealed class ParkTransactionLedger
    {
        private ParkTransactionBatch pending = new ParkTransactionBatch();
        public int PendingAdmissions => pending.Admissions.Count;
        public bool HasPending => pending.Admissions.Count > 0 || pending.Management.Count > 0;
        public void EnqueueAdmission(ParkAdmissionRequest request) { if (request.Fee > 0) pending.Admissions.Add(request); }
        public void EnqueueManagement(Entity area, ulong parkId, long amount)
        {
            if (amount <= 0) return;
            pending.Management.Add(new ParkManagementCharge { Area = area, ParkId = parkId, Paid = amount });
            pending.CityDelta -= amount;
        }
        public ParkTransactionBatch TakeBatch() { var batch = pending; pending = new ParkTransactionBatch(); return batch; }
        public void Clear() => pending = new ParkTransactionBatch();
        public static ParkTransactionOutcomes Resolve(ParkTransactionBatch batch, IReadOnlyList<long> collected, long managementPaid)
        {
            var outcomes = new ParkTransactionOutcomes();
            int count = Math.Min(batch.Admissions.Count, collected.Count);
            for (int i = 0; i < count; i++)
            {
                var request = batch.Admissions[i];
                outcomes.Admissions.Add(new ParkAdmissionOutcome { Request = request, Collected = Math.Max(0, Math.Min(request.Fee, collected[i])) });
            }
            // Allocate the actual debit in request order. Unpaid expenses are never counted as city spending.
            long remaining = Math.Max(0, managementPaid);
            foreach (var charge in batch.Management)
            {
                long paid = Math.Min(remaining, charge.Paid);
                if (paid > 0) outcomes.Management.Add(new ParkManagementCharge { Area = charge.Area, ParkId = charge.ParkId, Paid = paid });
                remaining -= paid;
                if (remaining == 0) break;
            }
            return outcomes;
        }
    }
}
