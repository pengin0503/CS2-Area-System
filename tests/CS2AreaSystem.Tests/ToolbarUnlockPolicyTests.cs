using CS2AreaSystem.Core.Prefabs;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ToolbarUnlockPolicyTests {
 [Test] public void PublishesAfterEveryToolbarChangeAndAfterEachLoad() {
  Assert.That(ToolbarUnlockPolicy.NeedsPublish(0,-1,true),Is.False,"nothing attached yet");
  Assert.That(ToolbarUnlockPolicy.NeedsPublish(1,-1,false),Is.True,"first attachment");
  Assert.That(ToolbarUnlockPolicy.NeedsPublish(3,3,false),Is.False,"already published");
  Assert.That(ToolbarUnlockPolicy.NeedsPublish(4,3,false),Is.True,"late attachment of another type");
  Assert.That(ToolbarUnlockPolicy.NeedsPublish(3,3,true),Is.True,"load recomputes vanilla lock state and bindings");
 }
 [Test] public void GroupsFollowTheVanillaRequireAnyRule() {
  Assert.That(ToolbarUnlockPolicy.ShouldUnlock(true,true),Is.True);
  Assert.That(ToolbarUnlockPolicy.ShouldUnlock(true,false),Is.False);
  Assert.That(ToolbarUnlockPolicy.ShouldUnlock(false,true),Is.False);
 }
}
