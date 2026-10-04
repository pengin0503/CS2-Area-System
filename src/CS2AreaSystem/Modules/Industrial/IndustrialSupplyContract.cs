using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Phase 3 D1: one advisory supply agreement owned by its source Industrial Area. Only the player's configuration is
    // saved; source capacity, destination need and status are derived at runtime and never persisted. The destination is
    // referenced by its StableId, so a deleted and recreated Area is never silently rebound.
    public struct IndustrialSupplyContract : IBufferElementData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public int ContractId;
        public ulong DestinationStableId;
        // One existing vanilla Game.Economy.Resource bit (see IndustrialSupplyContractModel.IsValidResource).
        public ulong Resource;
        public int TargetPerDay;
        public bool Enabled;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(ContractId);
            writer.Write(DestinationStableId);
            writer.Write(Resource);
            writer.Write(TargetPerDay);
            writer.Write(Enabled);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out ContractId);
            reader.Read(out DestinationStableId);
            reader.Read(out Resource);
            reader.Read(out TargetPerDay);
            reader.Read(out Enabled);
            TargetPerDay = IndustrialSupplyContractModel.NormalizeTarget(TargetPerDay);
        }
    }
    // Per-source-Area contract id allocator: ids are monotonic and never reused, so a stale UI action can never address a
    // contract created after the one it was rendered for.
    public struct IndustrialSupplyContractState : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public int NextContractId;
        public static IndustrialSupplyContractState CreateDefault() => new IndustrialSupplyContractState { SchemaVersion = CurrentSchemaVersion, NextContractId = 1 };
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(NextContractId);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out NextContractId);
        }
    }
}
