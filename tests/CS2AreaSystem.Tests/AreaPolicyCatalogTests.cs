using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Modules.Tourism;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaPolicyCatalogTests {
    private const int Park = (int)AreaTypeId.Park, Industrial = (int)AreaTypeId.Industrial, Tourism = (int)AreaTypeId.Tourism;
    // Present in the supplied CS2-UI-reference-1.6.2f1 Media/Game/Icons directory (checked file by file).
    private static readonly HashSet<string> VerifiedIcons = new() {
        "Administration", "ServicesUpgrade", "Entertainment", "NoisePollution", "Landscaping", "ServiceFees", "Attractions",
        "DisasterControl", "Pollution", "Production", "CargoCar", "Calendar", "Soundbarrier", "Research" };
    private static IReadOnlyList<AreaPolicyDefinition> All() =>
        CommonAreaPolicies.Definitions.Concat(ParkPolicyCatalog.Definitions).Concat(IndustrialPolicyCatalog.Definitions).ToList();
    private static object[] Row(AreaPolicyDefinition d) => new object[] { d.PolicyId, d.MinLevel, d.ConflictGroup, d.EffectClass, d.AreaTypeIds.OrderBy(t => t).ToArray() };
    [Test] public void CommonFocusedManagementIsLevelTwoForParkAndIndustrialOnly() {
        Assert.That(CommonAreaPolicies.Definitions.Select(Row), Is.EqualTo(new[] {
            new object[] { 1001, 2, null, AreaPolicyEffectClass.AreaInternal, new[] { Park, Industrial } } }));
    }
    [Test] public void ParkCatalogMatchesTheSpecLevelsAndConflicts() {
        Assert.That(ParkPolicyCatalog.Definitions.Select(Row), Is.EqualTo(new[] {
            new object[] { 2001, 1, null, AreaPolicyEffectClass.AreaInternal, new[] { Park } },
            new object[] { 2002, 2, "park.activity", AreaPolicyEffectClass.VanillaSimulation, new[] { Park } },
            new object[] { 2003, 2, "park.activity", AreaPolicyEffectClass.VanillaSimulation, new[] { Park } },
            new object[] { 2004, 3, null, AreaPolicyEffectClass.AreaInternal, new[] { Park } },
            new object[] { 2005, 4, null, AreaPolicyEffectClass.AreaInternal, new[] { Park } },
            new object[] { 2006, 5, null, AreaPolicyEffectClass.VanillaSimulation, new[] { Park } } }));
    }
    [Test] public void IndustrialCatalogMatchesTheSpecLevelsAndConflicts() {
        Assert.That(IndustrialPolicyCatalog.Definitions.Select(Row), Is.EqualTo(new[] {
            new object[] { 3001, 1, null, AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } },
            new object[] { 3002, 2, "industrial.environment-production", AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } },
            new object[] { 3003, 2, "industrial.environment-production", AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } },
            new object[] { 3004, 3, null, AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } },
            new object[] { 3005, 4, "industrial.hours", AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } },
            new object[] { 3006, 4, "industrial.hours", AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } },
            new object[] { 3007, 5, null, AreaPolicyEffectClass.VanillaSimulation, new[] { Industrial } } }));
    }
    [Test] public void NoPolicyTargetsTourism() {
        Assert.That(All().Where(d => d.Supports(Tourism)), Is.Empty);
        Assert.That(new TourismAreaModule(), Is.Not.InstanceOf<IAreaPolicyProvider>());
    }
    [Test] public void EveryDefinitionHasPositiveBaseUpkeepAndNoActivityBilling() {
        foreach (var d in All()) {
            Assert.That(d.Upkeep.BaseDailyUpkeep, Is.GreaterThan(0), d.PolicyId.ToString());
            Assert.That(d.Upkeep.PerActivityUnit, Is.EqualTo(0), "first release has no activity-based billing");
            Assert.That(d.Upkeep.ActivityBasis, Is.Null);
        }
    }
    [Test] public void KeysAndIconsUseTheVanillaPolicyConventions() {
        foreach (var d in All()) {
            Assert.That(d.NameKey, Does.Match(@"^Policy\.TITLE\[CS2AS\.Policy\.[A-Za-z0-9]+\]$"), d.PolicyId.ToString());
            Assert.That(d.DescriptionKey, Is.EqualTo(d.NameKey.Replace("Policy.TITLE[", "Policy.DESCRIPTION[")));
            Assert.That(d.IconUri, Does.StartWith("Media/Game/Icons/").And.EndWith(".svg"));
            Assert.That(VerifiedIcons, Does.Contain(d.IconUri.Substring("Media/Game/Icons/".Length).Replace(".svg", "")));
        }
        Assert.That(All().Select(d => d.NameKey).Distinct().Count(), Is.EqualTo(All().Count));
    }
    [Test] public void ParkAndIndustrialModulesPublishTheirOwnCatalogs() {
        var park = new ParkAreaModule(null);
        var industrial = new IndustrialAreaModule(null);
        Assert.That(((IAreaPolicyProvider)park).PolicyDefinitions, Is.SameAs(ParkPolicyCatalog.Definitions));
        Assert.That(((IAreaPolicyProvider)industrial).PolicyDefinitions, Is.SameAs(IndustrialPolicyCatalog.Definitions));
    }
    [Test] public void UnprovenEffectsReportUnavailableUntilTheirAdapterTask() {
        IAreaPolicyProvider park = new ParkAreaModule(null), industrial = new IndustrialAreaModule(null);
        // Park: only the Area-internal effects of Task 7 exist (ParkPolicyEffectTests covers each one).
        var parkAvailable = new[] { AreaPolicyIds.EnhancedMaintenance, AreaPolicyIds.LandscapeProtection, AreaPolicyIds.PremiumServices };
        foreach (var d in ParkPolicyCatalog.Definitions) {
            bool available = parkAvailable.Contains(d.PolicyId);
            Assert.That(park.IsPolicyEffectAvailable(Entity.Null, d.PolicyId, default, out var reason), Is.EqualTo(available), d.PolicyId.ToString());
            Assert.That(reason, available ? Is.Null : Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
        }
        foreach (var d in IndustrialPolicyCatalog.Definitions) {
            Assert.That(industrial.IsPolicyEffectAvailable(Entity.Null, d.PolicyId, default, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
        }
        Assert.That(CommonAreaPolicies.Provider.IsPolicyEffectAvailable(Entity.Null, AreaPolicyIds.FocusedManagement, default, out var common), Is.True);
        Assert.That(common, Is.Null);
    }
}
