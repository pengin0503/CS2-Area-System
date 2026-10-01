using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Policies;
namespace CS2AreaSystem.Modules.Industrial
{
    // Industrial-owned policy definitions (design §12). Every effect targets the vanilla simulation and must go through
    // a verified adapter; Industrial scores are never written by a policy. Upkeep is the plan's v1 tuning table.
    public static class IndustrialPolicyCatalog
    {
        public const string EnvironmentProductionGroup = "industrial.environment-production";
        public const string HoursGroup = "industrial.hours";
        public static readonly IReadOnlyList<AreaPolicyDefinition> Definitions = new[] {
            Define(AreaPolicyIds.EnhancedSafetyAudits, "EnhancedSafetyAudits", "DisasterControl", 1, 400, 20, null),
            Define(AreaPolicyIds.EnvironmentalRegulation, "EnvironmentalRegulation", "Pollution", 2, 500, 30, EnvironmentProductionGroup),
            Define(AreaPolicyIds.ProductionPriority, "ProductionPriority", "Production", 2, 600, 30, EnvironmentProductionGroup),
            Define(AreaPolicyIds.LogisticsPriority, "LogisticsPriority", "CargoCar", 3, 600, 40, null),
            Define(AreaPolicyIds.TwentyFourHourOperations, "TwentyFourHourOperations", "Calendar", 4, 800, 50, HoursGroup),
            Define(AreaPolicyIds.RestrictedOperatingHours, "RestrictedOperatingHours", "Soundbarrier", 4, 250, 10, HoursGroup),
            Define(AreaPolicyIds.AdvancedAutomation, "AdvancedAutomation", "Research", 5, 1200, 75, null),
        };
        // No Industrial vanilla adapter is verified yet: all policies stay visible but unavailable and non-billable.
        public static bool IsEffectAvailable(int policyId, out string reasonKey)
        {
            reasonKey = AreaPolicyReasons.EffectUnavailable;
            return false;
        }
        // Per-facility upkeep basis: classified member facilities from the latest observation cycle (roles, not
        // throughput or jobs). Producer policies count producer facilities; site-wide policies count every eligible
        // industrial facility. null until the first observation after load, so billing falls back to base upkeep.
        public static int? EligibleFacilities(int policyId, IndustrialObservationResult observed)
        {
            if (observed == null) return null;
            switch (policyId)
            {
                case AreaPolicyIds.EnvironmentalRegulation:
                case AreaPolicyIds.ProductionPriority:
                case AreaPolicyIds.AdvancedAutomation:
                    return System.Math.Max(0, observed.ProducerFacilities);
                case AreaPolicyIds.EnhancedSafetyAudits:
                case AreaPolicyIds.LogisticsPriority:
                case AreaPolicyIds.TwentyFourHourOperations:
                case AreaPolicyIds.RestrictedOperatingHours:
                    return System.Math.Max(0, observed.Members - observed.IgnoredMembers);
                default:
                    return null;
            }
        }
        private static AreaPolicyDefinition Define(int id, string name, string icon, int level, double baseDaily, double perFacility, string conflictGroup) =>
            new AreaPolicyDefinition(id, "Policy.TITLE[CS2AS.Policy." + name + "]", "Policy.DESCRIPTION[CS2AS.Policy." + name + "]",
                "Media/Game/Icons/" + icon + ".svg", new[] { (int)AreaTypeId.Industrial }, level,
                new AreaPolicyUpkeep(baseDaily, 0, perFacility, 0, null), conflictGroup, AreaPolicyEffectClass.VanillaSimulation, "industrial." + name);
    }
}
