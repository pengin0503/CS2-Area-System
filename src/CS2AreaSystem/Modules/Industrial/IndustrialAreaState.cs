using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Authoritative Industrial-only data (design §17). Level/Experience stay in shared ManagedAreaProgress. Only the
    // minimal history needed for XP deduplication and the daily-activity streak is stored; current scores, jobs,
    // efficiency, facility caches and rolling windows are runtime-only and rebuilt after load.
    public struct IndustrialAreaState : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public const long NoHour = -1;
        public const uint NoDay = uint.MaxValue;
        public ushort SchemaVersion;
        public int LifetimePeakEmployees;
        // Game calendar day whose XP grants XpGrantedToday counts (daily cap), and the last settled hour bucket.
        public uint XpDay;
        public int XpGrantedToday;
        public long LastSettledXpHour;
        // Current-day time-weighted productive activity: Σ activity·hours, Σ covered hours, last credited hour bucket.
        public uint ActivityDay;
        public double ActivityWeightedSum;
        public double ActivityObservedHours;
        public long LastCreditedActivityHour;
        // Consecutive qualifying completed days and the last completed day evaluated (NoDay before the first).
        public int HighThroughputStreakDays;
        public uint HighThroughputLastDay;
        public static IndustrialAreaState CreateDefault(uint currentDay) => new IndustrialAreaState {
            SchemaVersion = CurrentSchemaVersion, XpDay = currentDay, ActivityDay = currentDay,
            LastSettledXpHour = NoHour, LastCreditedActivityHour = NoHour, HighThroughputLastDay = NoDay };
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(LifetimePeakEmployees);
            writer.Write(XpDay);
            writer.Write(XpGrantedToday);
            writer.Write(LastSettledXpHour);
            writer.Write(ActivityDay);
            writer.Write(ActivityWeightedSum);
            writer.Write(ActivityObservedHours);
            writer.Write(LastCreditedActivityHour);
            writer.Write(HighThroughputStreakDays);
            writer.Write(HighThroughputLastDay);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out LifetimePeakEmployees);
            reader.Read(out XpDay);
            reader.Read(out XpGrantedToday);
            reader.Read(out LastSettledXpHour);
            reader.Read(out ActivityDay);
            reader.Read(out ActivityWeightedSum);
            reader.Read(out ActivityObservedHours);
            reader.Read(out LastCreditedActivityHour);
            reader.Read(out HighThroughputStreakDays);
            reader.Read(out HighThroughputLastDay);
        }
    }
}
