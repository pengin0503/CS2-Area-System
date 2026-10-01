using CS2AreaSystem.Core.Policies;
using NUnit.Framework;
using static CS2AreaSystem.Tests.AreaPolicyRegistryTests;
namespace CS2AreaSystem.Tests;
public class AreaPolicyModelTests {
    private const int Park = 1, Industrial = 2;
    private static AreaPolicyRegistry Registry() {
        var registry = new AreaPolicyRegistry();
        registry.Register(Definition(1001, 2, null, Park, Industrial));
        registry.Register(Definition(2001, 1, null, Park));
        registry.Register(Definition(2002, 2, "park.activity", Park));
        registry.Register(Definition(2003, 2, "park.activity", Park));
        registry.Register(Definition(2004, 3, null, Park));
        registry.Register(Definition(3002, 2, "industrial.environment-production", Industrial));
        return registry;
    }
    [Test] public void NewPolicyEntryIsOffByDefault() {
        Assert.That(default(ManagedAreaPolicyEntry).IsDesiredEnabled, Is.False);
        Assert.That(new ManagedAreaPolicyEntry(2001, false).IsDesiredEnabled, Is.False);
        Assert.That(new ManagedAreaPolicyEntry(2001, true).IsDesiredEnabled, Is.True);
    }
    [Test] public void EnablingAnUnlockedPolicyIsAcceptedWithNothingToDisable() {
        var transition = AreaPolicyModel.EvaluateToggle(Registry(), Park, 1, new int[0], 2001, true);
        Assert.That(transition.Accepted, Is.True);
        Assert.That(transition.PolicyId, Is.EqualTo(2001));
        Assert.That(transition.DesiredEnabled, Is.True);
        Assert.That(transition.DisablePolicyIds, Is.Empty);
        Assert.That(transition.ReasonKey, Is.Null);
    }
    [Test] public void LevelBelowMinimumLocksEnablingButNeverBlocksTurningOff() {
        var locked = AreaPolicyModel.EvaluateToggle(Registry(), Park, 2, new int[0], 2004, true);
        Assert.That(locked.Accepted, Is.False);
        Assert.That(locked.ReasonKey, Is.EqualTo(AreaPolicyReasons.Locked));
        Assert.That(AreaPolicyModel.EvaluateToggle(Registry(), Park, 3, new int[0], 2004, true).Accepted, Is.True);
        var off = AreaPolicyModel.EvaluateToggle(Registry(), Park, 2, new[] { 2004 }, 2004, false);
        Assert.That(off.Accepted, Is.True);
        Assert.That(off.DesiredEnabled, Is.False);
    }
    [Test] public void EnablingAConflictingPolicyDisablesOnlyTheSameGroupInOneTransition() {
        var transition = AreaPolicyModel.EvaluateToggle(Registry(), Park, 5, new[] { 1001, 2001, 2002 }, 2003, true);
        Assert.That(transition.Accepted, Is.True);
        Assert.That(transition.DisablePolicyIds, Is.EqualTo(new[] { 2002 }));
        Assert.That(transition.DisablePolicyIds, Does.Not.Contain(1001).And.Not.Contain(2001), "unrelated policies stay enabled");
    }
    [Test] public void TurningOffNeverDisablesOtherPolicies() {
        var transition = AreaPolicyModel.EvaluateToggle(Registry(), Park, 5, new[] { 2002, 2001 }, 2002, false);
        Assert.That(transition.Accepted, Is.True);
        Assert.That(transition.DisablePolicyIds, Is.Empty);
    }
    [Test] public void ThereIsNoSimultaneousPolicyCap() {
        var registry = new AreaPolicyRegistry();
        var enabled = Enumerable.Range(2001, 40).ToArray();
        foreach (var id in enabled) registry.Register(Definition(id, 1, null, Park));
        registry.Register(Definition(2100, 1, null, Park));
        var transition = AreaPolicyModel.EvaluateToggle(registry, Park, 1, enabled, 2100, true);
        Assert.That(transition.Accepted, Is.True);
        Assert.That(transition.DisablePolicyIds, Is.Empty);
    }
    [TestCase(true)] [TestCase(false)]
    public void UnknownPolicyIdIsRejected(bool enable) {
        var transition = AreaPolicyModel.EvaluateToggle(Registry(), Park, 5, new int[0], 2999, enable);
        Assert.That(transition.Accepted, Is.False);
        Assert.That(transition.ReasonKey, Is.EqualTo(AreaPolicyReasons.Unknown));
    }
    [Test] public void PolicyOfAnotherAreaTypeIsRejected() {
        var transition = AreaPolicyModel.EvaluateToggle(Registry(), Industrial, 5, new int[0], 2001, true);
        Assert.That(transition.Accepted, Is.False);
        Assert.That(transition.ReasonKey, Is.EqualTo(AreaPolicyReasons.UnsupportedAreaType));
    }
    [Test] public void SavedIdsWithoutADefinitionAreNeverTouchedByAConflictSwap() {
        var transition = AreaPolicyModel.EvaluateToggle(Registry(), Park, 5, new[] { 2999, 2002 }, 2003, true);
        Assert.That(transition.DisablePolicyIds, Is.EqualTo(new[] { 2002 }));
    }
    [Test] public void ConflictsAreScopedToTheAreaTypeBeingChanged() {
        var registry = Registry();
        registry.Register(Definition(3003, 2, "industrial.environment-production", Industrial));
        var transition = AreaPolicyModel.EvaluateToggle(registry, Industrial, 5, new[] { 3002, 1001 }, 3003, true);
        Assert.That(transition.DisablePolicyIds, Is.EqualTo(new[] { 3002 }));
    }
    [Test] public void MissingInputsAreProgrammingErrors() {
        Assert.Throws<System.ArgumentNullException>(() => AreaPolicyModel.EvaluateToggle(null, Park, 1, new int[0], 2001, true));
        Assert.Throws<System.ArgumentNullException>(() => AreaPolicyModel.EvaluateToggle(Registry(), Park, 1, null, 2001, true));
    }
}
