using CS2AreaSystem.Core.UI;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class LocalizationSourceRegistrationTests {
    private sealed class Source { public string Locale = ""; }
    private static readonly string[] Locales = { "de-DE", "en-US", "ja-JP", "zh-HANS" };
    [Test] public void RegistersEveryLocaleAndRemovesTheSameSources() {
        var registration = new LocalizationSourceRegistration<Source>();
        var added = new List<(string, Source)>();
        var removed = new List<(string, Source)>();
        Assert.That(registration.Register(Locales, l => new Source { Locale = l }, (l, s) => added.Add((l, s)), (_, e) => Assert.Fail(e.Message)), Is.EqualTo(4));
        Assert.That(added.Select(x => x.Item1), Is.EqualTo(Locales));
        registration.Unregister((l, s) => removed.Add((l, s)), (_, e) => Assert.Fail(e.Message));
        Assert.That(removed, Is.EqualTo(added));
        Assert.That(registration.Count, Is.Zero);
    }
    [Test] public void OneFailingLocaleDoesNotStopTheRemainingLocales() {
        var registration = new LocalizationSourceRegistration<Source>();
        var failures = new List<string>();
        var added = new List<string>();
        registration.Register(Locales, l => new Source { Locale = l }, (l, s) => { if (l == "de-DE") throw new InvalidOperationException("boom"); added.Add(l); }, (l, _) => failures.Add(l));
        Assert.That(failures, Is.EqualTo(new[] { "de-DE" }));
        Assert.That(added, Is.EqualTo(new[] { "en-US", "ja-JP", "zh-HANS" }));
        var removed = new List<string>();
        registration.Unregister((l, _) => removed.Add(l), (_, e) => Assert.Fail(e.Message));
        Assert.That(removed, Is.EqualTo(added), "only successfully added sources are removed");
    }
    [Test] public void RemovalFailureDoesNotStopOtherRemovals() {
        var registration = new LocalizationSourceRegistration<Source>();
        registration.Register(Locales, l => new Source { Locale = l }, (_, _) => { }, (_, e) => Assert.Fail(e.Message));
        var removed = new List<string>();
        var failures = new List<string>();
        registration.Unregister((l, _) => { if (l == "en-US") throw new InvalidOperationException("boom"); removed.Add(l); }, (l, _) => failures.Add(l));
        Assert.That(removed, Is.EqualTo(new[] { "de-DE", "ja-JP", "zh-HANS" }));
        Assert.That(failures, Is.EqualTo(new[] { "en-US" }));
        Assert.That(registration.Count, Is.Zero);
    }
}
