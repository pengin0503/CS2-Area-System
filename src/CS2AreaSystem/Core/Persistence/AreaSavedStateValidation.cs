using System;
using System.Collections.Generic;
using System.Globalization;
using CS2AreaSystem.Core.Areas;
using Unity.Entities;
namespace CS2AreaSystem.Core.Persistence
{
    // Semantic check of an Area's saved components after a load (#69). Deserialize stays a plain schema read; nothing here
    // writes a component. Invalid: a value the models cannot interpret (non-finite, a negative counter, a Level outside
    // 1..5, a satisfaction aggregate outside its bounds), so the Area is quarantined instead of feeding it downstream or
    // letting a progression step rewrite it. Warning: a finite value the models already clamp whenever they read it
    // (budget, admission fee); it keeps working and is only reported.
    public enum AreaSavedValueSeverity { Warning, Invalid }
    public readonly struct AreaSavedStateIssue
    {
        public string Field { get; }
        public string Value { get; }
        public AreaSavedValueSeverity Severity { get; }
        public AreaSavedStateIssue(string field, string value, AreaSavedValueSeverity severity) { Field = field; Value = value; Severity = severity; }
        public override string ToString() => Field + "=" + Value;
    }
    public sealed class AreaSavedStateReport
    {
        private readonly List<AreaSavedStateIssue> issues = new List<AreaSavedStateIssue>();
        public IReadOnlyList<AreaSavedStateIssue> Issues => issues;
        public bool IsInvalid { get; private set; }
        public bool HasWarnings { get; private set; }
        public void Clear() { issues.Clear(); IsInvalid = HasWarnings = false; }
        public void Invalid(string field, object value) { issues.Add(new AreaSavedStateIssue(field, Format(value), AreaSavedValueSeverity.Invalid)); IsInvalid = true; }
        public void Warning(string field, object value) { issues.Add(new AreaSavedStateIssue(field, Format(value), AreaSavedValueSeverity.Warning)); HasWarnings = true; }
        public string Describe(AreaSavedValueSeverity severity)
        {
            var parts = new List<string>();
            foreach (var issue in issues) if (issue.Severity == severity) parts.Add(issue.ToString());
            return string.Join(", ", parts);
        }
        // Helpers shared by the module validators.
        public void RequireFinite(string field, double value) { if (double.IsNaN(value) || double.IsInfinity(value)) Invalid(field, value); }
        public void RequireFiniteNonNegative(string field, double value) { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) Invalid(field, value); }
        public void RequireNonNegative(string field, long value) { if (value < 0) Invalid(field, value); }
        private static string Format(object value) => Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    // Implemented by an Area module that owns saved components (Park, Industrial).
    public interface IAreaSavedStateValidator
    {
        void ValidateSavedState(Entity area, EntityManager entities, AreaSavedStateReport report);
    }
    public static class CoreSavedStateValidator
    {
        public const float MinBudget = 0.5f, MaxBudget = 1.5f;
        public static void ValidateProgress(in ManagedAreaProgress progress, AreaSavedStateReport report)
        {
            if (progress.Level < Policies.AreaPolicyDefinition.MinAreaLevel || progress.Level > Policies.AreaPolicyDefinition.MaxAreaLevel)
                report.Invalid("ManagedAreaProgress.Level", progress.Level);
            report.RequireNonNegative("ManagedAreaProgress.Experience", progress.Experience);
        }
        public static void ValidateSettings(in ManagedAreaSettings settings, AreaSavedStateReport report)
        {
            float budget = settings.BudgetMultiplier;
            if (float.IsNaN(budget) || float.IsInfinity(budget)) report.Invalid("ManagedAreaSettings.BudgetMultiplier", budget);
            else if (budget < MinBudget || budget > MaxBudget) report.Warning("ManagedAreaSettings.BudgetMultiplier", budget);
        }
    }
}
