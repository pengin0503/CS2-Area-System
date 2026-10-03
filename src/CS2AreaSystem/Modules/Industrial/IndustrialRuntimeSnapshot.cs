using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Why a metric has (or lacks) a value. Only Available/Partial enter any weighted score (design §12.1).
    // Unavailable is the default, so an unset metric can never read as a real zero.
    public enum IndustrialMetricStatus : byte { Unavailable, Available, Partial, NotApplicable, WarmingUp, Stale }
    // Availability-aware 0..100 (or raw) value with observed/eligible coverage. Value is meaningful only when HasValue.
    public readonly struct IndustrialMetric
    {
        public IndustrialMetricStatus Status { get; }
        public double Value { get; }
        public double Observed { get; }
        public double Eligible { get; }
        private IndustrialMetric(IndustrialMetricStatus status, double value, double observed, double eligible)
        { Status = status; Value = value; Observed = observed; Eligible = eligible; }
        public bool HasValue => Status == IndustrialMetricStatus.Available || Status == IndustrialMetricStatus.Partial;
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        // Full coverage unless observed < eligible; non-finite values are unavailable, never zero.
        public static IndustrialMetric Of(double value, double observed = 1, double eligible = 1) =>
            !Finite(value) || !Finite(observed) || !Finite(eligible) ? Unavailable(observed, eligible)
            : new IndustrialMetric(observed < eligible ? IndustrialMetricStatus.Partial : IndustrialMetricStatus.Available, value, observed, eligible);
        public static IndustrialMetric Partial(double value, double observed, double eligible) =>
            Finite(value) ? new IndustrialMetric(IndustrialMetricStatus.Partial, value, observed, eligible) : Unavailable(observed, eligible);
        public static IndustrialMetric Unavailable(double observed = 0, double eligible = 0) => new IndustrialMetric(IndustrialMetricStatus.Unavailable, 0, observed, eligible);
        public static IndustrialMetric NotApplicable() => new IndustrialMetric(IndustrialMetricStatus.NotApplicable, 0, 0, 0);
        public static IndustrialMetric WarmingUp(double observed, double eligible) => new IndustrialMetric(IndustrialMetricStatus.WarmingUp, 0, observed, eligible);
        public IndustrialMetric AsStale() => new IndustrialMetric(IndustrialMetricStatus.Stale, Value, Observed, Eligible);
        public static double Clamp(double value) => value < 0 ? 0 : value > 100 ? 100 : value;
        public override string ToString() => HasValue ? Status + " " + Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : Status.ToString();
    }
    // One diagnostic (design §10.3, §13): an observed symptom, never an asserted root cause or a simulation modifier.
    public sealed class IndustrialBottleneck
    {
        public string Key;
        // 0..1, higher is worse.
        public double Severity;
        public int AffectedFacilities;
        public double ObservedHours;
        // Displayed evidence value (percent) behind the diagnostic.
        public double EvidenceValue;
        // One representative facility from the same observation cycle (Wave A3); Entity.Null when none is reliable.
        public Entity Target;
    }
    // Per-channel flow state of one completed observation (weights w = clamp(jobs, 1, 100)).
    public struct IndustrialFlowSample
    {
        public double AffectedWeight, ValidWeight;
        public int Applicable, Affected, Unavailable, NotApplicable;
    }
    public struct IndustrialResourceTotal
    {
        public ulong Resource;
        // Capacity-normalized weighted activity (comparable across resources) and the raw actual rate (units/day).
        public double WeightedActivity, ActualPerDay;
        public int Producers;
    }
    // One coherent, completed observation generation of one Area. Immutable once published.
    public sealed class IndustrialObservationResult
    {
        public long CycleId, HourBucket;
        public uint StartFrame, EndFrame;
        public double StartHours, EndHours;
        public ulong MembershipToken, StructureToken;
        public bool Active, Delayed;
        // Members: primary-owned buildings read this cycle; Ignored: of those, without an Industrial role; NonPrimary:
        // overlapping members credited to another Industrial Area (shown, never counted here).
        public int Members, IgnoredMembers, NonPrimaryMembers, FailedReads;
        public int Extractors, Processors, StorageFacilities, CargoFacilities, CargoOperational;
        public int ProducerFacilities, OperatingProducers;
        public int Jobs, Employees, ProducerJobs, ProducerEmployees, SupportJobs, SupportEmployees, InvalidJobFacilities, JobFacilities;
        public int[] VacanciesByLevel = new int[5];
        public List<IndustrialResourceTotal> Resources = new List<IndustrialResourceTotal>();
        // Observed input -> output processes grouped by exact recipe (Wave A4).
        public List<IndustrialProcessSummary> Processes = new List<IndustrialProcessSummary>();
        public double? RawActivity, RawEfficiency;
        public double StoredAmount, StorageCapacity;
        public int StorageObserved;
        public IndustrialFlowSample InputSample, OutputSample;
        public double InputObservedHours, OutputObservedHours;
        public int MeaningfulOutputs, RollingMeaningfulOutputs;
        public IndustrialMetric Activity, ActiveShare, Diversity, Production, InputContinuity, OutputContinuity, Logistics,
            JobFill, StaffingBalance, Employment, BuildingEfficiency, Profitability, Efficiency, Performance;
        // Top three diagnostics, and every current diagnostic (for the Logistics tab).
        public IReadOnlyList<IndustrialBottleneck> Bottlenecks = Array.Empty<IndustrialBottleneck>();
        public List<IndustrialBottleneck> AllBottlenecks = new List<IndustrialBottleneck>();
        // Companies resolved to this Area in this cycle, ascending Entity Index/Version (C4 support receivers); complete
        // only when every member read succeeded. Same generation tokens as the rest of the result.
        public IReadOnlyList<Entity> SupportRoster = Array.Empty<Entity>();
        public bool SupportRosterComplete;
        public ulong Generation => IndustrialGenerations.Combine(MembershipToken, StructureToken);
    }
    public static class IndustrialGenerations
    {
        // Equality-only identity of a membership/structure generation (never ordered).
        public static ulong Combine(ulong membership, ulong structure) => unchecked(membership * 0x9E3779B97F4A7C15UL ^ (structure + 0x632BE59BD9B4E019UL));
    }
    // Validated normalized activity endpoint for time-weighted daily accumulation (runtime only).
    public struct IndustrialActivitySample
    {
        public double Hours;
        public long HourBucket;
        public double Activity;
        public ulong Generation;
    }
    // Rolling flow coverage: covered intervals [start, end) carrying the earlier sample's weights (design §10.1).
    public sealed class IndustrialFlowWindow
    {
        public const double WindowHours = 6, RequiredHours = 4;
        private struct Interval { public double Start, End, Affected, Valid; }
        private readonly List<Interval> intervals = new List<Interval>();
        public void Add(double start, double end, double affectedWeight, double validWeight)
        {
            if (!(end > start) || !(validWeight > 0) || affectedWeight < 0) return;
            intervals.Add(new Interval { Start = start, End = end, Affected = Math.Min(affectedWeight, validWeight), Valid = validWeight });
        }
        public void Prune(double now)
        {
            double from = now - WindowHours;
            intervals.RemoveAll(i => i.End <= from);
        }
        public double CoveredHours(double now)
        {
            double from = now - WindowHours, hours = 0;
            foreach (var i in intervals) hours += Math.Max(0, Math.Min(i.End, now) - Math.Max(i.Start, from));
            return hours;
        }
        // Σ affected·Δt and Σ valid·Δt inside the window.
        public (double Affected, double Valid) Totals(double now)
        {
            double from = now - WindowHours, affected = 0, valid = 0;
            foreach (var i in intervals)
            {
                double dt = Math.Max(0, Math.Min(i.End, now) - Math.Max(i.Start, from));
                affected += i.Affected * dt; valid += i.Valid * dt;
            }
            return (affected, valid);
        }
        public void Clear() => intervals.Clear();
    }
    // Rolling per-output activity over the six-hour window (Level 5 "meaningful output classes" prestige).
    public sealed class IndustrialResourceHistory
    {
        private struct Sample { public double Hours; public Dictionary<ulong, double> Activity; }
        private readonly List<Sample> samples = new List<Sample>();
        public void Add(double hours, IEnumerable<IndustrialResourceTotal> totals)
        {
            var activity = new Dictionary<ulong, double>();
            foreach (var t in totals) activity[t.Resource] = t.WeightedActivity;
            samples.Add(new Sample { Hours = hours, Activity = activity });
            samples.RemoveAll(s => s.Hours <= hours - IndustrialFlowWindow.WindowHours);
        }
        public int MeaningfulOutputs(double now)
        {
            var sums = new Dictionary<ulong, double>(); int count = 0;
            foreach (var s in samples)
            {
                if (s.Hours <= now - IndustrialFlowWindow.WindowHours || s.Hours > now) continue;
                count++;
                foreach (var pair in s.Activity) { sums.TryGetValue(pair.Key, out var v); sums[pair.Key] = v + pair.Value; }
            }
            if (count == 0) return 0;
            var averages = new List<double>();
            foreach (var v in sums.Values) averages.Add(v / count);
            return IndustrialProductionModel.MeaningfulOutputs(averages);
        }
        public void Clear() => samples.Clear();
    }
    // Runtime-only hourly trend samples of one Industrial Area (Wave A1). Never serialized; a load clears the store.
    public sealed class IndustrialTrendSnapshot
    {
        public const int Capacity = 24;
        public readonly CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries Production = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity),
            Logistics = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity), Employment = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity),
            Efficiency = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity), Performance = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity),
            ProductiveFacilities = new CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries(Capacity);
        // Only a current (Active, not Delayed) result is a sample; metrics without a value are skipped, never zero.
        public void Record(IndustrialObservationResult result)
        {
            if (result == null || !result.Active || result.Delayed) return;
            long bucket = result.HourBucket;
            Add(Production, bucket, result.Production); Add(Logistics, bucket, result.Logistics); Add(Employment, bucket, result.Employment);
            Add(Efficiency, bucket, result.Efficiency); Add(Performance, bucket, result.Performance);
            ProductiveFacilities.Record(bucket, result.OperatingProducers);
        }
        private static void Add(CS2AreaSystem.Core.Diagnostics.RuntimeTrendSeries series, long bucket, IndustrialMetric metric)
        { if (metric.HasValue) series.Record(bucket, metric.Value); }
    }
    // Derived, runtime-only view of one Industrial Area. Never serialized; cleared on load.
    public sealed class IndustrialRuntimeSnapshot
    {
        public ulong StableId;
        public Entity Area;
        public readonly IndustrialCompanyCandidateSet OwnershipCandidates = new IndustrialCompanyCandidateSet();
        // Last completed coherent generation; null after load until observation rebuilds it.
        public IndustrialObservationResult Current { get; private set; }
        public bool HasPublished => Current != null;
        // The Area is not Active: the last result is shown only as paused/stale and earns nothing.
        public bool Paused;
        public readonly IndustrialFlowWindow InputWindow = new IndustrialFlowWindow(), OutputWindow = new IndustrialFlowWindow();
        // Previous flow endpoint (time, generation and channel weights) used to close the next covered interval.
        public double FlowBaselineHours;
        public ulong FlowBaselineGeneration;
        public IndustrialFlowSample FlowBaselineInput, FlowBaselineOutput;
        public bool HasFlowBaseline;
        public IndustrialActivitySample? ActivityBaseline;
        public readonly IndustrialResourceHistory ResourceHistory = new IndustrialResourceHistory();
        // Latest XP settlement/promotion evaluation, for the panel (runtime only).
        public IndustrialXpResult LastXp;
        public bool HasXpResult;
        public IndustrialDayResult? LastCompletedDay;
        // Last observation cycle consumed by progression (runtime de-duplication; hour/day de-duplication is persisted).
        public long LastConsumedCycle = -1;
        // Recorded for tuning: cycles that took more than one game hour.
        public int DelayedCycles;
        public readonly IndustrialTrendSnapshot Trends = new IndustrialTrendSnapshot();
        public void Publish(IndustrialObservationResult result) { Current = result; Trends.Record(result); }
        public void InvalidateOwnership()
        {
            Paused = true; OwnershipCandidates.Clear(); ResetBaselines();
            InputWindow.Clear(); OutputWindow.Clear(); ResourceHistory.Clear();
            // The previous result remains visible as stale, but cannot be settled under the new ownership.
            if (Current != null) LastConsumedCycle = Current.CycleId;
        }
        public void ResetBaselines() { HasFlowBaseline = false; ActivityBaseline = null; }
    }
    public sealed class IndustrialRuntimeStore
    {
        public Action<IndustrialRuntimeSnapshot> Initialize;
        private readonly Dictionary<ulong, IndustrialRuntimeSnapshot> areas = new Dictionary<ulong, IndustrialRuntimeSnapshot>();
        public IEnumerable<IndustrialRuntimeSnapshot> All => areas.Values;
        public int Count => areas.Count;
        public bool TryGet(ulong id, out IndustrialRuntimeSnapshot snapshot) => areas.TryGetValue(id, out snapshot);
        public IndustrialRuntimeSnapshot GetOrCreate(ulong id, Entity area)
        {
            if (!areas.TryGetValue(id, out var snapshot)) {
                areas[id] = snapshot = new IndustrialRuntimeSnapshot { StableId = id, Area = area };
                Initialize?.Invoke(snapshot);
            }
            snapshot.Area = area;
            return snapshot;
        }
        public void InvalidateOwnership(IEnumerable<ulong> affectedAreas)
        {
            foreach (var id in affectedAreas) if (areas.TryGetValue(id, out var snapshot)) snapshot.InvalidateOwnership();
        }
        public void Remove(ulong id) => areas.Remove(id);
        public void Clear() => areas.Clear();
        // Drops snapshots of Industrial Areas that no longer exist; Suspended Areas stay.
        // Reuses its sets, so an unchanged prune does not allocate (#61).
        public void Prune(IEnumerable<IndustrialAreaRef> alive)
        {
            IndustrialAreaIds.Fill(alive, ids);
            stale.Clear();
            foreach (var id in areas.Keys) if (!ids.Contains(id)) stale.Add(id);
            foreach (var id in stale) areas.Remove(id);
        }
        private readonly HashSet<ulong> ids = new HashSet<ulong>();
        private readonly List<ulong> stale = new List<ulong>();
    }
    public static class IndustrialAreaIds
    {
        // Without boxing the enumerator for the usual list/array inputs.
        public static void Fill(IEnumerable<IndustrialAreaRef> areas, HashSet<ulong> into)
        {
            into.Clear();
            if (areas is IReadOnlyList<IndustrialAreaRef> list) { for (int i = 0; i < list.Count; i++) into.Add(list[i].StableId); return; }
            foreach (var area in areas) into.Add(area.StableId);
        }
    }
}
