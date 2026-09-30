using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Prefabs;
using Game.Areas;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class NativeGameplayCreationTests {
 [TestCase(AreaTypeId.Park)][TestCase(AreaTypeId.Industrial)][TestCase(AreaTypeId.Tourism)]
 public void EveryRegisteredKindPassesNativeGameplayCreationGate(AreaTypeId kind) {
  var metadata=ManagedAreaPrefabFactory.Map(ManagedAreaPrefabFactory.Describe(kind),_=>true);
  Assert.That(NativeAreaCreationPolicy.CanCreate(false,metadata.GeometryType,0,false),Is.True,"Native tool activation alone does not allow drawing");
 }
 [Test] public void LotRemainsEditorOnlyAndUnrenderedSurfaceIsRejected() {
  Assert.That(NativeAreaCreationPolicy.CanCreate(false,Game.Areas.AreaType.Lot,0,false),Is.False);
  Assert.That(NativeAreaCreationPolicy.CanCreate(true,Game.Areas.AreaType.Lot,0,false),Is.True);
  Assert.That(NativeAreaCreationPolicy.CanCreate(false,Game.Areas.AreaType.Surface,0,false),Is.False);
  Assert.That(NativeAreaCreationPolicy.CanCreate(false,Game.Areas.AreaType.Surface,0,true),Is.True);
 }
}
