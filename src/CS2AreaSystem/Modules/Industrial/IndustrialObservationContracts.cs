using System;
using Game.Common;
using Game.Simulation;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Independent capability flags; one facility may hold several. SupportingWorkplace is descriptive only: it marks
    // storage/cargo jobs inside an otherwise eligible facility and never makes a building eligible on its own.
    [Flags]
    public enum IndustrialFacilityRole { None = 0, Extractor = 1, Processor = 2, Storage = 4, Cargo = 8, SupportingWorkplace = 16 }
    // Semantic facts about one member building used by classification and HQ policy. Only component/prefab facts:
    // no names, no raw ECS objects. Static building/prefab capability survives tenant vacancy.
    public struct IndustrialFacilityTraits
    {
        public bool IsBuilding;
        public Entity Prefab;
        public bool HasCommercialProperty;
        // Building/prefab capability (Game.Buildings.*Property components and BuildingPropertyData).
        public bool HasExtractorProperty, ManufacturesGoods, HasStorageProperty, HasOfficeProperty;
        // Cargo station building or prefab; extractor facility (extractor upgrade/sub-building).
        public bool HasCargoStation, HasExtractorFacility;
        // Current tenants (Renter -> company components).
        public bool HasExtractorCompany, HasProcessorCompany, HasStorageCompany, HasOfficeCompany;
        // The building itself or an attributable tenant offers workplaces (WorkProvider).
        public bool HasWorkplaces;
    }
    // Availability of one flow channel at one facility for one sample.
    public enum IndustrialFlowState : byte { Unavailable, NotApplicable, Healthy, Affected }
    // One producer's sampled output (units/day, same resource units for actual and capacity).
    public struct IndustrialResourceActivity
    {
        public ulong Resource;
        public double ActualPerDay, CapacityPerDay;
    }
    // Normalized observation of one member building (all its attributable industrial tenants). Optional values are
    // null when the source is missing or invalid; never a sentinel zero.
    public struct IndustrialEntityObservation
    {
        public Entity Facility;
        public IndustrialFacilityRole Roles;
        // Producer capability (extractor or processor) and confirmed current production (> 0 actual output).
        public bool IsProducer, IsOperatingProducer;
        // Attributable extractor/processor companies (0 for a vacant producer building).
        public int ProducerTenants;
        // Facility is physically usable (not abandoned/destroyed/inactive); known for every read building.
        public bool Available;
        // Actual / attainable production in percent (raw, may exceed 100); null when unproven for this facility.
        public double? ProductiveActivity;
        public IndustrialResourceActivity[] Resources;
        // Vanilla recipes of the producer tenants (Wave A4); empty when none has valid process data.
        public IndustrialProcessActivity[] Processes;
        public double? StoredAmount, StorageCapacity;
        public IndustrialFlowState Input, Output;
        public bool? CargoOperational;
        // Valid jobs/employees of attributable industrial tenants (or the facility itself), counted once.
        public int? Jobs, Employees;
        // Vacant jobs by required education level (Employee.m_Level), when jobs are known.
        public int[] VacanciesByLevel;
        // Building efficiency in percent (raw, may exceed 100) and per-company viability 0..100.
        public double? Efficiency, Profitability;
        public int? ProfitIndex;
        // Input-starved facilities whose vanilla LackResources factor is 0 at the same sample.
        public bool LackResourcesFactor;
    }
    // Conversions from proven vanilla units (see docs/verification/industrial-observation-contracts.md).
    public static class IndustrialSourceNormalization
    {
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        // BuildingUtils.GetEfficiency multiplier, 1.0 = 100%. Raw value is kept (bonuses exceed 100).
        public static double? EfficiencyPercent(float multiplier) => Finite(multiplier) && multiplier >= 0 ? multiplier * 100.0 : (double?)null;
        // ProfitabilitySection.OnProcess (one company): profit = round(m_Profitability * 2 - 255), range -255..255.
        public static int ProfitIndex(byte raw) => (int)Math.Round(raw * 2f - 255f);
        // Piecewise-linear 0..100 with the vanilla CompanyUIUtils.GetProfitabilityKey tier thresholds as band edges:
        // Bankrupt 0-20, LosingMoney 20-40, BreakingEven 40-60, GettingBy 60-80, Profitable 80-100.
        private static readonly double[] ProfitAnchors = { -255, -182, -64, 32, 128, 255 };
        public static double ProfitViability(int profitIndex)
        {
            double x = Math.Max(-255, Math.Min(255, profitIndex));
            for (int i = 1; i < ProfitAnchors.Length; i++)
                if (x <= ProfitAnchors[i])
                    return 20.0 * (i - 1) + 20.0 * (x - ProfitAnchors[i - 1]) / (ProfitAnchors[i] - ProfitAnchors[i - 1]);
            return 100;
        }
        // Actual production rate / attainable production rate (both units/day of the same resource).
        public static double? ActivityPercent(double actualPerDay, double capacityPerDay) =>
            Finite(actualPerDay) && Finite(capacityPerDay) && actualPerDay >= 0 && capacityPerDay > 0 ? 100.0 * actualPerDay / capacityPerDay : (double?)null;
    }
    // Game calendar from TimeData (TimeSystem.GetDay): day = floor((frame - firstFrame) / ticksPerDay + timeOffset).
    // Hours are fractional game hours since the calendar origin; never a hard-coded frames-per-hour cadence.
    public readonly struct IndustrialGameClock
    {
        public uint FirstFrame { get; }
        public double TimeOffset { get; }
        public double TicksPerDay { get; }
        public IndustrialGameClock(uint firstFrame, double timeOffset, double ticksPerDay) { FirstFrame = firstFrame; TimeOffset = timeOffset; TicksPerDay = ticksPerDay; }
        public static IndustrialGameClock From(TimeData data) => new IndustrialGameClock(data.m_FirstFrame, data.TimeOffset, TimeSystem.kTicksPerDay);
        public bool IsValid => TicksPerDay > 0;
        public double GameHours(uint frame) => ((double)unchecked(frame - FirstFrame) / TicksPerDay + TimeOffset) * 24.0;
        public uint Day(uint frame) => (uint)Math.Max(0, Math.Floor(GameHours(frame) / 24.0));
        public long HourBucket(uint frame) => (long)Math.Floor(GameHours(frame));
    }
}
