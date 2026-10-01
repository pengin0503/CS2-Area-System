using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaPolicyRuntimeTests {
    private const int Park = (int)AreaTypeId.Park, Industrial = (int)AreaTypeId.Industrial;
    private static readonly Entity A = new() { Index = 1, Version = 1 }, B = new() { Index = 2, Version = 1 }, C = new() { Index = 3, Version = 1 };
    private sealed class Store : IAreaPolicyStore {
        public readonly Dictionary<Entity, AreaPolicyAreaInfo> Areas = new();
        public readonly Dictionary<Entity, List<ManagedAreaPolicyEntry>> Entries = new();
        public int Writes;
        public bool TryGetArea(Entity area, out AreaPolicyAreaInfo info) => Areas.TryGetValue(area, out info);
        public bool TryRead(Entity area, List<ManagedAreaPolicyEntry> into) {
            into.Clear();
            if (!Entries.TryGetValue(area, out var list)) return false;
            into.AddRange(list); return true;
        }
        public void Write(Entity area, IReadOnlyList<ManagedAreaPolicyEntry> entries) { Writes++; Entries[area] = entries.ToList(); }
        public void Add(Entity area, ulong id, int type, int level, AreaLifecycleState state = AreaLifecycleState.Active, params ManagedAreaPolicyEntry[] entries) {
            Areas[area] = new AreaPolicyAreaInfo(id, type, state, level, false);
            Entries[area] = entries.ToList();
        }
        public bool Desired(Entity area, int id) => Entries[area].Any(e => e.PolicyId == id && e.IsDesiredEnabled);
    }
    private sealed class Provider : IAreaPolicyProvider {
        public IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions { get; set; } = new AreaPolicyDefinition[0];
        public HashSet<int> Unavailable = new();
        public Exception Throw;
        public int Calls;
        public bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey) {
            Calls++;
            if (Throw != null) throw Throw;
            reasonKey = Unavailable.Contains(policyId) ? "Test.Unavailable" : null;
            return reasonKey == null;
        }
    }
    private static AreaPolicyDefinition Def(int id, int type, int level = 1, string group = null) =>
        AreaPolicyRegistryTests.Definition(id, level, group, type);
    private static (AreaPolicyRuntime Runtime, Store Store, Provider Park, Provider Industrial, List<string> Errors) Create() {
        var park = new Provider { PolicyDefinitions = new[] { Def(2001, Park), Def(2002, Park, 2, "park.activity"), Def(2003, Park, 2, "park.activity"), Def(2004, Park, 3) } };
        var industrial = new Provider { PolicyDefinitions = new[] { Def(3002, Industrial, 2, "industrial.environment-production"), Def(3003, Industrial, 2, "industrial.environment-production") } };
        var errors = new List<string>();
        var runtime = AreaPolicyRuntime.Build(CommonAreaPolicies.Provider,
            new Dictionary<int, IAreaPolicyProvider> { [Park] = park, [Industrial] = industrial }, errors.Add);
        return (runtime, new Store(), park, industrial, errors);
    }
    [Test] public void RegistryHoldsCommonAndModuleDefinitions() {
        var (runtime, _, _, _, errors) = Create();
        Assert.That(runtime.Registry.All.Select(d => d.PolicyId).OrderBy(i => i), Is.EqualTo(new[] { 1001, 2001, 2002, 2003, 2004, 3002, 3003 }));
        Assert.That(errors, Is.Empty);
    }
    [Test] public void RealModuleCatalogsBuildWithoutErrors() {
        var errors = new List<string>();
        var runtime = AreaPolicyRuntime.Build(CommonAreaPolicies.Provider,
            new Dictionary<int, IAreaPolicyProvider> { [Park] = new ParkAreaModule(null), [Industrial] = new IndustrialAreaModule(null) }, errors.Add);
        Assert.That(errors, Is.Empty);
        Assert.That(runtime.Registry.All, Has.Count.EqualTo(14));
    }
    [Test] public void BuildRejectsDuplicatesForeignTypesAndFailingProvidersWithoutStoppingOthers() {
        var errors = new List<string>();
        var duplicate = new Provider { PolicyDefinitions = new[] { Def(1001, Park), Def(2001, Park) } };
        var foreign = new Provider { PolicyDefinitions = new[] { Def(2002, Park), Def(3001, Industrial) } };
        var failing = new FailingProvider();
        var runtime = AreaPolicyRuntime.Build(CommonAreaPolicies.Provider,
            new Dictionary<int, IAreaPolicyProvider> { [Park] = duplicate, [Industrial] = foreign, [99] = failing }, errors.Add);
        Assert.That(runtime.Registry.All.Select(d => d.PolicyId).OrderBy(i => i), Is.EqualTo(new[] { 1001, 2001, 3001 }));
        Assert.That(runtime.Registry.TryGet(2002, out _), Is.False, "a module cannot register policies for another Area type");
        Assert.That(runtime.Registry.TryGet(1001, out var common), Is.True);
        Assert.That(common, Is.SameAs(CommonAreaPolicies.Definitions[0]), "duplicate never replaces the common definition");
        Assert.That(errors, Has.Count.EqualTo(3), "duplicate, foreign type and failing provider are each reported");
    }
    private sealed class FailingProvider : IAreaPolicyProvider {
        public IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions => throw new InvalidOperationException("broken module");
        public bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey) => throw new InvalidOperationException();
    }
    [Test] public void PoliciesAreIndependentPerArea() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5); store.Add(B, 11, Park, 5);
        Assert.That(runtime.TrySetDesired(store, A, 2002, true, default, out _), Is.True);
        Assert.That(runtime.IsEffective(store, A, 2002, default), Is.True);
        Assert.That(runtime.IsEffective(store, B, 2002, default), Is.False);
        Assert.That(store.Entries[B], Is.Empty);
    }
    [Test] public void LevelUnlocksAreDerivedFromTheCurrentLevel() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 1);
        var locked = runtime.GetState(store, A, 1001, default);
        Assert.That(locked.Unlocked, Is.False);
        Assert.That(locked.ReasonKey, Is.EqualTo(AreaPolicyReasons.Locked));
        Assert.That(runtime.TrySetDesired(store, A, 1001, true, default, out var reason), Is.False);
        Assert.That(reason, Is.EqualTo(AreaPolicyReasons.Locked));
        store.Areas[A] = new AreaPolicyAreaInfo(10, Park, AreaLifecycleState.Active, 2, false);
        Assert.That(runtime.GetState(store, A, 1001, default).Unlocked, Is.True);
        Assert.That(runtime.TrySetDesired(store, A, 1001, true, default, out _), Is.True);
    }
    [Test] public void UnlockingNeverEnablesAPolicy() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5);
        var state = runtime.GetState(store, A, 2004, default);
        Assert.That(state.Unlocked, Is.True);
        Assert.That(state.DesiredEnabled, Is.False);
        Assert.That(state.Effective, Is.False);
    }
    [Test] public void ConflictReplacementIsOneWriteThatSwapsBothPolicies() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.Active, new ManagedAreaPolicyEntry(2002, true), new ManagedAreaPolicyEntry(2001, true));
        Assert.That(runtime.TrySetDesired(store, A, 2003, true, default, out _), Is.True);
        Assert.That(store.Writes, Is.EqualTo(1));
        Assert.That(store.Desired(A, 2002), Is.False);
        Assert.That(store.Desired(A, 2003), Is.True);
        Assert.That(store.Desired(A, 2001), Is.True, "unrelated policy remains enabled");
        Assert.That(runtime.IsEffective(store, A, 2002, default), Is.False);
        Assert.That(runtime.IsEffective(store, A, 2003, default), Is.True);
    }
    [Test] public void SuspendedAreaKeepsDesiredStateWithoutEffectAndResumesWhenActive() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.Active, new ManagedAreaPolicyEntry(2001, true));
        store.Areas[A] = new AreaPolicyAreaInfo(10, Park, AreaLifecycleState.Suspended, 5, false);
        var suspended = runtime.GetState(store, A, 2001, default);
        Assert.That(suspended.DesiredEnabled, Is.True);
        Assert.That(suspended.Suspended, Is.True);
        Assert.That(suspended.Effective, Is.False);
        Assert.That(suspended.ReasonKey, Is.EqualTo(AreaPolicyReasons.Suspended));
        Assert.That(store.Desired(A, 2001), Is.True);
        store.Areas[A] = new AreaPolicyAreaInfo(10, Park, AreaLifecycleState.Active, 5, false);
        Assert.That(runtime.GetState(store, A, 2001, default).Effective, Is.True);
    }
    [Test] public void SuspendedAreaCanStillBeConfigured() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.Suspended);
        Assert.That(runtime.TrySetDesired(store, A, 2001, true, default, out _), Is.True);
        Assert.That(runtime.GetState(store, A, 2001, default).Effective, Is.False);
    }
    [Test] public void DeletedOrUnavailableAreasRejectChanges() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.ModuleUnavailable);
        store.Add(B, 11, Park, 5); store.Areas[B] = new AreaPolicyAreaInfo(11, Park, AreaLifecycleState.Active, 5, true);
        Assert.That(runtime.TrySetDesired(store, A, 2001, true, default, out var unavailable), Is.False);
        Assert.That(unavailable, Is.EqualTo(AreaPolicyReasons.AreaUnavailable));
        Assert.That(runtime.TrySetDesired(store, B, 2001, true, default, out var deleted), Is.False);
        Assert.That(deleted, Is.EqualTo(AreaPolicyReasons.AreaUnavailable));
        Assert.That(runtime.TrySetDesired(store, C, 2001, true, default, out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(AreaPolicyReasons.AreaUnavailable));
        Assert.That(store.Writes, Is.EqualTo(0));
    }
    [Test] public void EffectUnavailablePolicyIsNeitherEffectiveNorEnableableButCanBeTurnedOff() {
        var (runtime, store, park, _, _) = Create();
        park.Unavailable.Add(2002);
        store.Add(A, 10, Park, 5, AreaLifecycleState.Active, new ManagedAreaPolicyEntry(2002, true));
        var state = runtime.GetState(store, A, 2002, default);
        Assert.That(state.DesiredEnabled, Is.True);
        Assert.That(state.EffectAvailable, Is.False);
        Assert.That(state.Effective, Is.False);
        Assert.That(state.ReasonKey, Is.EqualTo("Test.Unavailable"));
        Assert.That(runtime.TrySetDesired(store, A, 2003, false, default, out _), Is.True);
        park.Unavailable.Add(2003);
        Assert.That(runtime.TrySetDesired(store, A, 2003, true, default, out var reason), Is.False);
        Assert.That(reason, Is.EqualTo("Test.Unavailable"));
        Assert.That(runtime.TrySetDesired(store, A, 2002, false, default, out _), Is.True);
        Assert.That(store.Desired(A, 2002), Is.False);
    }
    [Test] public void UnknownSavedIdsStayInertAndReturnWithTheirDefinition() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.Active, new ManagedAreaPolicyEntry(2999, true));
        var state = runtime.GetState(store, A, 2999, default);
        Assert.That(state.Effective, Is.False);
        Assert.That(state.ReasonKey, Is.EqualTo(AreaPolicyReasons.Unknown));
        Assert.That(runtime.TrySetDesired(store, A, 2999, false, default, out _), Is.False, "the UI cannot change a policy it does not know");
        Assert.That(runtime.TrySetDesired(store, A, 2001, true, default, out _), Is.True);
        Assert.That(store.Entries[A].Single(e => e.PolicyId == 2999).IsDesiredEnabled, Is.True, "unknown entry untouched");
        var restored = new Provider { PolicyDefinitions = new[] { Def(2001, Park), Def(2999, Park) } };
        var reinstalled = AreaPolicyRuntime.Build(CommonAreaPolicies.Provider, new Dictionary<int, IAreaPolicyProvider> { [Park] = restored }, _ => { });
        Assert.That(reinstalled.IsEffective(store, A, 2999, default), Is.True);
    }
    [Test] public void ConflictingDesiredEntriesFromSavedDataNeverBothTakeEffect() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.Active, new ManagedAreaPolicyEntry(2003, true), new ManagedAreaPolicyEntry(2002, true));
        Assert.That(runtime.IsEffective(store, A, 2002, default), Is.True, "lowest PolicyId of the group wins deterministically");
        var shadowed = runtime.GetState(store, A, 2003, default);
        Assert.That(shadowed.Effective, Is.False);
        Assert.That(shadowed.ReasonKey, Is.EqualTo(AreaPolicyReasons.Conflict));
    }
    [Test] public void FocusedManagementMultiplierIsExactlyOnePointOneOnlyWhenEffective() {
        var (runtime, store, _, _, _) = Create();
        store.Add(A, 10, Industrial, 2);
        Assert.That(runtime.GetExperienceMultiplier(store, A, default), Is.EqualTo(1.0));
        runtime.TrySetDesired(store, A, 1001, true, default, out _);
        Assert.That(runtime.GetExperienceMultiplier(store, A, default), Is.EqualTo(1.10));
        store.Areas[A] = new AreaPolicyAreaInfo(10, Industrial, AreaLifecycleState.Suspended, 2, false);
        Assert.That(runtime.GetExperienceMultiplier(store, A, default), Is.EqualTo(1.0));
        store.Areas[A] = new AreaPolicyAreaInfo(10, Industrial, AreaLifecycleState.Active, 1, false);
        Assert.That(runtime.GetExperienceMultiplier(store, A, default), Is.EqualTo(1.0), "a Level drop below the minimum makes it inactive");
        Assert.That(store.Desired(A, 1001), Is.True, "but keeps the desired state");
    }
    [Test] public void AvailabilityFailureDisablesOnlyThatPolicyAndIsLoggedOncePerLoadGeneration() {
        var (runtime, store, park, _, errors) = Create();
        store.Add(A, 10, Park, 5, AreaLifecycleState.Active, new ManagedAreaPolicyEntry(2001, true), new ManagedAreaPolicyEntry(1001, true));
        park.Throw = new InvalidOperationException("adapter broke");
        var state = runtime.GetState(store, A, 2001, default);
        Assert.That(state.EffectAvailable, Is.False);
        Assert.That(state.ReasonKey, Is.EqualTo(AreaPolicyReasons.EffectUnavailable));
        runtime.GetState(store, A, 2001, default);
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(runtime.IsEffective(store, A, 1001, default), Is.True, "the common policy is unaffected");
        runtime.NewLoadGeneration();
        runtime.GetState(store, A, 2001, default);
        Assert.That(errors, Has.Count.EqualTo(2));
    }
    [Test] public void FailureLogKeyIsAreaPolicyFailureClassAndGeneration() {
        var log = new AreaPolicyFailureLog();
        Assert.That(log.ShouldLog(10, 2001, "availability"), Is.True);
        Assert.That(log.ShouldLog(10, 2001, "availability"), Is.False);
        Assert.That(log.ShouldLog(11, 2001, "availability"), Is.True);
        Assert.That(log.ShouldLog(10, 2002, "availability"), Is.True);
        Assert.That(log.ShouldLog(10, 2001, "apply"), Is.True);
        log.Clear(10, 2001, "availability");
        Assert.That(log.ShouldLog(10, 2001, "availability"), Is.True, "a cleared failure that recurs is logged again");
        log.NewGeneration();
        Assert.That(log.Generation, Is.EqualTo(1));
        Assert.That(log.ShouldLog(10, 2001, "apply"), Is.True);
    }
}
