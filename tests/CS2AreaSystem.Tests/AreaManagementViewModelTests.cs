using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.UI;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaManagementViewModelTests {
    private static AreaManagementViewModel View() => new() {
        StableId = "7", AnchorName = "Harbor Offices", TypeKey = "CS2AS.Type.Industrial", State = "CS2AS.State.Active",
        Summary = "CS2AS.Summary.Active", AnchorRole = "Headquarters", Level = 2, Experience = "10",
        Panel = AreaPanelModelTests.Document(),
        Anchors = new[] { new AreaAnchorView { Key = "30.1", Name = "North Gate" } }
    };
    [Test] public void SectionPropertiesCarryStructuredTabsAndAnchorList() {
        var writer = new CS2AreaSystem.Tests.Support.RecordingJsonWriter();
        writer.MapBegin(0); View().WriteProperties(writer); writer.MapEnd();
        var json = writer.Parse();
        Assert.That(json.GetProperty("stableId").GetString(), Is.EqualTo("7"));
        Assert.That(json.GetProperty("tabs")[1].GetProperty("items")[0].GetProperty("kind").GetString(), Is.EqualTo("slider"));
        Assert.That(json.GetProperty("anchors")[0].GetProperty("key").GetString(), Is.EqualTo("30.1"));
        Assert.That(json.GetProperty("anchors")[0].GetProperty("name").GetString(), Is.EqualTo("North Gate"));
    }
    [Test] public void IdenticalContentIsEquivalentEvenWithNewTabInstances() {
        Assert.That(AreaManagementViewModel.Equivalent(View(), View()), Is.True);
        Assert.That(AreaManagementViewModel.Equivalent(null, null), Is.True);
    }
    [Test] public void EveryPublishedFieldChangeIsDetected() {
        var changes = new Action<AreaManagementViewModel>[] {
            v => v.StableId = "8", v => v.AnchorName = "Other HQ", v => v.AnchorName = null, v => v.ConfirmDelete = false, v => v.TypeKey = "CS2AS.Type.Park", v => v.State = "CS2AS.State.Suspended",
            v => v.Summary = "CS2AS.Summary.AnchorRequired", v => v.AnchorRole = null, v => v.Level = 3, v => v.Level = null,
            v => v.Experience = "11", v => v.PendingAnchor = true,
            v => v.Panel = new AreaPanelDocument(),
            v => v.Panel.Tabs[0].Items[0].Value = "13",
            v => v.Anchors = new AreaAnchorView[0],
            v => v.Anchors = new[] { new AreaAnchorView { Key = "30.1", Name = "South Gate" } },
            v => v.Anchors = new[] { new AreaAnchorView { Key = "31.1", Name = "North Gate" } }
        };
        foreach (var change in changes) {
            var changed = View(); change(changed);
            Assert.That(AreaManagementViewModel.Equivalent(View(), changed), Is.False);
        }
        Assert.That(AreaManagementViewModel.Equivalent(View(), null), Is.False);
        Assert.That(AreaManagementViewModel.Equivalent(null, View()), Is.False);
    }
}
public class PendingAssignmentViewTests {
    [Test] public void BannerIsRepublishedOnlyWhenItsContentChanges() {
        PendingAssignmentView View(string name="Harbor",string role="Headquarters")=>new(){StableId="7",Name=name,Role=role};
        Assert.That(PendingAssignmentView.Equivalent(View(),View()),Is.True);
        Assert.That(PendingAssignmentView.Equivalent(null,null),Is.True);
        Assert.That(PendingAssignmentView.Equivalent(View(),null),Is.False);
        Assert.That(PendingAssignmentView.Equivalent(View(),View(name:"Renamed")),Is.False);
        Assert.That(PendingAssignmentView.Equivalent(View(),View(role:"Entrance")),Is.False);
    }
}
