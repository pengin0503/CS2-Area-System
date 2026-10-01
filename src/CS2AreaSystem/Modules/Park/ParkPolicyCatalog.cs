using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Policies;
namespace CS2AreaSystem.Modules.Park
{
    // Park-owned policy definitions (design §11). Upkeep coefficients are the v1 tuning table of the implementation plan.
    public static class ParkPolicyCatalog
    {
        public static readonly IReadOnlyList<AreaPolicyDefinition> Definitions = new[] {
            Define(AreaPolicyIds.EnhancedMaintenance, "EnhancedMaintenance", "ServicesUpgrade", 1, 300, 20, 25, null, AreaPolicyEffectClass.AreaInternal),
            Define(AreaPolicyIds.NightEvents, "NightEvents", "Entertainment", 2, 400, 15, 20, ActivityGroup, AreaPolicyEffectClass.VanillaSimulation),
            Define(AreaPolicyIds.QuietOperations, "QuietOperations", "NoisePollution", 2, 200, 10, 10, ActivityGroup, AreaPolicyEffectClass.VanillaSimulation),
            Define(AreaPolicyIds.LandscapeProtection, "LandscapeProtection", "Landscaping", 3, 250, 30, 0, null, AreaPolicyEffectClass.AreaInternal),
            Define(AreaPolicyIds.PremiumServices, "PremiumServices", "ServiceFees", 4, 350, 0, 20, null, AreaPolicyEffectClass.AreaInternal),
            Define(AreaPolicyIds.MajorEventAttraction, "MajorEventAttraction", "Attractions", 5, 1000, 30, 50, null, AreaPolicyEffectClass.VanillaSimulation),
        };
        public const string ActivityGroup = "park.activity";
        // Enhanced Maintenance, Landscape Protection and Premium Services change Park model inputs (ParkPolicyEffects).
        // Night Events, Quiet Operations and Major Event Attraction have no verified safe 1.6.2f1 path
        // (docs/verification/park-policy-effect-contracts.md): they stay visible but unavailable and non-billable.
        public static bool IsEffectAvailable(int policyId, out string reasonKey)
        {
            switch (policyId)
            {
                case AreaPolicyIds.EnhancedMaintenance:
                case AreaPolicyIds.LandscapeProtection:
                case AreaPolicyIds.PremiumServices:
                    reasonKey = null;
                    return true;
                default:
                    reasonKey = AreaPolicyReasons.EffectUnavailable;
                    return false;
            }
        }
        // Per-facility upkeep basis: the Park's relevant managed facilities (the same count Park management uses).
        // null until the first facility scan after load, so billing falls back to base and per-hectare upkeep.
        public static int? EligibleFacilities(ParkFacilitySnapshot facilities) => facilities?.RelevantFacilities;
        private static AreaPolicyDefinition Define(int id, string name, string icon, int level, double baseDaily, double perHectare,
            double perFacility, string conflictGroup, AreaPolicyEffectClass effectClass) =>
            new AreaPolicyDefinition(id, "Policy.TITLE[CS2AS.Policy." + name + "]", "Policy.DESCRIPTION[CS2AS.Policy." + name + "]",
                "Media/Game/Icons/" + icon + ".svg", new[] { (int)AreaTypeId.Park }, level,
                new AreaPolicyUpkeep(baseDaily, perHectare, perFacility, 0, null), conflictGroup, effectClass, "park." + name);
    }
}
