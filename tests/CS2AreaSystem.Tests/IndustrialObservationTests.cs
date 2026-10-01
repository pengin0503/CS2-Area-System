using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class IndustrialObservationTests {
 private const int Park = (int)AreaTypeId.Park, Industrial = (int)AreaTypeId.Industrial;
 private static Entity E(int i) => new() { Index = i, Version = 1 };
 private static readonly Entity factory = E(100), mine = E(101), warehouse = E(102), terminal = E(103), office = E(104), house = E(105);
 // ---- helpers
 private static IndustrialEntityObservation Producer(Entity e, double activity, int jobs, int employees, ulong resource = (ulong)Resource.Furniture,
  IndustrialFlowState input = IndustrialFlowState.Healthy, IndustrialFlowState output = IndustrialFlowState.Healthy, double efficiency = 100, double profit = 80) => new() {
  Facility = e, Roles = IndustrialFacilityRole.Processor, IsProducer = true, ProducerTenants = 1, Available = true, IsOperatingProducer = activity > 0, ProductiveActivity = activity,
  Resources = new[] { new IndustrialResourceActivity { Resource = resource, ActualPerDay = activity, CapacityPerDay = 100 } },
  Input = input, Output = output, Jobs = jobs, Employees = employees, VacanciesByLevel = new[] { jobs - employees, 0, 0, 0, 0 }, Efficiency = efficiency, Profitability = profit };
 private static IndustrialEntityObservation Cargo(Entity e, bool operational) => new() {
  Facility = e, Roles = IndustrialFacilityRole.Cargo | IndustrialFacilityRole.SupportingWorkplace, Available = true, CargoOperational = operational, Jobs = 30, Employees = 30,
  VacanciesByLevel = new int[5], Input = IndustrialFlowState.NotApplicable, Output = IndustrialFlowState.NotApplicable, Efficiency = operational ? 100 : 0 };
 private static IndustrialEntityObservation Office(Entity e) => new() { Facility = e, Roles = IndustrialFacilityRole.None, Available = true, Jobs = 500, Employees = 500 };
 private static IndustrialCycleInfo Info(double start, double end, ulong membership = 1, ulong structure = 1) =>
  new() { CycleId = 1, HourBucket = (long)Math.Floor(start), StartHours = start, EndHours = end, MembershipToken = membership, StructureToken = structure, Active = true };
 private static IndustrialObservationResult Run(IndustrialRuntimeSnapshot snapshot, IndustrialCycleInfo info, params IndustrialEntityObservation[] observations) {
  var acc = new IndustrialCycleAccumulator();
  foreach (var o in observations) IndustrialObservationAggregator.Apply(acc, o);
  return IndustrialObservationAggregator.Finalize(acc, snapshot, info);
 }
 private static IndustrialObservationResult Hours(IndustrialRuntimeSnapshot snapshot, int hours, Func<int, IndustrialEntityObservation[]> at) {
  IndustrialObservationResult last = null;
  for (int h = 0; h <= hours; h++) last = Run(snapshot, Info(h, h + 0.1), at(h));
  return last!;
 }
 private sealed class Source : IIndustrialCycleSource {
  public readonly AreaMembershipIndex Index = new();
  public readonly IndustrialStructureTracker Structure = new();
  private IndustrialMembershipCycleSource Inner => new(Index, Structure);
  public ulong MembershipToken(ulong areaId) => Inner.MembershipToken(areaId);
  public ulong StructureToken(ulong areaId) => Inner.StructureToken(areaId);
  public IReadOnlyList<Entity> PrimaryMembers(ulong areaId, out int nonPrimary) => Inner.PrimaryMembers(areaId, out nonPrimary);
 }
 private static IndustrialAreaRef Area(ulong id, AreaLifecycleState state = AreaLifecycleState.Active) => new(E((int)id), id, state);
 private static List<IndustrialReadRequest> Drain(IndustrialObservationScheduler s) { var list = new List<IndustrialReadRequest>(); s.NextReads(list); return list; }
 // ---- ownership and overlap
 [Test] public void ABuildingIsReadOnlyForItsPrimaryIndustrialArea() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(30, Industrial, true), new AreaMembership(5, Park, true) });
  Assert.That(source.PrimaryMembers(10, out var none), Is.EqualTo(new[] { factory })); Assert.That(none, Is.Zero);
  Assert.That(source.PrimaryMembers(30, out var overlap), Is.Empty, "non-primary Area does not claim the factory"); Assert.That(overlap, Is.EqualTo(1));
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10), Area(30) }, 0, 0, 0, source);
  Assert.That(Drain(scheduler).Select(r => (r.Building, r.AreaId)), Is.EqualTo(new[] { (factory, 10UL) }));
 }
 [Test] public void PrimaryOwnershipTransferRestartsBothAreasAndCreditsTheBuildingOnce() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(30, Industrial, true) });
  source.Index.ReplaceMembership(mine, new[] { new AreaMembership(10, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10), Area(30) }, 0, 0, 0, source);
  long ten = scheduler.Cycle(10)!.CycleId, thirty = scheduler.Cycle(30)!.CycleId;
  // Area 10 grows over the factory and becomes its primary owner.
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(30, Industrial, true), new AreaMembership(10, Industrial, true) });
  scheduler.Sync(new[] { Area(10), Area(30) }, 0, 10, 0.01, source);
  Assert.That(scheduler.Cycle(10)!.CycleId, Is.Not.EqualTo(ten)); Assert.That(scheduler.Cycle(30)!.CycleId, Is.Not.EqualTo(thirty));
  Assert.That(scheduler.Cycle(10)!.Members, Is.EquivalentTo(new[] { factory, mine })); Assert.That(scheduler.Cycle(30)!.Members, Is.Empty);
  Assert.That(scheduler.RestartCount, Is.EqualTo(2));
 }
 [Test] public void OtherAreaAndParkEditsDoNotRestartThisArea() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true) });
  source.Index.ReplaceMembership(mine, new[] { new AreaMembership(20, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10), Area(20) }, 0, 0, 0, source);
  long cycle = scheduler.Cycle(10)!.CycleId;
  source.Index.ReplaceMembership(mine, new[] { new AreaMembership(20, Industrial, true), new AreaMembership(7, Park, true) });
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(7, Park, true) });
  source.Index.ReplaceMembership(warehouse, new[] { new AreaMembership(20, Industrial, true) });
  source.Structure.Bump(20);
  scheduler.Sync(new[] { Area(10), Area(20) }, 0, 10, 0.01, source);
  Assert.That(scheduler.Cycle(10)!.CycleId, Is.EqualTo(cycle));
  Assert.That(scheduler.RestartCount, Is.EqualTo(1), "only Area 20 restarted");
 }
 [Test] public void LocalStructureChangeRestartsButEconomicValuesDoNot() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10) }, 0, 0, 0, source);
  long cycle = scheduler.Cycle(10)!.CycleId;
  // Employee/stock fluctuations carry no change evidence: tokens stay equal.
  scheduler.Sync(new[] { Area(10) }, 0, 10, 0.01, source);
  Assert.That(scheduler.Cycle(10)!.CycleId, Is.EqualTo(cycle));
  source.Structure.Bump(10); // tenant moved in/out of a member (RentersUpdated) or a member building changed
  scheduler.Sync(new[] { Area(10) }, 0, 20, 0.02, source);
  Assert.That(scheduler.Cycle(10)!.CycleId, Is.Not.EqualTo(cycle));
 }
 [Test] public void StructureTrackerBumpsOnlyIndustrialAreasOfTheChangedMember() {
  var index = new AreaMembershipIndex();
  index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true), new AreaMembership(30, Industrial, true), new AreaMembership(5, Park, true) });
  var tracker = new IndustrialStructureTracker();
  tracker.MemberChanged(factory, index);
  Assert.That(tracker.Get(10), Is.Not.EqualTo(0UL)); Assert.That(tracker.Get(30), Is.Not.EqualTo(0UL)); Assert.That(tracker.Get(5), Is.EqualTo(0UL));
  tracker.MemberChanged(house, index);
  ulong before = tracker.Get(10); tracker.Remove(10);
  Assert.That(tracker.Get(10), Is.Not.EqualTo(before), "removed token never equals a captured one");
  tracker.Clear(); Assert.That(tracker.Get(30), Is.EqualTo(0UL));
 }
 [Test] public void CompletedCyclesPublishOnlyWithUnchangedTokens() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10) }, 0, 0, 0, source);
  var reads = Drain(scheduler);
  foreach (var r in reads) scheduler.Complete(r, Producer(r.Building, 50, 10, 10));
  source.Structure.Bump(10);
  Assert.That(scheduler.TakeFinished(source, 0, 30, 0.05), Is.Empty, "a generation changed before publish is discarded");
  var again = Drain(scheduler);
  Assert.That(again, Has.Count.EqualTo(1));
  foreach (var r in again) scheduler.Complete(r, Producer(r.Building, 50, 10, 10));
  foreach (var stale in reads) scheduler.Complete(stale, Producer(stale.Building, 50, 10, 10)); // late result of the discarded cycle
  var finished = scheduler.TakeFinished(source, 0, 40, 0.06);
  Assert.That(finished, Has.Count.EqualTo(1));
  Assert.That(finished[0].Accumulator.ProducerFacilities, Is.EqualTo(1), "never mixes generations");
 }
 [Test] public void OneCyclePerGameHourAndOnlyForActiveAreas() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true) });
  source.Index.ReplaceMembership(mine, new[] { new AreaMembership(20, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10), Area(20, AreaLifecycleState.Suspended) }, 5, 0, 5.0, source);
  Assert.That(scheduler.Cycle(20), Is.Null, "suspended Areas publish no fresh snapshot");
  foreach (var r in Drain(scheduler)) scheduler.Complete(r, Producer(r.Building, 50, 10, 10));
  Assert.That(scheduler.TakeFinished(source, 5, 10, 5.1), Has.Count.EqualTo(1));
  scheduler.Sync(new[] { Area(10) }, 5, 20, 5.5, source);
  Assert.That(scheduler.Cycle(10), Is.Null, "same hour bucket is not observed twice");
  scheduler.Sync(new[] { Area(10) }, 6, 30, 6.0, source);
  Assert.That(scheduler.Cycle(10), Is.Not.Null);
  scheduler.Sync(new[] { Area(10, AreaLifecycleState.Suspended) }, 6, 40, 6.1, source);
  Assert.That(scheduler.Cycle(10), Is.Null, "suspension drops the running cycle");
 }
 [Test] public void ReadBudgetIsSharedFairlyAcrossAreas() {
  var source = new Source();
  for (int i = 0; i < 200; i++) source.Index.ReplaceMembership(E(1000 + i), new[] { new AreaMembership(10, Industrial, true) });
  for (int i = 0; i < 5; i++) source.Index.ReplaceMembership(E(2000 + i), new[] { new AreaMembership(20, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler(128);
  scheduler.Sync(new[] { Area(10), Area(20) }, 0, 0, 0, source);
  var first = Drain(scheduler);
  Assert.That(first, Has.Count.EqualTo(128));
  Assert.That(first.Count(r => r.AreaId == 20), Is.EqualTo(5), "the small Area is not starved by the large one");
  Assert.That(Drain(scheduler), Has.Count.EqualTo(77), "remaining reads continue next update");
  Assert.That(Drain(scheduler), Is.Empty);
 }
 [Test] public void LoadClearsAllCycles() {
  var source = new Source();
  source.Index.ReplaceMembership(factory, new[] { new AreaMembership(10, Industrial, true) });
  var scheduler = new IndustrialObservationScheduler();
  scheduler.Sync(new[] { Area(10) }, 0, 0, 0, source);
  scheduler.Clear();
  Assert.That(scheduler.Cycle(10), Is.Null); Assert.That(Drain(scheduler), Is.Empty);
  scheduler.Sync(new[] { Area(10) }, 0, 10, 0.01, source);
  Assert.That(scheduler.Cycle(10), Is.Not.Null, "after load the current hour is observed again");
 }
 // ---- aggregation semantics
 [Test] public void MultiRoleFacilitiesCountJobsOnceAndOfficesNeverContribute() {
  var hub = Producer(factory, 100, 40, 40); hub.Roles |= IndustrialFacilityRole.Storage;
  var r = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), hub, Office(office), new IndustrialEntityObservation { Facility = house, Available = true });
  Assert.That(r.Jobs, Is.EqualTo(40)); Assert.That(r.Employees, Is.EqualTo(40));
  Assert.That(r.Processors, Is.EqualTo(1)); Assert.That(r.StorageFacilities, Is.EqualTo(1));
  Assert.That(r.Members, Is.EqualTo(3)); Assert.That(r.ProducerFacilities, Is.EqualTo(1));
  var duplicate = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), hub, hub);
  Assert.That(duplicate.Jobs, Is.EqualTo(40), "a building applied twice is counted once");
 }
 [Test] public void InactiveOrIdleProducersAreNotActiveProductiveFacilities() {
  var idle = Producer(mine, 0, 20, 20); idle.Roles = IndustrialFacilityRole.Extractor;
  var abandoned = Producer(E(200), 80, 20, 0); abandoned.Available = false;
  var vacant = new IndustrialEntityObservation { Facility = E(201), Roles = IndustrialFacilityRole.Processor, IsProducer = true, Available = true };
  var r = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), Producer(factory, 80, 20, 20), idle, abandoned, vacant);
  Assert.That(r.ProducerFacilities, Is.EqualTo(4)); Assert.That(r.OperatingProducers, Is.EqualTo(1));
  Assert.That(r.ActiveShare.Value, Is.EqualTo(25).Within(1e-9));
  Assert.That(r.Extractors, Is.EqualTo(1)); Assert.That(r.Processors, Is.EqualTo(3));
 }
 [Test] public void CargoPlacementAloneDoesNotChangeLogistics() {
  IndustrialEntityObservation[] Healthy(int h) => new[] { Producer(factory, 100, 40, 40), Producer(mine, 100, 30, 30) };
  var without = Hours(new IndustrialRuntimeSnapshot(), 6, Healthy);
  var with = Hours(new IndustrialRuntimeSnapshot(), 6, h => Healthy(h).Concat(new[] { Cargo(terminal, true), Cargo(E(300), false) }).ToArray());
  Assert.That(with.Logistics.Value, Is.EqualTo(without.Logistics.Value).Within(1e-9));
  Assert.That(with.CargoFacilities, Is.EqualTo(2)); Assert.That(with.CargoOperational, Is.EqualTo(1));
 }
 [Test] public void HealthyFlowWithExternalCargoAndNoWarehouseScores100() {
  var r = Hours(new IndustrialRuntimeSnapshot(), 6, h => new[] { Producer(factory, 100, 40, 40) });
  Assert.That(r.StorageFacilities, Is.Zero); Assert.That(r.CargoFacilities, Is.Zero);
  Assert.That(r.Logistics.Value, Is.EqualTo(100).Within(1e-9)); Assert.That(r.Logistics.Status, Is.EqualTo(IndustrialMetricStatus.Available));
 }
 [Test] public void FlowCoverageNeedsFourHoursAndContinuityFollowsAffectedTime() {
  var snapshot = new IndustrialRuntimeSnapshot();
  var early = Hours(snapshot, 3, h => new[] { Producer(factory, 100, 40, 40) });
  Assert.That(early.Logistics.Status, Is.EqualTo(IndustrialMetricStatus.WarmingUp), "3 covered hours");
  var r = Hours(new IndustrialRuntimeSnapshot(), 6, h => new[] { Producer(factory, 100, 40, 40, input: h % 2 == 0 ? IndustrialFlowState.Affected : IndustrialFlowState.Healthy) });
  Assert.That(r.InputContinuity.Value, Is.EqualTo(50).Within(1e-9));
  Assert.That(r.OutputContinuity.Value, Is.EqualTo(100).Within(1e-9));
  Assert.That(r.Logistics.Value, Is.EqualTo(75).Within(1e-9));
 }
 [Test] public void GapsLongerThanOneHourAndGenerationChangesAddNoCoverage() {
  var snapshot = new IndustrialRuntimeSnapshot();
  for (int h = 0; h < 3; h++) Run(snapshot, Info(h, h + 0.1), Producer(factory, 100, 40, 40));
  var afterGap = Run(snapshot, Info(5, 5.1), Producer(factory, 100, 40, 40));
  Assert.That(afterGap.InputObservedHours, Is.EqualTo(2).Within(1e-9), "the 2..5 gap is not bridged");
  var newGeneration = Run(snapshot, Info(6, 6.1, membership: 2), Producer(factory, 100, 40, 40));
  Assert.That(newGeneration.InputObservedHours, Is.EqualTo(2).Within(1e-9), "no interval across a structural generation change; earlier coverage kept");
 }
 [Test] public void CycleLongerThanOneGameHourIsDelayedAndAddsNoCoverage() {
  var snapshot = new IndustrialRuntimeSnapshot();
  for (int h = 0; h < 5; h++) Run(snapshot, Info(h, h + 0.1), Producer(factory, 100, 40, 40));
  double before = snapshot.InputWindow.CoveredHours(5);
  var delayed = Run(snapshot, Info(5, 6.2), Producer(factory, 100, 40, 40));
  Assert.That(delayed.Delayed, Is.True);
  Assert.That(snapshot.InputWindow.CoveredHours(5), Is.EqualTo(before), "delayed cycle supplies no coverage");
  Assert.That(snapshot.HasFlowBaseline, Is.False, "and is not a baseline");
  Assert.That(snapshot.DelayedCycles, Is.EqualTo(1), "recorded for tuning");
 }
 [Test] public void AttributableEmploymentSplitsProducerAndSupportJobs() {
  var r = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), Producer(factory, 100, 40, 30), Cargo(terminal, true), Office(office));
  Assert.That(r.ProducerJobs, Is.EqualTo(40)); Assert.That(r.ProducerEmployees, Is.EqualTo(30));
  Assert.That(r.SupportJobs, Is.EqualTo(30)); Assert.That(r.SupportEmployees, Is.EqualTo(30));
  Assert.That(r.Jobs, Is.EqualTo(70)); Assert.That(r.VacanciesByLevel[0], Is.EqualTo(10));
  Assert.That(r.Employment.HasValue, Is.True);
 }
 [Test] public void DiversityUsesOutputResourcesAndPerformanceIsFinalized() {
  var r = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), Producer(factory, 100, 40, 40, (ulong)Resource.Furniture),
   Producer(mine, 100, 40, 40, (ulong)Resource.Steel), Producer(E(210), 100, 40, 40, (ulong)Resource.Paper));
  Assert.That(r.MeaningfulOutputs, Is.EqualTo(3)); Assert.That(r.Diversity.Value, Is.EqualTo(100));
  Assert.That(r.Resources, Has.Count.EqualTo(3));
  Assert.That(r.Performance.HasValue, Is.True, "Production/Employment/Efficiency available; Logistics warming up");
  Assert.That(r.Logistics.Status, Is.EqualTo(IndustrialMetricStatus.WarmingUp));
 }
 // ---- data fidelity
 [Test] public void ProductiveActivityFollowsTheValidatedRateOnly() {
  var company = new IndustrialCompanyRead { Producer = true, HasJobs = true, Jobs = 40, Employees = 40, HasActivity = true, OutputResource = (ulong)Resource.Furniture,
   ActualPerDay = 30, CapacityPerDay = 60, Input = IndustrialFlowState.Healthy, Output = IndustrialFlowState.Healthy };
  var building = new IndustrialBuildingRead { Building = factory, Exists = true, Available = true, Traits = new IndustrialFacilityTraits { IsBuilding = true, ManufacturesGoods = true, HasProcessorCompany = true } };
  var obs = IndustrialObservationMapper.ToObservation(building, new[] { company });
  Assert.That(obs.ProductiveActivity, Is.EqualTo(50).Within(1e-9)); Assert.That(obs.IsOperatingProducer, Is.True);
  // Stock changes (deliveries, transfers, consumption) are not an input of the mapper: without the validated rate
  // there is no activity, whatever happens to inventory.
  var noRate = company; noRate.HasActivity = false;
  var withStock = building; withStock.HasStorage = true; withStock.Stored = 5000; withStock.Capacity = 6000;
  var unproven = IndustrialObservationMapper.ToObservation(withStock, new[] { noRate });
  Assert.That(unproven.ProductiveActivity, Is.Null); Assert.That(unproven.IsOperatingProducer, Is.False);
  Assert.That(unproven.Jobs, Is.EqualTo(40), "other metrics continue");
  Assert.That(typeof(IndustrialCompanyRead).GetFields().Select(f => f.Name), Has.None.Contains("Delta"));
 }
 [Test] public void UnavailableActivityForOneFacilityLeavesOtherMetricsAndFacilities() {
  var unknown = Producer(mine, 0, 20, 20); unknown.ProductiveActivity = null; unknown.IsOperatingProducer = false; unknown.Resources = Array.Empty<IndustrialResourceActivity>();
  var r = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), Producer(factory, 60, 20, 20), unknown);
  Assert.That(r.Activity.Value, Is.EqualTo(60).Within(1e-9)); Assert.That(r.Activity.Status, Is.EqualTo(IndustrialMetricStatus.Partial));
  Assert.That(r.Employment.Value, Is.EqualTo(100).Within(1e-9));
  var allUnknown = Run(new IndustrialRuntimeSnapshot(), Info(0, 0.1), unknown);
  Assert.That(allUnknown.Production.HasValue, Is.False, "never fabricated");
  Assert.That(allUnknown.Employment.HasValue, Is.True);
 }
 [Test] public void MapperCombinesTenantsOnceAndKeepsWorstFlowState() {
  var building = new IndustrialBuildingRead { Building = factory, Exists = true, Available = true, HasEfficiency = true, Efficiency = 1.25f,
   Traits = new IndustrialFacilityTraits { IsBuilding = true, ManufacturesGoods = true, HasProcessorCompany = true } };
  var a = new IndustrialCompanyRead { Producer = true, HasJobs = true, Jobs = 10, Employees = 5, HasActivity = true, ActualPerDay = 10, CapacityPerDay = 20, OutputResource = 1,
   Input = IndustrialFlowState.Healthy, Output = IndustrialFlowState.Healthy, HasProfit = true, ProfitIndex = 255, Vacancy0 = 5 };
  var b = new IndustrialCompanyRead { Producer = true, HasJobs = true, Jobs = 30, Employees = 30, HasActivity = true, ActualPerDay = 0, CapacityPerDay = 20, OutputResource = 2,
   Input = IndustrialFlowState.Affected, Output = IndustrialFlowState.NotApplicable, HasProfit = true, ProfitIndex = -255 };
  var obs = IndustrialObservationMapper.ToObservation(building, new[] { a, b });
  Assert.That(obs.Jobs, Is.EqualTo(40)); Assert.That(obs.Employees, Is.EqualTo(35)); Assert.That(obs.VacanciesByLevel![0], Is.EqualTo(5));
  Assert.That(obs.ProductiveActivity, Is.EqualTo(25).Within(1e-9), "Σ actual / Σ attainable");
  Assert.That(obs.Input, Is.EqualTo(IndustrialFlowState.Affected)); Assert.That(obs.Output, Is.EqualTo(IndustrialFlowState.Healthy));
  Assert.That(obs.Efficiency, Is.EqualTo(125).Within(1e-4), "raw >100% kept");
  Assert.That(obs.Profitability, Is.EqualTo((100.0 * 10 + 0 * 30) / 40).Within(1e-9), "company-job weighted viability");
  Assert.That(obs.Resources, Has.Length.EqualTo(2));
  var gone = IndustrialObservationMapper.ToObservation(new IndustrialBuildingRead { Building = house, Exists = false }, Array.Empty<IndustrialCompanyRead>());
  Assert.That(gone.Roles, Is.EqualTo(IndustrialFacilityRole.None)); Assert.That(gone.Jobs, Is.Null);
 }
 [Test] public void FlowRulesMirrorTheVanillaProductionConditions() {
  // Input starvation: positive potential output but too little stock for one unit (ProcessingCompanySystem).
  Assert.That(IndustrialFlowRules.Input(true, 2, 1, 1, false, 0, 0, 500), Is.EqualTo(IndustrialFlowState.Affected));
  Assert.That(IndustrialFlowRules.Input(true, 2, 1, 2, false, 0, 0, 500), Is.EqualTo(IndustrialFlowState.Healthy));
  Assert.That(IndustrialFlowRules.Input(true, 1, 2, 0, true, 1, 2, 500), Is.EqualTo(IndustrialFlowState.Affected), "fractional ratio needs > 0 stock");
  Assert.That(IndustrialFlowRules.Input(true, 1, 1, 0, false, 0, 0, 0), Is.EqualTo(IndustrialFlowState.Unavailable), "idle production alone does not prove input starvation");
  Assert.That(IndustrialFlowRules.Input(false, 0, 1, 0, false, 0, 0, 500), Is.EqualTo(IndustrialFlowState.NotApplicable), "no imported input");
  Assert.That(IndustrialFlowRules.Input(true, 1, 0, 0, false, 0, 0, 500), Is.EqualTo(IndustrialFlowState.Unavailable), "invalid process");
  // Output blockage: extractor storage full (ExtractorCompanySystem); processor weighted storage exhausted.
  Assert.That(IndustrialFlowRules.ExtractorOutput(true, 1000, 1000), Is.EqualTo(IndustrialFlowState.Affected));
  Assert.That(IndustrialFlowRules.ExtractorOutput(true, 1000, 999), Is.EqualTo(IndustrialFlowState.Healthy));
  Assert.That(IndustrialFlowRules.ProcessorOutput(true, 1000, 1000), Is.EqualTo(IndustrialFlowState.Affected));
  Assert.That(IndustrialFlowRules.ProcessorOutput(true, 1000, 400), Is.EqualTo(IndustrialFlowState.Healthy));
  Assert.That(IndustrialFlowRules.ProcessorOutput(false, 0, 400), Is.EqualTo(IndustrialFlowState.Unavailable), "no storage limit");
  Assert.That(IndustrialFlowRules.ProcessorOutput(true, 0, 0), Is.EqualTo(IndustrialFlowState.Unavailable), "invalid limit");
 }
}
