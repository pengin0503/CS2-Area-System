using System.Reflection;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class IndustrialSerializationTests {
 internal static IndustrialAreaState Sample() => new() {
  SchemaVersion = IndustrialAreaState.CurrentSchemaVersion, LifetimePeakEmployees = 1234, XpDay = 41, XpGrantedToday = 170, LastSettledXpHour = 1003,
  ActivityDay = 41, ActivityWeightedSum = 1234.5678, ActivityObservedHours = 15.25, LastCreditedActivityHour = 1002,
  HighThroughputStreakDays = 4, HighThroughputLastDay = 40 };
 [Test] public void SchemaV1WritesExactlyTheApprovedFieldsInOrder() {
  var io = MemorySerializer.Create();
  Sample().Serialize(io.Writer);
  Assert.That(io.Data.Tokens, Is.EqualTo(new object[] { (ushort)1, 1234, 41u, 170, 1003L, 41u, 1234.5678, 15.25, 1002L, 4, 40u }));
  Assert.That(io.Data.Tokens.Select(t => t.GetType()), Is.EqualTo(new[] { typeof(ushort), typeof(int), typeof(uint), typeof(int), typeof(long), typeof(uint),
   typeof(double), typeof(double), typeof(long), typeof(int), typeof(uint) }));
 }
 [Test] public void RoundTripPreservesPartialDayAccumulatorsAndDeduplicationHours() {
  var io = MemorySerializer.Create();
  Sample().Serialize(io.Writer);
  var copy = new IndustrialAreaState(); copy.Deserialize(io.Reader);
  Assert.That(io.Data.Position, Is.EqualTo(io.Data.Tokens.Count), "reads exactly what it writes");
  Assert.That(copy, Is.EqualTo(Sample()));
 }
 [Test] public void UnsupportedFutureSchemaIsRejectedBeforeReadingPayload() {
  var io = MemorySerializer.Create(); io.Data.Tokens.Add((ushort)2); io.Data.Tokens.Add(99);
  var state = new IndustrialAreaState();
  Assert.Throws<System.IO.InvalidDataException>(() => state.Deserialize(io.Reader));
  Assert.That(io.Data.Position, Is.EqualTo(1));
  var writer = MemorySerializer.Create();
  Assert.Throws<System.IO.InvalidDataException>(() => new IndustrialAreaState { SchemaVersion = 7 }.Serialize(writer.Writer), "never overwrites an unknown schema");
 }
 [Test] public void DefaultStateStartsOnTheCurrentGameDayWithoutSentinelCollisions() {
  var state = IndustrialAreaState.CreateDefault(0);
  Assert.That(state.SchemaVersion, Is.EqualTo(IndustrialAreaState.CurrentSchemaVersion));
  Assert.That(state.XpDay, Is.Zero); Assert.That(state.ActivityDay, Is.Zero);
  Assert.That(state.LastSettledXpHour, Is.EqualTo(-1)); Assert.That(state.LastCreditedActivityHour, Is.EqualTo(-1));
  Assert.That(state.HighThroughputLastDay, Is.EqualTo(uint.MaxValue), "game day zero stays a valid completed day");
  Assert.That(state.ActivityObservedHours, Is.Zero, "zero hours means no coverage");
  Assert.That(state.XpGrantedToday, Is.Zero); Assert.That(state.HighThroughputStreakDays, Is.Zero); Assert.That(state.LifetimePeakEmployees, Is.Zero);
  var later = IndustrialAreaState.CreateDefault(57);
  Assert.That(later.XpDay, Is.EqualTo(57u)); Assert.That(later.ActivityDay, Is.EqualTo(57u));
 }
 [Test] public void ZeroSchemaVersionIsWrittenAsCurrent() {
  var io = MemorySerializer.Create();
  new IndustrialAreaState().Serialize(io.Writer);
  Assert.That(io.Data.Tokens[0], Is.EqualTo(IndustrialAreaState.CurrentSchemaVersion));
 }
 [Test] public void RepeatedSaveAndLoadNeverResetsDayAccumulators() {
  var state = Sample();
  for (int i = 0; i < 3; i++) state = MemorySerializer.RoundTrip(state);
  Assert.That(state.ActivityWeightedSum, Is.EqualTo(1234.5678)); Assert.That(state.ActivityObservedHours, Is.EqualTo(15.25));
  Assert.That(state.XpGrantedToday, Is.EqualTo(170)); Assert.That(state.LastSettledXpHour, Is.EqualTo(1003));
 }
 [Test] public void StateHoldsNoCurrentScoresLevelOrRawWindows() {
  var fields = typeof(IndustrialAreaState).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name);
  Assert.That(fields, Is.EqualTo(new[] { "SchemaVersion", "LifetimePeakEmployees", "XpDay", "XpGrantedToday", "LastSettledXpHour", "ActivityDay",
   "ActivityWeightedSum", "ActivityObservedHours", "LastCreditedActivityHour", "HighThroughputStreakDays", "HighThroughputLastDay" }),
   "Level/Experience stay in ManagedAreaProgress; scores, jobs, efficiency and windows are runtime-only");
 }
}
