using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Tourism;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaAnchorServiceTests {
    private sealed class Store : IAreaAnchorStore {
        public Dictionary<Entity,IReadOnlyList<AreaAnchorLink>> Values = new();
        public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area,out var links)?links:Array.Empty<AreaAnchorLink>();
        public void Write(Entity area,IReadOnlyList<AreaAnchorLink> links) => Values[area]=links.ToArray();
    }
    private readonly Entity area=new(){Index=10,Version=1}, other=new(){Index=20,Version=1}, hq=new(){Index=30,Version=1}, replacement=new(){Index=40,Version=1};
    [Test] public void HeadquartersLossSuspendsOnlyItsAreaAndReassignmentPreservesProgress() {
        var invalid=new HashSet<Entity>();
        var store=new Store(); var anchors=new AreaAnchorService(store,e=>!invalid.Contains(e),(a,e,r)=>true);
        var industrial=new IndustrialAreaModule(anchors); var tourism=new TourismAreaModule();
        AreaLifecycleState State(Entity a)=>AreaLifecyclePolicy.Resolve(true,industrial.ValidateActivation(a),false);
        var progress=new ManagedAreaProgress{Level=3,Experience=1000};
        Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Suspended));
        Assert.That(anchors.Assign(area,hq,AreaAnchorRole.Headquarters),Is.True);
        anchors.Assign(other,replacement,AreaAnchorRole.Headquarters);
        Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Active));
        invalid.Add(hq); Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Suspended)); Assert.That(State(other),Is.EqualTo(AreaLifecycleState.Active));
        anchors.Assign(area,replacement,AreaAnchorRole.Headquarters);
        Assert.That(State(area),Is.EqualTo(AreaLifecycleState.Active));
        Assert.That(store.Read(area).Count,Is.EqualTo(1)); Assert.That(progress.Level,Is.EqualTo(3)); Assert.That(progress.Experience,Is.EqualTo(1000));
        Assert.That(tourism.ValidateActivation(area),Is.True);
    }
    [Test] public void ParkNeedsEntranceAndInvalidAssignmentCannotReplaceExistingLinks() {
        var store=new Store(); var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(a,e,r)=>r==AreaAnchorRole.Entrance);
        var park=new ParkAreaModule(anchors);
        Assert.That(park.ValidateActivation(area),Is.False);
        Assert.That(anchors.Assign(area,hq,AreaAnchorRole.Entrance),Is.True);
        Assert.That(park.ValidateActivation(area),Is.True);
        Assert.That(anchors.Assign(area,Entity.Null,AreaAnchorRole.Entrance),Is.False);
        Assert.That(anchors.Assign(area,replacement,AreaAnchorRole.Headquarters),Is.False);
        Assert.That(anchors.Remove(area,hq,AreaAnchorRole.Entrance),Is.True);
        Assert.That(park.ValidateActivation(area),Is.False);
    }
    [Test] public void AnchorUsesSerializerEntityOverloadAndReaderRemapping() {
        var io=MemorySerializer.Create();
        var link=new AreaAnchorLink{Anchor=hq,Role=AreaAnchorRole.Headquarters}; link.Serialize(io.Writer);
        Assert.That(io.Data.Tokens,Does.Contain(hq));
        io.Data.Remap=e=>e==hq?replacement:e;
        var loaded=new AreaAnchorLink();loaded.Deserialize(io.Reader);
        Assert.That(loaded.Anchor,Is.EqualTo(replacement));Assert.That(loaded.Role,Is.EqualTo(AreaAnchorRole.Headquarters));
    }
    [Test] public void ForgetAreaPrunesReverseIndexButQueuesFinalValidation() {
        var store=new Store();
        var anchors=new AreaAnchorService(store,e=>e!=Entity.Null,(a,e,r)=>true);
        Assert.That(anchors.Assign(area,hq,AreaAnchorRole.Headquarters),Is.True);
        anchors.DrainDirty();
        anchors.ForgetArea(area);
        Assert.That(anchors.DrainDirty(),Is.EquivalentTo(new[]{area}));
        anchors.InvalidateAnchor(hq);
        Assert.That(anchors.DrainDirty(),Is.Empty);
    }
}
