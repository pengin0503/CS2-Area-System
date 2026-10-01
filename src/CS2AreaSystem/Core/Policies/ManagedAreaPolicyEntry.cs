using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Core.Policies
{
    // Only the player's authoritative choice is saved. Unlock, conflict, effective state and upkeep are derived after load.
    // An ID without a registered definition (module removed) is kept as inert data so the choice returns with the module.
    public struct ManagedAreaPolicyEntry : IBufferElementData, ISerializable
    {
        public const ushort SchemaVersion = 1;
        public int PolicyId;
        public byte DesiredEnabled;
        public ManagedAreaPolicyEntry(int policyId, bool desiredEnabled)
        {
            PolicyId = policyId;
            DesiredEnabled = desiredEnabled ? (byte)1 : (byte)0;
        }
        public bool IsDesiredEnabled => DesiredEnabled != 0;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        { writer.Write(SchemaVersion); writer.Write(PolicyId); writer.Write(DesiredEnabled); }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        { reader.Read(out ushort version); ComponentSchema.RequireSupported(version); reader.Read(out PolicyId); reader.Read(out DesiredEnabled); }
    }
}
