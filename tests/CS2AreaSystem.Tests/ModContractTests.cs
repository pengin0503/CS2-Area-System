using System.Runtime.Serialization;
using NUnit.Framework;
using Game.Modding;
namespace CS2AreaSystem.Tests;
public class ModContractTests {
    // Game.Modding.ModManager (1.6.2f1) creates mods with FormatterServices.GetUninitializedObject:
    // no constructor or instance field initializer runs.
#pragma warning disable SYSLIB0050
    private static IMod CreateLikeGame() => (IMod)FormatterServices.GetUninitializedObject(typeof(Mod));
#pragma warning restore SYSLIB0050
    [Test] public void ModSupportsGameLifecycle() {
        IMod mod = new Mod();
        Assert.DoesNotThrow(() => mod.OnDispose());
    }
    [Test] public void DisposeWithoutLoadDoesNotThrowWhenCreatedLikeTheGame() {
        var mod = CreateLikeGame();
        Assert.DoesNotThrow(() => mod.OnDispose());
        Assert.DoesNotThrow(() => mod.OnDispose());
    }
    [Test] public void ModConstructorDoesNothingTheGameWouldSkip() {
        // Only "ldarg.0; call object::.ctor; [nop]; ret" is allowed: field initializers compile into the constructor.
        var il = typeof(Mod).GetConstructor(Type.EmptyTypes)!.GetMethodBody()!.GetILAsByteArray()!;
        Assert.That(il.Length, Is.LessThanOrEqualTo(8), "Mod instance state must be created in OnLoad, not by initializers or constructors");
    }
}
