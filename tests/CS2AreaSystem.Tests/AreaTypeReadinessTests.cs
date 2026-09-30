using CS2AreaSystem.Core.Prefabs;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaTypeReadinessTests {
    [Test] public void UnavailableTypeDoesNotBlockOtherReadyTypes() {
        var readiness=new AreaTypeReadiness();
        readiness.Set(1,true); readiness.Set(2,false); readiness.Set(3,true);
        Assert.That(readiness.IsReady(1),Is.True);
        Assert.That(readiness.IsReady(2),Is.False);
        Assert.That(readiness.IsReady(3),Is.True);
        Assert.That(readiness.AllReady(new[]{1,2,3}),Is.False);
    }
    [TestCase(false,false,true)]
    [TestCase(true,false,true)]
    [TestCase(true,true,false)]
    public void RegistrationPolicyRetriesMissingOrUnregisteredPrefab(bool hasPrefab,bool registered,bool shouldAttempt) =>
        Assert.That(AreaPrefabRegistrationPolicy.ShouldAttempt(hasPrefab,registered),Is.EqualTo(shouldAttempt));
}
