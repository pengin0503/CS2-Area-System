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
        public int Members, Extractors, Processors, StorageFacilities, CargoFacilities, CargoOperational;
        public int ProducerFacilities, OperatingProducers;
        public int Jobs, Employees, ProducerJobs, ProducerEmployees, SupportJobs, SupportEmployees, InvalidJobFacilities, JobFacilities;
        public int[] VacanciesByLevel = new int[5];
        public List<IndustrialResourceTotal> Resources = new List<IndustrialResourceTotal>();
        public double? RawActivity, RawEfficiency;
        public double StoredAmount, StorageCapacity;
        public int StorageObserved;
        public IndustrialFlowSample InputSample, OutputSample;
        public double InputObservedHours, OutputObservedHours;
        public int MeaningfulOutputs;
        public IndustrialMetric Activity, ActiveShare, Diversity, Production, InputContinuity, OutputContinuity, Logistics,
            JobFill, StaffingBalance, Employment, BuildingEfficiency, Profitability, Efficiency, Performance;
        public IReadOnlyList<IndustrialBottleneck> Bottlenecks = Array.Empty<IndustrialBottleneck>();
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
    // Derived, runtime-only view of one Industrial Area. Never serialized; cleared on load.
    public sealed class IndustrialRuntimeSnapshot
    {
        public ulong StableId;
        public Entity Area;
        // Last completed coherent generation; null after load until observation rebuilds it.
        public IndustrialObservationResult Current { get; private set; }
        public bool HasPublished => Current != null;
        public readonly IndustrialFlowWindow InputWindow = new IndustrialFlowWindow(), OutputWindow = new IndustrialFlowWindow();
        // Previous flow endpoint (time, generation and channel weights) used to close the next covered interval.
        public double FlowBaselineHours;
        public ulong FlowBaselineGeneration;
        public IndustrialFlowSample FlowBaselineInput, FlowBaselineOutput;
        public bool HasFlowBaseline;
        public IndustrialActivitySample? ActivityBaseline;
        // Last observation cycle consumed by progression (runtime de-duplication; hour/day de-duplication is persisted).
        public long LastConsumedCycle = -1;
        // Recorded for tuning: cycles that took more than one game hour.
        public int DelayedCycles;
        public void Publish(IndustrialObservationResult result) => Current = result;
        public void ResetBaselines() { HasFlowBaseline = false; ActivityBaseline = null; }
    }
    public sealed class IndustrialRuntimeStore
    {
        private readonly Dictionary<ulong, IndustrialRuntimeSnapshot> areas = new Dictionary<ulong, IndustrialRuntimeSnapshot>();
        public IEnumerable<IndustrialRuntimeSnapshot> All => areas.Values;
        public int Count => areas.Count;
        public bool TryGet(ulong id, out IndustrialRuntimeSnapshot snapshot) => areas.TryGetValue(id, out snapshot);
        public IndustrialRuntimeSnapshot GetOrCreate(ulong id, Entity area)
        {
            if (!areas.TryGetValue(id, out var snapshot)) areas[id] = snapshot = new IndustrialRuntimeSnapshot { StableId = id };
            snapshot.Area = area;
            return snapshot;
        }
        public void Remove(ulong id) => areas.Remove(id);
        public void Clear() => areas.Clear();
        // Drops snapshots of Industrial Areas that no longer exist; Suspended Areas stay.
        public void Prune(IEnumerable<IndustrialAreaRef> alive)
        {
            var ids = new HashSet<ulong>();
            foreach (var area in alive) ids.Add(area.StableId);
            var stale = new List<ulong>();
            foreach (var id in areas.Keys) if (!ids.Contains(id)) stale.Add(id);
            foreach (var id in stale) areas.Remove(id);
        }
    }
}
