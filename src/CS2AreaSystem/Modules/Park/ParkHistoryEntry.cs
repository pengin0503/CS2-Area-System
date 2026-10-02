using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // One hour, six A1 metrics. No live entity or runtime observation state is persisted.
    [InternalBufferCapacity(0)]
    public struct ParkHistoryEntry : IBufferElementData, ISerializable
    {
        public const ushort CurrentSchemaVersion=1;
        public ushort SchemaVersion;
        public long HourBucket;
        public byte Availability;
        public double Visitors,Quality,Satisfaction,Occupancy,AdmissionIncome,ManagementExpense;
        public void Serialize<TWriter>(TWriter writer) where TWriter:IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion==0?CurrentSchemaVersion:SchemaVersion);
            writer.Write(CurrentSchemaVersion);writer.Write(HourBucket);writer.Write(Availability);
            writer.Write(Visitors); writer.Write(Quality); writer.Write(Satisfaction); writer.Write(Occupancy); writer.Write(AdmissionIncome); writer.Write(ManagementExpense);
        }
        public void Deserialize<TReader>(TReader reader) where TReader:IReader
        {
            reader.Read(out SchemaVersion);ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out HourBucket);reader.Read(out Availability);
            reader.Read(out Visitors); reader.Read(out Quality); reader.Read(out Satisfaction); reader.Read(out Occupancy); reader.Read(out AdmissionIncome); reader.Read(out ManagementExpense);
        }
    }
}
