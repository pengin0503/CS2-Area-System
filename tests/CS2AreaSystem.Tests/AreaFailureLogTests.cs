using CS2AreaSystem.Core.Diagnostics;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #67/#68: one suppression policy for every failure log.
public class AreaFailureLogTests {
    private static readonly Entity A = new() { Index = 1, Version = 1 }, B = new() { Index = 2, Version = 1 };
    private static (AreaFailureLog Log, List<string> Lines) Create() {
        var lines = new List<string>();
        return (new AreaFailureLog((e, m) => lines.Add("E:" + m), m => lines.Add("W:" + m)), lines);
    }
    [Test] public void RepeatedFailureInOneGenerationIsLoggedOnce() {
        var (log, lines) = Create();
        for (int i = 0; i < 5; i++) log.Report(A, "Activation", new InvalidOperationException(), "module failed");
        Assert.That(lines, Is.EqualTo(new[] { "E:module failed" }));
    }
    [Test] public void NewLoadGenerationLogsAgain() {
        var (log, lines) = Create();
        log.Report(A, "Activation", new InvalidOperationException(), "first");
        log.NewGeneration();
        log.Report(A, "Activation", new InvalidOperationException(), "after load");
        Assert.That(lines, Is.EqualTo(new[] { "E:first", "E:after load" }));
        Assert.That(log.Generation, Is.EqualTo(1));
    }
    [Test] public void RecurrenceAfterRecoveryIsLogged() {
        var (log, lines) = Create();
        log.Report(A, "Activation", new InvalidOperationException(), "fail");
        log.Recovered(A, "Activation");
        log.Report(A, "Activation", new InvalidOperationException(), "fail again");
        Assert.That(lines, Is.EqualTo(new[] { "E:fail", "E:fail again" }));
    }
    [Test] public void AreasSubjectsAndFailureClassesAreIndependent() {
        var (log, lines) = Create();
        log.Report(A, "Activation", new InvalidOperationException(), "a");
        log.Report(B, "Activation", new InvalidOperationException(), "b");
        log.Report(A, "Policy", new InvalidOperationException(), "a-policy");
        log.Report(A, "Activation", new ArgumentException(), "a-other-class");
        log.Report(Entity.Null, "Activation", new InvalidOperationException(), "system");
        Assert.That(lines, Has.Count.EqualTo(5));
        log.Recovered(B, "Activation");
        Assert.That(log.IsSuppressed(A, "Activation", AreaFailureLog.ClassOf(new InvalidOperationException())), Is.True, "another Area's recovery changes nothing");
        Assert.That(log.IsSuppressed(A, "Policy", AreaFailureLog.ClassOf(new InvalidOperationException())), Is.True);
    }
    [Test] public void WarningsFollowTheSamePolicy() {
        var (log, lines) = Create();
        log.Warn(A, "Industrial.Read", "Unreadable", "skipped"); log.Warn(A, "Industrial.Read", "Unreadable", "skipped");
        log.Recovered(A, "Industrial.Read");
        log.Warn(A, "Industrial.Read", "Unreadable", "skipped");
        Assert.That(lines, Is.EqualTo(new[] { "W:skipped", "W:skipped" }));
    }
    [Test] public void NoSystemKeepsAPrivateLogOnceFlag() {
        // Every once-per-session flag was replaced by AreaFailureLog (or the policy/industrial-effect logs with the same policy).
        var root = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem");
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
            // Prefab registration is session-wide (prefabs outlive loads), so its once-per-session flags are not per generation.
            .Where(f => !f.EndsWith("AreaPrefabRegistrationSystem.cs"))
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"\b\w*(failureLogged|FailureLogged|failuresLogged)\b"))
            .Select(Path.GetFileName).ToList();
        Assert.That(offenders, Is.Empty);
    }
}
