using CS2AreaSystem.Core.UI;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaEditWorkflowTests {
    [Test] public void EditingClearsRecreationBeforeEnteringNativeEditAndFocusesRequestedArea() {
        var area = new Entity {Index=17, Version=3};
        var staleRecreation = new Entity {Index=44, Version=2};
        var recreation = staleRecreation;
        var focused = Entity.Null;
        var events = new List<string>();
        AreaEditWorkflow.Start(area,
            entity => { recreation=entity; events.Add("clear"); },
            () => { Assert.That(recreation,Is.EqualTo(Entity.Null)); events.Add("prefab"); },
            () => events.Add("edit"),
            () => events.Add("activate"),
            entity => { focused=entity; events.Add("focus"); });
        Assert.That(recreation,Is.EqualTo(Entity.Null));
        Assert.That(focused,Is.EqualTo(area));
        Assert.That(events,Is.EqualTo(new[]{"clear","prefab","edit","activate","focus"}));
    }
}
