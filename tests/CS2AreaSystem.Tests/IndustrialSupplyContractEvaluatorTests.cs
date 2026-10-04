using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 2: advisory potential from existing observations only; never a shipped volume, never a false zero.
public class IndustrialSupplyContractEvaluatorTests
{
 private const ulong Source = 1, Destination = 2;
 private static readonly ulong Timber = (ulong)Resource.Timber, Wood = (ulong)Resource.Wood, Paper = (ulong)Resource.Paper, Furniture = (ulong)Resource.Furniture;
 private static IndustrialSupplyContract Contract(int target = 1000, bool enabled = true, ulong resource = 0, ulong destination = Destination) =>
  new() { SchemaVersion = 1, ContractId = 5, DestinationStableId = destination, Resource = resource == 0 ? Timber : resource, TargetPerDay = target, Enabled = enabled };
 private static IndustrialObservationResult SourceResult(double timberPerDay = 1500, bool delayed = false, int failedReads = 0) {
  var r = new IndustrialObservationResult { Active = true, Delayed = delayed, FailedReads = failedReads };
  if (timberPerDay >= 0) r.Resources.Add(new IndustrialResourceTotal { Resource = Timber, ActualPerDay = timberPerDay, Producers = 2 });
  return r;
 }
 // Furniture from Timber 2 : 1 output, plus Paper from Wood (unrelated).
 private static IndustrialObservationResult DestinationResult(double furniturePerDay = 400, int companies = 2, int measured = 2, int timberAmount = 2, int outputAmount = 1, bool delayed = false) {
  var r = new IndustrialObservationResult { Active = true, Delayed = delayed };
  r.Processes.Add(new IndustrialProcessSummary { Process = new IndustrialProcessKey(Timber, timberAmount, 0, 0, Furniture, outputAmount), Companies = companies, MeasuredCompanies = measured, ActualOutputPerDay = furniturePerDay });
  r.Processes.Add(new IndustrialProcessSummary { Process = new IndustrialProcessKey(Wood, 1, 0, 0, Paper, 1), Companies = 1, MeasuredCompanies = 1, ActualOutputPerDay = 999 });
  return r;
 }
 private static IndustrialSupplyContractEvaluation Eval(IndustrialSupplyContract c, IndustrialObservationResult? source, IndustrialObservationResult? destination,
  bool destinationLive = true, bool sourceStale = false, bool destinationStale = false) =>
  IndustrialSupplyContractEvaluator.Evaluate(c, Source, destinationLive, new IndustrialSupplySide(source, sourceStale), new IndustrialSupplySide(destination, destinationStale));

 [Test] public void PotentialIsTheMinimumOfTargetCapacityAndNeed() {
  var e = Eval(Contract(target: 1000), SourceResult(1500), DestinationResult(400));
  Assert.That(e.Status, Is.EqualTo(IndustrialSupplyContractStatus.Constrained));
  Assert.That((e.SourceCapacityPerDay, e.DestinationNeedPerDay, e.PotentialPerDay), Is.EqualTo((1500d, 800d, 800d)), "need = 400 furniture × 2 timber / 1");
  var available = Eval(Contract(target: 500), SourceResult(1500), DestinationResult(400));
  Assert.That(available.Status, Is.EqualTo(IndustrialSupplyContractStatus.Available)); Assert.That(available.PotentialPerDay, Is.EqualTo(500));
  var capped = Eval(Contract(target: 1000), SourceResult(300), DestinationResult(400));
  Assert.That(capped.Status, Is.EqualTo(IndustrialSupplyContractStatus.Constrained)); Assert.That(capped.PotentialPerDay, Is.EqualTo(300));
 }
 [Test] public void NeedSumsEveryConsumingRecipeIncludingASecondInput() {
  var d = DestinationResult(400);
  d.Processes.Add(new IndustrialProcessSummary { Process = new IndustrialProcessKey(Wood, 1, Timber, 3, Paper, 2), Companies = 1, MeasuredCompanies = 1, ActualOutputPerDay = 100 });
  Assert.That(Eval(Contract(target: 5000), SourceResult(5000), d).DestinationNeedPerDay, Is.EqualTo(800 + 150));
 }
 [Test] public void MissingOrStaleObservationIsUnknownNeverZero() {
  foreach (var e in new[] { Eval(Contract(), null, DestinationResult()), Eval(Contract(), SourceResult(), null),
   Eval(Contract(), SourceResult(), DestinationResult(), sourceStale: true), Eval(Contract(), SourceResult(), DestinationResult(), destinationStale: true) }) {
   Assert.That(e.Status, Is.EqualTo(IndustrialSupplyContractStatus.Unknown));
   Assert.That(e.PotentialPerDay, Is.Null); Assert.That(e.SourceCapacityPerDay, Is.Null); Assert.That(e.DestinationNeedPerDay, Is.Null);
  }
 }
 [Test] public void DelayedFailedOrPartiallyMeasuredObservationIsPartialData() {
  Assert.That(Eval(Contract(), SourceResult(delayed: true), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData));
  Assert.That(Eval(Contract(), SourceResult(failedReads: 1), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData));
  Assert.That(Eval(Contract(), SourceResult(), DestinationResult(delayed: true)).Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData));
  var partial = Eval(Contract(), SourceResult(), DestinationResult(400, companies: 3, measured: 2));
  Assert.That(partial.Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData));
  Assert.That(partial.DestinationNeedPerDay, Is.EqualTo(800), "the measured part is shown as an estimate, not as a full value");
  var noSupplyYet = Eval(Contract(), SourceResult(-1, failedReads: 1), DestinationResult());
  Assert.That(noSupplyYet.Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData), "partial data never claims there is no supply");
 }
 [Test] public void AnInvalidRecipeAmountMakesTheNeedPartial() {
  var e = Eval(Contract(), SourceResult(), DestinationResult(400, outputAmount: 0));
  Assert.That(e.Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData));
  Assert.That(e.DestinationNeedPerDay, Is.EqualTo(0));
 }
 [Test] public void ResourceAbsentFromObservationsIsReportedExplicitly() {
  Assert.That(Eval(Contract(resource: (ulong)Resource.Grain), SourceResult(), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.Unavailable));
  Assert.That(Eval(Contract(), SourceResult(-1), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.NoObservedSupply));
  Assert.That(Eval(Contract(), SourceResult(0), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.NoObservedSupply));
  Assert.That(Eval(Contract(), SourceResult(), DestinationResult(0)).Status, Is.EqualTo(IndustrialSupplyContractStatus.NoObservedNeed));
  var noRecipe = DestinationResult(); noRecipe.Processes.RemoveAt(0);
  Assert.That(Eval(Contract(), SourceResult(), noRecipe).Status, Is.EqualTo(IndustrialSupplyContractStatus.NoObservedNeed));
 }
 [Test] public void InvalidOrDisabledContractsAreNotEvaluated() {
  Assert.That(Eval(Contract(), SourceResult(), DestinationResult(), destinationLive: false).Status, Is.EqualTo(IndustrialSupplyContractStatus.Invalid), "deleted destination");
  Assert.That(Eval(Contract(destination: Source), SourceResult(), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.Invalid), "self");
  Assert.That(Eval(Contract(resource: (ulong)Resource.Money), SourceResult(), DestinationResult()).Status, Is.EqualTo(IndustrialSupplyContractStatus.Invalid));
  var disabled = Eval(Contract(enabled: false), SourceResult(), DestinationResult());
  Assert.That(disabled.Status, Is.EqualTo(IndustrialSupplyContractStatus.Disabled)); Assert.That(disabled.PotentialPerDay, Is.Null);
 }
 [Test] public void NonFiniteObservedRatesAreNotTreatedAsValues() {
  var e = Eval(Contract(), SourceResult(double.NaN), DestinationResult(double.PositiveInfinity));
  Assert.That(e.Status, Is.EqualTo(IndustrialSupplyContractStatus.PartialData));
  Assert.That(double.IsFinite(e.PotentialPerDay ?? 0), Is.True);
 }
 // The evaluator is pure: it only reads the two observation results it is handed.
 [Test] public void EvaluatorHasNoSimulationWrites() =>
  Assert.That(IndustrialSupportSourceGuardTests.Source("Modules/Industrial/IndustrialSupplyContractEvaluator.cs"),
   Does.Not.Match(@"EntityManager|SystemAPI|Transfer|PathFind|Pathfind|ResourceBuyer|Price|Demand|Unity\.Entities"));
}
