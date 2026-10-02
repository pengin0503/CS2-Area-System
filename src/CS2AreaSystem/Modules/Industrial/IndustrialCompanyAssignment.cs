using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Authoritative Area-owned claim. Building captures the renter identity at assignment time;
    // both references use the game's remapping contract, never raw Entity indices in saved data.
    public struct IndustrialCompanyAssignment : IBufferElementData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public Entity Company, Building;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion); writer.Write(Company); writer.Write(Building);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion); ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out Company); reader.Read(out Building);
        }
    }
}
