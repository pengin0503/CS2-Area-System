using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Policies
{
    public static class AreaPolicyReasons
    {
        public const string Unknown = "CS2AS.Policy.Reason.Unknown";
        public const string UnsupportedAreaType = "CS2AS.Policy.Reason.UnsupportedAreaType";
        public const string Locked = "CS2AS.Policy.Reason.Locked";
        public const string EffectUnavailable = "CS2AS.Policy.Reason.EffectUnavailable";
        public const string Suspended = "CS2AS.Policy.Reason.Suspended";
        public const string Conflict = "CS2AS.Policy.Reason.Conflict";
        public const string AreaUnavailable = "CS2AS.Policy.Reason.AreaUnavailable";
    }
    // One atomic desired-state change: the conflicting policies listed here are switched OFF in the same write that
    // switches PolicyId to DesiredEnabled, so no update ever sees both.
    public sealed class AreaPolicyTransition
    {
        private static readonly int[] None = new int[0];
        public bool Accepted { get; }
        public string ReasonKey { get; }
        public int PolicyId { get; }
        public bool DesiredEnabled { get; }
        public IReadOnlyList<int> DisablePolicyIds { get; }
        private AreaPolicyTransition(bool accepted, string reasonKey, int policyId, bool desiredEnabled, int[] disable)
        {
            Accepted = accepted;
            ReasonKey = reasonKey;
            PolicyId = policyId;
            DesiredEnabled = desiredEnabled;
            DisablePolicyIds = Array.AsReadOnly(disable ?? None);
        }
        public static AreaPolicyTransition Accept(int policyId, bool desiredEnabled, int[] disablePolicyIds) =>
            new AreaPolicyTransition(true, null, policyId, desiredEnabled, disablePolicyIds);
        public static AreaPolicyTransition Reject(int policyId, bool desiredEnabled, string reasonKey) =>
            new AreaPolicyTransition(false, reasonKey ?? throw new ArgumentNullException(nameof(reasonKey)), policyId, desiredEnabled, None);
    }
}
