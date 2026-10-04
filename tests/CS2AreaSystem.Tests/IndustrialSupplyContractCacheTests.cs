using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 D1 Task 4: contracts are re-evaluated only when a contract, a published observation or a destination's
// liveness/staleness changes, not on every panel refresh.
public class IndustrialSupplyContractCacheTests
{
 private static IndustrialSupplyContract Contract(int id, ulong destination, int target = 1000) =>
  new() { SchemaVersion = 1, ContractId = id, DestinationStableId = destination, Resource = (ulong)Resource.Timber, TargetPerDay = target, Enabled = true };
 private static IndustrialObservationResult Result() {
  var r = new IndustrialObservationResult { Active = true };
  r.Resources.Add(new IndustrialResourceTotal { Resource = (ulong)Resource.Timber, ActualPerDay = 500 });
  r.Processes.Add(new IndustrialProcessSummary { Process = new IndustrialProcessKey((ulong)Resource.Timber, 1, 0, 0, (ulong)Resource.Furniture, 1), Companies = 1, MeasuredCompanies = 1, ActualOutputPerDay = 300 });
  return r;
 }
 [Test] public void ReevaluatesOnlyWhatChanged() {
  var cache = new IndustrialSupplyContractCache();
  var source = Result(); var north = Result(); var harbor = Result();
  var sides = new Dictionary<ulong, (bool, IndustrialSupplySide)> { [2] = (true, new IndustrialSupplySide(north, false)), [3] = (true, new IndustrialSupplySide(harbor, false)) };
  (bool, IndustrialSupplySide) Dest(ulong id) => sides.TryGetValue(id, out var s) ? s : (false, default);
  var contracts = new List<IndustrialSupplyContract> { Contract(1, 2), Contract(2, 3) };
  var first = cache.Evaluate(1, contracts, new IndustrialSupplySide(source, false), Dest);
  Assert.That(first.Select(e => e.Status), Is.EqualTo(new[] { IndustrialSupplyContractStatus.Constrained, IndustrialSupplyContractStatus.Constrained }), "target 1000 > need 300");
  Assert.That(cache.Evaluations, Is.EqualTo(2));
  var again = cache.Evaluate(1, contracts, new IndustrialSupplySide(source, false), Dest);
  Assert.That(cache.Evaluations, Is.EqualTo(2), "unchanged inputs reuse the results");
  Assert.That(again[0], Is.SameAs(first[0]));
  contracts[1] = Contract(2, 3, target: 2000);
  cache.Evaluate(1, contracts, new IndustrialSupplySide(source, false), Dest);
  Assert.That(cache.Evaluations, Is.EqualTo(3), "only the edited contract");
  sides[2] = (true, new IndustrialSupplySide(Result(), false));
  cache.Evaluate(1, contracts, new IndustrialSupplySide(source, false), Dest);
  Assert.That(cache.Evaluations, Is.EqualTo(4), "a new destination observation");
  sides.Remove(3);
  var deleted = cache.Evaluate(1, contracts, new IndustrialSupplySide(source, false), Dest);
  Assert.That(cache.Evaluations, Is.EqualTo(5)); Assert.That(deleted[1].Status, Is.EqualTo(IndustrialSupplyContractStatus.Invalid));
  cache.Evaluate(1, contracts, new IndustrialSupplySide(source, true), Dest);
  Assert.That(cache.Evaluations, Is.EqualTo(7), "the source going stale affects every contract");
  cache.Clear();
  cache.Evaluate(1, contracts, new IndustrialSupplySide(source, true), Dest);
  Assert.That(cache.Evaluations, Is.EqualTo(9));
 }
 [Test] public void PruneDropsRemovedSources() {
  var cache = new IndustrialSupplyContractCache();
  cache.Evaluate(1, new[] { Contract(1, 2) }, default, _ => (false, default));
  cache.Evaluate(5, new[] { Contract(1, 2) }, default, _ => (false, default));
  Assert.That(cache.Sources, Is.EqualTo(2));
  cache.Retain(new HashSet<ulong> { 5 });
  Assert.That(cache.Sources, Is.EqualTo(1));
 }
}
