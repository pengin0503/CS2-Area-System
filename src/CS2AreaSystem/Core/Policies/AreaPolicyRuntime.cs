using System;
using System.Collections.Generic;
using System.Linq;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Core.Policies
{
    public readonly struct AreaPolicyAreaInfo
    {
        public ulong StableId { get; }
        public int TypeId { get; }
        public AreaLifecycleState State { get; }
        public int Level { get; }
        public bool Deleted { get; }
        public AreaPolicyAreaInfo(ulong stableId, int typeId, AreaLifecycleState state, int level, bool deleted)
        { StableId = stableId; TypeId = typeId; State = state; Level = level; Deleted = deleted; }
    }
    // Entity access for the runtime; AreaPolicySystem backs it with the Area's ManagedAreaPolicyEntry buffer.
    public interface IAreaPolicyStore
    {
        bool TryGetArea(Entity area, out AreaPolicyAreaInfo info);
        bool TryRead(Entity area, List<ManagedAreaPolicyEntry> into);
        void Write(Entity area, IReadOnlyList<ManagedAreaPolicyEntry> entries);
    }
    // Resolves per-Area policy state from the saved desired entries every time; derived state is never stored.
    public sealed class AreaPolicyRuntime
    {
        public const double FocusedManagementMultiplier = AreaPolicyExperience.FocusedManagementMultiplier;
        private const string AvailabilityFailure = "availability", UpkeepInputsFailure = "upkeep-inputs";
        private readonly Dictionary<int, IAreaPolicyProvider> owners = new Dictionary<int, IAreaPolicyProvider>();
        private readonly AreaPolicyFailureLog failures = new AreaPolicyFailureLog();
        private readonly Action<string> reportError;
        private readonly List<ManagedAreaPolicyEntry> entries = new List<ManagedAreaPolicyEntry>();
        public AreaPolicyRegistry Registry { get; } = new AreaPolicyRegistry();
        public int LoadGeneration => failures.Generation;
        private AreaPolicyRuntime(Action<string> reportError) { this.reportError = reportError ?? (_ => { }); }
        // Common definitions first, then each module's own definitions in Area-type order. A module may only define
        // policies for its own Area type; duplicates never replace an earlier definition; a failing module is skipped.
        public static AreaPolicyRuntime Build(IAreaPolicyProvider common, IEnumerable<KeyValuePair<int, IAreaPolicyProvider>> modules, Action<string> reportError)
        {
            var runtime = new AreaPolicyRuntime(reportError);
            if (common != null) runtime.RegisterFrom(common, null);
            foreach (var module in (modules ?? Enumerable.Empty<KeyValuePair<int, IAreaPolicyProvider>>()).OrderBy(m => m.Key))
                if (module.Value != null) runtime.RegisterFrom(module.Value, module.Key);
            return runtime;
        }
        private void RegisterFrom(IAreaPolicyProvider provider, int? moduleType)
        {
            IReadOnlyList<AreaPolicyDefinition> definitions;
            try { definitions = provider.PolicyDefinitions ?? new AreaPolicyDefinition[0]; }
            catch (Exception error) { reportError("Area policy definitions unavailable for Area type " + moduleType + ": " + error.Message); return; }
            foreach (var definition in definitions)
            {
                if (definition == null) continue;
                if (moduleType.HasValue && definition.AreaTypeIds.Any(t => t != moduleType.Value))
                { reportError("Area policy " + definition.PolicyId + " targets another Area type; ignored."); continue; }
                if (!Registry.Register(definition))
                { reportError("Duplicate Area policy ID " + definition.PolicyId + "; first definition kept."); continue; }
                owners[definition.PolicyId] = provider;
            }
        }
        public void NewLoadGeneration() => failures.NewGeneration();
        public AreaPolicyRuntimeState GetState(IAreaPolicyStore store, Entity area, int policyId, in AreaPolicyContext context)
        {
            if (!store.TryGetArea(area, out var info) || info.Deleted)
                return new AreaPolicyRuntimeState(false, false, false, true, false, AreaPolicyReasons.AreaUnavailable);
            store.TryRead(area, entries);
            bool desired = IsDesired(entries, policyId);
            if (!Registry.TryGet(policyId, out var definition))
                return new AreaPolicyRuntimeState(false, desired, false, info.State != AreaLifecycleState.Active, false, AreaPolicyReasons.Unknown);
            return Resolve(area, info, definition, desired, context);
        }
        public bool IsEffective(IAreaPolicyStore store, Entity area, int policyId, in AreaPolicyContext context) => GetState(store, area, policyId, context).Effective;
        public double GetExperienceMultiplier(IAreaPolicyStore store, Entity area, in AreaPolicyContext context) =>
            IsEffective(store, area, AreaPolicyIds.FocusedManagement, context) ? FocusedManagementMultiplier : 1.0;
        // Upkeep formula inputs: Core geometry for per-hectare upkeep, the owning module's stable counts for per-facility or
        // per-activity upkeep. Missing or failing module inputs count as zero, so base and per-hectare upkeep still apply.
        public AreaPolicyUpkeepInputs GetUpkeepInputs(Entity area, ulong stableId, AreaPolicyDefinition definition, in AreaPolicyContext context, Func<double> areaSquareMeters)
        {
            var inputs = new AreaPolicyUpkeepInputs();
            var upkeep = definition.Upkeep;
            if (upkeep.PerHectare > 0 && areaSquareMeters != null)
            {
                double squareMeters = areaSquareMeters();
                inputs.AreaSquareMeters = double.IsNaN(squareMeters) || double.IsInfinity(squareMeters) ? 0 : Math.Max(0, squareMeters);
            }
            if ((upkeep.PerEligibleFacility > 0 || upkeep.PerActivityUnit > 0) && owners.TryGetValue(definition.PolicyId, out var owner) && owner is IAreaPolicyUpkeepProvider provider)
            {
                try
                {
                    if (provider.TryGetPolicyUpkeepInputs(area, definition.PolicyId, context, out var module))
                    {
                        inputs.EligibleFacilityCount = Math.Max(0, module.EligibleFacilityCount);
                        inputs.ActivityUnits = double.IsNaN(module.ActivityUnits) || double.IsInfinity(module.ActivityUnits) ? 0 : Math.Max(0, module.ActivityUnits);
                    }
                }
                catch (Exception error)
                {
                    if (failures.ShouldLog(stableId, definition.PolicyId, UpkeepInputsFailure))
                        reportError("Area policy " + definition.PolicyId + " upkeep inputs failed for Area " + stableId + "; base upkeep is charged: " + error.Message);
                }
            }
            return inputs;
        }
        public double EstimateDailyUpkeep(IAreaPolicyStore store, Entity area, AreaPolicyDefinition definition, in AreaPolicyContext context, Func<double> areaSquareMeters)
        {
            ulong stableId = store.TryGetArea(area, out var info) ? info.StableId : 0;
            return AreaPolicyUpkeepSettlement.DailyUpkeep(definition, GetUpkeepInputs(area, stableId, definition, context, areaSquareMeters));
        }
        // Validates and applies one toggle as a single buffer write: conflicting policies switch OFF in the same write
        // that switches this one ON. Enabling also requires the effect to be available; turning OFF is always allowed.
        public bool TrySetDesired(IAreaPolicyStore store, Entity area, int policyId, bool enabled, in AreaPolicyContext context, out string reasonKey)
        {
            if (!store.TryGetArea(area, out var info) || info.Deleted || info.State == AreaLifecycleState.Deleted ||
                info.State == AreaLifecycleState.ModuleUnavailable || !store.TryRead(area, entries))
            { reasonKey = AreaPolicyReasons.AreaUnavailable; return false; }
            var desiredIds = entries.Where(e => e.IsDesiredEnabled).Select(e => e.PolicyId).ToArray();
            var transition = AreaPolicyModel.EvaluateToggle(Registry, info.TypeId, NormalizeLevel(info.Level), desiredIds, policyId, enabled);
            if (!transition.Accepted) { reasonKey = transition.ReasonKey; return false; }
            if (enabled && !IsAvailable(area, info, policyId, context, out reasonKey)) return false;
            var next = new List<ManagedAreaPolicyEntry>(entries);
            bool changed = false, found = false;
            for (int i = 0; i < next.Count; i++)
            {
                var entry = next[i];
                bool target = entry.PolicyId == policyId;
                if (!target && !transition.DisablePolicyIds.Contains(entry.PolicyId)) continue;
                bool value = target && transition.DesiredEnabled;
                found |= target;
                if (entry.IsDesiredEnabled == value && (entry.DesiredEnabled == 0 || entry.DesiredEnabled == 1)) continue;
                next[i] = new ManagedAreaPolicyEntry(entry.PolicyId, value);
                changed = true;
            }
            if (!found && transition.DesiredEnabled) { next.Add(new ManagedAreaPolicyEntry(policyId, true)); changed = true; }
            if (changed) store.Write(area, next);
            reasonKey = null;
            return true;
        }
        private AreaPolicyRuntimeState Resolve(Entity area, in AreaPolicyAreaInfo info, AreaPolicyDefinition definition, bool desired, in AreaPolicyContext context)
        {
            bool suspended = info.State != AreaLifecycleState.Active;
            if (!definition.Supports(info.TypeId))
                return new AreaPolicyRuntimeState(false, desired, false, suspended, false, AreaPolicyReasons.UnsupportedAreaType);
            bool unlocked = NormalizeLevel(info.Level) >= definition.MinLevel;
            bool available = IsAvailable(area, info, definition.PolicyId, context, out var unavailableReason);
            bool shadowed = desired && IsShadowed(definition, info.TypeId);
            bool effective = unlocked && desired && available && !suspended && !shadowed;
            string reason = !unlocked ? AreaPolicyReasons.Locked
                : !available ? unavailableReason
                : shadowed ? AreaPolicyReasons.Conflict
                : suspended ? AreaPolicyReasons.Suspended
                : null;
            return new AreaPolicyRuntimeState(unlocked, desired, available, suspended, effective, reason);
        }
        // Toggles keep a conflict group exclusive. If saved data still holds two desired policies of one group, only
        // the lowest PolicyId takes effect so there is never a frame with both effects or both upkeep charges.
        private bool IsShadowed(AreaPolicyDefinition definition, int typeId)
        {
            if (definition.ConflictGroup == null) return false;
            foreach (var entry in entries)
                if (entry.IsDesiredEnabled && entry.PolicyId < definition.PolicyId && Registry.TryGet(entry.PolicyId, out var other) &&
                    other.Supports(typeId) && string.Equals(other.ConflictGroup, definition.ConflictGroup, StringComparison.Ordinal))
                    return true;
            return false;
        }
        private bool IsAvailable(Entity area, in AreaPolicyAreaInfo info, int policyId, in AreaPolicyContext context, out string reasonKey)
        {
            if (!owners.TryGetValue(policyId, out var owner)) { reasonKey = AreaPolicyReasons.Unknown; return false; }
            try
            {
                bool available = owner.IsPolicyEffectAvailable(area, policyId, context, out reasonKey);
                failures.Clear(info.StableId, policyId, AvailabilityFailure);
                if (!available && reasonKey == null) reasonKey = AreaPolicyReasons.EffectUnavailable;
                return available;
            }
            catch (Exception error)
            {
                if (failures.ShouldLog(info.StableId, policyId, AvailabilityFailure))
                    reportError("Area policy " + policyId + " effect check failed for Area " + info.StableId + "; effect disabled: " + error.Message);
                reasonKey = AreaPolicyReasons.EffectUnavailable;
                return false;
            }
        }
        private static bool IsDesired(List<ManagedAreaPolicyEntry> list, int policyId)
        {
            foreach (var entry in list) if (entry.PolicyId == policyId && entry.IsDesiredEnabled) return true;
            return false;
        }
        private static int NormalizeLevel(int level) =>
            level < AreaPolicyDefinition.MinAreaLevel ? AreaPolicyDefinition.MinAreaLevel : level > AreaPolicyDefinition.MaxAreaLevel ? AreaPolicyDefinition.MaxAreaLevel : level;
    }
}
