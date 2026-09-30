using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkSerializationTests {
 internal static ParkAreaState Sample() => new() {
  AdmissionFee = 25, LifetimeVisitors = 30001, LifetimeResidentVisitors = 24000, LifetimeTouristVisitors = 6001,
  LifetimeAdmissionRevenue = 9_000_000_123, LifetimeManagementExpense = 4_500_000_456,
  Satisfaction = new ParkSatisfactionState { Mean = 71.5f, Weight = 180f },
  ResidentSatisfaction = new ParkSatisfactionState { Mean = 70f, Weight = 150f },
  TouristSatisfaction = new ParkSatisfactionState { Mean = 78f, Weight = 30f },
  PromotionHoldFrames = 123456, PassiveXpDay = 77, PassiveXpToday = 12 };
 [Test] public void CurrentSchemaRoundTripsEveryAuthoritativeField() {
  var io = MemorySerializer.Create();
  var state = Sample(); state.Serialize(io.Writer);
  Assert.That(io.Data.Tokens[0], Is.EqualTo(ParkAreaState.CurrentSchemaVersion));
  var copy = new ParkAreaState(); copy.Deserialize(io.Reader);
  Assert.That(io.Data.Position, Is.EqualTo(io.Data.Tokens.Count), "reads exactly what it writes");
  Assert.That(copy.AdmissionFee, Is.EqualTo(25));
  Assert.That(copy.LifetimeVisitors, Is.EqualTo(30001)); Assert.That(copy.LifetimeResidentVisitors, Is.EqualTo(24000)); Assert.That(copy.LifetimeTouristVisitors, Is.EqualTo(6001));
  Assert.That(copy.LifetimeAdmissionRevenue, Is.EqualTo(9_000_000_123)); Assert.That(copy.LifetimeManagementExpense, Is.EqualTo(4_500_000_456));
  Assert.That(copy.Satisfaction.Mean, Is.EqualTo(71.5f)); Assert.That(copy.Satisfaction.Weight, Is.EqualTo(180f));
  Assert.That(copy.ResidentSatisfaction.Mean, Is.EqualTo(70f)); Assert.That(copy.TouristSatisfaction.Weight, Is.EqualTo(30f));
  Assert.That(copy.PromotionHoldFrames, Is.EqualTo(123456u)); Assert.That(copy.PassiveXpDay, Is.EqualTo(77u)); Assert.That(copy.PassiveXpToday, Is.EqualTo(12));
 }
 [Test] public void UnsupportedFutureSchemaIsRejectedBeforeReadingPayload() {
  var io = MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(99);
  var state = new ParkAreaState();
  Assert.Throws<System.IO.InvalidDataException>(() => state.Deserialize(io.Reader));
  Assert.That(io.Data.Position, Is.EqualTo(1));
 }
 [Test] public void NewStateStartsFreeWithNeutralSatisfaction() {
  var state = ParkAreaState.CreateDefault();
  Assert.That(state.SchemaVersion, Is.EqualTo(ParkAreaState.CurrentSchemaVersion));
  Assert.That(state.AdmissionFee, Is.Zero); Assert.That(state.LifetimeVisitors, Is.Zero);
  Assert.That(state.Satisfaction.Display, Is.EqualTo(50f).Within(0.01));
 }
}
