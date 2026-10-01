namespace CS2AreaSystem.Core.Policies
{
    // Explicit inputs of the daily upkeep formula (design §13). Core supplies AreaSquareMeters from the Area geometry;
    // the owning module supplies only stable billing counts it guarantees.
    public struct AreaPolicyUpkeepInputs
    {
        public double AreaSquareMeters;
        public int EligibleFacilityCount;
        public double ActivityUnits;
    }
}
