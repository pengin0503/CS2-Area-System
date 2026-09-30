using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Persistence;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Bounded rolling satisfaction aggregate with a neutral low-confidence prior (design §9.7).
    public struct ParkSatisfactionState
    {
        public const float Prior = 50f, PriorWeight = 3f, MaxWeight = 100f;
        public float Mean;
        public float Weight;
        public float Display => (Prior * PriorWeight + Mean * Weight) / (PriorWeight + Weight);
        public float Confidence => Weight / (PriorWeight + Weight);
        public ParkSatisfactionState Add(float sample)
        {
            if (float.IsNaN(sample)) return this;
            sample = sample < 0 ? 0 : sample > 100 ? 100 : sample;
            var next = this;
            next.Weight = Weight + 1f > MaxWeight ? MaxWeight : Weight + 1f;
            next.Mean = Weight <= 0f ? sample : Mean + (sample - Mean) / next.Weight;
            return next;
        }
    }
    // Authoritative Park-only data. Derived caches (landscape, facilities, sessions, current Quality) are never stored.
    public struct ParkAreaState : IComponentData, ISerializable
    {
        public const ushort CurrentSchemaVersion = 1;
        public ushort SchemaVersion;
        public int AdmissionFee;
        public long LifetimeVisitors, LifetimeResidentVisitors, LifetimeTouristVisitors;
        public long LifetimeAdmissionRevenue, LifetimeManagementExpense;
        public ParkSatisfactionState Satisfaction, ResidentSatisfaction, TouristSatisfaction;
        // Simulation frames the next promotion's requirements have been continuously met.
        public uint PromotionHoldFrames;
        // In-game day index and passive Quality XP already granted that day (daily cap survives save/load).
        public uint PassiveXpDay;
        public int PassiveXpToday;
        public static ParkAreaState CreateDefault() => new ParkAreaState { SchemaVersion = CurrentSchemaVersion };
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            ComponentSchema.RequireSupported(SchemaVersion == 0 ? CurrentSchemaVersion : SchemaVersion);
            writer.Write(CurrentSchemaVersion);
            writer.Write(AdmissionFee);
            writer.Write(LifetimeVisitors); writer.Write(LifetimeResidentVisitors); writer.Write(LifetimeTouristVisitors);
            writer.Write(LifetimeAdmissionRevenue); writer.Write(LifetimeManagementExpense);
            Write(writer, Satisfaction); Write(writer, ResidentSatisfaction); Write(writer, TouristSatisfaction);
            writer.Write(PromotionHoldFrames);
            writer.Write(PassiveXpDay); writer.Write(PassiveXpToday);
        }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out SchemaVersion);
            ComponentSchema.RequireSupported(SchemaVersion);
            reader.Read(out AdmissionFee);
            reader.Read(out LifetimeVisitors); reader.Read(out LifetimeResidentVisitors); reader.Read(out LifetimeTouristVisitors);
            reader.Read(out LifetimeAdmissionRevenue); reader.Read(out LifetimeManagementExpense);
            Satisfaction = Read(reader); ResidentSatisfaction = Read(reader); TouristSatisfaction = Read(reader);
            reader.Read(out PromotionHoldFrames);
            reader.Read(out PassiveXpDay); reader.Read(out PassiveXpToday);
        }
        private static void Write<TWriter>(TWriter writer, ParkSatisfactionState state) where TWriter : IWriter
        { writer.Write(state.Mean); writer.Write(state.Weight); }
        private static ParkSatisfactionState Read<TReader>(TReader reader) where TReader : IReader
        { reader.Read(out float mean); reader.Read(out float weight); return new ParkSatisfactionState { Mean = mean, Weight = weight }; }
    }
}
