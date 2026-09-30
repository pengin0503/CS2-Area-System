using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Core.Areas
{
    public struct ManagedAreaProgress : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public int Level;
        public long Experience;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(Level);
            writer.Write(Experience);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out Level);
            reader.Read(out Experience);
        }
    }
}
