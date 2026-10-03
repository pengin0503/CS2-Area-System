using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // One started Park event (newest ParkEventModel.HistoryCapacity kept, saved so the Events tab survives load).
    [InternalBufferCapacity(0)]
    public struct ParkEventHistoryEntry : IBufferElementData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public int EventId;
        public uint StartFrame, EndFrameExclusive;
        public long SettledExpense;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(EventId); writer.Write(StartFrame); writer.Write(EndFrameExclusive); writer.Write(SettledExpense);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out EventId); reader.Read(out StartFrame); reader.Read(out EndFrameExclusive); reader.Read(out SettledExpense);
        }
    }
}
