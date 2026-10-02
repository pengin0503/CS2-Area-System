using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public static class IndustrialTiming
    {
        // A cycle longer than one game hour cannot supply current scores, XP or coverage (design §18.2).
        public const double MaxCycleHours = 1.0;
        // Hourly cycles start at the first update after each hour boundary; one game minute absorbs that update
        // jitter. Endpoints further apart are a gap and are never bridged (design §10.1, §14.1a).
        public const double SchedulingToleranceHours = 1.0 / 60.0;
        public const double MaxSampleGapHours = 1.0 + SchedulingToleranceHours;
    }
    public struct IndustrialCycleInfo
    {
        public long CycleId, HourBucket;
        public uint StartFrame, EndFrame;
        public double StartHours, EndHours;
        public ulong MembershipToken, StructureToken;
        public bool Active;
        public int NonPrimaryMembers;
    }
    // Per-cycle running totals of primary-owned member observations. Discarded on restart; never published partially.
    public sealed class IndustrialCycleAccumulator
    {
        internal readonly HashSet<Entity> Seen = new HashSet<Entity>();
        public int Members, Ignored, Extractors, Processors, StorageFacilities, CargoFacilities, CargoOperational, ProducerFacilities, OperatingProducers;
        public int ProducerJobs, ProducerEmployees, SupportJobs, SupportEmployees;
        public readonly int[] VacanciesByLevel = new int[5];
        public readonly List<IndustrialJobFacility> JobFacilities = new List<IndustrialJobFacility>();
        public readonly List<IndustrialWeightedValue> Activity = new List<IndustrialWeightedValue>();
        public readonly List<IndustrialWeightedValue> Efficiency = new List<IndustrialWeightedValue>();
        public readonly List<IndustrialWeightedValue> Profitability = new List<IndustrialWeightedValue>();
        public double RawActivitySum, RawActivityWeight, RawEfficiencySum, RawEfficiencyWeight;
        public readonly Dictionary<ulong, IndustrialResourceTotal> Resources = new Dictionary<ulong, IndustrialResourceTotal>();
        public readonly Dictionary<IndustrialProcessKey, IndustrialProcessSummary> Processes = new Dictionary<IndustrialProcessKey, IndustrialProcessSummary>();
        public IndustrialFlowSample Input, Output;
        public double Stored, Capacity;
        public int StorageObserved;
        public int Understaffed, LowEfficiency, LowProfit, InputAffected, OutputAffected, IdleProducers, FailedReads;
        public readonly IndustrialBottleneckTargets Targets = new IndustrialBottleneckTargets();
    }
    // Bounded per-cycle evidence: at most one representative facility per bottleneck key, chosen while the cycle's
    // observations are applied (no extra scan). Ties keep the first observed facility.
    public sealed class IndustrialBottleneckTargets
    {
        private Entity workforce, efficiency, profitability, input, output, cargo, idle;
        private double workforceFill = double.MaxValue, efficiencyValue = double.MaxValue, profitabilityValue = double.MaxValue, inputWeight, outputWeight;
        public Entity For(string key)
        {
            switch (key)
            {
                case "WorkforceShortage": return workforce;
                case "LowEfficiency": return efficiency;
                case "LowProfitability": return profitability;
                case "InputStarvation": return input;
                case "StorageSaturation": return output;
                case "InsufficientCargo": return cargo != Entity.Null ? cargo : output;
                case "LowActiveProducerShare": return idle;
                default: return Entity.Null;
            }
        }
        internal void Understaffed(Entity facility, double fill) { if (fill < workforceFill) { workforceFill = fill; workforce = facility; } }
        internal void Efficiency(Entity facility, double value) { if (value < efficiencyValue) { efficiencyValue = value; efficiency = facility; } }
        internal void Profitability(Entity facility, double value) { if (value < profitabilityValue) { profitabilityValue = value; profitability = facility; } }
        internal void InputAffected(Entity facility, double weight) { if (input == Entity.Null || weight > inputWeight) { inputWeight = weight; input = facility; } }
        internal void OutputAffected(Entity facility, double weight) { if (output == Entity.Null || weight > outputWeight) { outputWeight = weight; output = facility; } }
        internal void CargoNotOperating(Entity facility) { if (cargo == Entity.Null) cargo = facility; }
        internal void IdleProducer(Entity facility) { if (idle == Entity.Null) idle = facility; }
    }
    // Aggregates normalized observations into one coherent Area result through the pure Task 3 models.
    public static class IndustrialObservationAggregator
    {
        public static void Apply(IndustrialCycleAccumulator acc, in IndustrialEntityObservation obs)
        {
            // Each member building is credited once per cycle, even if it was read twice.
            if (obs.CandidateOnly || !acc.Seen.Add(obs.Facility)) return;
            acc.Members++;
            var roles = obs.Roles;
            if ((roles & IndustrialFacilityClassifier.EligibleRoles) == 0) { acc.Ignored++; return; }
            if ((roles & IndustrialFacilityRole.Extractor) != 0) acc.Extractors++;
            if ((roles & IndustrialFacilityRole.Processor) != 0) acc.Processors++;
            if ((roles & IndustrialFacilityRole.Storage) != 0) acc.StorageFacilities++;
            if ((roles & IndustrialFacilityRole.Cargo) != 0) { acc.CargoFacilities++; if (obs.CargoOperational == true) acc.CargoOperational++; else acc.Targets.CargoNotOperating(obs.Facility); }
            double weight = IndustrialWeighting.Weight(obs.Jobs);
            // Jobs of a multi-role facility are counted once; producer and support jobs are split by facility.
            if (obs.Jobs.HasValue)
            {
                int jobs = obs.Jobs.Value, employees = obs.Employees ?? 0;
                acc.JobFacilities.Add(new IndustrialJobFacility { Jobs = jobs, Employees = employees });
                if (obs.IsProducer) { acc.ProducerJobs += Math.Max(0, jobs); acc.ProducerEmployees += Math.Max(0, employees); }
                else { acc.SupportJobs += Math.Max(0, jobs); acc.SupportEmployees += Math.Max(0, employees); }
                if (jobs > 0 && employees < 0.75 * jobs) { acc.Understaffed++; acc.Targets.Understaffed(obs.Facility, (double)Math.Max(0, employees) / jobs); }
                if (obs.VacanciesByLevel != null) for (int i = 0; i < 5 && i < obs.VacanciesByLevel.Length; i++) acc.VacanciesByLevel[i] += Math.Max(0, obs.VacanciesByLevel[i]);
            }
            if (obs.Efficiency.HasValue)
            {
                acc.Efficiency.Add(new IndustrialWeightedValue { Value = obs.Efficiency, Jobs = obs.Jobs });
                acc.RawEfficiencySum += obs.Efficiency.Value * weight; acc.RawEfficiencyWeight += weight;
                if (obs.Efficiency.Value < IndustrialPerformanceModel.EfficiencyThreshold) acc.LowEfficiency++;
                acc.Targets.Efficiency(obs.Facility, obs.Efficiency.Value);
            }
            if (obs.Profitability.HasValue)
            {
                acc.Profitability.Add(new IndustrialWeightedValue { Value = obs.Profitability, Jobs = obs.Jobs });
                if (obs.Profitability.Value < IndustrialPerformanceModel.ProfitabilityThreshold) acc.LowProfit++;
                acc.Targets.Profitability(obs.Facility, obs.Profitability.Value);
            }
            if (obs.StorageCapacity.HasValue && obs.StoredAmount.HasValue && obs.StorageCapacity.Value > 0 && (roles & (IndustrialFacilityRole.Storage | IndustrialFacilityRole.Cargo)) != 0)
            { acc.Stored += obs.StoredAmount.Value; acc.Capacity += obs.StorageCapacity.Value; acc.StorageObserved++; }
            if (!obs.IsProducer) return;
            acc.ProducerFacilities++;
            if (obs.IsOperatingProducer && obs.Available) acc.OperatingProducers++; else { acc.IdleProducers++; acc.Targets.IdleProducer(obs.Facility); }
            IndustrialProcessDiagnostics.Add(acc.Processes, obs.Processes, obs.Available);
            if (obs.ProducerTenants > 0)
            {
                // Unusable (abandoned/destroyed/inactive) facilities have no productive activity.
                double? activity = obs.ProductiveActivity.HasValue ? (obs.Available ? obs.ProductiveActivity : 0) : null;
                acc.Activity.Add(new IndustrialWeightedValue { Value = activity, Jobs = obs.Jobs });
                if (activity.HasValue) { acc.RawActivitySum += activity.Value * weight; acc.RawActivityWeight += weight; }
                if (activity.HasValue && obs.Resources != null && obs.Resources.Length > 0)
                    foreach (var r in obs.Resources)
                    {
                        var share = IndustrialSourceNormalization.ActivityPercent(obs.Available ? r.ActualPerDay : 0, r.CapacityPerDay);
                        acc.Resources.TryGetValue(r.Resource, out var total);
                        total.Resource = r.Resource; total.Producers++;
                        total.ActualPerDay += obs.Available ? Math.Max(0, r.ActualPerDay) : 0;
                        if (share.HasValue) total.WeightedActivity += weight / obs.Resources.Length * IndustrialMetric.Clamp(share.Value) / 100.0;
                        acc.Resources[r.Resource] = total;
                    }
            }
            AddFlow(ref acc.Input, obs.Input, weight, ref acc.InputAffected);
            AddFlow(ref acc.Output, obs.Output, weight, ref acc.OutputAffected);
            if (obs.Input == IndustrialFlowState.Affected) acc.Targets.InputAffected(obs.Facility, weight);
            if (obs.Output == IndustrialFlowState.Affected) acc.Targets.OutputAffected(obs.Facility, weight);
        }
        private static void AddFlow(ref IndustrialFlowSample sample, IndustrialFlowState state, double weight, ref int affectedFacilities)
        {
            switch (state)
            {
                case IndustrialFlowState.Affected: sample.Applicable++; sample.Affected++; sample.AffectedWeight += weight; sample.ValidWeight += weight; affectedFacilities++; break;
                case IndustrialFlowState.Healthy: sample.Applicable++; sample.ValidWeight += weight; break;
                case IndustrialFlowState.Unavailable: sample.Applicable++; sample.Unavailable++; break;
                default: sample.NotApplicable++; break;
            }
        }
        // Computes the coherent result, integrates flow coverage against the previous endpoint and publishes.
        public static IndustrialObservationResult Finalize(IndustrialCycleAccumulator acc, IndustrialRuntimeSnapshot snapshot, in IndustrialCycleInfo info)
        {
            var result = new IndustrialObservationResult {
                CycleId = info.CycleId, HourBucket = info.HourBucket, StartFrame = info.StartFrame, EndFrame = info.EndFrame, StartHours = info.StartHours, EndHours = info.EndHours,
                MembershipToken = info.MembershipToken, StructureToken = info.StructureToken, Active = info.Active,
                Delayed = info.EndHours - info.StartHours > IndustrialTiming.MaxCycleHours,
                Members = acc.Members, NonPrimaryMembers = info.NonPrimaryMembers, IgnoredMembers = acc.Ignored,
                Extractors = acc.Extractors, Processors = acc.Processors, StorageFacilities = acc.StorageFacilities, CargoFacilities = acc.CargoFacilities,
                CargoOperational = acc.CargoOperational, ProducerFacilities = acc.ProducerFacilities, OperatingProducers = acc.OperatingProducers,
                ProducerJobs = acc.ProducerJobs, ProducerEmployees = acc.ProducerEmployees, SupportJobs = acc.SupportJobs, SupportEmployees = acc.SupportEmployees,
                Jobs = acc.ProducerJobs + acc.SupportJobs, Employees = acc.ProducerEmployees + acc.SupportEmployees, JobFacilities = acc.JobFacilities.Count,
                StoredAmount = acc.Stored, StorageCapacity = acc.Capacity, StorageObserved = acc.StorageObserved,
                InputSample = acc.Input, OutputSample = acc.Output, FailedReads = acc.FailedReads };
            Array.Copy(acc.VacanciesByLevel, result.VacanciesByLevel, 5);
            foreach (var total in acc.Resources.Values) result.Resources.Add(total);
            result.Resources.Sort((a, b) => b.WeightedActivity.CompareTo(a.WeightedActivity));
            result.Processes.AddRange(acc.Processes.Values);
            IndustrialProcessDiagnostics.Sort(result.Processes);
            result.RawActivity = acc.RawActivityWeight > 0 ? acc.RawActivitySum / acc.RawActivityWeight : (double?)null;
            result.RawEfficiency = acc.RawEfficiencyWeight > 0 ? acc.RawEfficiencySum / acc.RawEfficiencyWeight : (double?)null;
            // Production.
            var activity = IndustrialWeighting.Mean(acc.Activity);
            if (activity.Status == IndustrialMetricStatus.NotApplicable && acc.ProducerFacilities > 0) activity = IndustrialMetric.Unavailable(0, acc.ProducerFacilities);
            result.Activity = activity;
            result.ActiveShare = IndustrialProductionModel.ActiveShare(acc.ProducerFacilities, acc.OperatingProducers);
            var outputs = new List<double>();
            foreach (var total in result.Resources) outputs.Add(total.WeightedActivity);
            var outputList = activity.HasValue ? outputs : null;
            result.Diversity = IndustrialProductionModel.Diversity(activity, outputList);
            result.MeaningfulOutputs = activity.HasValue && activity.Value > 0 ? IndustrialProductionModel.MeaningfulOutputs(outputs) : 0;
            result.Production = IndustrialProductionModel.Compute(new IndustrialProductionInputs {
                Activity = activity, KnownProducers = acc.ProducerFacilities, OperatingProducers = acc.OperatingProducers, OutputActivity = outputList });
            if (!result.Delayed && result.Active && activity.HasValue) snapshot.ResourceHistory.Add(info.StartHours, result.Resources);
            result.RollingMeaningfulOutputs = snapshot.ResourceHistory.MeaningfulOutputs(info.StartHours);
            // Logistics: integrate the previous endpoint over the elapsed interval, then score the rolling window.
            IntegrateFlow(snapshot, result);
            double now = info.StartHours;
            result.InputObservedHours = snapshot.InputWindow.CoveredHours(now);
            result.OutputObservedHours = snapshot.OutputWindow.CoveredHours(now);
            result.InputContinuity = IndustrialLogisticsModel.Channel(ChannelInputs(snapshot.InputWindow, acc.Input, now));
            result.OutputContinuity = IndustrialLogisticsModel.Channel(ChannelInputs(snapshot.OutputWindow, acc.Output, now));
            result.Logistics = IndustrialLogisticsModel.Combine(result.InputContinuity, result.OutputContinuity);
            // Employment.
            result.JobFill = IndustrialEmploymentModel.JobFill(acc.JobFacilities);
            result.StaffingBalance = IndustrialEmploymentModel.StaffingBalance(acc.JobFacilities);
            result.Employment = IndustrialEmploymentModel.Compute(new IndustrialEmploymentInputs { Facilities = acc.JobFacilities });
            // Efficiency and composite.
            result.BuildingEfficiency = NotApplicableToUnavailable(IndustrialWeighting.Mean(acc.Efficiency), result.Members - result.IgnoredMembers);
            result.Profitability = NotApplicableToUnavailable(IndustrialWeighting.Mean(acc.Profitability), result.Members - result.IgnoredMembers);
            result.Efficiency = IndustrialPerformanceModel.ComputeEfficiency(result.BuildingEfficiency, result.Profitability);
            result.Performance = IndustrialPerformanceModel.ComputePerformance(result.Production, result.Logistics, result.Employment, result.Efficiency);
            result.AllBottlenecks = IndustrialPerformanceModel.FindAllBottlenecks(new IndustrialBottleneckInputs {
                JobFill = result.JobFill, BuildingEfficiency = result.BuildingEfficiency, Profitability = result.Profitability,
                InputContinuity = result.InputContinuity, OutputContinuity = result.OutputContinuity, ActiveShare = result.ActiveShare,
                ProducerFacilities = acc.ProducerFacilities, UnderstaffedFacilities = acc.Understaffed, LowEfficiencyFacilities = acc.LowEfficiency,
                LowProfitFacilities = acc.LowProfit, InputAffectedFacilities = acc.InputAffected, OutputAffectedFacilities = acc.OutputAffected,
                IdleProducers = acc.IdleProducers, InputObservedHours = result.InputObservedHours, OutputObservedHours = result.OutputObservedHours,
                SampleHours = Math.Max(0, info.EndHours - info.StartHours), CargoFacilities = acc.CargoFacilities, CargoNonOperational = acc.CargoFacilities - acc.CargoOperational });
            // Only this cycle's candidate is attached (never a facility from an earlier generation).
            foreach (var bottleneck in result.AllBottlenecks) bottleneck.Target = acc.Targets.For(bottleneck.Key);
            result.Bottlenecks = result.AllBottlenecks.Count > IndustrialPerformanceModel.MaxBottlenecks
                ? result.AllBottlenecks.GetRange(0, IndustrialPerformanceModel.MaxBottlenecks) : result.AllBottlenecks;
            snapshot.Publish(result);
            return result;
        }
        // Eligible facilities exist but none exposes the value: unavailable rather than not applicable.
        private static IndustrialMetric NotApplicableToUnavailable(IndustrialMetric metric, int eligibleFacilities) =>
            metric.Status == IndustrialMetricStatus.NotApplicable && eligibleFacilities > 0 ? IndustrialMetric.Unavailable(0, eligibleFacilities) : metric;
        private static void IntegrateFlow(IndustrialRuntimeSnapshot snapshot, IndustrialObservationResult result)
        {
            double now = result.StartHours;
            snapshot.InputWindow.Prune(now); snapshot.OutputWindow.Prune(now);
            if (result.Delayed || !result.Active)
            {
                // A delayed or inactive observation supplies no coverage and cannot be the next interval's endpoint.
                snapshot.HasFlowBaseline = false;
                if (result.Delayed) snapshot.DelayedCycles++;
                return;
            }
            ulong generation = result.Generation;
            double gap = now - snapshot.FlowBaselineHours;
            if (snapshot.HasFlowBaseline && snapshot.FlowBaselineGeneration == generation && gap > 0 && gap <= IndustrialTiming.MaxSampleGapHours)
            {
                // Covered interval carries the earlier validated sample; gaps and other generations are never bridged.
                snapshot.InputWindow.Add(snapshot.FlowBaselineHours, now, snapshot.FlowBaselineInput.AffectedWeight, snapshot.FlowBaselineInput.ValidWeight);
                snapshot.OutputWindow.Add(snapshot.FlowBaselineHours, now, snapshot.FlowBaselineOutput.AffectedWeight, snapshot.FlowBaselineOutput.ValidWeight);
            }
            snapshot.HasFlowBaseline = true;
            snapshot.FlowBaselineHours = now;
            snapshot.FlowBaselineGeneration = generation;
            snapshot.FlowBaselineInput = result.InputSample;
            snapshot.FlowBaselineOutput = result.OutputSample;
        }
        private static IndustrialChannelInputs ChannelInputs(IndustrialFlowWindow window, in IndustrialFlowSample sample, double now)
        {
            var (affected, valid) = window.Totals(now);
            double covered = window.CoveredHours(now);
            return new IndustrialChannelInputs {
                Applicable = sample.Applicable > 0 || covered > 0, Supported = sample.ValidWeight > 0 || covered > 0,
                AffectedWeightedTime = affected, ValidWeightedTime = valid, CoveredHours = covered, AnyUnavailable = sample.Unavailable > 0 };
        }
    }
}
