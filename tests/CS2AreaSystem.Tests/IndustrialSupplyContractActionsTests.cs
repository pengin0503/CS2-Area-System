using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 3: guarded contract actions. Core already checked the source StableId and live selection; these rules
// re-validate every value against live state and address contracts only by their stable id.
public class IndustrialSupplyContractActionsTests
{
 private const ulong Source = 10, North = 20, Harbor = 30, Deleted = 40;
 private static readonly string Timber = ((ulong)Resource.Timber).ToString(System.Globalization.CultureInfo.InvariantCulture);
 private sealed class Fixture {
  public readonly IndustrialSupplyContractDraft Draft = new();
  public readonly List<IndustrialSupplyContract> Contracts = new();
  public IndustrialSupplyContractState State = IndustrialSupplyContractState.CreateDefault();
  public bool Apply(string action, string value, out bool changed) =>
   IndustrialSupplyContractActions.TryApply(action, value, Source, Draft, Contracts, ref State, id => id == North || id == Harbor, r => r == (ulong)Resource.Timber || r == (ulong)Resource.Paper, out changed);
  public bool Apply(string action, string value) => Apply(action, value, out _);
 }
 [Test] public void DraftThenCreateMakesOneContract() {
  var f = new Fixture();
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftDestination, "20", out var c1), Is.True); Assert.That(c1, Is.False, "a draft is runtime-only");
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftResource, Timber), Is.True);
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftTarget, "2010"), Is.True);
  Assert.That(f.Draft.TargetPerDay, Is.EqualTo(2000));
  Assert.That(f.Apply(IndustrialSupplyContractActions.Create, "", out var c2), Is.True); Assert.That(c2, Is.True);
  var contract = f.Contracts.Single();
  Assert.That((contract.ContractId, contract.DestinationStableId, contract.Resource, contract.TargetPerDay, contract.Enabled), Is.EqualTo((1, North, (ulong)Resource.Timber, 2000, true)));
  Assert.That(f.Apply(IndustrialSupplyContractActions.Create, ""), Is.False, "repeating the same draft changes nothing");
  Assert.That(f.Contracts.Count, Is.EqualTo(1));
 }
 [TestCase("10")] [TestCase("40")] [TestCase("0")] [TestCase("-20")] [TestCase("20x")] [TestCase("")] [TestCase(null)]
 public void DraftDestinationMustBeAnotherLiveIndustrialArea(string? value) {
  var f = new Fixture(); f.Draft.Destination = Harbor;
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftDestination, value!), Is.False);
  Assert.That(f.Draft.Destination, Is.EqualTo(Harbor));
 }
 [Test] public void DraftResourceMustBeOfferedAndValid() {
  var f = new Fixture();
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftResource, ((ulong)Resource.Grain).ToString()), Is.False, "not observed at the source");
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftResource, "1"), Is.False, "money");
  Assert.That(f.Apply(IndustrialSupplyContractActions.DraftResource, "Timber"), Is.False, "numeric values only");
  Assert.That(f.Draft.Resource, Is.EqualTo(0UL));
 }
 [Test] public void CreateRevalidatesTheDestinationAtCommitTime() {
  var f = new Fixture(); f.Draft.Destination = Deleted; f.Draft.Resource = (ulong)Resource.Timber;
  Assert.That(f.Apply(IndustrialSupplyContractActions.Create, ""), Is.False, "the destination was deleted after it was drafted");
  f.Draft.Destination = 0;
  Assert.That(f.Apply(IndustrialSupplyContractActions.Create, ""), Is.False, "incomplete draft");
  Assert.That(f.Contracts, Is.Empty); Assert.That(f.State.NextContractId, Is.EqualTo(1));
 }
 [Test] public void EditsUseStableIdsAndWorkOnInvalidContracts() {
  var f = new Fixture();
  f.Contracts.Add(new IndustrialSupplyContract { SchemaVersion = 1, ContractId = 7, DestinationStableId = Deleted, Resource = (ulong)Resource.Timber, TargetPerDay = 1000, Enabled = true });
  f.State.NextContractId = 8;
  Assert.That(f.Apply(IndustrialSupplyContractActions.TargetPrefix + "7", "1500", out var changed), Is.True); Assert.That(changed, Is.True);
  Assert.That(f.Contracts[0].TargetPerDay, Is.EqualTo(1500));
  Assert.That(f.Apply(IndustrialSupplyContractActions.TargetPrefix + "8", "1500"), Is.False, "unknown id");
  Assert.That(f.Apply(IndustrialSupplyContractActions.TargetPrefix + "x", "1500"), Is.False);
  Assert.That(f.Apply(IndustrialSupplyContractActions.TargetPrefix + "7", "NaN"), Is.False);
  Assert.That(f.Apply(IndustrialSupplyContractActions.Toggle, "7"), Is.True); Assert.That(f.Contracts[0].Enabled, Is.False);
  Assert.That(f.Apply(IndustrialSupplyContractActions.Remove, "8"), Is.False);
  Assert.That(f.Apply(IndustrialSupplyContractActions.Remove, "7"), Is.True, "a contract to a deleted destination stays removable");
  Assert.That(f.Contracts, Is.Empty);
 }
 // Review fix: a saved contract with a corrupted id stays removable and toggleable by that id.
 [TestCase(0)] [TestCase(-3)]
 public void CorruptedIdsStayRemovable(int id) {
  var f = new Fixture();
  f.Contracts.Add(new IndustrialSupplyContract { SchemaVersion = 1, ContractId = id, DestinationStableId = North, Resource = (ulong)Resource.Timber, TargetPerDay = 1000, Enabled = true });
  string text = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
  Assert.That(f.Apply(IndustrialSupplyContractActions.Toggle, text), Is.True);
  Assert.That(f.Apply(IndustrialSupplyContractActions.Remove, text), Is.True);
  Assert.That(f.Contracts, Is.Empty);
  Assert.That(f.Apply(IndustrialSupplyContractActions.Remove, "+-1"), Is.False);
 }
 [Test] public void CreateRevalidatesTheResourceAtCommitTime() {
  var f = new Fixture(); f.Draft.Destination = North; f.Draft.Resource = (ulong)Resource.Grain;
  Assert.That(f.Apply(IndustrialSupplyContractActions.Create, ""), Is.False, "the drafted resource is no longer an observed output");
  Assert.That(f.Contracts, Is.Empty);
 }
 [Test] public void UnknownActionsAreNotContractActions() {
  Assert.That(IndustrialSupplyContractActions.IsContractAction("setSupportPerCompany"), Is.False);
  Assert.That(IndustrialSupplyContractActions.IsContractAction(IndustrialSupplyContractActions.TargetPrefix + "3"), Is.True);
  Assert.That(IndustrialSupplyContractActions.IsContractAction(null!), Is.False);
  Assert.That(new Fixture().Apply("contractNuke", "1"), Is.False);
 }
}
