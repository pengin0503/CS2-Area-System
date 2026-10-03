using System;
using System.Globalization;
namespace CS2AreaSystem.Modules.Industrial
{
    // The support setting action (Phase 2 C4). Values are invariant numbers from the panel slider; anything else is
    // rejected. Only the setting changes: the paid-day dedupe and lifetime accounting are never touched by the UI.
    public static class IndustrialSupportActions
    {
        public static bool TryParseSetting(string value, out int setting)
        {
            setting = 0;
            if (value == null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                double.IsNaN(parsed) || double.IsInfinity(parsed)) return false;
            setting = IndustrialSupportModel.NormalizeSetting((int)Math.Round(Math.Min(int.MaxValue, Math.Max(int.MinValue, parsed))));
            return true;
        }
        // available: the support module is registered and enabled; hasState: the Area carries IndustrialSupportState.
        public static bool TryApply(bool available, bool hasState, string value, ref IndustrialSupportState state)
        {
            if (!available || !hasState || !TryParseSetting(value, out var setting)) return false;
            state.DailySupportPerCompany = setting;
            return true;
        }
    }
}
