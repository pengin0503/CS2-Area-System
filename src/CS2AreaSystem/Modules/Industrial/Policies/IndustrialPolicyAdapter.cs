using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial.Policies
{
    // One vanilla effect family of one Industrial policy (design §9.2). An adapter changes only the vanilla state its
    // contract in docs/verification/industrial-policy-effect-contracts.md names, and only for the given Area. It never
    // writes Industrial scores: those change only when IndustrialObservationSystem observes the changed vanilla state.
    public interface IIndustrialPolicyAdapter
    {
        int PolicyId { get; }
        // Whether the effect can run for this Area in this game version (reasonKey explains a false result).
        bool IsSupported(Entity area, in AreaPolicyContext context, out string reasonKey);
        // Called once when the policy becomes effective for the Area, and again after a load or rebuild.
        void Apply(Entity area, in AreaPolicyContext context);
        // Called once when the policy stops being effective, or the Area leaves; restores the vanilla baseline.
        void Remove(Entity area, in AreaPolicyContext context);
    }
    // The verified adapters shipped in this release. None of the 1.6.2f1 candidates is verified (see the contract), so
    // every Industrial policy stays visible but EffectUnavailable and is never billed.
    public static class IndustrialPolicyAdapters
    {
        public static IIndustrialPolicyAdapter[] Create() => Array.Empty<IIndustrialPolicyAdapter>();
        private static readonly HashSet<int> implemented = new HashSet<int>(Array.ConvertAll(Create(), adapter => adapter.PolicyId));
        public static bool IsImplemented(int policyId) => implemented.Contains(policyId);
    }
}
