using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.UI;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaManagementViewModelTests {
    private static AreaManagementViewModel View() => new() {
        StableId = "7", Name = "Harbor", TypeKey = "CS2AS.Type.Industrial", State = "CS2AS.State.Active",
        Summary = "CS2AS.Summary.Active", AnchorRole = "Headquarters", Level = 2, Experience = "10",
        Tabs = new[] { new AreaPanelTab("overview", "CS2AS.Tab.Overview") }
    };
    [Test] public void IdenticalContentIsEquivalentEvenWithNewTabInstances() {
        Assert.That(AreaManagementViewModel.Equivalent(View(), View()), Is.True);
        Assert.That(AreaManagementViewModel.Equivalent(null, null), Is.True);
    }
    [Test] public void EveryPublishedFieldChangeIsDetected() {
        var changes = new Action<AreaManagementViewModel>[] {
            v => v.StableId = "8", v => v.Name = "Renamed", v => v.TypeKey = "CS2AS.Type.Park", v => v.State = "CS2AS.State.Suspended",
            v => v.Summary = "CS2AS.Summary.AnchorRequired", v => v.AnchorRole = null, v => v.Level = 3, v => v.Level = null,
            v => v.Experience = "11", v => v.PendingAnchor = true,
            v => v.Tabs = new AreaPanelTab[0],
            v => v.Tabs = new[] { new AreaPanelTab("overview", "CS2AS.Tab.Other") },
            v => v.Tabs = new[] { new AreaPanelTab("details", "CS2AS.Tab.Overview") }
        };
        foreach (var change in changes) {
            var changed = View(); change(changed);
            Assert.That(AreaManagementViewModel.Equivalent(View(), changed), Is.False);
        }
        Assert.That(AreaManagementViewModel.Equivalent(View(), null), Is.False);
        Assert.That(AreaManagementViewModel.Equivalent(null, View()), Is.False);
    }
}
