using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Authoritative Park event data (Phase 2 C5). The pending start request is runtime-only and never saved; only a
    // settled, started event and its paid cost are. StartFrame/EndFrameExclusive keep the last started event.
    public struct ParkEventState : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public int ActiveEventId;
        public uint StartFrame, EndFrameExclusive;
        public long LifetimeEvents, LifetimeEventExpense;
        public static ParkEventState CreateDefault() => new ParkEventState { SchemaVersion = CurrentSchemaVersion };
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(ActiveEventId);
            writer.Write(StartFrame); writer.Write(EndFrameExclusive);
            writer.Write(LifetimeEvents); writer.Write(LifetimeEventExpense);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out ActiveEventId);
            reader.Read(out StartFrame); reader.Read(out EndFrameExclusive);
            reader.Read(out LifetimeEvents); reader.Read(out LifetimeEventExpense);
        }
    }
}
