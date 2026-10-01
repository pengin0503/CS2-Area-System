using System;
using System.Collections.Generic;
using System.Linq;
namespace CS2AreaSystem.Core.Policies
{
    public static class AreaPolicyModel
    {
        // Pure toggle validation. Turning a policy OFF is allowed even below its Level so a configured policy can always
        // be cleared; turning one ON disables every other desired policy of this Area type sharing its conflict group.
        // Saved IDs without a definition are inert and never part of a conflict.
        public static AreaPolicyTransition EvaluateToggle(AreaPolicyRegistry registry, int areaTypeId, int level,
            IReadOnlyCollection<int> desiredEnabledIds, int policyId, bool enable)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (desiredEnabledIds == null) throw new ArgumentNullException(nameof(desiredEnabledIds));
            if (!registry.TryGet(policyId, out var definition)) return AreaPolicyTransition.Reject(policyId, enable, AreaPolicyReasons.Unknown);
            if (!definition.Supports(areaTypeId)) return AreaPolicyTransition.Reject(policyId, enable, AreaPolicyReasons.UnsupportedAreaType);
            if (!enable) return AreaPolicyTransition.Accept(policyId, false, null);
            if (level < definition.MinLevel) return AreaPolicyTransition.Reject(policyId, true, AreaPolicyReasons.Locked);
            if (definition.ConflictGroup == null) return AreaPolicyTransition.Accept(policyId, true, null);
            var disable = desiredEnabledIds
                .Where(id => id != policyId && registry.TryGet(id, out var other) && other.Supports(areaTypeId) &&
                             string.Equals(other.ConflictGroup, definition.ConflictGroup, StringComparison.Ordinal))
                .Distinct().OrderBy(id => id).ToArray();
            return AreaPolicyTransition.Accept(policyId, true, disable);
        }
    }
}
