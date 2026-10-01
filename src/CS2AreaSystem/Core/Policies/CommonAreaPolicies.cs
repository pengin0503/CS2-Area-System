using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Core.Policies
{
    // Policies owned by Area Core. Focused Management only scales XP the owning module already awards, so its effect
    // is always available wherever it is defined (Park and Industrial in this release).
    public static class CommonAreaPolicies
    {
        public static readonly IReadOnlyList<AreaPolicyDefinition> Definitions = new[] {
            new AreaPolicyDefinition(AreaPolicyIds.FocusedManagement, "Policy.TITLE[CS2AS.Policy.FocusedManagement]",
                "Policy.DESCRIPTION[CS2AS.Policy.FocusedManagement]", "Media/Game/Icons/Administration.svg",
                new[] { (int)AreaTypeId.Park, (int)AreaTypeId.Industrial }, 2, new AreaPolicyUpkeep(250, 0, 0, 0, null), null,
                AreaPolicyEffectClass.AreaInternal, "common.focused-management")
        };
        public static IAreaPolicyProvider Provider { get; } = new CommonProvider();
        private sealed class CommonProvider : IAreaPolicyProvider
        {
            public IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions => Definitions;
            public bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey)
            {
                reasonKey = null;
                return policyId == AreaPolicyIds.FocusedManagement;
            }
        }
    }
}
