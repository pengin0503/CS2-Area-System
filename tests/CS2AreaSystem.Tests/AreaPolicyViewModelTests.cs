using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaPolicyViewModelTests {
    private static readonly AreaPolicyRuntimeState Off = new(true, false, true, false, false, null);
    private static IReadOnlyList<AreaPolicyDefinition> ParkDefinitions() {
        var registry = new AreaPolicyRegistry();
        foreach (var d in ParkPolicyCatalog.Definitions.Reverse()) registry.Register(d);
        foreach (var d in CommonAreaPolicies.Definitions) registry.Register(d);
        return registry.ForAreaType((int)AreaTypeId.Park);
    }
    [Test] public void PoliciesAreOrderedCommonFirstThenModuleByPolicyId() {
        var views = AreaPolicyViews.Build(ParkDefinitions(), _ => Off, _ => 0);
        Assert.That(views.Select(v => v.PolicyId), Is.EqualTo(new[] { 1001, 2001, 2002, 2003, 2004, 2005, 2006 }));
    }
    [Test] public void ViewCarriesDefinitionAndRuntimeStateFromCSharp() {
        var states = new Dictionary<int, AreaPolicyRuntimeState> {
            [1001] = new(false, false, true, false, false, AreaPolicyReasons.Locked),
            [2001] = new(true, true, true, true, false, AreaPolicyReasons.Suspended),
            [2002] = new(true, true, false, false, false, AreaPolicyReasons.EffectUnavailable),
        };
        var views = AreaPolicyViews.Build(ParkDefinitions(), id => states.TryGetValue(id, out var s) ? s : Off, d => d.Upkeep.BaseDailyUpkeep + 0.5);
        var locked = views.Single(v => v.PolicyId == 1001);
        Assert.That(locked.Locked, Is.True); Assert.That(locked.MinLevel, Is.EqualTo(2)); Assert.That(locked.ReasonKey, Is.EqualTo(AreaPolicyReasons.Locked));
        Assert.That(locked.NameKey, Is.EqualTo("Policy.TITLE[CS2AS.Policy.FocusedManagement]"));
        Assert.That(locked.Icon, Is.EqualTo("Media/Game/Icons/Administration.svg"));
        Assert.That(locked.EstimatedDailyUpkeep, Is.EqualTo(251), "rounded half away from zero");
        var suspended = views.Single(v => v.PolicyId == 2001);
        Assert.That(suspended.DesiredEnabled, Is.True); Assert.That(suspended.Suspended, Is.True); Assert.That(suspended.Effective, Is.False);
        var unavailable = views.Single(v => v.PolicyId == 2002);
        Assert.That(unavailable.EffectAvailable, Is.False); Assert.That(unavailable.ReasonKey, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
    }
    [Test] public void SectionJsonHasTheDocumentedShape() {
        var section = new AreaPolicySectionView { StableId = "42", Policies = AreaPolicyViews.Build(ParkDefinitions(), id => id == 2001 ? new AreaPolicyRuntimeState(true, true, true, false, true, null) : Off, d => d.Upkeep.BaseDailyUpkeep) };
        var writer = new RecordingJsonWriter();
        writer.MapBegin(0); section.WriteProperties(writer); writer.MapEnd();
        var json = writer.Parse();
        Assert.That(json.GetProperty("stableId").GetString(), Is.EqualTo("42"));
        var first = json.GetProperty("policies")[1];
        Assert.That(first.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "__Type", "policyId", "nameKey", "descriptionKey", "icon", "minLevel",
            "desiredEnabled", "effective", "locked", "suspended", "effectAvailable", "reasonKey", "estimatedDailyUpkeep" }));
        Assert.That(first.GetProperty("policyId").GetInt32(), Is.EqualTo(2001));
        Assert.That(first.GetProperty("effective").GetBoolean(), Is.True);
        Assert.That(first.GetProperty("reasonKey").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
        Assert.That(first.GetProperty("estimatedDailyUpkeep").GetInt64(), Is.EqualTo(300));
    }
    [Test] public void EquivalenceDetectsEveryPublishedChange() {
        AreaPolicySectionView Make() => new() { StableId = "42", Policies = AreaPolicyViews.Build(ParkDefinitions(), _ => Off, _ => 10) };
        Assert.That(AreaPolicySectionView.Equivalent(Make(), Make()), Is.True);
        Assert.That(AreaPolicySectionView.Equivalent(null, null), Is.True);
        Assert.That(AreaPolicySectionView.Equivalent(Make(), null), Is.False);
        var changes = new Action<AreaPolicyView>[] {
            v => v.DesiredEnabled = true, v => v.Effective = true, v => v.Locked = true, v => v.Suspended = true, v => v.EffectAvailable = false,
            v => v.ReasonKey = "x", v => v.EstimatedDailyUpkeep = 11, v => v.Icon = "y", v => v.MinLevel = 4, v => v.NameKey = "n", v => v.DescriptionKey = "d" };
        foreach (var change in changes) {
            var changed = Make(); change(changed.Policies[2]);
            Assert.That(AreaPolicySectionView.Equivalent(Make(), changed), Is.False);
        }
        var other = Make(); other.StableId = "43";
        Assert.That(AreaPolicySectionView.Equivalent(Make(), other), Is.False);
    }
    [Test] public void ConflictToggleRefreshShowsOldPolicyOffAndNewPolicyOn() {
        var store = new Dictionary<int, bool> { [2002] = true };
        AreaPolicyRuntimeState State(int id) => new(true, store.TryGetValue(id, out var on) && on, true, false, store.TryGetValue(id, out var e) && e, null);
        var before = AreaPolicyViews.Build(ParkDefinitions(), State, _ => 0);
        store[2002] = false; store[2003] = true;
        var after = AreaPolicyViews.Build(ParkDefinitions(), State, _ => 0);
        Assert.That(before.Single(v => v.PolicyId == 2002).DesiredEnabled, Is.True);
        Assert.That(after.Single(v => v.PolicyId == 2002).DesiredEnabled, Is.False);
        Assert.That(after.Single(v => v.PolicyId == 2003).DesiredEnabled, Is.True);
        Assert.That(AreaPolicySectionView.Equivalent(new() { StableId = "1", Policies = before }, new() { StableId = "1", Policies = after }), Is.False,
            "the swap republishes the section so both rows refresh together");
    }
    [Test] public void IndustrialViewHasNoParkPolicies() {
        var registry = new AreaPolicyRegistry();
        foreach (var d in CommonAreaPolicies.Definitions.Concat(ParkPolicyCatalog.Definitions).Concat(IndustrialPolicyCatalog.Definitions)) registry.Register(d);
        var views = AreaPolicyViews.Build(registry.ForAreaType((int)AreaTypeId.Industrial), _ => Off, _ => 0);
        Assert.That(views.Select(v => v.PolicyId), Is.EqualTo(new[] { 1001, 3001, 3002, 3003, 3004, 3005, 3006, 3007 }));
    }
    [Test] public void EstimatedUpkeepUsesTheSpecFormulaWithSafeInputs() {
        var upkeep = new AreaPolicyUpkeep(300, 20, 25, 0, null);
        Assert.That(upkeep.EstimateDaily(0, 0, 0), Is.EqualTo(300));
        Assert.That(upkeep.EstimateDaily(25000, 4, 0), Is.EqualTo(300 + 20 * 2.5 + 25 * 4));
        Assert.That(upkeep.EstimateDaily(double.NaN, -3, double.PositiveInfinity), Is.EqualTo(300), "invalid inputs never produce invalid finance values");
    }
}
