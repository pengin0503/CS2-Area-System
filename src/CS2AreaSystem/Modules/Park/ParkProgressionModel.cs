using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Park
{
    public struct ParkProgressInputs
    {
        public float Quality, RecentSatisfaction, Maintenance, Variety, Landscape;
        public long LifetimeVisitors, LifetimeTouristVisitors;
        public uint HoldFrames;
    }
    public sealed class ParkPromotionTarget
    {
        public long Experience, Visitors;
        public float Quality;
        public float? MinVariety, MinLandscape, MinSatisfaction, MinMaintenance;
        public int PrestigeRequired;
    }
    public struct ParkPromotionCheck
    {
        public string Key;
        public double Current, Required;
        public bool Met, Prestige;
    }
    public sealed class ParkPromotionResult
    {
        public int NextLevel;
        public bool CanPromote, RequirementsMet, QualityRequirementsMet;
        public ParkPromotionTarget Target;
        public IReadOnlyList<ParkPromotionCheck> Checklist = Array.Empty<ParkPromotionCheck>();
    }
    // Five persistent levels, no demotion (design §17). Targets are initial balancing values.
    public static class ParkProgressionModel
    {
        public const int MinLevel = 1, MaxLevel = 5;
        public const uint FramesPerDay = 262144;
        // Promotion quality thresholds must hold for about one in-game day.
        public const uint QualityHoldFrames = FramesPerDay;
        public const int PassiveXpDailyCap = 40;
        public const float PassiveXpMinQuality = 60f;
        public const float LandmarkLandscape = 80f, LandmarkVariety = 75f;
        public const long LandmarkTouristVisitors = 5_000;
        private static readonly ParkPromotionTarget[] targets = {
            new ParkPromotionTarget { Experience = 1_000, Quality = 40, Visitors = 250 },
            new ParkPromotionTarget { Experience = 4_000, Quality = 55, Visitors = 1_500, MinVariety = 45, MinLandscape = 40 },
            new ParkPromotionTarget { Experience = 12_000, Quality = 70, Visitors = 7_500, MinSatisfaction = 65, MinMaintenance = 60 },
            new ParkPromotionTarget { Experience = 30_000, Quality = 82, Visitors = 25_000, PrestigeRequired = 2 }
        };
        public static int NormalizeLevel(int level) => Math.Min(MaxLevel, Math.Max(MinLevel, level));
        public static ParkPromotionTarget GetTarget(int level) => level >= MinLevel && level < MaxLevel ? targets[level - 1] : null;
        public static ParkPromotionResult EvaluatePromotion(int level, long experience, in ParkProgressInputs inputs)
        {
            level = NormalizeLevel(level);
            var target = GetTarget(level);
            var result = new ParkPromotionResult { NextLevel = level, Target = target };
            if (target == null) return result;
            var checks = new List<ParkPromotionCheck>();
            bool quality = true, counts = true;
            void Check(string key, double current, double required, bool isQuality)
            {
                bool met = current >= required;
                checks.Add(new ParkPromotionCheck { Key = key, Current = current, Required = required, Met = met });
                if (isQuality) quality &= met; else counts &= met;
            }
            Check("Experience", experience, target.Experience, false);
            Check("Quality", inputs.Quality, target.Quality, true);
            Check("Visitors", inputs.LifetimeVisitors, target.Visitors, false);
            if (target.MinVariety.HasValue) Check("Variety", inputs.Variety, target.MinVariety.Value, true);
            if (target.MinLandscape.HasValue) Check("Landscape", inputs.Landscape, target.MinLandscape.Value, true);
            if (target.MinSatisfaction.HasValue) Check("Satisfaction", inputs.RecentSatisfaction, target.MinSatisfaction.Value, true);
            if (target.MinMaintenance.HasValue) Check("Maintenance", inputs.Maintenance, target.MinMaintenance.Value, true);
            if (target.PrestigeRequired > 0)
            {
                // Any two objectives keep several Landmark styles viable (designed local park or tourism-heavy park).
                var prestige = new[] {
                    new ParkPromotionCheck { Key = "PrestigeLandscape", Current = inputs.Landscape, Required = LandmarkLandscape, Prestige = true },
                    new ParkPromotionCheck { Key = "PrestigeVariety", Current = inputs.Variety, Required = LandmarkVariety, Prestige = true },
                    new ParkPromotionCheck { Key = "PrestigeTourists", Current = inputs.LifetimeTouristVisitors, Required = LandmarkTouristVisitors, Prestige = true } };
                int met = 0;
                for (int i = 0; i < prestige.Length; i++) { prestige[i].Met = prestige[i].Current >= prestige[i].Required; if (prestige[i].Met) met++; checks.Add(prestige[i]); }
                quality &= met >= target.PrestigeRequired;
            }
            bool held = inputs.HoldFrames >= QualityHoldFrames;
            checks.Add(new ParkPromotionCheck { Key = "Hold", Current = inputs.HoldFrames, Required = QualityHoldFrames, Met = held });
            result.Checklist = checks;
            result.QualityRequirementsMet = quality;
            result.RequirementsMet = quality && counts;
            result.CanPromote = result.RequirementsMet && held;
            result.NextLevel = result.CanPromote ? level + 1 : level;
            return result;
        }
        public static uint UpdateHold(uint hold, bool qualityRequirementsMet, uint elapsedFrames) =>
            !qualityRequirementsMet ? 0u : (uint)Math.Min(uint.MaxValue, (ulong)hold + elapsedFrames);
        public static int ApplyPromotion(int level, bool canPromote)
        {
            level = NormalizeLevel(level);
            return canPromote ? Math.Min(MaxLevel, level + 1) : level;
        }
        // Small satisfaction-adjusted reward per completed visit; low-satisfaction visits still teach a little.
        public static long ComputeVisitXp(double satisfaction)
        {
            double s = double.IsNaN(satisfaction) ? 0 : Math.Min(100, Math.Max(0, satisfaction)) / 100.0;
            return (long)Math.Round(2 + 8 * s * s);
        }
        // Hourly reward for sustaining strong Quality while genuinely receiving visitors, capped per in-game day.
        public static int ComputePassiveXp(float quality, long recentVisitors, bool active, int grantedToday)
        {
            if (!active || recentVisitors <= 0 || float.IsNaN(quality) || quality < PassiveXpMinQuality) return 0;
            int amount = Math.Max(1, (int)Math.Round((quality - PassiveXpMinQuality) / 8f));
            return Math.Max(0, Math.Min(amount, PassiveXpDailyCap - Math.Max(0, grantedToday)));
        }
    }
}
