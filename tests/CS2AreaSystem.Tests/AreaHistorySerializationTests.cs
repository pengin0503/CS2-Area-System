using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaHistorySerializationTests {
 [Test] public void RuntimeStoreSeedsBeforeFirstPublishOncePerLoadAndPreservesCompletedFlowValues() {
  var store=new IndustrialRuntimeStore();int seeded=0;
  store.Initialize=s=>{seeded++;IndustrialHistorySystem.Seed(new[]{new IndustrialHistoryEntry{HourBucket=8,Availability=16,Performance=80}},s);};
  var first=store.GetOrCreate(7,new(){Index=1,Version=1});first.Publish(new(){Active=true,HourBucket=9,Performance=IndustrialMetric.Of(90)});
  Assert.That(first.Trends.Performance.TryPrevious(out var previous),Is.True);Assert.That(previous.Value,Is.EqualTo(80));
  store.GetOrCreate(7,first.Area);Assert.That(seeded,Is.EqualTo(1));store.Clear();store.GetOrCreate(7,first.Area);Assert.That(seeded,Is.EqualTo(2));
  var rows=new List<ParkHistoryEntry>{new(){HourBucket=8,Availability=3,Visitors=12,Quality=80}};
  ParkHistorySystem.Record(rows,new(){HourBucket=8,Availability=2,Quality=90});Assert.That(rows[0].Visitors,Is.EqualTo(12));Assert.That(rows[0].Availability,Is.EqualTo(3));
 }
 [Test] public void ModuleHistoryRoundTripsAllValuesAndAvailabilityWithoutEntityReferences() {
  var park=new ParkHistoryEntry{HourBucket=42,Availability=63,Visitors=1,Quality=2,Satisfaction=3,Occupancy=4,AdmissionIncome=5,ManagementExpense=6};
  var restored=MemorySerializer.RoundTrip(park);Assert.That(restored,Is.EqualTo(new ParkHistoryEntry{SchemaVersion=1,HourBucket=42,Availability=63,Visitors=1,Quality=2,Satisfaction=3,Occupancy=4,AdmissionIncome=5,ManagementExpense=6}));
  var industrial=new IndustrialHistoryEntry{HourBucket=43,Availability=21,Production=7,Logistics=8,Employment=9,Efficiency=10,Performance=11,ProductiveFacilities=12};
  var result=MemorySerializer.RoundTrip(industrial);Assert.That(result.Performance,Is.EqualTo(11));Assert.That(result.Availability,Is.EqualTo(21));
  var io=MemorySerializer.Create();park.Serialize(io.Writer);Assert.That(io.Data.Tokens.Any(v=>v is Entity),Is.False);
 }
 [Test] public void RetentionReplacesExistingHoursIgnoresOldUnknownHoursAndEvictsAt168() {
  var entries=new List<ParkHistoryEntry>();
  for(int i=0;i<200;i++)ParkHistorySystem.Record(entries,new(){HourBucket=i,Availability=2,Quality=i});
  Assert.That(entries.Count,Is.EqualTo(168));Assert.That(entries[0].HourBucket,Is.EqualTo(32));
  ParkHistorySystem.Record(entries,new(){HourBucket=198,Availability=2,Quality=99});Assert.That(entries[^2].Quality,Is.EqualTo(99));
  ParkHistorySystem.Record(entries,new(){HourBucket=0});Assert.That(entries.Count,Is.EqualTo(168));
  var industrial=new List<IndustrialHistoryEntry>();for(int i=0;i<200;i++)IndustrialHistorySystem.Record(industrial,new(){HourBucket=i});Assert.That(industrial.Count,Is.EqualTo(168));
 }
 [Test] public void Newest24SeedA1AndCurrentHourReplacementDoesNotDuplicateSamples() {
  var history=Enumerable.Range(0,168).Select(i=>new IndustrialHistoryEntry{HourBucket=i,Availability=17,Production=i,Performance=2*i}).ToArray();
  var runtime=new IndustrialRuntimeSnapshot();IndustrialHistorySystem.Seed(history,runtime);
  Assert.That(runtime.Trends.Performance.Count,Is.EqualTo(24));Assert.That(runtime.Trends.Logistics.Count,Is.Zero);
  runtime.Publish(new(){Active=true,HourBucket=167,Performance=IndustrialMetric.Of(999)});
  Assert.That(runtime.Trends.Performance.Count,Is.EqualTo(24));Assert.That(runtime.Trends.Performance.TryLatest(out var latest),Is.True);Assert.That(latest.Value,Is.EqualTo(999));
  var park=new ParkRuntimeSnapshot();ParkHistorySystem.Seed(Array.Empty<ParkHistoryEntry>(),park);Assert.That(park.Trends.Quality.Count,Is.Zero);
 }
 [Test] public void UnsupportedHistorySchemasAreRejectedBeforeReadingValues() {
  var io=MemorySerializer.Create();io.Data.Tokens.Add((ushort)2);var entry=new ParkHistoryEntry();Assert.Throws<System.IO.InvalidDataException>(()=>entry.Deserialize(io.Reader));Assert.That(io.Data.Position,Is.EqualTo(1));
  var other=MemorySerializer.Create();Assert.Throws<System.IO.InvalidDataException>(()=>new IndustrialHistoryEntry{SchemaVersion=3}.Serialize(other.Writer));Assert.That(other.Data.Tokens,Is.Empty);
 }
 [Test] public void CapturingA1PreservesDelayedFlowHoursWithoutInventingUnavailableSamples() {
  var runtime=new ParkRuntimeSnapshot();runtime.Trends.Quality.Record(20,80);runtime.Trends.Visitors.Record(19,12);
  var entries=ParkHistorySystem.Capture(runtime);
  Assert.That(entries.Select(e=>e.HourBucket),Is.EqualTo(new long[]{19,20}));
  Assert.That(entries[0].Availability,Is.EqualTo(1));Assert.That(entries[1].Availability,Is.EqualTo(2));
  var seeded=new ParkRuntimeSnapshot();ParkHistorySystem.Seed(entries,seeded);Assert.That(seeded.Trends.Satisfaction.Count,Is.Zero);
 }
}
