using CS2AreaSystem.Core.Policies;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaPolicyRegistryTests {
    internal static AreaPolicyDefinition Definition(int id, int minLevel = 1, string conflictGroup = null, params int[] types) =>
        new AreaPolicyDefinition(id, "CS2AS.Policy.Name." + id, "CS2AS.Policy.Description." + id, "Media/Game/Icons/Policies.svg",
            types.Length == 0 ? new[] { 1 } : types, minLevel, new AreaPolicyUpkeep(100, 0, 0, 0, null), conflictGroup,
            AreaPolicyEffectClass.AreaInternal, "test." + id);
    [Test] public void PolicyIdsAreTheReservedReleasedValues() {
        Assert.That(AreaPolicyIds.FocusedManagement, Is.EqualTo(1001));
        Assert.That(AreaPolicyIds.EnhancedMaintenance, Is.EqualTo(2001));
        Assert.That(AreaPolicyIds.NightEvents, Is.EqualTo(2002));
        Assert.That(AreaPolicyIds.QuietOperations, Is.EqualTo(2003));
        Assert.That(AreaPolicyIds.LandscapeProtection, Is.EqualTo(2004));
        Assert.That(AreaPolicyIds.PremiumServices, Is.EqualTo(2005));
        Assert.That(AreaPolicyIds.MajorEventAttraction, Is.EqualTo(2006));
        Assert.That(AreaPolicyIds.EnhancedSafetyAudits, Is.EqualTo(3001));
        Assert.That(AreaPolicyIds.EnvironmentalRegulation, Is.EqualTo(3002));
        Assert.That(AreaPolicyIds.ProductionPriority, Is.EqualTo(3003));
        Assert.That(AreaPolicyIds.LogisticsPriority, Is.EqualTo(3004));
        Assert.That(AreaPolicyIds.TwentyFourHourOperations, Is.EqualTo(3005));
        Assert.That(AreaPolicyIds.RestrictedOperatingHours, Is.EqualTo(3006));
        Assert.That(AreaPolicyIds.AdvancedAutomation, Is.EqualTo(3007));
    }
    [Test] public void DuplicateRegistrationIsRejectedWithoutReplacingTheFirstDefinition() {
        var registry = new AreaPolicyRegistry();
        var first = Definition(2001);
        Assert.That(registry.Register(first), Is.True);
        Assert.That(registry.Register(Definition(2001, minLevel: 3)), Is.False);
        Assert.That(registry.TryGet(2001, out var actual), Is.True);
        Assert.That(actual, Is.SameAs(first));
        Assert.That(registry.TryGet(2999, out _), Is.False);
        Assert.Throws<System.ArgumentNullException>(() => registry.Register(null));
    }
    [Test] public void ForAreaTypeReturnsOnlySupportingDefinitionsOrderedByPolicyId() {
        var registry = new AreaPolicyRegistry();
        registry.Register(Definition(2003, types: 1));
        registry.Register(Definition(3001, types: 2));
        registry.Register(Definition(1001, types: new[] { 1, 2 }));
        registry.Register(Definition(2001, types: 1));
        Assert.That(registry.ForAreaType(1).Select(d => d.PolicyId), Is.EqualTo(new[] { 1001, 2001, 2003 }));
        Assert.That(registry.ForAreaType(2).Select(d => d.PolicyId), Is.EqualTo(new[] { 1001, 3001 }));
        Assert.That(registry.ForAreaType(3), Is.Empty, "Tourism has no policies in this release");
    }
    [Test] public void DefinitionCopiesSupportedAreaTypes() {
        var types = new[] { 1 };
        var definition = new AreaPolicyDefinition(2001, "n", "d", "i", types, 1, new AreaPolicyUpkeep(1, 0, 0, 0, null), null, AreaPolicyEffectClass.AreaInternal, "h");
        types[0] = 2;
        Assert.That(definition.AreaTypeIds, Is.EqualTo(new[] { 1 }));
        Assert.That(definition.Supports(1), Is.True);
        Assert.That(definition.Supports(2), Is.False);
    }
    [TestCase(0.0)] [TestCase(-1.0)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
    public void EveryDefinitionRequiresPositiveFiniteBaseUpkeep(double baseDaily) {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new AreaPolicyDefinition(2001, "n", "d", "i", new[] { 1 }, 1,
            new AreaPolicyUpkeep(baseDaily, 0, 0, 0, null), null, AreaPolicyEffectClass.AreaInternal, "h"));
    }
    [TestCase(-1.0, 0.0, 0.0)] [TestCase(0.0, double.NaN, 0.0)] [TestCase(0.0, 0.0, double.PositiveInfinity)]
    public void ScalingCoefficientsMustBeNonNegativeAndFinite(double perHectare, double perFacility, double perActivity) {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new AreaPolicyDefinition(2001, "n", "d", "i", new[] { 1 }, 1,
            new AreaPolicyUpkeep(1, perHectare, perFacility, perActivity, null), null, AreaPolicyEffectClass.AreaInternal, "h"));
    }
    [TestCase(0)] [TestCase(6)]
    public void MinimumLevelIsWithinTheAreaLevelRange(int minLevel) {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => Definition(2001, minLevel));
    }
    [Test] public void DefinitionRejectsMissingIdentity() {
        var upkeep = new AreaPolicyUpkeep(1, 0, 0, 0, null);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new AreaPolicyDefinition(0, "n", "d", "i", new[] { 1 }, 1, upkeep, null, AreaPolicyEffectClass.AreaInternal, "h"));
        Assert.Throws<System.ArgumentException>(() => new AreaPolicyDefinition(2001, "", "d", "i", new[] { 1 }, 1, upkeep, null, AreaPolicyEffectClass.AreaInternal, "h"));
        Assert.Throws<System.ArgumentException>(() => new AreaPolicyDefinition(2001, "n", "d", "i", new int[0], 1, upkeep, null, AreaPolicyEffectClass.AreaInternal, "h"));
        Assert.Throws<System.ArgumentException>(() => new AreaPolicyDefinition(2001, "n", "d", "i", new[] { 1 }, 1, upkeep, null, AreaPolicyEffectClass.AreaInternal, " "));
        Assert.Throws<System.ArgumentException>(() => new AreaPolicyDefinition(2001, "n", "d", "i", new[] { 1 }, 1, upkeep, " ", AreaPolicyEffectClass.AreaInternal, "h"),
            "a conflict group is either absent or a real key");
    }
}
