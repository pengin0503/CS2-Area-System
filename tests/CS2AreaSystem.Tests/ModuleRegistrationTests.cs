using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Prefabs;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// #59: a module is registered completely or not at all; a dependent module needs its prerequisite.
public class ModuleRegistrationTests {
    private sealed class A { } private sealed class B { } private sealed class C { }
    [Test] public void FailureDisablesEverySystemAndNamesRegisteredAndMissingOnes() {
        var module = new ModuleRegistration("Park", typeof(A), typeof(B), typeof(C));
        module.Step();
        var disabled = new List<Type>(); var reports = new List<string>();
        module.Fail(new InvalidOperationException("boom"), disabled.Add, (_, m) => reports.Add(m));
        Assert.That(disabled, Is.EqualTo(new[] { typeof(A), typeof(B), typeof(C) }), "registered systems would otherwise keep running");
        Assert.That(module.Succeeded, Is.False); Assert.That(module.Failed, Is.True);
        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain("failed at B").And.Contain("now disabled: [A]").And.Contain("not registered: [B, C]"));
    }
    [Test] public void DisableFailureIsReportedAndDoesNotStopTheOthers() {
        var module = new ModuleRegistration("Industrial", typeof(A), typeof(B));
        var disabled = new List<Type>(); var reports = new List<string>();
        module.Fail(new Exception("x"), t => { if (t == typeof(A)) throw new Exception("cannot"); disabled.Add(t); }, (_, m) => reports.Add(m));
        Assert.That(disabled, Is.EqualTo(new[] { typeof(B) }));
        Assert.That(reports, Has.Count.EqualTo(2)); Assert.That(reports[0], Does.Contain("A could not be disabled"));
    }
    [Test] public void CompleteRequiresEveryStep() {
        var module = new ModuleRegistration("Finance", typeof(A), typeof(B));
        module.Step();
        Assert.Throws<InvalidOperationException>(() => module.Complete());
        module.Step();
        Assert.That(module.Complete(), Is.True); Assert.That(module.Succeeded, Is.True);
        Assert.Throws<InvalidOperationException>(() => module.Step());
    }
    [Test] public void DependentModuleRequiresACompletePrerequisite() {
        var finance = new ModuleRegistration("Area finance", typeof(A), typeof(B));
        var reports = new List<string>();
        finance.Step();
        Assert.That(ModuleRegistration.Requires(finance, "Park gameplay", reports.Add), Is.False, "partially registered finance");
        finance.Fail(new Exception(), _ => { }, (_, _) => { });
        Assert.That(ModuleRegistration.Requires(finance, "Park gameplay", reports.Add), Is.False);
        Assert.That(ModuleRegistration.Requires(null!, "Park gameplay", reports.Add), Is.False);
        Assert.That(reports, Has.All.Contain("Park gameplay not registered"));
        var ok = new ModuleRegistration("Area finance", typeof(A)); ok.Step(); ok.Complete();
        Assert.That(ModuleRegistration.Requires(ok, "Park gameplay", reports.Add), Is.True);
    }
    [Test] public void ModRegistersFinanceDependentsOnlyThroughRequires() {
        // Park and policy upkeep book money through Area finance; their registration is gated on the finance module.
        var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Mod.cs"));
        Assert.That(source, Does.Contain("ModuleRegistration.Requires(finance, \"Area policy upkeep\""));
        Assert.That(source, Does.Contain("ModuleRegistration.Requires(finance, \"Park gameplay\""));
        // Industrial support books city money and credits companies: it needs complete finance and Industrial modules and
        // registers its scheduler and save flush as one unit, so it never runs without the flush.
        Assert.That(source, Does.Contain("ModuleRegistration.Requires(finance, \"Industrial support\""));
        Assert.That(source, Does.Contain("ModuleRegistration.Requires(industrial, \"Industrial support\""));
        Assert.That(source, Does.Contain("new ModuleRegistration(\"Industrial support\",\n                typeof(CS2AreaSystem.Modules.Industrial.IndustrialSupportSystem), typeof(CS2AreaSystem.Modules.Industrial.IndustrialSupportSaveSystem))"));
    }
}
// #49: a non-native tool blocks only its Area type, the native tool restores it, and a new load forgets every block.
public class ManagedAreaToolGuardTests {
    [Test] public void NonNativeToolBlocksThatTypeOnceAndNativeToolRecoversIt() {
        var guard = new ManagedAreaToolGuard();
        var first = guard.Validate(1, true, false);
        Assert.That(first.Allow, Is.False); Assert.That(first.NewlyBlocked, Is.True);
        Assert.That(guard.Validate(1, true, false).NewlyBlocked, Is.False, "logged once");
        Assert.That(guard.IsBlocked(2), Is.False, "other types are unaffected");
        var recovered = guard.Validate(1, true, true);
        Assert.That(recovered.Allow, Is.True); Assert.That(recovered.Recovered, Is.True);
        Assert.That(guard.IsBlocked(1), Is.False, "the block is not permanent");
        Assert.That(guard.Validate(1, true, true).Recovered, Is.False);
    }
    [Test] public void NativeToolWithUnreadyPrefabIsDeniedWithoutBlocking() {
        var guard = new ManagedAreaToolGuard();
        var decision = guard.Validate(3, false, true);
        Assert.That(decision.Allow, Is.False); Assert.That(guard.IsBlocked(3), Is.False);
    }
    [Test] public void NewLoadGenerationClearsBlocks() {
        var guard = new ManagedAreaToolGuard();
        guard.Validate(1, true, false); guard.Validate(2, true, false);
        guard.NewLoadGeneration();
        Assert.That(guard.IsBlocked(1), Is.False); Assert.That(guard.IsBlocked(2), Is.False);
    }
}
// #48: a toolbar failure is bounded; prefabs are then registered without the toolbar relationship so saved areas load.
public class AreaToolbarExposureStateTests {
    [Test] public void ReadyGraphRegistersAndAttaches() {
        var state = new AreaToolbarExposureState();
        var d = state.Update(() => true);
        Assert.That(d.RegisterPrefabs && d.AttachToolbar && !d.BecameUnavailable, Is.True);
        Assert.That(state.Exposure, Is.EqualTo(AreaToolbarExposure.Native));
    }
    [Test] public void RepeatedFailureRegistersWithoutToolbarAfterBoundedAttempts() {
        var state = new AreaToolbarExposureState(); int calls = 0;
        for (int i = 1; i < AreaToolbarExposureState.MaxFailedAttempts; i++) {
            var waiting = state.Update(() => { calls++; return false; });
            Assert.That(waiting.RegisterPrefabs, Is.False, "attempt " + i);
        }
        var d = state.Update(() => { calls++; return false; });
        Assert.That(d.RegisterPrefabs, Is.True); Assert.That(d.AttachToolbar, Is.False); Assert.That(d.BecameUnavailable, Is.True);
        Assert.That(state.Exposure, Is.EqualTo(AreaToolbarExposure.Unavailable));
        var later = state.Update(() => { calls++; return true; });
        Assert.That(later.RegisterPrefabs && !later.AttachToolbar && !later.BecameUnavailable, Is.True, "the decision is stable for the session");
        Assert.That(calls, Is.EqualTo(AreaToolbarExposureState.MaxFailedAttempts), "the graph is not retried once unavailable");
    }
    [Test] public void TransientFailureBeforeTheLimitStillAttaches() {
        var state = new AreaToolbarExposureState();
        state.Update(() => false); state.Update(() => false);
        var d = state.Update(() => true);
        Assert.That(d.AttachToolbar, Is.True); Assert.That(state.Exposure, Is.EqualTo(AreaToolbarExposure.Native));
    }
    [Test] public void NativeExposureWaitsInsteadOfDroppingTheToolbar() {
        var state = new AreaToolbarExposureState();
        state.Update(() => true);
        for (int i = 0; i < AreaToolbarExposureState.MaxFailedAttempts * 2; i++) {
            var d = state.Update(() => false);
            Assert.That(d.RegisterPrefabs, Is.False); Assert.That(d.BecameUnavailable, Is.False);
        }
        Assert.That(state.Exposure, Is.EqualTo(AreaToolbarExposure.Native));
    }
}
