using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    // Optional module contract: stable, module-owned billing inputs for one of its policies. Asked only when the policy
    // has a non-zero per-facility or per-activity coefficient. false (no data yet) bills base and per-hectare upkeep only.
    public interface IAreaPolicyUpkeepProvider
    {
        bool TryGetPolicyUpkeepInputs(Entity area, int policyId, in AreaPolicyContext context, out AreaPolicyUpkeepInputs inputs);
    }
}
