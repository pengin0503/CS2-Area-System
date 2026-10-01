using System.Collections.Generic;
using CS2AreaSystem.Core.Policies;
namespace CS2AreaSystem.Core.Modules
{
    // Optional module contract: a module publishes its own policy definitions; Core never hard-codes module policies.
    public interface IAreaPolicyProvider
    {
        IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions { get; }
    }
}
