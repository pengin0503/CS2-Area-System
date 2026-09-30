using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Core.Areas
{
    public struct ManagedAreaSettings : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public float BudgetMultiplier;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(BudgetMultiplier);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out BudgetMultiplier);
        }
    }
}
