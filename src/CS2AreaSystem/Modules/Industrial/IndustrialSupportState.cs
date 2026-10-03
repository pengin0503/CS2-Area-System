using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Authoritative Industrial support data (Phase 2 C4): the player's per-company daily setting, the day whose request
    // was accepted (save/load dedupe) and lifetime accounting. Pending rosters and requests are runtime-only and never
    // saved, so a load can never replay a payment. Accounting invariant: LifetimeSettled = LifetimeCredited + LifetimeUnallocated.
    public struct IndustrialSupportState : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public const long NoDay = -1;
        public ushort SchemaVersion;
        public int DailySupportPerCompany;
        public long LastScheduledDay;
        public long LifetimeRequested, LifetimeSettled, LifetimeCredited, LifetimeUnallocated;
        public static IndustrialSupportState CreateDefault() => new IndustrialSupportState {
            SchemaVersion = CurrentSchemaVersion, DailySupportPerCompany = IndustrialSupportModel.DefaultPerCompany, LastScheduledDay = NoDay };
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(DailySupportPerCompany);
            writer.Write(LastScheduledDay);
            writer.Write(LifetimeRequested);
            writer.Write(LifetimeSettled);
            writer.Write(LifetimeCredited);
            writer.Write(LifetimeUnallocated);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out DailySupportPerCompany);
            reader.Read(out LastScheduledDay);
            reader.Read(out LifetimeRequested);
            reader.Read(out LifetimeSettled);
            reader.Read(out LifetimeCredited);
            reader.Read(out LifetimeUnallocated);
            DailySupportPerCompany = IndustrialSupportModel.NormalizeSetting(DailySupportPerCompany);
        }
    }
}
