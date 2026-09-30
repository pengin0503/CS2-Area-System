using System;
using System.Collections.Generic;
using Game.Agents;
namespace CS2AreaSystem.Modules.Park
{
    public struct ParkMetric
    {
        public float Value;
        public bool Available;
        public static ParkMetric Of(float value) => new ParkMetric { Value = value, Available = true };
        public static ParkMetric Unavailable => new ParkMetric();
    }
    public struct ParkQualityInputs
    {
        public ParkMetric Satisfaction, Attractiveness, Accessibility, Variety, Maintenance, Comfort;
    }
    public struct ParkQualityResult
    {
        public float Quality;
        // Share of the Quality weight backed by available sources (1 = all known).
        public float Confidence;
        public bool Degraded;
    }
    // Comfortable-capacity inputs. Park Level is deliberately absent: Level never manufactures physical capacity.
    public struct ParkCapacityInputs
    {
        public float AreaSquareMeters, PathLength, LeisureEfficiencySum;
        public int AmenityCount;
    }
    // Six-dimensional Park Quality (design §10–16). All tuning constants live here.
    public static class ParkQualityModel
    {
        public const float SatisfactionWeight = 0.25f, AttractivenessWeight = 0.20f, AccessibilityWeight = 0.15f, VarietyWeight = 0.15f, MaintenanceWeight = 0.15f, ComfortWeight = 0.10f;
        public const float NeutralMetric = 50f;
        public const float MinComfort = 10f;
        private static float Clamp(float v) => float.IsNaN(v) ? 0f : v < 0 ? 0 : v > 100 ? 100 : v;
        public static ParkQualityResult ComputeQuality(in ParkQualityInputs inputs)
        {
            float quality = 0f, confidence = 0f;
            void Add(ParkMetric metric, float weight)
            {
                // A temporarily unavailable source contributes a conservative neutral value; the other weights are not
                // renormalized, so dropping a difficult metric can never raise Quality (design §16.1).
                if (metric.Available) { quality += weight * Clamp(metric.Value); confidence += weight; }
                else quality += weight * NeutralMetric;
            }
            Add(inputs.Satisfaction, SatisfactionWeight); Add(inputs.Attractiveness, AttractivenessWeight); Add(inputs.Accessibility, AccessibilityWeight);
            Add(inputs.Variety, VarietyWeight); Add(inputs.Maintenance, MaintenanceWeight); Add(inputs.Comfort, ComfortWeight);
            confidence = (float)Math.Round(confidence, 4);
            return new ParkQualityResult { Quality = Clamp(quality), Confidence = confidence, Degraded = confidence < 0.9999f };
        }
        // <60% none, 60–100% mild, 100–150% meaningful, beyond 150% severe but bounded.
        public static float ComputeComfort(double occupancy, double comfortableCapacity)
        {
            if (comfortableCapacity <= 0) return occupancy <= 0 ? 100f : MinComfort;
            double r = Math.Max(0, occupancy) / comfortableCapacity;
            double value;
            if (r <= 0.6) value = 100;
            else if (r <= 1.0) value = 100 - 15 * (r - 0.6) / 0.4;
            else if (r <= 1.5) value = 85 - 40 * (r - 1.0) / 0.5;
            else value = MinComfort + (45 - MinComfort) * Math.Exp(-(r - 1.5) * 1.5);
            return (float)value;
        }
        public static float ComputeComfortableCapacity(in ParkCapacityInputs inputs)
        {
            double area = Math.Max(8, 30 * Math.Pow(Math.Max(0, inputs.AreaSquareMeters) / 10_000.0, 0.75));
            double paths = 25 * (1 - Math.Exp(-Math.Max(0, inputs.PathLength) / 600.0));
            double facilities = 4 * Math.Sqrt(Math.Max(0, inputs.LeisureEfficiencySum));
            double amenities = 15 * (1 - Math.Exp(-Math.Max(0, inputs.AmenityCount) / 15.0));
            return (float)(area + paths + facilities + amenities);
        }
        // 40% baseline entrance, 20% additional/distributed entrances, 20% nearby transit, 20% internal paths (design §11).
        public static float ComputeAccessibility(int entrances, float spread, int transitStops, float internalPathProvision)
        {
            if (entrances <= 0) return 0f;
            double extra = entrances > 1 ? (1 - Math.Exp(-(entrances - 1) / 1.5)) * (0.4 + 0.6 * Math.Min(1, Math.Max(0, spread))) : 0;
            double transit = 1 - Math.Exp(-Math.Max(0, transitStops) / 1.5);
            double paths = Math.Min(1, Math.Max(0, internalPathProvision));
            return Clamp((float)(40 + 20 * extra + 20 * transit + 20 * paths));
        }
        // 0 for one or stacked entrances, 1 when entrances are about one park-width apart.
        public static float ComputeEntranceSpread(IReadOnlyList<(float X, float Z)> positions, float areaSquareMeters)
        {
            if (positions == null || positions.Count < 2 || areaSquareMeters <= 0) return 0f;
            double max = 0;
            for (int i = 0; i < positions.Count; i++)
                for (int j = i + 1; j < positions.Count; j++)
                {
                    double dx = positions[i].X - positions[j].X, dz = positions[i].Z - positions[j].Z;
                    max = Math.Max(max, Math.Sqrt(dx * dx + dz * dz));
                }
            return (float)Math.Min(1, max / Math.Sqrt(areaSquareMeters));
        }
        // ~60% real facility attractiveness (area-normalized, diminishing) + ~40% landscape (design §12).
        public static float ComputeAttractiveness(float facilityAttractivenessSum, float areaSquareMeters, float landscape)
        {
            double target = 30 * Math.Max(0.5, Math.Sqrt(Math.Max(0, areaSquareMeters) / 10_000.0));
            double facility = 100 * (1 - Math.Exp(-Math.Max(0, facilityAttractivenessSum) / target));
            return Clamp((float)(0.6 * facility + 0.4 * Clamp(landscape)));
        }
        // Travel providers are mainly an access concept, so they add only partial facility diversity (design §13).
        public static float FacilityCategoryWeight(LeisureType type) => type == LeisureType.Travel ? 0.3f : 1f;
        // ~70% facility/leisure-category breadth + ~30% landscape-category breadth.
        public static float ComputeVariety(float weightedFacilityCategories, int landscapeCategories)
        {
            double facility = 1 - Math.Exp(-Math.Max(0, weightedFacilityCategories) / 2.2);
            double landscape = Math.Min(1, Math.Max(0, landscapeCategories) / 5.0);
            return Clamp((float)(70 * facility + 30 * landscape));
        }
        // Management coverage = funding relative to need (1 = 100% budget). Overfunding has diminishing returns.
        public static float CoverageScore(float coverage)
        {
            if (float.IsNaN(coverage) || coverage <= 0) return 0f;
            double value = coverage >= 1 ? 90 + 10 * (1 - Math.Exp(-(coverage - 1) * 3)) : 90 * Math.Pow(coverage, 1.3);
            return Clamp((float)value);
        }
        // Real vanilla Park building condition (when the Park has any) plus area management coverage (design §14).
        public static float ComputeMaintenance(ParkMetric vanillaParkCondition, float coverage)
        {
            float managed = CoverageScore(coverage);
            return vanillaParkCondition.Available ? Clamp(0.5f * Clamp(vanillaParkCondition.Value) + 0.5f * managed) : managed;
        }
        // Derived export for a future Tourism Area; never applied to vanilla tourism in v1 (design §18).
        public static float ComputeTourismContribution(int level, float quality, float recentTouristVisits, float touristSatisfaction, float attractiveness)
        {
            double volume = 1 - Math.Exp(-Math.Max(0, recentTouristVisits) / 100.0);
            double experience = 0.35 * Clamp(quality) + 0.25 * Clamp(attractiveness) + 0.40 * Clamp(touristSatisfaction);
            double prestige = 1 + 0.25 * (Math.Min(5, Math.Max(1, level)) - 1);
            return (float)(experience * prestige * volume);
        }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    public struct ParkQualityContext
    {
        public float AreaSquareMeters, EntranceSpread, Budget, RecentTouristVisits;
        public int Entrances, ActiveVisitors, Level, AdmissionFee;
        public ParkLandscapeSnapshot Landscape;
        public ParkFacilitySnapshot Facilities;
        public ParkSatisfactionState Satisfaction, TouristSatisfaction;
        // False when some member building's same-kind primary owner is another Park (exclusive stats go there).
        public bool IsPrimaryForAllMembers, Active;
    }
    public sealed class ParkQualitySnapshot
    {
        public ParkMetric Satisfaction, Attractiveness, Accessibility, Variety, Maintenance, Comfort, Landscape;
        public ParkQualityResult Result;
        public float Quality => Result.Quality;
        public float ComfortableCapacity, OccupancyRatio, TourismContribution, ValueForMoney, PathProvision;
        public int EffectiveFee;
        public ParkVisitExperience Experience;
        public System.Collections.Generic.List<string> Warnings = new System.Collections.Generic.List<string>();
        public string NextActionKey;
    }
    // Combines the Park's derived inputs into the six Quality metrics (pure; no ECS access).
    public static class ParkQualityEvaluator
    {
        public const string WarningPrefix = "CS2AS.Park.Warning.", ActionPrefix = "CS2AS.Park.Action.";
        public const float LowMaintenance = 40f, PoorValue = 40f;
        public static ParkQualitySnapshot Evaluate(in ParkQualityContext c)
        {
            var q = new ParkQualitySnapshot();
            var facilities = c.Facilities ?? ParkFacilitySnapshot.Build(System.Array.Empty<ParkFacilityTraits>());
            var landscape = c.Landscape;
            bool hasLandscape = landscape != null && landscape.Available;
            q.Landscape = hasLandscape ? ParkMetric.Of(landscape.Result.Score) : ParkMetric.Unavailable;
            q.PathProvision = hasLandscape ? landscape.Result.Paths / ParkLandscapeModel.PathWeight : 0f;
            q.Satisfaction = ParkMetric.Of(c.Satisfaction.Display);
            // Landscape-dependent metrics stay neutral (unavailable) until the first landscape scan completes.
            q.Attractiveness = hasLandscape ? ParkMetric.Of(ParkQualityModel.ComputeAttractiveness(facilities.AttractivenessSum, c.AreaSquareMeters, landscape.Result.Score)) : ParkMetric.Unavailable;
            q.Variety = hasLandscape ? ParkMetric.Of(ParkQualityModel.ComputeVariety(facilities.WeightedCategories, landscape.Result.CategoriesPresent)) : ParkMetric.Unavailable;
            q.Accessibility = hasLandscape ? ParkMetric.Of(ParkQualityModel.ComputeAccessibility(c.Entrances, c.EntranceSpread, landscape.TransitStops, q.PathProvision)) : ParkMetric.Unavailable;
            q.Maintenance = ParkMetric.Of(ParkQualityModel.ComputeMaintenance(facilities.VanillaCondition, ParkFinanceModel.ClampBudget(c.Budget)));
            q.ComfortableCapacity = ParkQualityModel.ComputeComfortableCapacity(new ParkCapacityInputs {
                AreaSquareMeters = c.AreaSquareMeters, LeisureEfficiencySum = facilities.LeisureEfficiencySum,
                PathLength = hasLandscape ? landscape.Inputs.PathLength : 0f, AmenityCount = hasLandscape ? landscape.Inputs.Amenities.Count : 0 });
            q.OccupancyRatio = q.ComfortableCapacity > 0 ? c.ActiveVisitors / q.ComfortableCapacity : 0f;
            q.Comfort = ParkMetric.Of(ParkQualityModel.ComputeComfort(c.ActiveVisitors, q.ComfortableCapacity));
            q.Result = ParkQualityModel.ComputeQuality(new ParkQualityInputs {
                Satisfaction = q.Satisfaction, Attractiveness = q.Attractiveness, Accessibility = q.Accessibility,
                Variety = q.Variety, Maintenance = q.Maintenance, Comfort = q.Comfort });
            q.EffectiveFee = ParkFinanceModel.EffectiveFee(c.Level, c.AdmissionFee);
            q.ValueForMoney = ParkFinanceModel.ComputeValueForMoney(q.EffectiveFee, q.Quality);
            q.TourismContribution = c.Active ? ParkQualityModel.ComputeTourismContribution(c.Level, q.Quality, c.RecentTouristVisits, c.TouristSatisfaction.Display, Value(q.Attractiveness)) : 0f;
            q.Experience = new ParkVisitExperience {
                Facility = ParkVisitModel.NeutralFacilityExperience, Landscape = Value(q.Landscape), Comfort = q.Comfort.Value,
                Maintenance = q.Maintenance.Value, ValueForMoney = q.ValueForMoney, Accessibility = Value(q.Accessibility) };
            AddWarnings(q, c, facilities, landscape, hasLandscape);
            return q;
        }
        private static float Value(ParkMetric metric) => metric.Available ? metric.Value : ParkQualityModel.NeutralMetric;
        private static void AddWarnings(ParkQualitySnapshot q, in ParkQualityContext c, ParkFacilitySnapshot facilities, ParkLandscapeSnapshot landscape, bool hasLandscape)
        {
            void Warn(string key) => q.Warnings.Add(WarningPrefix + key);
            if (c.Entrances <= 0) Warn("NoEntrance");
            if (!c.IsPrimaryForAllMembers) Warn("NotPrimary");
            if (q.Result.Degraded) Warn("LowConfidence");
            if (facilities.RelevantFacilities == 0) Warn("NoFacilities");
            if (q.OccupancyRatio > 1f) Warn("Crowded");
            if (q.Maintenance.Value < LowMaintenance) Warn("LowMaintenance");
            if (hasLandscape && landscape.Inputs.PathLength < 1f) Warn("NoPaths");
            if (hasLandscape && landscape.Result.Cluttered) Warn("Cluttered");
            if (q.EffectiveFee > 0 && q.ValueForMoney < PoorValue) Warn("PoorValue");
            if (c.Entrances <= 0) { q.NextActionKey = ActionPrefix + "Entrance"; return; }
            // Next recommended action: the weakest available metric.
            string weakest = null; float lowest = float.MaxValue;
            void Consider(string name, ParkMetric metric) { if (metric.Available && metric.Value < lowest) { lowest = metric.Value; weakest = name; } }
            Consider("Attractiveness", q.Attractiveness); Consider("Accessibility", q.Accessibility); Consider("Variety", q.Variety);
            Consider("Maintenance", q.Maintenance); Consider("Comfort", q.Comfort); Consider("Satisfaction", q.Satisfaction);
            q.NextActionKey = ActionPrefix + (weakest ?? "Wait");
        }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    // Which Parks the Quality system evaluates this update: Parks never evaluated yet (new, or just after load) first,
    // then the round-robin rotation with the remaining budget.
    public static class ParkQualitySchedule
    {
        public static List<ulong> Pick(IReadOnlyList<ulong> parks, Func<ulong, bool> evaluated, int cursor, int count, out int nextCursor)
        {
            var picked = new List<ulong>();
            nextCursor = parks.Count == 0 ? 0 : ((cursor % parks.Count) + parks.Count) % parks.Count;
            foreach (var id in parks) { if (picked.Count >= count) break; if (!evaluated(id)) picked.Add(id); }
            int steps = 0;
            while (picked.Count < count && steps < parks.Count)
            {
                var id = parks[(nextCursor + steps) % parks.Count];
                steps++;
                if (!picked.Contains(id)) picked.Add(id);
            }
            if (parks.Count > 0) nextCursor = (nextCursor + steps) % parks.Count;
            return picked;
        }
    }
}
