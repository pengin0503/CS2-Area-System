using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Core.Anchors
{
    public struct AreaAnchorLink : IBufferElementData, ISerializable
    {
        public Entity Anchor;
        public AreaAnchorRole Role;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        { writer.Write((ushort)1); writer.Write(Anchor); writer.Write((byte)Role); }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        { reader.Read(out ushort version); ComponentSchema.RequireSupported(version); reader.Read(out Anchor); reader.Read(out byte role); Role = (AreaAnchorRole)role; }
    }
}
