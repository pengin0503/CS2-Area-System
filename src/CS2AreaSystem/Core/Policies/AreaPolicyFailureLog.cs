using System.Collections.Generic;
namespace CS2AreaSystem.Core.Policies
{
    // Logs the first occurrence of Area stable ID + PolicyId + failure class per load generation; repeats stay quiet
    // until the generation changes or the failure clears and later recurs.
    public sealed class AreaPolicyFailureLog
    {
        private readonly HashSet<(ulong Area, int Policy, string FailureClass)> active = new HashSet<(ulong, int, string)>();
        public int Generation { get; private set; }
        public bool ShouldLog(ulong areaStableId, int policyId, string failureClass) => active.Add((areaStableId, policyId, failureClass));
        public void Clear(ulong areaStableId, int policyId, string failureClass) => active.Remove((areaStableId, policyId, failureClass));
        public void NewGeneration() { Generation++; active.Clear(); }
    }
}
