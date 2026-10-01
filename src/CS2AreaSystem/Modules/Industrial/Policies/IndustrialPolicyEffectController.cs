using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial.Policies
{
    // Applies and removes Industrial policy adapters on effective-state transitions (pure; the ECS system feeds it).
    // A failing adapter disables only that policy effect for that Area until the next load, and each
    // Area + policy + failure class is reported once.
    public sealed class IndustrialPolicyEffectController
    {
        private readonly Dictionary<int, IIndustrialPolicyAdapter> adapters = new Dictionary<int, IIndustrialPolicyAdapter>();
        private readonly HashSet<(ulong Area, int Policy)> applied = new HashSet<(ulong, int)>();
        private readonly HashSet<(ulong Area, int Policy)> failed = new HashSet<(ulong, int)>();
        private readonly HashSet<string> reported = new HashSet<string>();
        private readonly Action<Exception, string> report;
        public IndustrialPolicyEffectController(IEnumerable<IIndustrialPolicyAdapter> adapters, Action<Exception, string> report)
        {
            this.report = report;
            foreach (var adapter in adapters)
            {
                if (adapter == null) throw new ArgumentNullException(nameof(adapters));
                if (!IndustrialPolicyCatalog.IsIndustrialPolicy(adapter.PolicyId)) throw new ArgumentException("Not an Industrial policy: " + adapter.PolicyId);
                if (this.adapters.ContainsKey(adapter.PolicyId)) throw new ArgumentException("Duplicate adapter for policy " + adapter.PolicyId);
                this.adapters.Add(adapter.PolicyId, adapter);
            }
        }
        public IReadOnlyCollection<int> PolicyIds => adapters.Keys;
        public bool HasAdapters => adapters.Count > 0;
        public bool IsApplied(ulong areaStableId, int policyId) => applied.Contains((areaStableId, policyId));
        public bool HasFailed(ulong areaStableId, int policyId) => failed.Contains((areaStableId, policyId));
        public bool IsAvailable(Entity area, ulong areaStableId, int policyId, in AreaPolicyContext context, out string reasonKey)
        {
            reasonKey = AreaPolicyReasons.EffectUnavailable;
            if (!adapters.TryGetValue(policyId, out var adapter) || HasFailed(areaStableId, policyId)) return false;
            try
            {
                if (adapter.IsSupported(area, context, out var adapterReason)) { reasonKey = null; return true; }
                reasonKey = adapterReason ?? AreaPolicyReasons.EffectUnavailable;
                return false;
            }
            catch (Exception error) { Report(error, areaStableId, policyId, "supported"); return false; }
        }
        public void Sync(Entity area, ulong areaStableId, int policyId, bool effective, in AreaPolicyContext context)
        {
            if (!adapters.TryGetValue(policyId, out var adapter) || HasFailed(areaStableId, policyId)) return;
            var key = (areaStableId, policyId);
            if (effective == applied.Contains(key)) return;
            if (effective) Apply(adapter, area, key, context);
            else Remove(adapter, area, key, context);
        }
        // The Area was deleted or stopped being an Industrial Area: remove whatever is still applied.
        public void Release(Entity area, ulong areaStableId, in AreaPolicyContext context)
        {
            foreach (var adapter in adapters.Values)
            {
                var key = (areaStableId, adapter.PolicyId);
                if (applied.Contains(key)) Remove(adapter, area, key, context);
            }
        }
        // A new load: the previous world and everything applied to it are gone, so nothing is removed.
        public void Reset() { applied.Clear(); failed.Clear(); reported.Clear(); }
        private void Apply(IIndustrialPolicyAdapter adapter, Entity area, (ulong Area, int Policy) key, in AreaPolicyContext context)
        {
            try { adapter.Apply(area, context); applied.Add(key); }
            catch (Exception error)
            {
                failed.Add(key);
                Report(error, key.Area, key.Policy, "apply");
                // Clean up whatever the failed apply already changed.
                try { adapter.Remove(area, context); } catch (Exception) { }
            }
        }
        private void Remove(IIndustrialPolicyAdapter adapter, Entity area, (ulong Area, int Policy) key, in AreaPolicyContext context)
        {
            applied.Remove(key);
            try { adapter.Remove(area, context); }
            catch (Exception error) { failed.Add(key); Report(error, key.Area, key.Policy, "remove"); }
        }
        private void Report(Exception error, ulong areaStableId, int policyId, string failureClass)
        {
            string key = areaStableId + ":" + policyId + ":" + failureClass;
            if (reported.Add(key)) report?.Invoke(error, key);
        }
    }
}
