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
 [Test] public void PolicyToggleArgumentsAreStrictlyParsed() {
  Assert.That(AreaPolicyToggleRequest.TryParse("2002","1",out var id,out var on),Is.True); Assert.That(id,Is.EqualTo(2002)); Assert.That(on,Is.True);
  Assert.That(AreaPolicyToggleRequest.TryParse("3005","0",out id,out on),Is.True); Assert.That(id,Is.EqualTo(3005)); Assert.That(on,Is.False);
  foreach(var (policy,enabled) in new[]{("2002","true"),("2002",""),("2002",null),("-1","1"),("0","1"),("20 02","1"),("+2002","1"),("2002.0","1"),(null,"1"),("99999999999","1")})
   Assert.That(AreaPolicyToggleRequest.TryParse(policy!,enabled!,out _,out _),Is.False,policy+"/"+enabled);
 }
}
