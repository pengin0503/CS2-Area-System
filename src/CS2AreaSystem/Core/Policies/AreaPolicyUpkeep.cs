using System;
namespace CS2AreaSystem.Core.Policies
{
    // Daily upkeep = Base + PerHectare * hectares + PerEligibleFacility * facilities + PerActivityUnit * activity.
    // Base is always positive: no effective policy is free.
    public readonly struct AreaPolicyUpkeep
    {
        public double BaseDailyUpkeep { get; }
        public double PerHectare { get; }
        public double PerEligibleFacility { get; }
        public double PerActivityUnit { get; }
        public string ActivityBasis { get; }
        public AreaPolicyUpkeep(double baseDailyUpkeep, double perHectare, double perEligibleFacility, double perActivityUnit, string activityBasis)
        {
            BaseDailyUpkeep = baseDailyUpkeep;
            PerHectare = perHectare;
            PerEligibleFacility = perEligibleFacility;
            PerActivityUnit = perActivityUnit;
            ActivityBasis = activityBasis;
        }
        internal void Validate()
        {
            if (!(BaseDailyUpkeep > 0) || double.IsInfinity(BaseDailyUpkeep)) throw new ArgumentOutOfRangeException(nameof(BaseDailyUpkeep), "Every policy needs positive finite base upkeep.");
            RequireCoefficient(PerHectare, nameof(PerHectare));
            RequireCoefficient(PerEligibleFacility, nameof(PerEligibleFacility));
            RequireCoefficient(PerActivityUnit, nameof(PerActivityUnit));
        }
        private static void RequireCoefficient(double value, string name)
        {
            if (!(value >= 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name, "Upkeep coefficients must be non-negative and finite.");
        }
    }
}
