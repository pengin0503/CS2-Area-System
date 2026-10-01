using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Finance;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Industrial;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaPolicyUpkeepTests {
 private const int Park = (int)AreaTypeId.Park, Industrial = (int)AreaTypeId.Industrial;
 private const uint Day = AreaFinanceModel.FramesPerDay;
 private static readonly Entity A = new() { Index = 1, Version = 1 }, B = new() { Index = 2, Version = 1 };
 private sealed class Store : IAreaPolicyStore {
  public readonly Dictionary<Entity, AreaPolicyAreaInfo> Areas = new();
  public readonly Dictionary<Entity, List<ManagedAreaPolicyEntry>> Entries = new();
  public bool TryGetArea(Entity area, out AreaPolicyAreaInfo info) => Areas.TryGetValue(area, out info);
  public bool TryRead(Entity area, List<ManagedAreaPolicyEntry> into) {
   into.Clear();
   if (!Entries.TryGetValue(area, out var list)) return false;
   into.AddRange(list); return true;
  }
  public void Write(Entity area, IReadOnlyList<ManagedAreaPolicyEntry> entries) => Entries[area] = entries.ToList();
  public void Add(Entity area, ulong id, int type, int level, AreaLifecycleState state = AreaLifecycleState.Active, params int[] desired) {
   Areas[area] = new AreaPolicyAreaInfo(id, type, state, level, false);
   Entries[area] = desired.Select(p => new ManagedAreaPolicyEntry(p, true)).ToList();
  }
 }
 // A module that reports every policy available and supplies a fixed facility count.
 private sealed class Module : IAreaPolicyProvider, IAreaPolicyUpkeepProvider {
  public IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions { get; init; } = Array.Empty<AreaPolicyDefinition>();
  public HashSet<int> Unavailable = new();
  public int? Facilities = 4;
  public Exception Throw;
  public int InputCalls;
  public bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey) {
   reasonKey = Unavailable.Contains(policyId) ? "Test.Unavailable" : null;
   return reasonKey == null;
  }
  public bool TryGetPolicyUpkeepInputs(Entity area, int policyId, in AreaPolicyContext context, out AreaPolicyUpkeepInputs inputs) {
   InputCalls++;
   if (Throw != null) throw Throw;
   inputs = new AreaPolicyUpkeepInputs { EligibleFacilityCount = Facilities ?? 0, AreaSquareMeters = 999_999, ActivityUnits = 50 };
   return Facilities.HasValue;
  }
 }
 private static AreaPolicyDefinition Def(int id, int type, double baseUpkeep, double perHa, double perFacility, string group = null, int level = 1) =>
  new(id, "Policy.TITLE[T" + id + "]", "Policy.DESCRIPTION[T" + id + "]", "Media/Game/Policies/Test.svg", new[] { type }, level,
   new AreaPolicyUpkeep(baseUpkeep, perHa, perFacility, 0, null), group, AreaPolicyEffectClass.AreaInternal, "test." + id);
 private static (AreaPolicyRuntime Runtime, Store Store, Module Park, List<string> Errors) Create() {
  var park = new Module { PolicyDefinitions = new[] {
   Def(2001, Park, 300, 20, 25), Def(2002, Park, 400, 15, 20, "park.activity"), Def(2003, Park, 200, 10, 10, "park.activity"), Def(2004, Park, 250, 30, 0, level: 3) } };
  var errors = new List<string>();
  var runtime = AreaPolicyRuntime.Build(CommonAreaPolicies.Provider, new Dictionary<int, IAreaPolicyProvider> { [Park] = park }, errors.Add);
  return (runtime, new Store(), park, errors);
 }
 private static List<AreaPolicyUpkeepCharge> Settle(AreaPolicyUpkeepSettlement settlement, Store store, Entity area, uint frames, double squareMeters = 20_000) {
  var charges = new List<AreaPolicyUpkeepCharge>();
  settlement.Settle(store, area, frames, default, () => squareMeters, charges);
  return charges;
 }
 [Test] public void InitialCoefficientsMatchTheV1Table() {
  var expected = new Dictionary<int, (double Base, double PerHa, double PerFacility)> {
   [1001] = (250, 0, 0), [2001] = (300, 20, 25), [2002] = (400, 15, 20), [2003] = (200, 10, 10), [2004] = (250, 30, 0), [2005] = (350, 0, 20),
   [2006] = (1000, 30, 50), [3001] = (400, 0, 20), [3002] = (500, 0, 30), [3003] = (600, 0, 30), [3004] = (600, 0, 40), [3005] = (800, 0, 50),
   [3006] = (250, 0, 10), [3007] = (1200, 0, 75) };
  var all = CommonAreaPolicies.Definitions.Concat(ParkPolicyCatalog.Definitions).Concat(IndustrialPolicyCatalog.Definitions).ToList();
  Assert.That(all.Select(d => d.PolicyId).OrderBy(i => i), Is.EqualTo(expected.Keys.OrderBy(i => i)));
  foreach (var d in all) {
   var e = expected[d.PolicyId];
   Assert.That((d.Upkeep.BaseDailyUpkeep, d.Upkeep.PerHectare, d.Upkeep.PerEligibleFacility, d.Upkeep.PerActivityUnit), Is.EqualTo((e.Base, e.PerHa, e.PerFacility, 0.0)), d.PolicyId.ToString());
   Assert.That(d.Upkeep.BaseDailyUpkeep, Is.GreaterThan(0), "every active policy costs something");
  }
 }
 [Test] public void DailyUpkeepFollowsTheSpecFormula() {
  var d = Def(2001, Park, 300, 20, 25);
  Assert.That(AreaPolicyUpkeepSettlement.DailyUpkeep(d, new AreaPolicyUpkeepInputs { AreaSquareMeters = 20_000, EligibleFacilityCount = 3 }), Is.EqualTo(300 + 20 * 2 + 25 * 3));
  Assert.That(AreaPolicyUpkeepSettlement.DailyUpkeep(d, default), Is.EqualTo(300), "base upkeep is never waived");
 }
 [Test] public void AnEffectivePolicyIsChargedProportionallyToSimulationFrames() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, AreaPolicyIds.FocusedManagement);
  Assert.That(Settle(settlement, store, A, Day / 2), Is.EqualTo(new[] { new AreaPolicyUpkeepCharge(7, AreaPolicyIds.FocusedManagement, 125) }));
  Assert.That(Settle(settlement, store, A, 0), Is.Empty, "zero frames, zero charge");
 }
 [Test] public void ModuleInputsAndHectaresScaleThePolicyThatUsesThem() {
  var (runtime, store, park, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 3, AreaLifecycleState.Active, 2001);
  Assert.That(Settle(settlement, store, A, Day).Single().Amount, Is.EqualTo(300 + 20 * 2 + 25 * 4), "Geometry hectares from Core, facilities from the module");
  Assert.That(park.InputCalls, Is.EqualTo(1));
 }
 [Test] public void BasePlusHectarePoliciesNeverAskTheModule() {
  var (runtime, store, park, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 3, AreaLifecycleState.Active, 2004, AreaPolicyIds.FocusedManagement);
  var charges = Settle(settlement, store, A, Day);
  Assert.That(charges.Select(c => (c.PolicyId, c.Amount)), Is.EquivalentTo(new[] { (2004, 250L + 30 * 2), (AreaPolicyIds.FocusedManagement, 250L) }));
  Assert.That(park.InputCalls, Is.Zero);
 }
 [Test] public void MissingOrFailingModuleInputsStillChargeBaseAndHectares() {
  var (runtime, store, park, errors) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 3, AreaLifecycleState.Active, 2001);
  park.Facilities = null;
  Assert.That(Settle(settlement, store, A, Day).Single().Amount, Is.EqualTo(340));
  park.Throw = new InvalidOperationException("boom");
  Assert.That(Settle(settlement, store, A, Day).Single().Amount, Is.EqualTo(340));
  Assert.That(Settle(settlement, store, A, Day).Single().Amount, Is.EqualTo(340));
  Assert.That(errors.Count(e => e.Contains("2001")), Is.EqualTo(1), "logged once per Area + policy + failure class");
 }
 [Test] public void SuspendedAreasPayNothingAndKeepTheirSettings() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 3, AreaLifecycleState.Inactive, 2001, AreaPolicyIds.FocusedManagement);
  Assert.That(Settle(settlement, store, A, Day), Is.Empty);
  Assert.That(store.Entries[A].Count(e => e.IsDesiredEnabled), Is.EqualTo(2));
 }
 [Test] public void UnavailableLockedOrUnknownPoliciesPayNothing() {
  var (runtime, store, park, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  park.Unavailable.Add(2001);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, 2001, 2004, 9999);
  Assert.That(Settle(settlement, store, A, Day), Is.Empty, "2001 unavailable, 2004 needs Level 3, 9999 is not registered");
 }
 [Test] public void ShadowedConflictingPoliciesAreNeverBothCharged() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, 2002, 2003);
  Assert.That(Settle(settlement, store, A, Day).Select(c => c.PolicyId), Is.EqualTo(new[] { 2002 }));
 }
 [Test] public void AConflictSwapAtASettlementBoundaryChargesEachPolicyOnlyForItsOwnFrames() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, 2002);
  var first = Settle(settlement, store, A, Day / 2, squareMeters: 0);
  Assert.That(runtime.TrySetDesired(store, A, 2003, true, default, out _), Is.True);
  var second = Settle(settlement, store, A, Day / 2, squareMeters: 0);
  Assert.That(first.Select(c => (c.PolicyId, c.Amount)), Is.EqualTo(new[] { (2002, (400L + 20 * 4) / 2) }));
  Assert.That(second.Select(c => (c.PolicyId, c.Amount)), Is.EqualTo(new[] { (2003, (200L + 10 * 4) / 2) }));
 }
 [Test] public void AZeroLengthSwapBetweenSettlementsChargesOnlyThePolicyInEffect() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, 2002);
  runtime.TrySetDesired(store, A, 2003, true, default, out _);
  runtime.TrySetDesired(store, A, 2002, true, default, out _);
  Assert.That(Settle(settlement, store, A, Day, squareMeters: 0).Select(c => c.PolicyId), Is.EqualTo(new[] { 2002 }));
 }
 [Test] public void FractionalCarryIsKeptPerAreaAndPolicy() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, AreaPolicyIds.FocusedManagement);
  store.Add(B, 8, Park, 2, AreaLifecycleState.Active, AreaPolicyIds.FocusedManagement);
  long a = 0, b = 0;
  for (int i = 0; i < 1024; i++) {
   a += Settle(settlement, store, A, Day / 1024).Sum(c => c.Amount);
   if (i % 2 == 0) b += Settle(settlement, store, B, Day / 512).Sum(c => c.Amount);
  }
  Assert.That(a, Is.EqualTo(250)); Assert.That(b, Is.EqualTo(250));
 }
 [Test] public void ClearingTheSettlementDropsTheCarry() {
  var (runtime, store, _, _) = Create(); var settlement = new AreaPolicyUpkeepSettlement(runtime);
  store.Add(A, 7, Park, 2, AreaLifecycleState.Active, AreaPolicyIds.FocusedManagement);
  Assert.That(Settle(settlement, store, A, Day / 1024), Is.Empty, "0.24 carried");
  settlement.Clear();
  long total = 0;
  for (int i = 0; i < 4; i++) total += Settle(settlement, store, A, Day / 1024).Sum(c => c.Amount);
  Assert.That(total, Is.Zero, "4 × 0.244 after a reset is still below one");
 }
 [Test] public void ElapsedFramesAreBoundedSoNoTimeIsBackCharged() {
  Assert.That(AreaFinanceModel.ElapsedFrames(1000, 1256, 512), Is.EqualTo(256u));
  Assert.That(AreaFinanceModel.ElapsedFrames(1000, 900_000, 512), Is.EqualTo(512u));
  Assert.That(AreaFinanceModel.ElapsedFrames(1000, 1000, 512), Is.Zero);
  Assert.That(AreaFinanceModel.ElapsedFrames(1000, 10, 512), Is.Zero, "a frame counter that went backwards (load) charges nothing");
 }
 [Test] public void UiEstimateUsesTheSameInputs() {
  var (runtime, store, _, _) = Create();
  store.Add(A, 7, Park, 3, AreaLifecycleState.Active);
  runtime.Registry.TryGet(2001, out var d);
  Assert.That(runtime.EstimateDailyUpkeep(store, A, d!, default, () => 20_000), Is.EqualTo(300 + 40 + 100));
 }
 [Test] public void ParkSuppliesItsRelevantFacilityCountAndIndustrialItsClassifiedFacilities() {
  Assert.That(typeof(IAreaPolicyUpkeepProvider).IsAssignableFrom(typeof(ParkAreaModule)), Is.True);
  Assert.That(typeof(IAreaPolicyUpkeepProvider).IsAssignableFrom(typeof(IndustrialAreaModule)), Is.True);
  Assert.That(ParkPolicyCatalog.EligibleFacilities(new ParkFacilitySnapshot { RelevantFacilities = 6 }), Is.EqualTo(6));
  Assert.That(ParkPolicyCatalog.EligibleFacilities(null), Is.Null, "no facility scan yet: unavailable, not zero");
  var observed = new IndustrialObservationResult { Members = 12, IgnoredMembers = 2, ProducerFacilities = 7 };
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.EnvironmentalRegulation, observed), Is.EqualTo(7));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.ProductionPriority, observed), Is.EqualTo(7));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.AdvancedAutomation, observed), Is.EqualTo(7));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.EnhancedSafetyAudits, observed), Is.EqualTo(10));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.LogisticsPriority, observed), Is.EqualTo(10));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.TwentyFourHourOperations, observed), Is.EqualTo(10));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.RestrictedOperatingHours, observed), Is.EqualTo(10));
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.FocusedManagement, observed), Is.Null);
  Assert.That(IndustrialPolicyCatalog.EligibleFacilities(AreaPolicyIds.ProductionPriority, null), Is.Null);
 }
 [Test] public void TheHectareSourceIsTheVanillaAreaGeometrySurface() {
  var field = typeof(Game.Areas.Geometry).GetField("m_SurfaceArea");
  Assert.That(field?.FieldType, Is.EqualTo(typeof(float)), "square metres; Park quality and landscape already read it");
 }
}
