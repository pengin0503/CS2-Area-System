using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Industrial;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #61: the Industrial runtime rebuilds its Area list only on change evidence (or a low-frequency fallback) and its
// bookkeeping does not allocate on unchanged frames. #72: primary-member lists are reused while membership is unchanged.
public class IndustrialRuntimePerformanceTests {
 private const int Industrial = (int)AreaTypeId.Industrial;
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static IndustrialAreaRef Area(ulong id, AreaLifecycleState state = AreaLifecycleState.Active) => new(E((int)id), id, state);
 // ---- change gate
 [Test] public void GateRefreshesOnceAndThenSkipsUnchangedUpdates() {
  var gate = new AreaChangeGate(fallbackUpdates: 256);
  Assert.That(gate.ShouldRefresh(false, 1, 0), Is.True, "nothing has been built yet");
  gate.Refreshed(1, 0);
  for (int i = 0; i < 100; i++) Assert.That(gate.ShouldRefresh(false, 1, 0), Is.False);
  Assert.That(gate.Refreshes, Is.EqualTo(1));
  Assert.That(gate.Skips, Is.EqualTo(100));
 }
 [TestCase(true, 1, 0)]
 [TestCase(false, 2, 0)]
 [TestCase(false, 1, 5)]
 public void GateRefreshesOnEvidenceANewLoadOrAQuarantineChange(bool evidence, int loadGeneration, int quarantineRevision) {
  var gate = new AreaChangeGate();
  gate.Refreshed(1, 0);
  Assert.That(gate.ShouldRefresh(evidence, loadGeneration, quarantineRevision), Is.True);
  gate.Refreshed(loadGeneration, quarantineRevision);
  Assert.That(gate.ShouldRefresh(false, loadGeneration, quarantineRevision), Is.False, "the same state is not rebuilt twice");
 }
 [Test] public void GateRefreshesAfterAnExplicitInvalidation() {
  var gate = new AreaChangeGate();
  gate.Refreshed(1, 0);
  gate.Invalidate();
  Assert.That(gate.ShouldRefresh(false, 1, 0), Is.True);
  Assert.That(gate.ShouldRefresh(false, 1, 0), Is.True, "an invalidation stays until a rebuild succeeds");
  gate.Refreshed(1, 0);
  Assert.That(gate.ShouldRefresh(false, 1, 0), Is.False);
 }
 [Test] public void GateKeepsALowFrequencyFallbackReconciliation() {
  var gate = new AreaChangeGate(fallbackUpdates: 8);
  gate.Refreshed(1, 0);
  int refreshes = 0;
  for (int i = 0; i < 64; i++) if (gate.ShouldRefresh(false, 1, 0)) { refreshes++; gate.Refreshed(1, 0); }
  Assert.That(refreshes, Is.EqualTo(8), "one reconciliation per 8 unchanged updates");
 }
 [Test] public void AFailedRebuildIsRetriedOnTheNextUpdate() {
  var gate = new AreaChangeGate();
  Assert.That(gate.ShouldRefresh(false, 1, 0), Is.True);
  // no Refreshed(): the rebuild threw
  Assert.That(gate.ShouldRefresh(false, 1, 0), Is.True);
 }
 // ---- allocation-free bookkeeping on unchanged frames
 private static long Allocated(Action action) {
  action(); action();
  long before = GC.GetAllocatedBytesForCurrentThread();
  for (int i = 0; i < 100; i++) action();
  return GC.GetAllocatedBytesForCurrentThread() - before;
 }
 [Test] public void PruneAndRetainDoNotAllocateWhenNothingIsStale() {
  var alive = new List<IndustrialAreaRef>();
  var store = new IndustrialRuntimeStore();
  var structure = new IndustrialStructureTracker();
  for (ulong id = 1; id <= 500; id++) { alive.Add(Area(id)); store.GetOrCreate(id, E((int)id)); structure.Bump(id); }
  Assert.That(Allocated(() => { store.Prune(alive); structure.RetainOnly(alive); }), Is.Zero);
  Assert.That(store.Count, Is.EqualTo(500)); Assert.That(structure.Count, Is.EqualTo(500));
 }
 [Test] public void PruneAndRetainStillDropDeletedAreasAndKeepSuspendedOnes() {
  var store = new IndustrialRuntimeStore();
  var structure = new IndustrialStructureTracker();
  foreach (ulong id in new ulong[] { 1, 2, 3 }) { store.GetOrCreate(id, E((int)id)); structure.Bump(id); }
  var alive = new List<IndustrialAreaRef> { Area(1), Area(3, AreaLifecycleState.Suspended) };
  store.Prune(alive); structure.RetainOnly(alive);
  Assert.That(store.TryGet(2, out _), Is.False); Assert.That(store.TryGet(3, out _), Is.True);
  Assert.That(structure.Get(2), Is.Zero); Assert.That(structure.Get(3), Is.Not.Zero);
 }
 // ---- primary-member cache (#72)
 [Test] public void PrimaryMembersAreReusedWhileMembershipIsUnchanged() {
  var index = new AreaMembershipIndex();
  var source = new IndustrialMembershipCycleSource(index, new IndustrialStructureTracker());
  index.ReplaceMembership(E(3), new[] { new AreaMembership(10, Industrial, true) });
  index.ReplaceMembership(E(1), new[] { new AreaMembership(10, Industrial, true) });
  index.ReplaceMembership(E(2), new[] { new AreaMembership(5, Industrial, true), new AreaMembership(10, Industrial, true) }); // Area 5 owns it (lowest ID)
  var first = source.PrimaryMembers(10, out int nonPrimary);
  Assert.That(first, Is.EqualTo(new[] { E(1), E(3) }), "stable order by entity");
  Assert.That(nonPrimary, Is.EqualTo(1));
  Assert.That(source.PrimaryMembers(10, out int again), Is.SameAs(first));
  Assert.That(again, Is.EqualTo(1));
  Assert.That(source.Rebuilds, Is.EqualTo(1));
  Assert.That(Allocated(() => source.PrimaryMembers(10, out _)), Is.Zero);
 }
 [Test] public void PrimaryMembersAreRebuiltWhenMembersOrPrimaryOwnershipChange() {
  var index = new AreaMembershipIndex();
  var source = new IndustrialMembershipCycleSource(index, new IndustrialStructureTracker());
  index.ReplaceMembership(E(1), new[] { new AreaMembership(10, Industrial, true) });
  var first = source.PrimaryMembers(10, out _);
  index.ReplaceMembership(E(5), new[] { new AreaMembership(10, Industrial, true) });
  var added = source.PrimaryMembers(10, out _);
  Assert.That(added, Is.Not.SameAs(first)); Assert.That(added, Is.EqualTo(new[] { E(1), E(5) }));
  Assert.That(first, Is.EqualTo(new[] { E(1) }), "a list held by a running cycle is never mutated");
  index.ReplaceMembership(E(1), new[] { new AreaMembership(5, Industrial, true), new AreaMembership(10, Industrial, true) }); // primary moves to Area 5
  var demoted = source.PrimaryMembers(10, out int nonPrimary);
  Assert.That(demoted, Is.EqualTo(new[] { E(5) })); Assert.That(nonPrimary, Is.EqualTo(1));
  index.RemoveMember(E(5));
  Assert.That(source.PrimaryMembers(10, out _), Is.Empty);
  index.Clear();
  index.ReplaceMembership(E(7), new[] { new AreaMembership(10, Industrial, true) });
  Assert.That(source.PrimaryMembers(10, out _), Is.EqualTo(new[] { E(7) }), "a cleared index never serves an older list");
 }
 [Test] public void PrimaryMemberListsOfGoneAreasAreDropped() {
  var index = new AreaMembershipIndex();
  var source = new IndustrialMembershipCycleSource(index, new IndustrialStructureTracker());
  index.ReplaceMembership(E(1), new[] { new AreaMembership(10, Industrial, true) });
  index.ReplaceMembership(E(2), new[] { new AreaMembership(20, Industrial, true) });
  source.PrimaryMembers(10, out _); source.PrimaryMembers(20, out _);
  Assert.That(source.CachedAreas, Is.EqualTo(2));
  source.RetainOnly(new List<IndustrialAreaRef> { Area(10) });
  Assert.That(source.CachedAreas, Is.EqualTo(1));
 }
 [Test] public void SchedulerCyclesUseTheCachedListUntilTheRevisionChanges() {
  var index = new AreaMembershipIndex();
  var source = new IndustrialMembershipCycleSource(index, new IndustrialStructureTracker());
  index.ReplaceMembership(E(1), new[] { new AreaMembership(10, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10) }, 0, 0, 0, source);
  var reads = new List<IndustrialReadRequest>(); scheduler.NextReads(reads);
  foreach (var r in reads) scheduler.Complete(r, new IndustrialEntityObservation { Facility = r.Building });
  scheduler.TakeFinished(source, 0, 0, 0);
  scheduler.Sync(new[] { Area(10) }, 1, 0, 1, source);
  Assert.That(source.Rebuilds, Is.EqualTo(1), "the next hourly cycle reuses the unchanged list");
 }
 // ---- runtime wiring (ECS worlds cannot be created in tests)
 [Test] public void RuntimeRebuildsTheAreaListOnlyThroughTheGate() {
  var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs"));
  Assert.That(source, Does.Contain("gate.ShouldRefresh("));
  Assert.That(source, Does.Contain("SetChangedVersionFilter(ComponentType.ReadOnly<ManagedArea>())"));
  Assert.That(source, Does.Contain("changedAreas"));
  Assert.That(source, Does.Contain("services.Identity.LoadGeneration"));
  Assert.That(source, Does.Contain("services.Quarantine.Revision"));
  Assert.That(source, Does.Contain("public IIndustrialCycleSource CycleSource => cycleSource"));
  Assert.That(source, Does.Not.Contain("new List<(Entity, ManagedArea, ulong)>"), "no per-update row list");
 }
}
