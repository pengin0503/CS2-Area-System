using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialRuntimeModelTests {
 private static readonly Entity a = new() { Index = 10, Version = 1 }, b = new() { Index = 11, Version = 1 }, c = new() { Index = 12, Version = 1 };
 private sealed class Target : IIndustrialStateTarget {
  public readonly Dictionary<Entity, IndustrialAreaState> States = new();
  public readonly Dictionary<Entity, ManagedAreaProgress> Progress = new();
  public readonly HashSet<Entity> Alive = new();
  public bool Exists(Entity area) => Alive.Contains(area);
  public bool HasState(Entity area) => States.ContainsKey(area);
  public void AddState(Entity area, IndustrialAreaState state) => States.Add(area, state);
  public bool HasProgress(Entity area) => Progress.ContainsKey(area);
  public void AddProgress(Entity area, ManagedAreaProgress progress) => Progress.Add(area, progress);
  public readonly Dictionary<Entity, IndustrialSupportState> Support = new();
  public bool HasSupport(Entity area) => Support.ContainsKey(area);
  public void AddSupport(Entity area, IndustrialSupportState state) => Support.Add(area, state);
  public readonly Dictionary<Entity, IndustrialSupplyContractState> Contracts = new();
  public bool HasContracts(Entity area) => Contracts.ContainsKey(area);
  public void AddContracts(Entity area, IndustrialSupplyContractState state) => Contracts.Add(area, state);
 }
 [Test] public void InitializationAddsDefaultSupportStateAndNeverReplacesASavedSetting() {
  var target = new Target(); target.Alive.UnionWith(new[] { a, b });
  var saved = IndustrialSupportModel.MarkScheduled(IndustrialSupportState.CreateDefault(), 40); saved.DailySupportPerCompany = 750;
  target.Support[b] = saved;
  var areas = new[] { new IndustrialAreaRef(a, 1, AreaLifecycleState.Active), new IndustrialAreaRef(b, 2, AreaLifecycleState.Suspended) };
  IndustrialStateInitializer.Ensure(areas, target, 41);
  Assert.That(target.Support[a], Is.EqualTo(IndustrialSupportState.CreateDefault()), "support starts disabled and unscheduled");
  Assert.That(target.Support[b], Is.EqualTo(saved));
 }
 [Test] public void SnapshotsAreKeyedByStableIdAndFollowTheCurrentEntity() {
  var store = new IndustrialRuntimeStore();
  var first = store.GetOrCreate(7, a);
  var reloaded = new Entity { Index = 99, Version = 3 };
  Assert.That(store.GetOrCreate(7, reloaded), Is.SameAs(first));
  Assert.That(first.Area, Is.EqualTo(reloaded));
  Assert.That(store.GetOrCreate(8, b), Is.Not.SameAs(first));
  Assert.That(store.Count, Is.EqualTo(2));
 }
 [Test] public void LoadClearsRuntimeSnapshotsAndNothingIsPublishableUntilObservationRebuilds() {
  var store = new IndustrialRuntimeStore();
  var snapshot = store.GetOrCreate(7, a);
  snapshot.Publish(new IndustrialObservationResult { CycleId = 3 });
  Assert.That(snapshot.HasPublished, Is.True);
  store.Clear();
  Assert.That(store.Count, Is.Zero); Assert.That(store.TryGet(7, out _), Is.False);
  var fresh = store.GetOrCreate(7, a);
  Assert.That(fresh.HasPublished, Is.False); Assert.That(fresh.Current, Is.Null);
  Assert.That(fresh.ActivityBaseline.HasValue, Is.False, "interpolation baseline is discarded after load");
  Assert.That(fresh.InputWindow.CoveredHours(1000), Is.Zero); Assert.That(fresh.OutputWindow.CoveredHours(1000), Is.Zero);
 }
 [Test] public void PruningRemovesOnlyDeletedIndustrialAreas() {
  var store = new IndustrialRuntimeStore();
  store.GetOrCreate(1, a); store.GetOrCreate(2, b); store.GetOrCreate(3, c);
  store.Prune(new[] { new IndustrialAreaRef(a, 1, AreaLifecycleState.Active), new IndustrialAreaRef(c, 3, AreaLifecycleState.Suspended) });
  Assert.That(store.TryGet(1, out _), Is.True); Assert.That(store.TryGet(2, out _), Is.False); Assert.That(store.TryGet(3, out _), Is.True, "suspended stays");
 }
 [Test] public void InitializationAddsOnlyIndustrialStateAndProgressAndNeverReplaces() {
  var target = new Target(); target.Alive.UnionWith(new[] { a, b, c });
  var existing = IndustrialSerializationTests.Sample();
  target.States[b] = existing;
  target.Progress[b] = new ManagedAreaProgress { SchemaVersion = 1, Level = 3, Experience = 9000 };
  var areas = new[] { new IndustrialAreaRef(a, 1, AreaLifecycleState.Created), new IndustrialAreaRef(b, 2, AreaLifecycleState.Suspended),
   new IndustrialAreaRef(c, 3, AreaLifecycleState.Active) };
  Assert.That(IndustrialStateInitializer.Ensure(areas, target, 12), Is.EqualTo(2));
  Assert.That(target.States[a], Is.EqualTo(IndustrialAreaState.CreateDefault(12)), "current game day identities");
  Assert.That(target.Progress[a].Level, Is.EqualTo(1)); Assert.That(target.Progress[a].Experience, Is.Zero);
  Assert.That(target.States[b], Is.EqualTo(existing), "suspended Area keeps authoritative state");
  Assert.That(target.Progress[b].Level, Is.EqualTo(3)); Assert.That(target.Progress[b].Experience, Is.EqualTo(9000));
  Assert.That(IndustrialStateInitializer.Ensure(areas, target, 13), Is.Zero, "idempotent; a later day never resets existing state");
  Assert.That(target.States[a].XpDay, Is.EqualTo(12u));
  Assert.That(typeof(IIndustrialStateTarget).GetMethods().Select(m => m.Name), Has.None.Contains("Settings"), "no Industrial budget/settings component");
 }
 [Test] public void MissingEntitiesAreSkipped() {
  var target = new Target();
  Assert.That(IndustrialStateInitializer.Ensure(new[] { new IndustrialAreaRef(a, 1, AreaLifecycleState.Active) }, target, 0), Is.Zero);
  Assert.That(target.States, Is.Empty);
 }
 [Test] public void OnlyIndustrialManagedAreasAreSelected() {
  var rows = new[] { (a, new ManagedArea { TypeId = (int)AreaTypeId.Park, State = AreaLifecycleState.Active }, 1UL),
   (b, new ManagedArea { TypeId = (int)AreaTypeId.Industrial, State = AreaLifecycleState.Suspended }, 2UL),
   (c, new ManagedArea { TypeId = (int)AreaTypeId.Tourism, State = AreaLifecycleState.Active }, 3UL) };
  var selected = new List<IndustrialAreaRef>();
  IndustrialAreaSelection.Select(rows, selected);
  Assert.That(selected.Select(r => r.StableId), Is.EqualTo(new[] { 2UL }));
  Assert.That(selected[0].State, Is.EqualTo(AreaLifecycleState.Suspended));
 }
}
