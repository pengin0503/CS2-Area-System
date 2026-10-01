using System.Reflection;
using CS2AreaSystem.Core.Prefabs;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// #77: GameUIResourceAvailability caches true and false per path. That is sound only while the "gameui" UI hosts cannot
// change after the first lookup; these tests pin that lifecycle in the shipped 1.6.2f1 binaries and the cache itself.
public class GameUIResourceAvailabilityTests {
    private static readonly Assembly Game = typeof(global::Game.SceneFlow.GameManager).Assembly;
    private static readonly Lazy<List<IlCallSite>> HostRegistrations = new(() => IlCallScanner.Find(Game, target =>
        target.DeclaringType?.FullName == "Colossal.UI.UISystem" && (target.Name == "AddHostLocation" || target.Name == "AddDatabaseHostLocation")));
    [Test] public void GameUIHostsAreRegisteredOnlyWhenTheGameBootsItsUI() {
        var owners = HostRegistrations.Value.Select(site => IlCallScanner.Outer(site.Caller.DeclaringType!).FullName).Distinct().OrderBy(n => n).ToArray();
        // GameManager.InitializeUI (every UIHostAsset), ModManager ("ui-mods"), the PDX mods view adapter (host keys passed
        // in by the Paradox SDK for its own pages, not determinable from the binary) and the development-only
        // UISystemBootstrapper. Only InitializeUI turns UIHostAssets (what the probe reads) into UI hosts. A new registration path in a game update fails here and the cache must be revisited.
        Assert.That(owners, Is.EqualTo(new[] { "Game.Modding.ModManager", "Game.PSI.PdxSdk.PdxModsUI+ColossalUIViewAdapter", "Game.SceneFlow.GameManager", "Game.UI.UISystemBootstrapper" }));
        var gameManager = HostRegistrations.Value.Where(site => IlCallScanner.Outer(site.Caller.DeclaringType!) == typeof(global::Game.SceneFlow.GameManager)).ToArray();
        Assert.That(gameManager.Select(site => site.Caller.DeclaringType!.Name), Is.All.StartsWith("<InitializeUI>"), "only InitializeUI registers asset hosts");
        var mods = HostRegistrations.Value.Where(site => IlCallScanner.Outer(site.Caller.DeclaringType!) == typeof(global::Game.Modding.ModManager)).ToArray();
        Assert.That(mods.Select(site => site.LastString), Is.All.EqualTo("ui-mods"), "mods only add the ui-mods host");
    }
    [Test] public void TheGameBootsItsUIBeforeAnyModIsLoaded() {
        var initialize = typeof(global::Game.SceneFlow.GameManager).GetNestedTypes(BindingFlags.NonPublic)
            .Single(t => t.Name.StartsWith("<Initialize>", StringComparison.Ordinal)).GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        var calls = IlCallScanner.Find(initialize, target => target.DeclaringType == typeof(global::Game.SceneFlow.GameManager) &&
            (target.Name == "InitializeUI" || target.Name == "InitializeModManager"));
        int ui = calls.Single(c => c.Target.Name == "InitializeUI").Offset, modding = calls.Single(c => c.Target.Name == "InitializeModManager").Offset;
        Assert.That(ui, Is.LessThan(modding), "the gameui host set is final before a mod (and its first lookup) exists");
    }
    [Test] public void EachPathIsProbedOnceAndHostedPathsAreNeverProbed() {
        var probed = new List<string>();
        var availability = new GameUIResourceAvailability(path => { probed.Add(path); return path.EndsWith("Park.svg", StringComparison.Ordinal); });
        for (int frame = 0; frame < 100; frame++) {
            Assert.That(availability.Exists("Media/Game/Icons/Park.svg"), Is.True);
            Assert.That(availability.Exists("Media/Game/Icons/Missing.svg"), Is.False);
            Assert.That(availability.Exists("coui://ui-mods/x.svg"), Is.False);
        }
        Assert.That(probed, Is.EqualTo(new[] { "Media/Game/Icons/Park.svg", "Media/Game/Icons/Missing.svg" }), "no asset database scan after the first lookup");
    }
}
