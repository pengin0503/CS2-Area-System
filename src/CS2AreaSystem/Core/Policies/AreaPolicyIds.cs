namespace CS2AreaSystem.Core.Policies
{
    // Released IDs are saved in Area policy buffers and must never be reassigned. Ranges: common 1000-1999,
    // Park 2000-2999, Industrial 3000-3999; later modules receive their own ranges.
    public static class AreaPolicyIds
    {
        public const int FocusedManagement = 1001;
        public const int EnhancedMaintenance = 2001;
        public const int NightEvents = 2002;
        public const int QuietOperations = 2003;
        public const int LandscapeProtection = 2004;
        public const int PremiumServices = 2005;
        public const int MajorEventAttraction = 2006;
        public const int EnhancedSafetyAudits = 3001;
        public const int EnvironmentalRegulation = 3002;
        public const int ProductionPriority = 3003;
        public const int LogisticsPriority = 3004;
        public const int TwentyFourHourOperations = 3005;
        public const int RestrictedOperatingHours = 3006;
        public const int AdvancedAutomation = 3007;
    }
}
