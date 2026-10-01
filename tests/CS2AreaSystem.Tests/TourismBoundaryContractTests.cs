using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Prefabs;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Tourism;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #78: the Tourism v1 contract is a managed boundary with no gameplay. Adding Tourism gameplay must update these tests
// on purpose; a Core change that pulls Tourism into Park/Industrial behaviour fails them.
public class TourismBoundaryContractTests {
 private const int Tourism = (int)AreaTypeId.Tourism;
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static string Source(string relative) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/" + relative));
 [Test] public void RegistrationDescribesTheTourismPrefabAndType() {
  var descriptor = new TourismAreaModule().DescribeType();
  Assert.That(descriptor.TypeId, Is.EqualTo(3));
  Assert.That(descriptor.PrefabName, Is.EqualTo("CS2AS.TourismArea"));
  Assert.That(descriptor.DisplayNameKey, Is.EqualTo("CS2AS.Type.Tourism"));
  Assert.That(descriptor.Module, Is.InstanceOf<TourismAreaModule>());
  var registry = new AreaTypeRegistry();
  Assert.That(registry.Register(descriptor), Is.True);
  Assert.That(registry.TryGet(Tourism, out var found) && found.Module is TourismAreaModule, Is.True);
  Assert.That(new ManagedAreaTypeResolver().TryResolve("CS2AS.TourismArea", out var type) && type == AreaTypeId.Tourism, Is.True);
  Assert.That(Source("Core/Areas/AreaCoreServicesSystem.cs"), Does.Contain("Registry.Register(new TourismAreaModule().DescribeType())"));
 }
 [Test] public void CreationGivesAManagedAreaAndAStableIdLikeEveryType() {
  var ids = new AreaStableIdAllocator();
  var identity = ManagedAreaIdentity.Ensure(Tourism, null, null, ids);
  Assert.That(identity.Managed.TypeId, Is.EqualTo(Tourism));
  Assert.That(identity.Managed.State, Is.EqualTo(AreaLifecycleState.Created));
  Assert.That(identity.StableId.Value, Is.Not.Zero);
  // No anchor requirement: a known Tourism Area activates on creation.
  var module = new TourismAreaModule();
  Assert.That(module.RequiredAnchorRole, Is.Null);
  Assert.That(AreaLifecyclePolicy.Resolve(true, module.ValidateActivation(E(1)), false), Is.EqualTo(AreaLifecycleState.Active));
 }
 [Test] public void SaveAndLoadKeepTheTypeStateAndIdentity() {
  var managed = MemorySerializer.RoundTrip(new ManagedArea { SchemaVersion = ManagedArea.CurrentSchemaVersion, TypeId = Tourism, State = AreaLifecycleState.Active });
  Assert.That(managed.TypeId, Is.EqualTo(Tourism)); Assert.That(managed.State, Is.EqualTo(AreaLifecycleState.Active));
  Assert.That(MemorySerializer.RoundTrip(new ManagedAreaStableId { Value = 42 }).Value, Is.EqualTo(42UL));
  var lifecycle = new AreaIdentityLifecycle();
  lifecycle.BeginLoad();
  var quarantined = lifecycle.Observe(new[] { new AreaIdentityRecord(E(1), 42), new AreaIdentityRecord(E(2), 43) });
  Assert.That(quarantined, Is.Empty, "a loaded Tourism Area keeps its saved ID");
  Assert.That(lifecycle.StableIds.Allocate(), Is.EqualTo(44UL), "new IDs continue after the saved ones");
 }
 [Test] public void DeletionNeedsNoModuleCleanup() {
  var module = new TourismAreaModule();
  Assert.DoesNotThrow(() => { module.OnAreaCreated(E(1)); module.OnAreaActivated(E(1)); module.OnAreaSuspended(E(1)); module.OnAreaDeleted(E(1)); });
  Assert.That(AreaLifecyclePolicy.Resolve(true, true, true), Is.EqualTo(AreaLifecycleState.Deleted));
 }
 [Test] public void OverlapIsAllowedWithEveryType() {
  var module = new TourismAreaModule();
  foreach (var type in new[] { (int)AreaTypeId.Park, (int)AreaTypeId.Industrial, Tourism, 99 })
   Assert.That(module.AllowsOverlapWith(type), Is.True, type.ToString());
 }
 [Test] public void BoundaryOnlyPanelAndNoActions() {
  var module = new TourismAreaModule();
  var context = default(AreaPanelContext);
  Assert.That(module.BuildSummary(E(1), context), Is.EqualTo("CS2AS.Summary.TourismBoundaryOnly"));
  Assert.That(AreaPanelDocument.Equivalent(module.BuildPanel(E(1), context), AreaPanelDocument.Overview()), Is.True);
  Assert.That(module.HandleAction(E(1), "anything", "1", context), Is.False);
  Assert.That(module.Classify(E(7), E(1)), Is.EqualTo(AreaMemberRole.Member), "buildings inside are plain members");
  Assert.That(module, Is.Not.InstanceOf<CS2AreaSystem.Core.Persistence.IAreaSavedStateValidator>(), "no Tourism saved state to validate");
 }
 [Test] public void NoParkOrIndustrialStateOrRuntimeForTourism() {
  var selected = new List<IndustrialAreaRef>();
  IndustrialAreaSelection.Select(new[] { (E(1), new ManagedArea { TypeId = Tourism, State = AreaLifecycleState.Active }, 5UL) }, selected);
  Assert.That(selected, Is.Empty, "the Industrial runtime never adopts a Tourism Area");
  Assert.That(Source("Modules/Park/ParkRuntimeSystem.cs"), Does.Contain("managed.TypeId != (int)AreaTypeId.Park"), "the Park runtime only collects Park Areas");
  var tourismSource = Source("Modules/Tourism/TourismAreaModule.cs");
  Assert.That(tourismSource, Does.Not.Contain("AddComponent"), "Tourism adds no components of its own");
  Assert.That(Directory.GetFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Modules/Tourism"), "*.cs"), Has.Length.EqualTo(1));
 }
 private sealed class Failing : IAreaModule {
  public AreaTypeDescriptor DescribeType() => new(99, "Future", "Future", "icon", this);
  public IAreaMemberClassifier MemberClassifier => throw new InvalidOperationException("future module broken");
  public IAreaPanelProvider PanelProvider => throw new InvalidOperationException();
  public bool ValidateActivation(Entity area) => throw new InvalidOperationException();
  public bool AllowsOverlapWith(int otherTypeId) => throw new InvalidOperationException();
  public void OnAreaCreated(Entity area) { } public void OnAreaActivated(Entity area) { } public void OnAreaSuspended(Entity area) { } public void OnAreaDeleted(Entity area) { }
 }
 [Test] public void AFailingFutureModuleDoesNotAffectTourismMembership() {
  var reported = new List<int>();
  var relations = AreaMembershipClassification.Collect(new[] {
   new ContainingArea(E(1), 10, 99, new Failing()), new ContainingArea(E(2), 20, Tourism, new TourismAreaModule()) }, E(50), (type, _) => reported.Add(type));
  Assert.That(relations.Select(r => (r.AreaStableId, r.TypeId)), Is.EqualTo(new[] { (20UL, Tourism) }));
  Assert.That(reported, Is.EqualTo(new[] { 99 }));
 }
}
