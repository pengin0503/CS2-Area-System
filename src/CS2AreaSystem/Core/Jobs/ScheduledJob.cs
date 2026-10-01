using System;
namespace CS2AreaSystem.Core.Jobs
{
    public enum ScheduledJobOutcome { NotScheduled, Scheduled, CompletedAfterRegistrationFailure }
    // Separates "before Schedule" from "after Schedule" (#58). A failure before the job is scheduled leaves its native
    // containers unused, so the caller may dispose them. Once Schedule returned, the job owns its containers: a failing
    // reader/writer registration (e.g. CityStatisticsSystem.AddWriter, ResourceSystem.AddPrefabsReader) completes the
    // job first, so the caller only ever disposes or clears containers of a finished job and can record its real result.
    public static class ScheduledJob
    {
        public static ScheduledJobOutcome Run<THandle>(Func<THandle> schedule, Action<THandle> register, Action<THandle> complete,
            out THandle handle, out Exception error)
        {
            handle = default;
            error = null;
            try { handle = schedule(); }
            catch (Exception scheduleError) { error = scheduleError; return ScheduledJobOutcome.NotScheduled; }
            try
            {
                register(handle);
                return ScheduledJobOutcome.Scheduled;
            }
            catch (Exception registrationError)
            {
                error = registrationError;
                complete(handle);
                return ScheduledJobOutcome.CompletedAfterRegistrationFailure;
            }
        }
    }
}
