using CS2AreaSystem.Core.UI;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaUIActionGuardTests {
 [Test] public void StaleSelectionOrIdCannotMutateAnotherArea() {
 var a=new Entity{Index=1,Version=2};var b=new Entity{Index=2,Version=2};
 Assert.That(AreaUIActionGuard.Matches(a,a,10,"10",true),Is.True);
 Assert.That(AreaUIActionGuard.Matches(a,b,10,"10",true),Is.False);
 Assert.That(AreaUIActionGuard.Matches(a,a,11,"10",true),Is.False);
 Assert.That(AreaUIActionGuard.Matches(a,a,10,"10",false),Is.False);
 Assert.That(AreaUIActionGuard.Matches(a,a,10,"invalid",true),Is.False);
 }
}
