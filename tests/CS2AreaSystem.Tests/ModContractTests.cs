using NUnit.Framework;
using Game.Modding;
namespace CS2AreaSystem.Tests;
public class ModContractTests {
    [Test] public void ModSupportsGameLifecycle() {
        IMod mod = new Mod();
        Assert.DoesNotThrow(() => mod.OnDispose());
    }
}

