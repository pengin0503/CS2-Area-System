namespace CS2AreaSystem.Modules.Park
{
    // Phase 2 C5 internal Park events. The catalog has exactly one event; ids are stable save data.
    public static class ParkEventCatalog
    {
        public const int ParkFestival = 1;
        public static readonly int[] Ids = { ParkFestival };
        // A full in-game day of simulation frames from positive settlement (not merely until the next midnight).
        public const uint DurationFrames = ParkFinanceModel.FramesPerDay;
        // Requested expense: this share of the Park's current daily management cost.
        public const double CostShareOfDailyManagement = 0.5;
        // Park-internal effects while active (never vanilla attractiveness or destination choice).
        public const float FestivalCapacityShare = 0.85f, FestivalSatisfactionBonus = 5f;
        public const double FestivalVisitXpMultiplier = 1.25;
        public static bool IsKnown(int id) => id == ParkFestival;
    }
}
