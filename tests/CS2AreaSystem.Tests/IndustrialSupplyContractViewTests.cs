using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 4: what the Contracts tab is filled with, from already collected Area rows (the facade only gathers them).
public class IndustrialSupplyContractViewTests
{
 private const ulong Source = 10;
 private static IndustrialObservationResult Result(params Resource[] outputs) {
  var r = new IndustrialObservationResult { Active = true };
  foreach (var o in outputs) r.Resources.Add(new IndustrialResourceTotal { Resource = (ulong)o, ActualPerDay = 100 });
  return r;
 }
 private static IndustrialSupplyContractArea Area(ulong id, string? name = null, bool selectable = true) =>
  new() { StableId = id, Name = name, Selectable = selectable, Side = new IndustrialSupplySide(Result(), false) };
 private static IndustrialSupplyContract Contract(int id, ulong destination) =>
  new() { SchemaVersion = 1, ContractId = id, DestinationStableId = destination, Resource = (ulong)Resource.Timber, TargetPerDay = 1000, Enabled = true };

 [Test] public void DestinationsAreOtherSelectableAreasWithinTheBound() {
  var areas = new List<IndustrialSupplyContractArea> { Area(Source, "Self"), Area(20, "North"), Area(30, selectable: false), Area(40) };
  for (ulong i = 0; i < 80; i++) areas.Add(Area(100 + i));
  var model = new IndustrialPanelModel();
  IndustrialSupplyContractView.Fill(model, Source, Array.Empty<IndustrialSupplyContract>(), default, areas, new IndustrialSupplyContractCache(), new IndustrialSupplyContractDraft());
  Assert.That(model.ContractDestinations.Count, Is.EqualTo(CS2AreaSystem.Core.Modules.AreaPanelOption.MaxOptions));
  Assert.That(model.ContractDestinations.Take(2).Select(o => (o.Value, o.Label)), Is.EqualTo(new[] { ("20", "North"), ("40", "40") }), "unnamed Areas show their id");
  Assert.That(model.ContractDestinations.Any(o => o.Value == "10" || o.Value == "30"), Is.False);
  Assert.That(model.ContractsAvailable, Is.True);
  Assert.That(model.ContractDestinationsTruncated, Is.True);
  var few = new IndustrialPanelModel();
  IndustrialSupplyContractView.Fill(few, Source, Array.Empty<IndustrialSupplyContract>(), default, new[] { Area(20) }, new IndustrialSupplyContractCache(), new IndustrialSupplyContractDraft());
  Assert.That(few.ContractDestinationsTruncated, Is.False);
 }
 [Test] public void ResourcesAreTheSourcesObservedValidOutputs() {
  var model = new IndustrialPanelModel();
  var source = Result(Resource.Timber, Resource.Money, Resource.Timber, Resource.Paper);
  IndustrialSupplyContractView.Fill(model, Source, Array.Empty<IndustrialSupplyContract>(), new IndustrialSupplySide(source, false), Array.Empty<IndustrialSupplyContractArea>(),
   new IndustrialSupplyContractCache(), new IndustrialSupplyContractDraft());
  Assert.That(model.ContractResources.Select(o => (o.Value, o.LabelKey)), Is.EqualTo(new[] {
   (((ulong)Resource.Timber).ToString(), "Resources.TITLE[Timber]"), (((ulong)Resource.Paper).ToString(), "Resources.TITLE[Paper]") }));
  IndustrialSupplyContractView.Fill(model, Source, Array.Empty<IndustrialSupplyContract>(), default, Array.Empty<IndustrialSupplyContractArea>(),
   new IndustrialSupplyContractCache(), new IndustrialSupplyContractDraft());
  Assert.That(model.ContractResources, Is.Empty, "no observation yet");
 }
 [Test] public void RowsNameLiveDestinationsAndMarkDeletedOnesInvalid() {
  var model = new IndustrialPanelModel(); var draft = new IndustrialSupplyContractDraft { Destination = 20 };
  var areas = new[] { Area(20, "North"), Area(30, selectable: false) };
  IndustrialSupplyContractView.Fill(model, Source, new[] { Contract(1, 20), Contract(2, 40), Contract(3, 30) }, new IndustrialSupplySide(Result(Resource.Timber), false), areas,
   new IndustrialSupplyContractCache(), draft);
  Assert.That(model.ContractDraft, Is.SameAs(draft));
  Assert.That(model.ContractRows.Select(r => r.DestinationName), Is.EqualTo(new[] { "North", null, "30" }));
  Assert.That(model.ContractRows[1].Evaluation.Status, Is.EqualTo(IndustrialSupplyContractStatus.Invalid), "the destination was deleted");
  Assert.That(model.ContractRows[2].Evaluation.Status, Is.Not.EqualTo(IndustrialSupplyContractStatus.Invalid), "a quarantined destination still exists");
 }
 [Test] public void OnlyTheDisplayedContractsAreEvaluated() {
  var contracts = Enumerable.Range(1, IndustrialSupplyContractModel.MaxContracts + 5).Select(i => Contract(i, 20)).ToArray();
  var cache = new IndustrialSupplyContractCache(); var model = new IndustrialPanelModel();
  IndustrialSupplyContractView.Fill(model, Source, contracts, default, new[] { Area(20) }, cache, new IndustrialSupplyContractDraft());
  Assert.That(model.ContractRows.Count, Is.EqualTo(IndustrialSupplyContractModel.MaxContracts));
  Assert.That(cache.Evaluations, Is.EqualTo(IndustrialSupplyContractModel.MaxContracts));
 }
}
