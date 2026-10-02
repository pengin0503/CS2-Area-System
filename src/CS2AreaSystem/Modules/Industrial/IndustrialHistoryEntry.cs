using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // One hour, six A1 metrics. No live entity or runtime observation state is persisted.
    [InternalBufferCapacity(0)]
    public struct IndustrialHistoryEntry : IBufferElementData, ISerializable
    {
        public const ushort CurrentSchemaVersion=1;
        public ushort SchemaVersion;
        public long HourBucket;
        public byte Availability;
        public double Production,Logistics,Employment,Efficiency,Performance,ProductiveFacilities;
        public void Serialize<TWriter>(TWriter writer) where TWriter:IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion==0?CurrentSchemaVersion:SchemaVersion);
            writer.Write(CurrentSchemaVersion);writer.Write(HourBucket);writer.Write(Availability);
            writer.Write(Production); writer.Write(Logistics); writer.Write(Employment); writer.Write(Efficiency); writer.Write(Performance); writer.Write(ProductiveFacilities);
        }
        public void Deserialize<TReader>(TReader reader) where TReader:IReader
        {
            reader.Read(out SchemaVersion);ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out HourBucket);reader.Read(out Availability);
            reader.Read(out Production); reader.Read(out Logistics); reader.Read(out Employment); reader.Read(out Efficiency); reader.Read(out Performance); reader.Read(out ProductiveFacilities);
        }
    }
}
