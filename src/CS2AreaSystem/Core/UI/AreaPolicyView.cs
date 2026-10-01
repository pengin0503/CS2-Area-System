using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Colossal.UI.Binding;
using CS2AreaSystem.Core.Policies;
namespace CS2AreaSystem.Core.UI
{
    // One row of the policy section. Every field is decided by C#; React only maps these flags onto the vanilla
    // Policy component and never derives unlock, conflict, effective state or upkeep itself.
    public sealed class AreaPolicyView
    {
        public int PolicyId, MinLevel;
        public string NameKey, DescriptionKey, Icon, ReasonKey;
        public bool DesiredEnabled, Effective, Locked, Suspended, EffectAvailable;
        public long EstimatedDailyUpkeep;
        public bool SameAs(AreaPolicyView other) => other != null && PolicyId == other.PolicyId && MinLevel == other.MinLevel &&
            NameKey == other.NameKey && DescriptionKey == other.DescriptionKey && Icon == other.Icon && ReasonKey == other.ReasonKey &&
            DesiredEnabled == other.DesiredEnabled && Effective == other.Effective && Locked == other.Locked && Suspended == other.Suspended &&
            EffectAvailable == other.EffectAvailable && EstimatedDailyUpkeep == other.EstimatedDailyUpkeep;
        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin("CS2AreaSystem.AreaPolicyView");
            writer.PropertyName("policyId"); writer.Write(PolicyId);
            writer.PropertyName("nameKey"); writer.Write(NameKey);
            writer.PropertyName("descriptionKey"); writer.Write(DescriptionKey);
            writer.PropertyName("icon"); if (Icon != null) writer.Write(Icon); else writer.WriteNull();
            writer.PropertyName("minLevel"); writer.Write(MinLevel);
            writer.PropertyName("desiredEnabled"); writer.Write(DesiredEnabled);
            writer.PropertyName("effective"); writer.Write(Effective);
            writer.PropertyName("locked"); writer.Write(Locked);
            writer.PropertyName("suspended"); writer.Write(Suspended);
            writer.PropertyName("effectAvailable"); writer.Write(EffectAvailable);
            writer.PropertyName("reasonKey"); if (ReasonKey != null) writer.Write(ReasonKey); else writer.WriteNull();
            writer.PropertyName("estimatedDailyUpkeep"); writer.Write(EstimatedDailyUpkeep);
            writer.TypeEnd();
        }
    }
    // Props of the policy section (CS2AreaSystem.Core.UI.AreaPolicyInfoSection) of the selected managed Area.
    public sealed class AreaPolicySectionView
    {
        public string StableId;
        public IReadOnlyList<AreaPolicyView> Policies = new AreaPolicyView[0];
        public static bool Equivalent(AreaPolicySectionView a, AreaPolicySectionView b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.StableId != b.StableId) return false;
            var left = a.Policies ?? new AreaPolicyView[0];
            var right = b.Policies ?? new AreaPolicyView[0];
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++) if (left[i] == null ? right[i] != null : !left[i].SameAs(right[i])) return false;
            return true;
        }
        public void WriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("stableId"); writer.Write(StableId);
            var policies = Policies ?? new AreaPolicyView[0];
            writer.PropertyName("policies"); writer.ArrayBegin((uint)policies.Count);
            foreach (var policy in policies) policy.Write(writer);
            writer.ArrayEnd();
        }
    }
    public static class AreaPolicyViews
    {
        // Stable order: Core's common policies first, then the module's policies by PolicyId.
        public static IReadOnlyList<AreaPolicyView> Build(IEnumerable<AreaPolicyDefinition> definitions, Func<int, AreaPolicyRuntimeState> state,
            Func<AreaPolicyDefinition, double> estimateDaily)
        {
            var common = new HashSet<int>(CommonAreaPolicies.Definitions.Select(d => d.PolicyId));
            return definitions.OrderBy(d => common.Contains(d.PolicyId) ? 0 : 1).ThenBy(d => d.PolicyId).Select(d => {
                var s = state(d.PolicyId);
                return new AreaPolicyView {
                    PolicyId = d.PolicyId, MinLevel = d.MinLevel, NameKey = d.NameKey, DescriptionKey = d.DescriptionKey, Icon = d.IconUri,
                    DesiredEnabled = s.DesiredEnabled, Effective = s.Effective, Locked = !s.Unlocked, Suspended = s.Suspended,
                    EffectAvailable = s.EffectAvailable, ReasonKey = s.ReasonKey, EstimatedDailyUpkeep = RoundCurrency(estimateDaily(d))
                };
            }).ToArray();
        }
        // Display rounding of the estimated daily upkeep: nearest whole currency unit, halves away from zero.
        public static long RoundCurrency(double value) =>
            double.IsNaN(value) || value <= 0 ? 0 : value >= long.MaxValue ? long.MaxValue : (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }
    // Arguments of cs2AreaSystem/setPolicyEnabled(stableId, policyId, enabled). Only canonical positive integers and
    // "1"/"0" are accepted; everything else is a malformed request and changes nothing.
    public static class AreaPolicyToggleRequest
    {
        public static bool TryParse(string policyIdText, string enabledText, out int policyId, out bool enabled)
        {
            enabled = enabledText == "1";
            if ((enabledText != "1" && enabledText != "0") || policyIdText == null ||
                !int.TryParse(policyIdText, NumberStyles.None, CultureInfo.InvariantCulture, out policyId) || policyId <= 0)
            { policyId = 0; return false; }
            return true;
        }
    }
}
