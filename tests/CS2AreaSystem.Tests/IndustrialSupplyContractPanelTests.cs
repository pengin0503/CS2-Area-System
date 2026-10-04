using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 4: the Contracts tab shows configuration and advisory estimates, labelled as such.
public class IndustrialSupplyContractPanelTests
{
 private const string P = "CS2AS.Industrial.";
 private static readonly string Timber = ((ulong)Resource.Timber).ToString();
 private static IndustrialSupplyContract Contract(int id, ulong destination = 20, bool enabled = true) =>
  new() { SchemaVersion = 1, ContractId = id, DestinationStableId = destination, Resource = (ulong)Resource.Timber, TargetPerDay = 1000, Enabled = enabled };
 private static IndustrialPanelModel Model(params IndustrialSupplyContractRow[] rows) => new() {
  StableId = 10, Lifecycle = AreaLifecycleState.Active, ContractsAvailable = true, ContractRows = rows,
  ContractDraft = new IndustrialSupplyContractDraft { Destination = 20, Resource = (ulong)Resource.Timber, TargetPerDay = 1500 },
  ContractDestinations = new[] { new AreaPanelOption { Value = "20", Label = "North works" }, new AreaPanelOption { Value = "30", Label = "Harbor" } },
  ContractResources = new[] { new AreaPanelOption { Value = Timber, LabelKey = "Resources.TITLE[Timber]" } } };
 private static IndustrialSupplyContractRow Row(IndustrialSupplyContract c, IndustrialSupplyContractStatus status, string? name = "North works", double? capacity = 1500, double? need = 800, double? potential = 800) =>
  new() { Contract = c, DestinationName = name, Evaluation = new IndustrialSupplyContractEvaluation { ContractId = c.ContractId, Status = status, SourceCapacityPerDay = capacity, DestinationNeedPerDay = need, PotentialPerDay = potential } };
 private static IReadOnlyList<AreaPanelItem> Tab(IndustrialPanelModel m) => IndustrialPanelBuilder.Build(m).Tabs.Single(t => t.Id == "contracts").Items;
 private static AreaPanelItem Item(IReadOnlyList<AreaPanelItem> items, string id) => items.Single(i => i.Id == id);

 [Test] public void ContractsTabIsLabelledAdvisoryAndOffersTheDraftControls() {
  var doc = IndustrialPanelBuilder.Build(Model());
  Assert.That(doc.Tabs.Single(t => t.Id == "contracts").LabelKey, Is.EqualTo("CS2AS.Tab.Contracts"));
  Assert.That(IndustrialPanelBuilder.Tabs, Does.Contain("contracts"));
  var items = Tab(Model());
  Assert.That(items[0].Kind, Is.EqualTo(AreaPanelItemKind.Note)); Assert.That(items[0].LabelKey, Is.EqualTo(P + "Contracts.Tip"));
  var destination = Item(items, "contracts.draft.destination");
  Assert.That((destination.Kind, destination.Action, destination.Value), Is.EqualTo((AreaPanelItemKind.Select, IndustrialSupplyContractActions.DraftDestination, "20")));
  Assert.That(destination.Options!.Select(o => o.Value), Is.EqualTo(new[] { "20", "30" }));
  var resource = Item(items, "contracts.draft.resource");
  Assert.That((resource.Kind, resource.Action, resource.Value), Is.EqualTo((AreaPanelItemKind.Select, IndustrialSupplyContractActions.DraftResource, Timber)));
  var target = Item(items, "contracts.draft.target");
  Assert.That((target.Kind, target.Action, target.NumericValue, target.Min, target.Max, target.Step), Is.EqualTo((AreaPanelItemKind.Slider, IndustrialSupplyContractActions.DraftTarget, (double?)1500, (double?)50, (double?)50000, (double?)50)));
  var create = Item(items, "contracts.create");
  Assert.That((create.Kind, create.Action, create.Disabled), Is.EqualTo((AreaPanelItemKind.Button, IndustrialSupplyContractActions.Create, false)));
  Assert.That(Item(items, "contracts.empty").LabelKey, Is.EqualTo(P + "Contracts.Empty"));
 }
 [Test] public void CreateIsDisabledUntilTheDraftIsCompleteAndOffered() {
  var m = Model(); m.ContractDraft.Resource = 0;
  Assert.That(Item(Tab(m), "contracts.create").Disabled, Is.True);
  m = Model(); m.ContractDraft.Destination = 99;
  Assert.That(Item(Tab(m), "contracts.create").Disabled, Is.True, "a destination no longer offered");
  m = Model(); m.ContractDestinations = Array.Empty<AreaPanelOption>();
  Assert.That(Item(Tab(m), "contracts.draft.destination").Disabled, Is.True);
 }
 [Test] public void EachContractShowsStatusEstimatesAndStableIdActions() {
  var items = Tab(Model(Row(Contract(5), IndustrialSupplyContractStatus.Constrained)));
  var head = Item(items, "contract.5.resource");
  Assert.That((head.LabelKey, head.Value), Is.EqualTo(("Resources.TITLE[Timber]", "North works")));
  Assert.That(Item(items, "contract.5.status").ValueKey, Is.EqualTo(P + "Contracts.Status.Constrained"));
  Assert.That(Item(items, "contract.5.capacity").Value, Is.EqualTo("1,500 /d"));
  Assert.That(Item(items, "contract.5.need").Value, Is.EqualTo("800 /d"));
  var potential = Item(items, "contract.5.potential");
  Assert.That((potential.Value, potential.TooltipKey), Is.EqualTo(("800 /d", P + "Contracts.Potential.Tip")));
  Assert.That(Item(items, "contract.5.target").Action, Is.EqualTo(IndustrialSupplyContractActions.TargetPrefix + "5"));
  var toggle = Item(items, "contract.5.toggle");
  Assert.That((toggle.Action, toggle.Value, toggle.LabelKey), Is.EqualTo((IndustrialSupplyContractActions.Toggle, "5", P + "Contracts.Disable")));
  var remove = Item(items, "contract.5.remove");
  Assert.That((remove.Action, remove.Value), Is.EqualTo((IndustrialSupplyContractActions.Remove, "5")));
  Assert.That(items.Any(i => i.Id == "contracts.empty"), Is.False);
 }
 [Test] public void UnknownPartialAndInvalidContractsNeverShowAFalseZero() {
  var items = Tab(Model(Row(Contract(1), IndustrialSupplyContractStatus.Unknown, capacity: null, need: null, potential: null),
   Row(Contract(2), IndustrialSupplyContractStatus.PartialData, capacity: 0, need: 400, potential: 0),
   Row(Contract(3, destination: 40), IndustrialSupplyContractStatus.Invalid, name: null, capacity: null, need: null, potential: null),
   Row(Contract(4, enabled: false), IndustrialSupplyContractStatus.Disabled, capacity: null, need: null, potential: null)));
  Assert.That(Item(items, "contract.1.potential").ValueKey, Is.EqualTo(P + "Status.Unavailable"));
  Assert.That(Item(items, "contract.1.potential").Value, Is.Null);
  Assert.That(Item(items, "contract.2.potential").Value, Is.EqualTo("≥0 /d"), "partial data is a lower bound");
  Assert.That(Item(items, "contract.3.resource").ValueKey, Is.EqualTo(P + "Contracts.DestinationMissing"));
  Assert.That(items.Any(i => i.Id == "contract.3.potential"), Is.False);
  Assert.That(Item(items, "contract.3.remove").Disabled, Is.False, "an invalid contract stays removable");
  Assert.That(Item(items, "contract.4.toggle").LabelKey, Is.EqualTo(P + "Contracts.Enable"));
 }
 [Test] public void CapDisablesNewPairsButNotUpdates() {
  var rows = Enumerable.Range(1, IndustrialSupplyContractModel.MaxContracts).Select(i => Row(Contract(i, destination: 100 + (ulong)i), IndustrialSupplyContractStatus.Unknown)).ToArray();
  var items = Tab(Model(rows));
  Assert.That(Item(items, "contracts.create").Disabled, Is.True);
  Assert.That(items.Any(i => i.LabelKey == P + "Contracts.Cap"), Is.True);
  var update = Model(rows); update.ContractDraft.Destination = 101; update.ContractDestinations = new[] { new AreaPanelOption { Value = "101", Label = "A" } };
  Assert.That(Item(Tab(update), "contracts.create").Disabled, Is.False, "an existing destination+resource can still be updated");
 }
 [Test] public void UnavailableContractsShowAWarningOnly() {
  var m = Model(); m.ContractsAvailable = false;
  var items = Tab(m);
  Assert.That(items.Select(i => i.LabelKey), Is.EqualTo(new[] { P + "Contracts.Tip", P + "Contracts.Unavailable" }));
 }
 [Test] public void DelayedEstimatesAreMarkedStaleAndDuplicateIdsRenderOnce() {
  var delayed = Row(Contract(5), IndustrialSupplyContractStatus.Constrained); delayed.Evaluation.Stale = true;
  var items = Tab(Model(delayed, Row(Contract(5, destination: 30), IndustrialSupplyContractStatus.Unknown)));
  Assert.That(Item(items, "contract.5.potential").TooltipKey, Is.EqualTo(P + "Status.Stale"));
  Assert.That(Item(items, "contract.5.potential").Value, Is.EqualTo("800 /d"), "a delayed cycle is not a lower bound");
  Assert.That(items.Select(i => i.Id).Distinct().Count(), Is.EqualTo(items.Count), "a corrupted duplicate id is shown once (removing it reveals the next)");
 }
 [Test] public void ATruncatedDestinationListSaysSo() {
  var m = Model(); m.ContractDestinationsTruncated = true;
  Assert.That(Tab(m).Any(i => i.LabelKey == P + "Contracts.DestinationsTruncated"), Is.True);
  Assert.That(Tab(Model()).Any(i => i.LabelKey == P + "Contracts.DestinationsTruncated"), Is.False);
 }
 [TestCase("en-US")] [TestCase("ja-JP")]
 public void EveryContractKeyIsLocalizedAndListed(string locale) {
  var entries = CS2AreaSystem.Core.UI.NativeAreaLocalization.Entries(locale);
  var all = IndustrialPanelBuilder.AllKeys().ToHashSet();
  var unavailable = Model(); unavailable.ContractsAvailable = false;
  var rows = Enumerable.Range(1, IndustrialSupplyContractModel.MaxContracts).Select(i => Row(Contract(i, destination: 100 + (ulong)i), (IndustrialSupplyContractStatus)(i % 9))).ToArray();
  var truncated = Model(); truncated.ContractDestinationsTruncated = true;
  var stale = Row(Contract(7), IndustrialSupplyContractStatus.Available); stale.Evaluation.Stale = true;
  foreach (var m in new[] { Model(Row(Contract(5), IndustrialSupplyContractStatus.Constrained), Row(Contract(6, enabled: false), IndustrialSupplyContractStatus.Disabled), stale), unavailable, Model(rows), Model(), truncated })
   foreach (var item in Tab(m)) {
    foreach (var key in new[] { item.LabelKey, item.ValueKey, item.TooltipKey }.Where(k => k != null && !k.StartsWith("Resources.TITLE[")))
     { Assert.That(entries.ContainsKey(key!), Is.True, locale + " is missing " + key); Assert.That(all, Does.Contain(key), key); }
   }
  foreach (var status in Enum.GetNames(typeof(IndustrialSupplyContractStatus))) Assert.That(entries.ContainsKey(P + "Contracts.Status." + status), Is.True, status);
  Assert.That(entries.ContainsKey("CS2AS.Tab.Contracts"), Is.True);
  Assert.That(entries[P + "Contracts.Tip"], Does.Match(locale == "ja-JP" ? "推定" : "(?i)estimate"));
 }
}
public class IndustrialSupplyContractLifecycleTests
{
 private sealed class Target : IIndustrialStateTarget {
  public readonly HashSet<Entity> Alive = new();
  public readonly Dictionary<Entity, IndustrialSupplyContractState> Contracts = new();
  public bool Exists(Entity area) => Alive.Contains(area);
  public bool HasState(Entity area) => true; public void AddState(Entity area, IndustrialAreaState state) { }
  public bool HasProgress(Entity area) => true; public void AddProgress(Entity area, Core.Areas.ManagedAreaProgress progress) { }
  public bool HasSupport(Entity area) => true; public void AddSupport(Entity area, IndustrialSupportState state) { }
  public bool HasContracts(Entity area) => Contracts.ContainsKey(area);
  public void AddContracts(Entity area, IndustrialSupplyContractState state) => Contracts.Add(area, state);
 }
 [Test] public void InitializationAddsContractStorageOnceAndNeverReplacesIt() {
  var a = new Entity { Index = 1, Version = 1 }; var b = new Entity { Index = 2, Version = 1 };
  var target = new Target(); target.Alive.UnionWith(new[] { a, b });
  target.Contracts[b] = new IndustrialSupplyContractState { SchemaVersion = 1, NextContractId = 9 };
  IndustrialStateInitializer.Ensure(new[] { new IndustrialAreaRef(a, 1, AreaLifecycleState.Active), new IndustrialAreaRef(b, 2, AreaLifecycleState.Suspended) }, target, 1);
  Assert.That(target.Contracts[a], Is.EqualTo(IndustrialSupplyContractState.CreateDefault()));
  Assert.That(target.Contracts[b].NextContractId, Is.EqualTo(9));
 }
 [Test] public void ContractSystemRunsAfterTheIndustrialRuntimeAsItsOwnModule() {
  var order = ModSystemOrderTests.Schedule(Game.SystemUpdatePhase.ModificationEnd);
  Assert.That(order.IndexOf(typeof(IndustrialSupplyContractSystem)), Is.GreaterThan(order.IndexOf(typeof(IndustrialRuntimeSystem))));
  Assert.That(IndustrialSupportSourceGuardTests.Source("Mod.cs"), Does.Contain("\"Industrial supply contracts\""));
 }
 [Test] public void AContractReadFailureOnlyDisablesTheContractsTab() {
  var facade = IndustrialSupportSourceGuardTests.Source("Modules/Industrial/IndustrialGameplayFacade.cs");
  Assert.That(facade, Does.Contain("catch (Exception error) { model.ContractsAvailable = false;"));
 }
 [Test] public void OnlyTheEnabledContractSystemWritesContracts() {
  var facade = IndustrialSupportSourceGuardTests.Source("Modules/Industrial/IndustrialGameplayFacade.cs");
  Assert.That(facade, Does.Contain("if (contract) return HandleContract(area, action, value, context);"), "after the Industrial Area checks");
  Assert.That(facade, Does.Contain("return system.TryApplyAction(area, areaId, action, value, live.Contains, outputs.Contains);"));
  var system = IndustrialSupportSourceGuardTests.Source("Modules/Industrial/IndustrialSupplyContractSystem.cs");
  Assert.That(system, Does.Contain("if (!Enabled || !EntityManager.HasComponent<IndustrialSupplyContractState>(area)"), "a failed registration disables editing");
  Assert.That(system, Does.Contain("if (!changed) return true;"), "an unchanged edit writes nothing");
 }
 // Advisory only: the D1 sources read observations and write only their own saved configuration.
 [Test] public void ContractSourcesNeverTouchRoutingTransfersPricesOrDemand() {
  foreach (var file in new[] { "IndustrialSupplyContract.cs", "IndustrialSupplyContractModel.cs", "IndustrialSupplyContractEvaluator.cs", "IndustrialSupplyContractActions.cs", "IndustrialSupplyContractSystem.cs", "IndustrialSupplyContractView.cs" })
   Assert.That(IndustrialSupportSourceGuardTests.Source("Modules/Industrial/" + file),
    Does.Not.Match(@"Pathfind|PathFind|PathInformation|SetupQueue|TransferRequest|ResourceBuyer|ResourceSeller|TradeCost|Price|Demand|EconomyUtils|AddResources|Game\.Vehicles|Game\.Pathfind"), file);
 }
}
