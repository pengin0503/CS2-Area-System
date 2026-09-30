using NUnit.Framework;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem.Tests;
public class AreaLifecyclePolicyTests {
    [TestCase(false, false, false, AreaLifecycleState.ModuleUnavailable)]
    [TestCase(true, false, true, AreaLifecycleState.Deleted)]
    [TestCase(false, true, true, AreaLifecycleState.Deleted)]
    [TestCase(true, false, false, AreaLifecycleState.Suspended)]
    [TestCase(true, true, false, AreaLifecycleState.Active)]
    public void ResolvesValidatedState(bool known, bool ready, bool deleted, AreaLifecycleState expected) =>
        Assert.That(AreaLifecyclePolicy.Resolve(known, ready, deleted), Is.EqualTo(expected));
}
