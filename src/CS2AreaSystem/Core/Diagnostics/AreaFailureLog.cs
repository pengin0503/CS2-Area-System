using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.Diagnostics
{
    // The one failure-log suppression policy (#67/#68), the same one AreaPolicyFailureLog applies to policies: the first
    // failure of an Area (Entity.Null for a system-wide failure) + subject + failure class is logged once per load
    // generation; repeats stay quiet until the generation changes (AreaLoadCoordinatorSystem preload) or the subject
    // recovers for that Area and later fails again. Areas, subjects and failure classes are independent.
    public sealed class AreaFailureLog
    {
        private readonly Dictionary<(Entity Area, string Subject), HashSet<string>> active = new Dictionary<(Entity, string), HashSet<string>>();
        private readonly Action<Exception, string> error;
        private readonly Action<string> warning;
        public int Generation { get; private set; }
        public AreaFailureLog(Action<Exception, string> error, Action<string> warning)
        {
            this.error = error ?? throw new ArgumentNullException(nameof(error));
            this.warning = warning ?? throw new ArgumentNullException(nameof(warning));
        }
        public static string ClassOf(Exception failure) => failure?.GetType().FullName ?? "";
        // True the first time the key is seen in this generation (since its last recovery).
        public bool ShouldReport(Entity area, string subject, string failureClass)
        {
            var key = (area, subject ?? "");
            if (!active.TryGetValue(key, out var classes)) active[key] = classes = new HashSet<string>();
            return classes.Add(failureClass ?? "");
        }
        public bool Report(Entity area, string subject, Exception failure, string message)
        {
            if (!ShouldReport(area, subject, ClassOf(failure))) return false;
            error(failure, message);
            return true;
        }
        public bool Warn(Entity area, string subject, string failureClass, string message)
        {
            if (!ShouldReport(area, subject, failureClass)) return false;
            warning(message);
            return true;
        }
        // The subject works again for this Area: its next failure is diagnosed. Allocation-free when nothing failed.
        public void Recovered(Entity area, string subject)
        {
            if (active.Count != 0) active.Remove((area, subject ?? ""));
        }
        public bool IsSuppressed(Entity area, string subject, string failureClass) =>
            active.TryGetValue((area, subject ?? ""), out var classes) && classes.Contains(failureClass ?? "");
        public void NewGeneration()
        {
            Generation++;
            active.Clear();
        }
    }
}
