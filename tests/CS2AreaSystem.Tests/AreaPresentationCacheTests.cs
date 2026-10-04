using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.UI;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #56 #70 #74: the Area list, its selection lookup and the Core selected-info view share one presentation revision and
// are rebuilt only when it advances.
public class AreaPresentationCacheTests {
 private static Entity E(int i, int v = 1) => new() { Index = i, Version = v };
 private static void Observe(AreaPresentationRevision r, int frame, bool evidence = false, int load = 1, int quarantine = 0, long anchors = 0) =>
  r.Observe(frame, evidence, load, quarantine, anchors);
 // ---- revision
 [Test] public void RevisionStaysPutOnUnchangedFrames() {
  var revision = new AreaPresentationRevision(fallbackFrames: 64);
  Observe(revision, 1);
  ulong value = revision.Value;
  for (int frame = 2; frame < 50; frame++) Observe(revision, frame);
  Assert.That(revision.Value, Is.EqualTo(value));
 }
 [Test] public void RevisionAdvancesOnNativeEvidence() {
  var revision = new AreaPresentationRevision();
  Observe(revision, 1); ulong value = revision.Value;
  Observe(revision, 2, evidence: true);
  Assert.That(revision.Value, Is.Not.EqualTo(value));
 }
 [TestCase(2, 0, 0)]
 [TestCase(1, 3, 0)]
 [TestCase(1, 0, 9)]
 public void RevisionAdvancesOnLoadQuarantineOrAnchorChanges(int load, int quarantine, long anchors) {
  var revision = new AreaPresentationRevision();
  Observe(revision, 1); ulong value = revision.Value;
  Observe(revision, 2, load: load, quarantine: quarantine, anchors: anchors);
  Assert.That(revision.Value, Is.Not.EqualTo(value));
  value = revision.Value;
  Observe(revision, 3, load: load, quarantine: quarantine, anchors: anchors);
  Assert.That(revision.Value, Is.EqualTo(value), "a stamp change advances once");
 }
 [Test] public void ObservationIsIdempotentWithinAFrame() {
  var revision = new AreaPresentationRevision();
  Observe(revision, 1); ulong value = revision.Value;
  Observe(revision, 2, evidence: true); ulong advanced = revision.Value;
  Observe(revision, 2, evidence: true);
  Assert.That(revision.Value, Is.EqualTo(advanced), "the section and the list in one frame see one revision");
  Assert.That(advanced, Is.Not.EqualTo(value));
  Assert.That(revision.NeedsObservation(2), Is.False);
  Assert.That(revision.NeedsObservation(3), Is.True);
 }
 [Test] public void ExplicitInvalidationAdvancesImmediately() {
  var revision = new AreaPresentationRevision();
  Observe(revision, 1); ulong value = revision.Value;
  revision.Invalidate();
  Assert.That(revision.Value, Is.Not.EqualTo(value), "our own action shows in the same frame");
 }
 [Test] public void FallbackReconcilesAtALowCadence() {
  var revision = new AreaPresentationRevision(fallbackFrames: 16);
  Observe(revision, 0); ulong start = revision.Value;
  for (int frame = 1; frame <= 64; frame++) Observe(revision, frame);
  Assert.That(revision.Value - start, Is.EqualTo(4UL));
 }
 // ---- single-slot cache
 [Test] public void CacheRebuildsOnlyWhenTheKeyChanges() {
  var cache = new AreaPresentationCache<AreaCoreViewKey, string>();
  int builds = 0;
  string Get(AreaCoreViewKey key) => cache.TryGet(key, out var value) ? value : cache.Set(key, "built" + ++builds);
  var key = new AreaCoreViewKey(E(1), 5, false, true);
  Assert.That(Get(key), Is.EqualTo("built1"));
  for (int i = 0; i < 10; i++) Assert.That(Get(key), Is.EqualTo("built1"));
  Assert.That(cache.Builds, Is.EqualTo(1)); Assert.That(cache.Hits, Is.EqualTo(10));
  Assert.That(Get(new AreaCoreViewKey(E(1), 6, false, true)), Is.EqualTo("built2"), "revision change");
  Assert.That(Get(new AreaCoreViewKey(E(2), 6, false, true)), Is.EqualTo("built3"), "selection change");
  Assert.That(Get(new AreaCoreViewKey(E(2, 2), 6, false, true)), Is.EqualTo("built4"), "recreated entity");
  Assert.That(Get(new AreaCoreViewKey(E(2, 2), 6, true, true)), Is.EqualTo("built5"), "pending anchor");
  Assert.That(Get(new AreaCoreViewKey(E(2, 2), 6, true, false)), Is.EqualTo("built6"), "delete confirmation option");
  cache.Clear();
  Assert.That(Get(new AreaCoreViewKey(E(2, 2), 6, true, false)), Is.EqualTo("built7"));
 }
 // ---- module content composition
 [Test] public void ModuleContentIsComposedOntoTheCachedCoreView() {
  var core = new AreaManagementViewModel { StableId = "7", TypeKey = "T", State = "S", Anchors = new[] { new AreaAnchorView { Key = "1:1", Name = "HQ", Valid = true } } };
  var panel = AreaPanelDocument.Overview();
  var first = core.WithModuleContent("summary", panel);
  Assert.That(first, Is.Not.SameAs(core));
  Assert.That(first.Summary, Is.EqualTo("summary")); Assert.That(first.Panel, Is.SameAs(panel));
  Assert.That(first.Anchors, Is.SameAs(core.Anchors)); Assert.That(first.StableId, Is.EqualTo("7"));
  Assert.That(core.Summary, Is.Null, "the cached core view is never changed");
  Assert.That(first.IsComposedFrom(core, "summary", AreaPanelDocument.Overview()), Is.True, "an equivalent module document reuses the composed view");
  Assert.That(first.IsComposedFrom(core, "other", panel), Is.False);
  Assert.That(first.IsComposedFrom(new AreaManagementViewModel(), "summary", panel), Is.False);
 }
 // ---- anchor revision
 [Test] public void AnchorRevisionAdvancesOnEveryAnchorChange() {
  var links = new Dictionary<Entity, IReadOnlyList<AreaAnchorLink>>();
  var store = new DictionaryStore(links);
  var service = new AreaAnchorService(store, e => e != Entity.Null, (_, _, _) => true);
  long r0 = service.Revision;
  Assert.That(service.Assign(E(1), E(10), AreaAnchorRole.Headquarters), Is.True);
  long r1 = service.Revision; Assert.That(r1, Is.Not.EqualTo(r0));
  service.InvalidateAnchor(E(10)); long r2 = service.Revision; Assert.That(r2, Is.Not.EqualTo(r1));
  service.InvalidateAnchor(E(99)); Assert.That(service.Revision, Is.EqualTo(r2), "an unrelated building changes nothing");
  service.InvalidateArea(E(1)); long r3 = service.Revision; Assert.That(r3, Is.Not.EqualTo(r2));
  Assert.That(service.Remove(E(1), E(10), AreaAnchorRole.Headquarters), Is.True); Assert.That(service.Revision, Is.Not.EqualTo(r3));
  long r4 = service.Revision;
  service.RebuildReverseIndex(new[] { E(1) }); Assert.That(service.Revision, Is.Not.EqualTo(r4));
 }
 private sealed class DictionaryStore : IAreaAnchorStore {
  private readonly Dictionary<Entity, IReadOnlyList<AreaAnchorLink>> links;
  public DictionaryStore(Dictionary<Entity, IReadOnlyList<AreaAnchorLink>> links) => this.links = links;
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => links.TryGetValue(area, out var l) ? l : Array.Empty<AreaAnchorLink>();
  public void Write(Entity area, IReadOnlyList<AreaAnchorLink> l) => links[area] = l.ToArray();
 }
 // ---- UI wiring (ECS worlds cannot be created in tests)
 private static string UISource() => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs"));
 private static string Method(string source, string signature) {
  int start = source.IndexOf(signature, StringComparison.Ordinal);
  Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
  int open = source.IndexOf('{', start), depth = 0;
  for (int i = open; i < source.Length; i++) {
   if (source[i] == '{') depth++;
   else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
  }
  throw new AssertionException("unbalanced " + signature);
 }
 [Test] public void ListSelectionAndViewReadThroughTheSharedRevision() {
  var source = UISource();
  var publish = Method(source, "private void PublishList()");
  Assert.That(publish, Does.Contain("listEntries.TryGet("), "an unchanged open list is not rebuilt");
  var select = Method(source, "private void SelectArea(string expectedId)");
  Assert.That(select, Does.Not.Contain("ToEntityArray"), "selection uses the cached lookup");
  Assert.That(select, Does.Contain("Candidates("));
  Assert.That(select, Does.Contain("AreaListViewModel.TryFind(new[]"), "the live entity is re-checked against the requested key");
  var view = Method(source, "public bool TryBuildView(Entity entity, out AreaManagementViewModel view)");
  Assert.That(view, Does.Contain("coreViews.TryGet("));
  Assert.That(view, Does.Not.Contain("GetValidAnchors"), "anchors are read only when the core view is rebuilt");
  Assert.That(source, Does.Contain("SetChangedVersionFilter(ComponentType.ReadOnly<ManagedArea>())"));
  Assert.That(source, Does.Contain("SetChangedVersionFilter(ComponentType.ReadOnly<ManagedAreaProgress>())"));
  Assert.That(source, Does.Contain("SetChangedVersionFilter(ComponentType.ReadOnly<AreaAnchorLink>())"));
  Assert.That(source, Does.Contain("ComponentType.ReadOnly<BatchesUpdated>()"), "renames mark the entity BatchesUpdated");
 }
 [Test] public void AnchorPolicyPresentationCallbacksAreFailureIsolated() {
  var source=UISource();
  var roles=Method(source,"private System.Collections.Generic.IReadOnlyList<AreaAnchorRole> GetAnchorRoles(");
  var assign=Method(source,"private bool CanAssignAnchorForUI(");
  Assert.That(roles,Does.Contain("try").And.Contain("catch (Exception error)").And.Contain("UI.AnchorRoles"));
  Assert.That(assign,Does.Contain("try").And.Contain("catch (Exception error)").And.Contain("UI.AnchorAssign."));
  var groups=Method(source,"private System.Collections.Generic.IReadOnlyList<AreaAnchorGroupView> BuildAnchorGroups(");
  Assert.That(groups,Does.Contain("CanAssignAnchorForUI(").And.Not.Contain("Derived"), "C2 retired the derived-anchor presentation");
 }
 [TestCase("private void SetPolicy(")]
 [TestCase("private void ModuleAction(")]
 [TestCase("private void RemoveAnchor(")]
 [TestCase("private void BeginAssignment(")]
 [TestCase("private void CancelAssignment(")]
 [TestCase("private void Delete(")]
 public void OwnActionsInvalidateThePresentation(string signature) {
  Assert.That(Method(UISource(), signature), Does.Contain("presentation.Invalidate()"));
 }
}
