using CS2AreaSystem.Core.UI;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaUIActionGuardTests {
 private static readonly Entity a=new(){Index=1,Version=2}, b=new(){Index=2,Version=2};
 [Test] public void StaleSelectionOrIdCannotMutateAnotherArea() {
  Assert.That(AreaUIActionGuard.Matches(a,a,a,10,"10",true),Is.True);
  Assert.That(AreaUIActionGuard.Matches(a,b,b,10,"10",true),Is.False);
  Assert.That(AreaUIActionGuard.Matches(a,a,a,11,"10",true),Is.False);
  Assert.That(AreaUIActionGuard.Matches(a,a,a,10,"10",false),Is.False);
  Assert.That(AreaUIActionGuard.Matches(a,a,a,10,"invalid",true),Is.False);
  Assert.That(AreaUIActionGuard.Matches(a,a,a,0,"0",true),Is.False);
 }
 [Test] public void DelayedTriggerIsRejectedWhenLiveSelectionMovedBeforeSelectedInfoRefreshed() {
  // published/cached A, but SetSelection already changed ToolSystem.selected to B or Null.
  Assert.That(AreaUIActionGuard.Matches(a,a,b,10,"10",true),Is.False);
  Assert.That(AreaUIActionGuard.Matches(a,a,Entity.Null,10,"10",true),Is.False);
 }
 [Test] public void RecreatedEntityWithOldIndexButNewVersionIsNotTheSameSelection() {
  var recreated=new Entity{Index=a.Index,Version=a.Version+1};
  Assert.That(AreaUIActionGuard.Matches(a,a,recreated,10,"10",true),Is.False);
 }
 [Test] public void CancelRestoresOnlyTheSameLivePendingArea() {
  Assert.That(AreaUIActionGuard.CanRestorePending(true,10,10,"10"),Is.True);
  Assert.That(AreaUIActionGuard.CanRestorePending(false,0,10,"10"),Is.False,"deleted pending area");
  Assert.That(AreaUIActionGuard.CanRestorePending(true,11,10,"10"),Is.False,"entity reused by another area");
  Assert.That(AreaUIActionGuard.CanRestorePending(true,10,10,"11"),Is.False,"stale cancel for another area");
 }
}
