using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Semantics;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 C2 Task 5: a Suspended Area names the dedicated asset it is missing, including when the AS_ package itself is not
// installed, and an old save keeps its links and authoritative state while it waits for the dedicated anchors.
public class DedicatedContentStateTests
{
 private sealed class Store : IAreaAnchorStore {
  public Dictionary<Entity, IReadOnlyList<AreaAnchorLink>> Values = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity area) => Values.TryGetValue(area, out var links) ? links : Array.Empty<AreaAnchorLink>();
  public void Write(Entity area, IReadOnlyList<AreaAnchorLink> links) => Values[area] = links.ToArray();
 }
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static readonly Entity Area = E(1), ParkHqPrefab = E(101), IndustrialHqPrefab = E(102), GatePrefab = E(103), OtherPrefab = E(104);
 private static AreaAssetSemanticRegistry Registry(bool parkHq = true, bool industrialHq = true, bool gate = true) {
  var registry = new AreaAssetSemanticRegistry(); var candidates = new List<AreaBuiltInPrefabCandidate>();
  if (parkHq) candidates.Add(new AreaBuiltInPrefabCandidate(ParkHqPrefab, "BuildingPrefab", "AS_ParkHeadquarters", true, false, "AS_ParkHeadquarters.cok"));
  if (industrialHq) candidates.Add(new AreaBuiltInPrefabCandidate(IndustrialHqPrefab, "BuildingPrefab", "AS_IndustrialHeadquarters", true, false, "AS_IndustrialHeadquarters.cok"));
  if (gate) candidates.Add(new AreaBuiltInPrefabCandidate(GatePrefab, "BuildingPrefab", "AS_ParkGate", true, false, "AS_ParkHeadquarters.cok"));
  new AreaBuiltInContentSemanticProvider().Refresh(registry, candidates);
  return registry;
 }
 // Anchors 10/11/12 are the dedicated Park HQ, Gate and Industrial HQ; 20/21 are a Foundation Park Maintenance and office.
 private static readonly Dictionary<Entity, Entity> Prefabs = new() { [E(10)] = ParkHqPrefab, [E(11)] = GatePrefab, [E(12)] = IndustrialHqPrefab };
 private static Entity PrefabOf(Entity e) => Prefabs.TryGetValue(e, out var p) ? p : OtherPrefab;
 private static (ParkAreaModule park, AreaAnchorService anchors, Store store) Park(AreaAssetSemanticRegistry registry) {
  var store = new Store(); ParkAreaModule park = null;
  var anchors = new AreaAnchorService(store, e => e != Entity.Null, (a, e, r) => park.AcceptsAnchor(a, e, r, default));
  park = new ParkAreaModule(anchors, (e, _) => new ParkFacilityTraits { IsBuilding = true, HasParkMaintenance = e == E(20), HasPark = true, HasRoadAccess = true, Prefab = PrefabOf(e) }, registry);
  return (park, anchors, store);
 }
 private static (IndustrialAreaModule industrial, Store store) Industrial(AreaAssetSemanticRegistry registry) {
  var store = new Store(); IndustrialAreaModule industrial = null;
  var anchors = new AreaAnchorService(store, e => e != Entity.Null, (a, e, r) => industrial.AcceptsAnchor(a, e, r, default));
  industrial = new IndustrialAreaModule(anchors, (e, _) => new IndustrialFacilityTraits { IsBuilding = true, HasOfficeProperty = e == E(21), HasOfficeCompany = e == E(21), Prefab = PrefabOf(e) }, registry);
  return (industrial, store);
 }
 private static AreaAnchorLink Link(int anchor, AreaAnchorRole role) => new() { Anchor = E(anchor), Role = role };

 [Test] public void ContentPresenceIsReadOnlyFromTheBuiltInProvider() {
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(Registry(), AreaAssetSemantic.ParkEntrance), Is.True);
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(Registry(gate: false), AreaAssetSemantic.ParkEntrance), Is.False);
  var foreign = new AreaAssetSemanticRegistry();
  foreign.Register("Other.Mod", OtherPrefab, new AreaAssetSemanticPatch(AreaAssetSemantic.ParkEntrance, AreaAssetSemantic.None, 99));
  Assert.That(AreaBuiltInAssetContract.IsContentPresent(foreign, AreaAssetSemantic.ParkEntrance), Is.False, "another mod cannot stand in for the AS_ package");
 }
 [TestCase(false, false, false, "ContentMissing")] [TestCase(false, true, false, "HeadquartersContentMissing")] [TestCase(true, false, false, "GateContentMissing")]
 [TestCase(true, true, false, "BothRequired")] [TestCase(true, true, true, "BothRequired")]
 public void ParkSummaryNamesTheMissingDedicatedContent(bool parkHq, bool gate, bool oldLinks, string suffix) {
  var (park, _, store) = Park(Registry(parkHq: parkHq, gate: gate));
  if (oldLinks) store.Values[Area] = new[] { Link(20, AreaAnchorRole.Headquarters), Link(30, AreaAnchorRole.Entrance) };
  Assert.That(park.BuildSummary(Area, default), Is.EqualTo("CS2AS.Park.Summary." + suffix));
  Assert.That(park.ValidateActivation(Area, default), Is.False);
 }
 [Test] public void MissingContentStillReportedWhenTheOtherRoleIsValid() {
  var (park, anchors, _) = Park(Registry(gate: false));
  Assert.That(anchors.Assign(Area, E(10), AreaAnchorRole.Headquarters), Is.True);
  Assert.That(park.BuildSummary(Area, default), Is.EqualTo("CS2AS.Park.Summary.GateContentMissing"));
  var (park2, anchors2, _) = Park(Registry(parkHq: false));
  Assert.That(anchors2.Assign(Area, E(11), AreaAnchorRole.Entrance), Is.True);
  Assert.That(park2.BuildSummary(Area, default), Is.EqualTo("CS2AS.Park.Summary.HeadquartersContentMissing"));
 }
 [Test] public void ParkStatesMapMissingContentPerRole() {
  Assert.That(ParkActivationSummary.Key(ParkHeadquartersState.Valid, ParkHeadquartersState.Valid), Is.EqualTo("CS2AS.Park.Summary.Active"));
  Assert.That(ParkActivationSummary.Key(ParkHeadquartersState.ContentMissing, ParkHeadquartersState.ContentMissing), Is.EqualTo("CS2AS.Park.Summary.ContentMissing"));
  Assert.That(ParkActivationSummary.Key(ParkHeadquartersState.ContentMissing, ParkHeadquartersState.Invalid), Is.EqualTo("CS2AS.Park.Summary.HeadquartersContentMissing"));
  Assert.That(ParkActivationSummary.Key(ParkHeadquartersState.Missing, ParkHeadquartersState.ContentMissing), Is.EqualTo("CS2AS.Park.Summary.GateContentMissing"));
  Assert.That(ParkActivationSummary.Key(ParkHeadquartersState.Invalid, ParkHeadquartersState.Valid), Is.EqualTo("CS2AS.Park.Summary.HeadquartersInvalid"));
  Assert.That(ParkActivationSummary.Key(ParkHeadquartersState.Valid, ParkHeadquartersState.Missing), Is.EqualTo("CS2AS.Park.Summary.EntranceRequired"));
 }
 [Test] public void ParkRoleStateReadsAnchorsAndContent() {
  var registry = Registry(gate: false); var (_, anchors, store) = Park(registry);
  Assert.That(ParkActivationSummary.State(anchors, registry, Area, AreaAnchorRole.Headquarters), Is.EqualTo(ParkHeadquartersState.Missing));
  store.Values[Area] = new[] { Link(20, AreaAnchorRole.Headquarters) };
  Assert.That(ParkActivationSummary.State(anchors, registry, Area, AreaAnchorRole.Headquarters), Is.EqualTo(ParkHeadquartersState.Invalid));
  Assert.That(ParkActivationSummary.State(anchors, registry, Area, AreaAnchorRole.Entrance), Is.EqualTo(ParkHeadquartersState.ContentMissing));
  Assert.That(ParkActivationSummary.State(anchors, null, Area, AreaAnchorRole.Entrance), Is.EqualTo(ParkHeadquartersState.Missing), "no registry: no content claim");
 }
 [TestCase(false, false, IndustrialHeadquartersState.ContentMissing, "HeadquartersContentMissing")]
 [TestCase(false, true, IndustrialHeadquartersState.ContentMissing, "HeadquartersContentMissing")]
 [TestCase(true, false, IndustrialHeadquartersState.Missing, "HeadquartersRequired")]
 [TestCase(true, true, IndustrialHeadquartersState.Invalid, "HeadquartersInvalid")]
 public void IndustrialSummaryNamesTheMissingDedicatedContent(bool content, bool oldOffice, IndustrialHeadquartersState state, string suffix) {
  var (industrial, store) = Industrial(Registry(industrialHq: content));
  if (oldOffice) store.Values[Area] = new[] { Link(21, AreaAnchorRole.Headquarters) };
  Assert.That(industrial.HeadquartersState(Area), Is.EqualTo(state));
  Assert.That(industrial.BuildSummary(Area, default), Is.EqualTo("CS2AS.Industrial.Summary." + suffix));
 }
 [Test] public void ParkPanelWarnsAboutMissingContentPerRole() {
  var m = ParkPanelBuilderTests.Model(quality: false); m.Lifecycle = AreaLifecycleState.Suspended;
  m.Headquarters = ParkHeadquartersState.ContentMissing; m.Entrance = ParkHeadquartersState.ContentMissing; m.Runtime!.EntranceCount = 0;
  var keys = ParkPanelBuilder.Build(m).Tabs[0].Items.Select(i => i.LabelKey).ToList();
  Assert.That(keys, Does.Contain("CS2AS.Park.Warning.HeadquartersContentMissing"));
  Assert.That(keys, Does.Contain("CS2AS.Park.Warning.GateContentMissing"));
  Assert.That(keys, Does.Not.Contain("CS2AS.Park.Warning.NoEntrance"), "the gate cannot be placed until its package is installed");
  Assert.That(keys, Does.Not.Contain("CS2AS.Park.Warning.HeadquartersRequired"));
 }
 // Quality also runs for Suspended Parks, so the gate-package warning must replace its generic NoEntrance as well.
 [Test] public void GateContentWarningReplacesQualityNoEntrance() {
  var m = ParkPanelBuilderTests.Model(quality: true, entrances: 0); m.Lifecycle = AreaLifecycleState.Suspended;
  m.Headquarters = ParkHeadquartersState.Valid; m.Entrance = ParkHeadquartersState.ContentMissing;
  Assert.That(m.Runtime!.Quality, Is.Not.Null);
  Assert.That(m.Runtime.Quality!.Warnings, Does.Contain("CS2AS.Park.Warning.NoEntrance"), "precondition: Quality reports no entrance");
  var keys = ParkPanelBuilder.Build(m).Tabs[0].Items.Select(i => i.LabelKey).ToList();
  Assert.That(keys, Does.Contain("CS2AS.Park.Warning.GateContentMissing"));
  Assert.That(keys, Does.Not.Contain("CS2AS.Park.Warning.NoEntrance"));
  Assert.That(keys.Count(k => k == "CS2AS.Park.Warning.GateContentMissing"), Is.EqualTo(1));
 }
 // A role whose package is missing offers no pick mode that cannot succeed; without a registry nothing is claimed.
 [Test] public void AssignmentIsOfferedOnlyForRolesWhoseContentIsInstalled() {
  var (park, _, _) = Park(Registry(gate: false));
  Assert.That(park.CanAssignAnchor(Area, AreaAnchorRole.Headquarters, default), Is.True);
  Assert.That(park.CanAssignAnchor(Area, AreaAnchorRole.Entrance, default), Is.False);
  var (industrial, _) = Industrial(Registry(industrialHq: false));
  Assert.That(industrial.CanAssignAnchor(Area, AreaAnchorRole.Headquarters, default), Is.False);
  Assert.That(Industrial(Registry()).industrial.CanAssignAnchor(Area, AreaAnchorRole.Headquarters, default), Is.True);
  Assert.That(new ParkAreaModule(new AreaAnchorService(new Store(), e => true, (a, e, r) => true)).CanAssignAnchor(Area, AreaAnchorRole.Entrance, default), Is.True);
 }
 [Test] public void IndustrialPanelShowsMissingContent() {
  var m = new IndustrialPanelModel { Lifecycle = AreaLifecycleState.Suspended, Headquarters = IndustrialHeadquartersState.ContentMissing };
  var items = IndustrialPanelBuilder.Build(m).Tabs[0].Items;
  Assert.That(items.Any(i => i.ValueKey == "CS2AS.Industrial.Hq.ContentMissing"), Is.True);
  Assert.That(items.Any(i => i.LabelKey == "CS2AS.Industrial.Warning.HeadquartersContentMissing"), Is.True);
  Assert.That(items.Any(i => i.LabelKey == "CS2AS.Industrial.Warning.HeadquartersRequired"), Is.False);
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryContentStateKeyIsLocalizedAndNamesTheDedicatedAsset(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  foreach (var key in new[] { "Park.Summary.ContentMissing", "Park.Summary.HeadquartersContentMissing", "Park.Summary.GateContentMissing",
   "Park.Warning.HeadquartersContentMissing", "Park.Warning.GateContentMissing", "Industrial.Summary.HeadquartersContentMissing",
   "Industrial.Warning.HeadquartersContentMissing", "Industrial.Hq.ContentMissing" })
   Assert.That(entries.ContainsKey("CS2AS." + key), Is.True, locale + " is missing " + key);
  Assert.That(ParkPanelBuilder.AllKeys(), Is.SupersetOf(new[] { "CS2AS.Park.Warning.HeadquartersContentMissing", "CS2AS.Park.Warning.GateContentMissing",
   "CS2AS.Park.Summary.ContentMissing", "CS2AS.Park.Summary.HeadquartersContentMissing", "CS2AS.Park.Summary.GateContentMissing" }));
  Assert.That(IndustrialPanelBuilder.AllKeys(), Is.SupersetOf(new[] { "CS2AS.Industrial.Hq.ContentMissing", "CS2AS.Industrial.Warning.HeadquartersContentMissing",
   "CS2AS.Industrial.Summary.HeadquartersContentMissing" }));
  foreach (var key in new[] { "Park.Summary.BothRequired", "Park.Summary.HeadquartersRequired", "Park.Summary.HeadquartersInvalid", "Park.Warning.HeadquartersRequired",
   "Park.Warning.HeadquartersInvalid", "Park.Summary.ContentMissing", "Park.Summary.HeadquartersContentMissing", "Park.Warning.HeadquartersContentMissing" })
   Assert.That(entries["CS2AS." + key], Does.Contain("AS_ParkHeadquarters"), locale + " " + key);
  foreach (var key in new[] { "Park.Summary.BothRequired", "Park.Summary.EntranceRequired", "Park.Summary.EntranceInvalid", "Park.Summary.ContentMissing",
   "Park.Summary.GateContentMissing", "Park.Warning.GateContentMissing", "Park.Warning.NoEntrance" })
   Assert.That(entries["CS2AS." + key], Does.Contain("AS_ParkGate"), locale + " " + key);
  foreach (var key in new[] { "Industrial.Summary.HeadquartersRequired", "Industrial.Summary.HeadquartersInvalid", "Industrial.Summary.HeadquartersContentMissing",
   "Industrial.Warning.HeadquartersRequired", "Industrial.Warning.HeadquartersContentMissing" })
   Assert.That(entries["CS2AS." + key], Does.Contain("AS_IndustrialHeadquarters"), locale + " " + key);
 }
 // Retired sources must not be advertised to the player any more.
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void NoPlayerTextAdvertisesARetiredAnchorSource(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  Assert.That(entries.ContainsKey("CS2AS.Park.Summary.EntrancePending"), Is.False, "path entrance scan is retired");
  foreach (var key in new[] { "CS2AS.UI.PathEntrances", "CS2AS.UI.PathEntrancesComplete", "CS2AS.UI.PathEntrancesUnknown" })
   Assert.That(entries.ContainsKey(key), Is.False, key);
  foreach (var pair in entries) {
   Assert.That(pair.Value, Does.Not.Contain("Park Maintenance").And.Not.Contain("公園整備"), pair.Key);
   Assert.That(pair.Value, Does.Not.Contain("dedicated pedestrian path").And.Not.Contain("専用歩道"), pair.Key);
   Assert.That(pair.Value, Does.Not.Contain("office building").And.Not.Contain("オフィス建物"), pair.Key);
  }
 }

 // ---- old saves (spec §2.3 / §7): links are kept and shown as invalid, never converted, and state is untouched.
 [Test] public void OldSaveAnchorLinksStillDeserializeWithTheUnchangedSchema() {
  var io = MemorySerializer.Create();
  io.Data.Tokens.Add((ushort)1); io.Data.Tokens.Add(E(20)); io.Data.Tokens.Add((byte)AreaAnchorRole.Headquarters);
  var link = new AreaAnchorLink(); link.Deserialize(io.Reader);
  Assert.That(link.Anchor, Is.EqualTo(E(20))); Assert.That(link.Role, Is.EqualTo(AreaAnchorRole.Headquarters));
  var copy = MemorySerializer.Create(); link.Serialize(copy.Writer);
  Assert.That(copy.Data.Tokens, Is.EqualTo(new object[] { (ushort)1, E(20), (byte)AreaAnchorRole.Headquarters }));
 }
 [Test] public void OldParkLinksStayVisibleAsInvalidAndAreNotConverted() {
  var (park, anchors, store) = Park(Registry());
  var saved = new[] { Link(20, AreaAnchorRole.Headquarters), Link(30, AreaAnchorRole.Entrance) };
  store.Values[Area] = saved;
  Assert.That(park.ValidateActivation(Area, default), Is.False, "Suspended until the dedicated anchors are assigned");
  Assert.That(anchors.GetAnchorLinks(Area, AreaAnchorRole.Headquarters).Single(), Is.EqualTo(new AreaAnchorLinkState(E(20), true, false)));
  Assert.That(anchors.GetAnchorLinks(Area, AreaAnchorRole.Entrance).Single(), Is.EqualTo(new AreaAnchorLinkState(E(30), true, false)));
  Assert.That(store.Values[Area], Is.EqualTo(saved), "the saved links are neither removed nor rewritten");
  Assert.That(anchors.Assign(Area, E(10), AreaAnchorRole.Headquarters), Is.True); Assert.That(anchors.Assign(Area, E(11), AreaAnchorRole.Entrance), Is.True);
  Assert.That(park.ValidateActivation(Area, default), Is.True, "reassigning the dedicated assets reactivates the same Area");
 }
 [Test] public void OldIndustrialOfficeLinkStaysVisibleAsInvalidAndIsNotConverted() {
  var (industrial, store) = Industrial(Registry());
  var saved = new[] { Link(21, AreaAnchorRole.Headquarters) };
  store.Values[Area] = saved;
  Assert.That(industrial.ValidateActivation(Area, default), Is.False);
  Assert.That(industrial.HeadquartersState(Area), Is.EqualTo(IndustrialHeadquartersState.Invalid));
  Assert.That(store.Values[Area], Is.EqualTo(saved));
 }
 // The cutover is decided purely by anchor validity: nothing on the C2 path writes progress, finance, policy, history or ownership state.
 [Test] public void CutoverCodeNeverWritesAuthoritativeAreaState() {
  foreach (var source in new[] { "Core/Semantics/AreaBuiltInAssetContract.cs", "Core/Semantics/AreaBuiltInContentSemanticProvider.cs", "Core/Semantics/AreaBuiltInContentSystem.cs",
   "Modules/Park/ParkAreaModule.cs", "Modules/Park/ParkActivationSummary.cs", "Modules/Park/ParkFacilityClassifier.cs",
   "Modules/Industrial/IndustrialAreaModule.cs", "Modules/Industrial/IndustrialFacilityClassifier.cs" })
   Assert.That(IndustrialSupportSourceGuardTests.Source(source),
    Does.Not.Match(@"SetComponentData|SetBuffer|AddComponent|RemoveComponent|ManagedAreaProgress|ManagedAreaStableId\s*\{|ParkAreaState\s*\{|IndustrialAreaState\s*\{|CompanyOwner|AreaPolicy(State|Buffer)"), source);
 }
}
