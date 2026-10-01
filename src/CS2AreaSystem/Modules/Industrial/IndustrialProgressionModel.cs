using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Industrial
{
    public struct IndustrialProgressInputs
    {
        public IndustrialMetric Production, Logistics, Employment, Efficiency, Performance;
        // Confirmed operating productive facilities and current employees of the latest coherent observation.
        public int ActiveProductiveFacilities, EmployedWorkers;
        // At least one in-boundary cargo facility currently operating (Level 3 -> 4 route alternative).
        public bool FunctionalCargo;
        // Output classes with rolling activity >= 20% of the dominant output.
        public int MeaningfulOutputs;
        public int StreakDays;
        // Completed, coherent, non-delayed hourly observation.
        public bool Fresh;
    }
    public sealed class IndustrialPromotionTarget
    {
        public long Experience;
        public double Performance;
        public int ActiveFacilities, Employees;
        public double? MinLogistics, MinProduction;
        public bool LogisticsRoute;
        public int PrestigeRequired;
    }
    public struct IndustrialPromotionCheck
    {
        public string Key;
        public double Current, Required;
        public bool Met, Prestige, Available;
    }
    public sealed class IndustrialPromotionResult
    {
        public int NextLevel;
        public bool CanPromote;
        public int PrestigeMet;
        public IndustrialPromotionTarget Target;
        public IReadOnlyList<IndustrialPromotionCheck> Checklist = Array.Empty<IndustrialPromotionCheck>();
    }
    public struct IndustrialXpResult
    {
        public int Hourly, Award;
        public double? OperatingScore;
        // Null when XP was awarded or the hour simply scored zero; otherwise why nothing (or less) was granted.
        public string BlockedReason;
    }
    public struct IndustrialLevelProgress
    {
        public int Level;
        public long Experience;
    }
    public struct IndustrialObservationStamp
    {
        public long HourBucket;
        public bool Active, Fresh;
    }
    public struct IndustrialProgressStep
    {
        public IndustrialXpResult Xp;
        public bool Promoted, Settled;
        public IndustrialPromotionResult Promotion;
    }
    // One validated activity endpoint for the time-weighted daily mean (design §14.1a).
    public struct IndustrialDailyActivityObservation
    {
        public double Hours;
        public long HourBucket;
        public double Activity;
        public ulong Generation;
        // Fresh, coherent, Active observation with validated productive activity.
        public bool Valid;
    }
    // Permanent Industrial progression (design §14). All thresholds live here; the panel only renders the checklist.
    public static class IndustrialProgressionModel
    {
        public const int MinLevel = IndustrialProgressionLevels.MinLevel, MaxLevel = IndustrialProgressionLevels.MaxLevel;
        public const int DailyXpCap = 240, MaxHourlyXp = 10, MinXpInputs = 3;
        public const double ProductionXpWeight = 0.35, EmploymentXpWeight = 0.25, LogisticsXpWeight = 0.20, PerformanceXpWeight = 0.20;
        public const double StreakActivity = 80, StreakHours = 18;
        public const int StreakDays = 7;
        public const double PrestigeLogistics = 80, PrestigeEmployment = 85, PrestigeEfficiency = 80;
        public const int PrestigeOutputs = 3;
        public const double RouteLogistics = 70;
        private static readonly IndustrialPromotionTarget[] targets = {
            new IndustrialPromotionTarget { Experience = 1_000, Performance = 45, ActiveFacilities = 3, Employees = 50 },
            new IndustrialPromotionTarget { Experience = 5_000, Performance = 55, ActiveFacilities = 8, Employees = 200, MinLogistics = 45 },
            new IndustrialPromotionTarget { Experience = 15_000, Performance = 65, ActiveFacilities = 20, Employees = 600, MinProduction = 60, LogisticsRoute = true },
            new IndustrialPromotionTarget { Experience = 35_000, Performance = 75, Employees = 1_500, MinProduction = 70, PrestigeRequired = 2 } };
        public static int NormalizeLevel(int level) => IndustrialProgressionLevels.Normalize(level);
        public static IndustrialPromotionTarget GetTarget(int level) => level >= MinLevel && level < MaxLevel ? targets[level - 1] : null;
        public static IndustrialPromotionResult EvaluatePromotion(int level, long experience, in IndustrialProgressInputs inputs)
        {
            level = NormalizeLevel(level);
            var target = GetTarget(level);
            var result = new IndustrialPromotionResult { NextLevel = level, Target = target };
            if (target == null) return result;
            var checks = new List<IndustrialPromotionCheck>();
            bool all = true; int prestige = 0;
            void Count(string key, double current, double required) { bool met = current >= required; checks.Add(new IndustrialPromotionCheck { Key = key, Current = current, Required = required, Met = met, Available = true }); all &= met; }
            // A missing metric stays visibly unmet: no fallback ever waives a promotion gate.
            void Metric(string key, IndustrialMetric metric, double required, bool isPrestige = false)
            {
                bool met = metric.HasValue && metric.Value >= required;
                checks.Add(new IndustrialPromotionCheck { Key = key, Current = metric.HasValue ? metric.Value : 0, Required = required, Met = met, Available = metric.HasValue, Prestige = isPrestige });
                if (isPrestige) { if (met) prestige++; } else all &= met;
            }
            void Prestige(string key, double current, double required)
            { bool met = current >= required; checks.Add(new IndustrialPromotionCheck { Key = key, Current = current, Required = required, Met = met, Available = true, Prestige = true }); if (met) prestige++; }
            Count("Experience", experience, target.Experience);
            Metric("Performance", inputs.Performance, target.Performance);
            if (target.ActiveFacilities > 0) Count("ActiveFacilities", inputs.ActiveProductiveFacilities, target.ActiveFacilities);
            Count("Employees", inputs.EmployedWorkers, target.Employees);
            if (target.MinLogistics.HasValue) Metric("Logistics", inputs.Logistics, target.MinLogistics.Value);
            if (target.MinProduction.HasValue) Metric("Production", inputs.Production, target.MinProduction.Value);
            if (target.LogisticsRoute)
            {
                bool route = inputs.FunctionalCargo || (inputs.Logistics.HasValue && inputs.Logistics.Value >= RouteLogistics);
                checks.Add(new IndustrialPromotionCheck { Key = "LogisticsRoute", Current = route ? 1 : 0, Required = 1, Met = route, Available = true });
                all &= route;
            }
            if (target.PrestigeRequired > 0)
            {
                Metric("PrestigeLogistics", inputs.Logistics, PrestigeLogistics, true);
                Metric("PrestigeEmployment", inputs.Employment, PrestigeEmployment, true);
                Metric("PrestigeEfficiency", inputs.Efficiency, PrestigeEfficiency, true);
                Prestige("PrestigeOutputs", inputs.MeaningfulOutputs, PrestigeOutputs);
                Prestige("PrestigeStreak", inputs.StreakDays, StreakDays);
                all &= prestige >= target.PrestigeRequired;
            }
            result.Checklist = checks;
            result.PrestigeMet = prestige;
            result.CanPromote = all;
            if (all) result.NextLevel = level + 1;
            return result;
        }
        // operatingScore = Σ available·w / Σ available w over Production .35, Employment .25, Logistics .20,
        // Performance .20; three of four required; hourly = clamp(roundAwayFromZero(score / 10), 0, 10).
        public static IndustrialXpResult ComputeHourlyXp(in IndustrialProgressInputs inputs, bool active, int grantedToday)
        {
            var result = new IndustrialXpResult();
            var parts = new[] { IndustrialScoreMath.Of(inputs.Production, ProductionXpWeight), IndustrialScoreMath.Of(inputs.Employment, EmploymentXpWeight),
                IndustrialScoreMath.Of(inputs.Logistics, LogisticsXpWeight), IndustrialScoreMath.Of(inputs.Performance, PerformanceXpWeight) };
            if (IndustrialScoreMath.TryCombine(parts, MinXpInputs, out var score, out _, out _))
            {
                result.OperatingScore = score;
                result.Hourly = (int)Math.Min(MaxHourlyXp, Math.Max(0, Math.Round(score / 10.0, MidpointRounding.AwayFromZero)));
            }
            if (!active) return Blocked(result, "Suspended");
            if (!inputs.Fresh) return Blocked(result, "Stale");
            if (inputs.ActiveProductiveFacilities < 1) return Blocked(result, "NoOperatingProducer");
            if (!result.OperatingScore.HasValue) return Blocked(result, "InsufficientInputs");
            int remaining = Math.Max(0, DailyXpCap - Math.Max(0, grantedToday));
            result.Award = Math.Min(result.Hourly, remaining);
            if (result.Award < result.Hourly) result.BlockedReason = "DailyCap";
            return result;
        }
        private static IndustrialXpResult Blocked(IndustrialXpResult result, string reason) { result.Award = 0; result.BlockedReason = reason; return result; }
        public static long AddSaturating(long experience, long award) =>
            award <= 0 ? experience : experience > long.MaxValue - award ? long.MaxValue : experience + award;
        public static uint DayOfHour(long hourBucket) => hourBucket <= 0 ? 0u : (uint)(hourBucket / 24);
        // Settles one hourly observation at most once (persisted LastSettledXpHour), then applies at most one promotion.
        public static IndustrialProgressStep Step(ref IndustrialLevelProgress progress, ref IndustrialAreaState state, in IndustrialProgressInputs inputs, in IndustrialObservationStamp stamp)
        {
            var step = new IndustrialProgressStep();
            progress.Level = NormalizeLevel(progress.Level);
            if (stamp.Active) state.LifetimePeakEmployees = Math.Max(state.LifetimePeakEmployees, inputs.EmployedWorkers);
            var current = inputs; current.Fresh = inputs.Fresh && stamp.Fresh;
            if (stamp.HourBucket > state.LastSettledXpHour)
            {
                uint day = DayOfHour(stamp.HourBucket);
                // The cap is per game calendar day and resets only when the day changes.
                if (day != state.XpDay) { state.XpDay = day; state.XpGrantedToday = 0; }
                step.Xp = ComputeHourlyXp(current, stamp.Active, state.XpGrantedToday);
                if (stamp.Active && current.Fresh)
                {
                    // A fresh Active hour is settled once, even when it earned nothing; missing hours are never backfilled.
                    state.LastSettledXpHour = stamp.HourBucket;
                    step.Settled = true;
                    if (step.Xp.Award > 0) { progress.Experience = AddSaturating(progress.Experience, step.Xp.Award); state.XpGrantedToday += step.Xp.Award; }
                }
            }
            else step.Xp = new IndustrialXpResult { BlockedReason = "AlreadySettled" };
            if (stamp.Active && current.Fresh)
            {
                step.Promotion = EvaluatePromotion(progress.Level, progress.Experience, current);
                if (step.Promotion.CanPromote && step.Promotion.NextLevel > progress.Level) { progress.Level = step.Promotion.NextLevel; step.Promoted = true; }
            }
            return step;
        }
        // ---- daily productive-activity streak (design §14.1a)
        public static bool Qualifies(double weightedSum, double observedHours) =>
            observedHours >= StreakHours - 1e-9 && observedHours > 0 && weightedSum / observedHours >= StreakActivity - 1e-9;
        // Integrates [previous endpoint, this endpoint) with the earlier validated value, split at day boundaries.
        public static void AccumulateDailyActivity(in IndustrialDailyActivityObservation observation, ref IndustrialAreaState state, ref IndustrialActivitySample? baseline)
        {
            if (!observation.Valid || double.IsNaN(observation.Activity) || double.IsInfinity(observation.Activity) || observation.HourBucket < state.LastCreditedActivityHour)
            {
                // Unavailable, stale or suspended: no coverage, and the next valid sample starts a new bracket.
                baseline = null;
                return;
            }
            if (baseline.HasValue)
            {
                var previous = baseline.Value;
                double gap = observation.Hours - previous.Hours;
                if (previous.Generation == observation.Generation && gap > 0 && gap <= IndustrialTiming.MaxSampleGapHours)
                {
                    Credit(previous.Hours, observation.Hours, previous.Activity, ref state);
                    state.LastCreditedActivityHour = observation.HourBucket;
                }
            }
            baseline = new IndustrialActivitySample { Hours = observation.Hours, HourBucket = observation.HourBucket, Activity = IndustrialMetric.Clamp(observation.Activity), Generation = observation.Generation };
        }
        private static void Credit(double start, double end, double activity, ref IndustrialAreaState state)
        {
            activity = IndustrialMetric.Clamp(activity);
            while (end > start)
            {
                uint day = (uint)Math.Max(0, Math.Floor(start / 24.0));
                double dayEnd = Math.Min(end, (day + 1) * 24.0);
                if (day > state.ActivityDay) AdvanceToDay(day, ref state);
                // A segment of an already completed day is never credited again.
                if (day == state.ActivityDay)
                {
                    double hours = dayEnd - start;
                    state.ActivityObservedHours += hours;
                    state.ActivityWeightedSum += activity * hours;
                }
                start = dayEnd;
            }
        }
        // Completes every day before currentDay exactly once (including days crossed while Suspended).
        public static void AdvanceToDay(uint currentDay, ref IndustrialAreaState state)
        {
            if (currentDay <= state.ActivityDay) return;
            FinalizeActivityDay(state.ActivityDay, ref state);
            uint firstSkipped = state.ActivityDay + 1;
            if (currentDay > firstSkipped)
            {
                // Days without any coverage cannot qualify; a skipped day breaks the streak.
                if (currentDay - firstSkipped > 31) { state.HighThroughputStreakDays = 0; state.HighThroughputLastDay = currentDay - 1; }
                else for (uint d = firstSkipped; d < currentDay; d++) UpdateHighThroughputStreak(d, false, ref state);
            }
            state.ActivityDay = currentDay;
            state.ActivityWeightedSum = 0;
            state.ActivityObservedHours = 0;
        }
        public static void FinalizeActivityDay(uint completedDay, ref IndustrialAreaState state)
        {
            bool qualifies = completedDay == state.ActivityDay && Qualifies(state.ActivityWeightedSum, state.ActivityObservedHours);
            UpdateHighThroughputStreak(completedDay, qualifies, ref state);
        }
        public static void UpdateHighThroughputStreak(uint completedDay, bool qualifies, ref IndustrialAreaState state)
        {
            bool evaluated = state.HighThroughputLastDay != IndustrialAreaState.NoDay;
            if (evaluated && completedDay <= state.HighThroughputLastDay) return;
            bool consecutive = evaluated && completedDay == state.HighThroughputLastDay + 1;
            state.HighThroughputStreakDays = qualifies ? (consecutive ? state.HighThroughputStreakDays + 1 : 1) : 0;
            state.HighThroughputLastDay = completedDay;
        }
    }
}
