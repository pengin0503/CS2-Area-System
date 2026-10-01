using System.Collections.Generic;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    // Optional module contract: a module publishes its own policy definitions and says whether each policy's effect
    // can run for an Area. Core never hard-codes module policies or their effects.
    public interface IAreaPolicyProvider
    {
        IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions { get; }
        // false keeps the policy visible but neither effective nor billable; reasonKey is a localization key.
        bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey);
    }
}
