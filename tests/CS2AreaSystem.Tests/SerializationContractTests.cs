using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class SerializationContractTests {
    [Test] public void UnsupportedSchemaStopsBeforeReadingUnknownPayload() {
        var io=MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(9999);
        var value=new ManagedArea();
        Assert.Throws<System.IO.InvalidDataException>(()=>value.Deserialize(io.Reader));
        Assert.That(io.Data.Position, Is.EqualTo(1));
    }
    private static T RoundTrip<T>(T value) where T:struct {
        Assert.That(value, Is.InstanceOf<ISerializable>(), "authoritative component must use game serialization");
        var io=MemorySerializer.Create();
        ((ISerializable)(object)value).Serialize(io.Writer);
        Assert.That(io.Data.Tokens[0], Is.TypeOf<ushort>(), "schema first");
        object copy=default(T); ((ISerializable)copy).Deserialize(io.Reader); return (T)copy;
    }
    [Test] public void UnknownFutureTypeSurvivesRoundTripAndDegradesWithoutDeleting() {
        var value=RoundTrip(new ManagedArea { SchemaVersion=1,TypeId=9999,State=AreaLifecycleState.Active });
        Assert.That(value.TypeId, Is.EqualTo(9999));
        Assert.That(value.State, Is.EqualTo(AreaLifecycleState.Active));
        var registry=new AreaTypeRegistry();
        Assert.That(registry.TryGet(value.TypeId,out _), Is.False);
        Assert.That(AreaLifecyclePolicy.Resolve(false,false,false), Is.EqualTo(AreaLifecycleState.ModuleUnavailable));
        Assert.That(RoundTrip(new ManagedAreaStableId{Value=87654321}).Value, Is.EqualTo(87654321));
    }
    [Test] public void ProgressAndSettingsRoundTripEveryField() {
        var progress=RoundTrip(new ManagedAreaProgress {Level=4,Experience=9223372036854});
        Assert.That(progress.Level, Is.EqualTo(4)); Assert.That(progress.Experience, Is.EqualTo(9223372036854));
        Assert.That(RoundTrip(new ManagedAreaSettings{BudgetMultiplier=1.25f}).BudgetMultiplier, Is.EqualTo(1.25f));
    }
}
