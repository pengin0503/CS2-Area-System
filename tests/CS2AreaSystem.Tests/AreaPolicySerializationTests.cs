using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaPolicySerializationTests {
    [Test] public void EntryIsAGameSerializedBufferElement() {
        Assert.That(typeof(IBufferElementData).IsAssignableFrom(typeof(ManagedAreaPolicyEntry)), Is.True);
        Assert.That(typeof(ISerializable).IsAssignableFrom(typeof(ManagedAreaPolicyEntry)), Is.True);
    }
    [Test] public void EntrySerializesSchemaThenPolicyIdThenDesiredByte() {
        var io = MemorySerializer.Create();
        new ManagedAreaPolicyEntry(2002, true).Serialize(io.Writer);
        Assert.That(io.Data.Tokens, Is.EqualTo(new object[] { (ushort)1, 2002, (byte)1 }));
    }
    [Test] public void OnAndOffStatesRoundTrip() {
        var on = MemorySerializer.RoundTrip(new ManagedAreaPolicyEntry(3005, true));
        Assert.That(on.PolicyId, Is.EqualTo(3005));
        Assert.That(on.IsDesiredEnabled, Is.True);
        var off = MemorySerializer.RoundTrip(new ManagedAreaPolicyEntry(3006, false));
        Assert.That(off.PolicyId, Is.EqualTo(3006));
        Assert.That(off.IsDesiredEnabled, Is.False);
    }
    [Test] public void UnknownPolicyIdRoundTripsUnchanged() {
        var registry = new AreaPolicyRegistry();
        var restored = MemorySerializer.RoundTrip(new ManagedAreaPolicyEntry { PolicyId = 4242, DesiredEnabled = 1 });
        Assert.That(restored.PolicyId, Is.EqualTo(4242));
        Assert.That(restored.DesiredEnabled, Is.EqualTo(1));
        Assert.That(registry.TryGet(restored.PolicyId, out _), Is.False, "unknown IDs are inert data, not an error");
        var again = MemorySerializer.RoundTrip(restored);
        Assert.That(again.PolicyId, Is.EqualTo(4242));
        Assert.That(again.DesiredEnabled, Is.EqualTo(1));
    }
    [Test] public void UnsupportedSchemaStopsBeforeReadingThePayload() {
        var io = MemorySerializer.Create();
        io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(2001); io.Data.Tokens.Add((byte)1);
        var entry = new ManagedAreaPolicyEntry();
        Assert.Throws<System.IO.InvalidDataException>(() => entry.Deserialize(io.Reader));
        Assert.That(io.Data.Position, Is.EqualTo(1));
    }
}
