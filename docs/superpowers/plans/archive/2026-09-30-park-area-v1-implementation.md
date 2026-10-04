# CS2 Area System — Park Area v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved Park Area v1 vertical slice on top of the current Area Core: real leisure-visitor observation, Park-local landscape evaluation, six-dimensional Quality, budget/admission finance, five-level progression, multiple entrances, and a structured Park management UI inside the vanilla selected-info panel.

**Architecture:** Keep the existing Area Core authoritative for geometry, stable identity, lifecycle, building membership, overlap ownership and basic anchors. Add only three generic Core capabilities Park genuinely needs—module-specific anchor eligibility, multi-anchor presentation/actions, and a structured module-panel contract—then keep Park simulation in focused systems under `Modules/Park`. Park gameplay observes vanilla ECS state and aggregates it; it does not replace `LeisureSystem`, pathfinding, or the vanilla economy.

**Tech Stack:** C# / .NET 8 with Cities: Skylines II 1.6.2f1 managed APIs and Unity Entities 1.3.10, TypeScript + React CS2 UI mod API, NUnit, Vitest, existing `scripts/verify.ps1` verification harness.

**Spec:** `docs/superpowers/specs/2026-09-30-park-area-v1-design.md`

## Global Constraints

- Target Cities: Skylines II **1.6.2f1**.
- The current Area Core implementation on `main` is authoritative where it improves on the older Core design document.
- Visitor experience is the primary gameplay axis; finance and progression support that loop.
- Observe vanilla leisure behavior; do **not** replace or Harmony-patch `Game.Simulation.LeisureSystem`.
- Do not access the private `LeisureSystem` event queue.
- Core membership remains building-like entities only; do **not** add a global tree/prop/path membership index.
- Same-kind overlapping Parks must count exclusive visitor metrics, admissions, occupancy and XP only in Core's deterministic primary Park.
- Cross-kind overlap remains allowed and independently simulated.
- Park becomes Active with at least one valid Entrance; multiple Entrances are supported.
- Entrance anchors remain management/access anchors, not mandatory physical gates.
- Park Quality is 0–100 with initial weights: Visitor Satisfaction 25%, Attractiveness 20%, Accessibility 15%, Variety 15%, Maintenance 15%, Comfort 10%.
- Park Level is persistent and never demotes; Level must not directly manufacture physical comfortable capacity.
- Admission unlocks at Level 2 and transfers actual household money into city money; no Park currency.
- v1 does not alter vanilla destination choice based on admission price.
- v1 does not directly modify citizen happiness, land value, residential demand, or vanilla city-wide tourism attractiveness.
- v1 does not implement Park Policies/ordinances or Events/Festivals; those remain post-v1 work.
- Park-specific member-building revenue/upkeep must not be double-counted in Area finance.
- Expected-but-temporarily-unavailable Quality inputs use a conservative neutral contribution near 50 with degraded confidence; missing data must not improve Quality by renormalizing difficult metrics away.
- UI failures must remain isolated from simulation and persistence.
- Persist only authoritative Park state; active sessions, current Quality and landscape/facility caches rebuild after load.
- Citizens already visiting at load must not be charged a fresh admission until they leave and begin a genuinely new visit.
- No per-frame all-citizens × all-Parks or all-props/trees × all-Parks scans.
- Keep localization available in at least `en-US` and `ja-JP` through the existing localization path.
- Use `scripts/verify.ps1 -ApiCheck` as the final automated verification entry point.

## Review Focus

1. **Same-kind overlap:** a visitor whose current leisure building belongs to two Parks must produce one exclusive visit/admission/XP/occupancy owner only; Task 5 pins primary-ownership behavior and Task 9 smoke-tests the warning path.
2. **Load during an active paid visit:** the restored citizen must be seeded fee-satisfied and must not be charged until a later genuinely new visit; Tasks 5 and 8 add regression coverage.
3. **Unavailable metric source:** losing one expected optional analyzer must not increase Quality; Task 6 asserts neutral fallback and degraded confidence.
4. **Household with insufficient admission money:** payment must never drive money below zero, and the recorded revenue must equal the actually collected amount defined by the approved spec; Task 7 tests zero/partial collection boundaries.
5. **Park module/UI failure:** geometry, Core lifecycle, save/load and other Area types must continue; Tasks 2, 8 and 9 retain boundary/fallback tests.

---

## Planned file structure

```text
src/CS2AreaSystem/
  Mod.cs
  Core/
    Anchors/
      AreaAnchorRole.cs
      AreaAnchorService.cs
      IAreaAnchorPolicy.cs                 # split/extend current policy contract
    Modules/
      IAreaModule.cs
      IAreaPanelProvider.cs
      AreaPanelContext.cs                  # new
      AreaPanelModels.cs                   # new structured module-neutral UI DTOs
    Areas/
      AreaCoreServicesSystem.cs
    UI/
      AreaManagementUISystem.cs
      AreaManagementViewModel.cs
      NativeAreaLocalization.cs
  Modules/Park/
    ParkAreaModule.cs
    ParkAreaState.cs                       # authoritative persisted Park data
    ParkRuntimeSnapshot.cs                 # derived current metrics exposed to UI
    ParkFacilityClassifier.cs
    ParkLandscapeModel.cs
    ParkLandscapeSystem.cs
    ParkVisitModel.cs
    ParkVisitSystem.cs
    ParkQualityModel.cs
    ParkQualitySystem.cs
    ParkProgressionModel.cs
    ParkProgressionSystem.cs
    ParkFinanceModel.cs
    ParkFinanceSystem.cs
    ParkGameplayFacade.cs                  # read/write boundary used by module UI

UI/src/
  bindings.ts
  AreaInfoSection.tsx
  AreaSystem.module.scss
  ParkPanel.tsx                            # new structured Park tabs renderer
  ParkPanel.test.tsx                       # new

tests/CS2AreaSystem.Tests/
  AreaAnchorServiceTests.cs
  AreaManagementViewModelTests.cs
  AreaPanelModelTests.cs                   # new
  ParkFacilityClassifierTests.cs           # new
  ParkLandscapeModelTests.cs               # new
  ParkVisitModelTests.cs                   # new
  ParkQualityModelTests.cs                 # new
  ParkProgressionModelTests.cs             # new
  ParkFinanceModelTests.cs                 # new
  ParkSerializationTests.cs                # new
  NativeAreaLocalizationTests.cs

scripts/
  runtime-smoke-checklist.md
```

The exact number of runtime system files may be reduced if a system remains small, but pure scoring/state logic stays separate from ECS traversal so it remains unit-testable.

---

### Task 1: Add module-specific anchor eligibility and multi-Entrance Core support

**Files:**
- Create: `src/CS2AreaSystem/Core/Anchors/IAreaAnchorPolicy.cs`
- Modify: `src/CS2AreaSystem/Core/Anchors/AreaAnchorRole.cs`
- Modify: `src/CS2AreaSystem/Core/Anchors/AreaAnchorService.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Modules/AnchorAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaMemberClassifierTests.cs`

**Interfaces:**
- Consumes: existing `AreaAnchorService`, `AreaMembershipIndex`, `AreaAnchorRole.Entrance`, `AreaAnchorRole.Headquarters`.
- Produces:
  - `IAreaAnchorPolicy.RequiredAnchorRole : AreaAnchorRole?`
  - `IAreaAnchorPolicy.AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities) : bool`
  - `AreaAnchorService.GetValidAnchors(Entity area, AreaAnchorRole role) : IReadOnlyList<Entity>`
  - existing `TryGetValidAnchor(...)` remains as first-valid compatibility helper.

- [ ] **Step 1: Write failing anchor-policy tests**

Add tests proving:

```csharp
Assert.That(policy.AcceptsAnchor(area, plausibleParkBuilding, AreaAnchorRole.Entrance, entities), Is.True);
Assert.That(policy.AcceptsAnchor(area, unrelatedBuilding, AreaAnchorRole.Entrance, entities), Is.False);
Assert.That(service.GetValidAnchors(area, AreaAnchorRole.Entrance).Count, Is.EqualTo(2));
```

Also retain the existing invariant that Headquarters behaves as a single role while Entrance may have multiple links.

- [ ] **Step 2: Run the focused tests and verify failure**

Run:

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "AreaAnchorServiceTests|AreaMemberClassifierTests"
```

Expected: FAIL because `AcceptsAnchor` / `GetValidAnchors` do not exist yet.

- [ ] **Step 3: Extend the generic anchor contract**

Move the policy interface out of `AreaAnchorRole.cs` into `IAreaAnchorPolicy.cs` and add:

```csharp
bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities);
```

`AnchorAreaModule` returns `true` by default after Core generic validity/membership checks, preserving Industrial behavior.

- [ ] **Step 4: Make Park reject nonsensical Entrance buildings**

`ParkAreaModule.AcceptsAnchor(...)` accepts buildings recognizable as plausible Park/leisure/attraction access facilities using stable typed game data available in 1.6.2f1 (`Game.Buildings.Park`, `Game.Buildings.LeisureProvider`, `Game.Buildings.AttractivenessProvider`, or equivalent verified prefab-side provider data). Do not use fragile display-name matching as the primary rule.

- [ ] **Step 5: Make Core compose generic and module-specific eligibility**

`AreaCoreServicesSystem.IsEligibleAnchor(...)` must enforce in order: valid area/entity → Building → correct module/role → current Area membership → `policy.AcceptsAnchor(...)`.

- [ ] **Step 6: Add valid-anchor enumeration**

Implement `GetValidAnchors` from the persisted links using the same validity/eligibility predicate. `TryGetValidAnchor` delegates to the enumeration and returns the first valid entry.

- [ ] **Step 7: Verify and commit**

Run the focused tests, then:

```bash
git add src/CS2AreaSystem/Core/Anchors src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs src/CS2AreaSystem/Core/Modules/AnchorAreaModule.cs src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs tests/CS2AreaSystem.Tests/AreaMemberClassifierTests.cs
git commit -m "feat(core): support Park-specific multi-entrance anchors"
```

---

### Task 2: Replace the minimal module panel payload with a structured generic contract

**Files:**
- Create: `src/CS2AreaSystem/Core/Modules/AreaPanelContext.cs`
- Create: `src/CS2AreaSystem/Core/Modules/AreaPanelModels.cs`
- Modify: `src/CS2AreaSystem/Core/Modules/IAreaPanelProvider.cs`
- Modify: `src/CS2AreaSystem/Core/Modules/AnchorAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementViewModel.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- Modify: `UI/src/bindings.ts`
- Modify: `UI/src/AreaInfoSection.tsx`
- Create: `UI/src/ParkPanel.tsx`
- Create: `UI/src/ParkPanel.test.tsx`
- Test: `tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaManagementViewModelTests.cs`

**Interfaces:**
- Consumes: current stable-ID/live-selection action guard and selected-info integration.
- Produces:

```csharp
public readonly struct AreaPanelContext
{
    public World World { get; }
    public EntityManager Entities { get; }
}

public sealed class AreaPanelDocument
{
    public IReadOnlyList<AreaPanelTabData> Tabs { get; init; }
}

public sealed class AreaPanelTabData
{
    public string Id { get; init; }
    public string LabelKey { get; init; }
    public IReadOnlyList<AreaPanelItem> Items { get; init; }
}

public sealed class AreaPanelItem
{
    public string Kind { get; init; }       // row|bar|warning|slider|button|anchor-list
    public string Id { get; init; }
    public string LabelKey { get; init; }
    public string Value { get; init; }
    public double? NumericValue { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Step { get; init; }
    public bool Disabled { get; init; }
    public string TooltipKey { get; init; }
}
```

`IAreaPanelProvider` becomes context-aware:

```csharp
string BuildSummary(Entity area, in AreaPanelContext context);
AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context);
bool HandleAction(Entity area, string action, string value, in AreaPanelContext context);
```

- [ ] **Step 1: Write C# serialization/equivalence tests and UI rendering tests**

Cover `row`, `bar`, `warning`, `slider`, `button`, and `anchor-list`; assert the selected Area stable ID remains part of every mutating action path.

- [ ] **Step 2: Run C# and UI tests and verify failure**

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "AreaPanelModelTests|AreaManagementViewModelTests"
Push-Location UI; npm test -- --run ParkPanel.test.tsx; Pop-Location
```

- [ ] **Step 3: Add structured Core DTOs and context-aware provider API**

Keep DTOs module-neutral; do not add Park-named fields to `AreaManagementViewModel`.

- [ ] **Step 4: Wire structured payload into `AreaManagementUISystem`**

Pass `AreaPanelContext(World, EntityManager)` only while building/handling the selected Area. Reject action requests unless the live selected entity and stable ID still match.

Add generic action trigger:

```csharp
TriggerBinding<string, string, string>("cs2AreaSystem", "moduleAction", (stableId, action, value) => ...)
```

If the installed binding API requires a different arity, preserve the same three logical fields in one serializable request object.

- [ ] **Step 5: Render generic module items in React**

`AreaInfoSection` keeps Core type/state/Level/XP/Edit/Delete framing and delegates structured module tab content to `ParkPanel`/generic item renderer. Use existing `PanelBoundary` so a module tab render error cannot take down the vanilla panel.

- [ ] **Step 6: Verify fallback behavior**

An Industrial/Tourism stub with no structured items must still render its current summary safely.

- [ ] **Step 7: Verify and commit**

Run C# focused tests plus all UI tests, then commit:

```bash
git add src/CS2AreaSystem/Core/Modules src/CS2AreaSystem/Core/UI UI/src tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs tests/CS2AreaSystem.Tests/AreaManagementViewModelTests.cs
git commit -m "feat(ui): add structured area module panels"
```

---

### Task 3: Add Park authoritative state and pure gameplay models

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkAreaState.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkLandscapeModel.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkVisitModel.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkQualityModel.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkProgressionModel.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkFinanceModel.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkSerializationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkLandscapeModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkVisitModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkQualityModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkProgressionModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFinanceModelTests.cs`

**Interfaces:**
- Produces schema-versioned `ParkAreaState : IComponentData, ISerializable` containing authoritative values only: admission fee, lifetime visitor splits, lifetime admission revenue, lifetime management expense, rolling satisfaction aggregate state and milestone/progression flags that cannot be reconstructed.
- Produces pure static model functions used by later ECS systems; no model function may read `World`/`EntityManager`.

- [ ] **Step 1: Write state round-trip and schema tests**

Assert current schema version round-trips all authoritative fields and unsupported future schema versions are rejected through the existing `ComponentSchema` pattern.

- [ ] **Step 2: Write pure model tests from the spec**

At minimum pin:

- landscape density saturation and same-prefab diminishing returns;
- category diversity worth more than repeated same-category diversity;
- clutter penalty starts only beyond the upper useful-density range;
- small/large Park normalization remains viable;
- Satisfaction neutral prior near 50 at zero/low samples and converges with samples;
- crowding curve: `<60%` essentially no penalty, `60–100%` mild, `100–150%` meaningful, `150%+` severe but bounded;
- Quality weights exactly `25/20/15/15/15/10`;
- missing expected metric contributes neutral ~50 rather than renormalizing remaining weights upward;
- Level never changes comfortable-capacity calculation;
- promotion thresholds use the approved initial XP/Quality/visitor requirements;
- admission is locked at Level 1 and available from Level 2;
- visit XP never becomes negative.

- [ ] **Step 3: Run model tests and verify failure**

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "Park"
```

- [ ] **Step 4: Implement minimal pure models**

Expose explicit signatures such as:

```csharp
public static double ComputeLandscape(in ParkLandscapeInputs inputs);
public static double ComputeSatisfaction(in ParkVisitExperience experience, in ParkSatisfactionState prior);
public static double ComputeComfort(double occupancy, double comfortableCapacity);
public static ParkQualityResult ComputeQuality(in ParkQualityInputs inputs);
public static ParkPromotionResult EvaluatePromotion(int level, long experience, in ParkProgressInputs inputs);
public static long ComputeVisitXp(double satisfaction);
public static long ComputeCollectibleAdmission(long requestedFee, long availableHouseholdMoney);
```

Keep exact tuning constants centralized in the corresponding model file, not scattered through ECS systems/UI.

- [ ] **Step 5: Verify and commit**

```bash
git add src/CS2AreaSystem/Modules/Park tests/CS2AreaSystem.Tests/Park*Tests.cs
git commit -m "feat(park): add Park state and gameplay models"
```

---

### Task 4: Classify Park facilities and implement budgeted landscape analysis

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkFacilityClassifier.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkLandscapeSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFacilityClassifierTests.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkLandscapeModelTests.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`

**Interfaces:**
- Consumes: Core `AreaMembershipIndex.GetMembers(ulong areaStableId)` for building members and Park geometry/search APIs only for Park-local vegetation/prop/path analysis.
- Produces `ParkFacilitySnapshot` and `ParkLandscapeSnapshot` keyed by Park stable ID.

- [ ] **Step 1: Write classifier tests**

Prove recognized Park/leisure/attraction providers contribute roles and unrelated residential/industrial/office members contribute no Park Quality bonus solely because they lie inside the boundary.

- [ ] **Step 2: Write landscape scheduling tests around a pure scheduler helper**

Assert geometry/object dirty marks queue a Park, only the configured work budget is consumed per update, and periodic fallback refresh does not scan every Park every frame.

- [ ] **Step 3: Implement facility classification**

Prefer typed component/prefab data including `Game.Buildings.Park`, `Game.Buildings.LeisureProvider`, `Game.Prefabs.LeisureProviderData`, and `Game.Buildings.AttractivenessProvider`. Use stable `LeisureType` categories for facility diversity. Do not use arbitrary prefab-name heuristics as the main classifier.

- [ ] **Step 4: Implement Park-local landscape scanning**

Collect the approved categories: Vegetation, Rest & Amenities, Decoration, Lighting/Utility and pedestrian Paths. Curated vanilla mappings are allowed only where typed semantics are unavailable; unknown props receive at most conservative density contribution and no large semantic diversity bonus.

- [ ] **Step 5: Feed snapshots into pure landscape model**

Use sub-linear area normalization, diminishing returns, diversity bonus and modest excess/clutter penalty. Do not persist the snapshot as authoritative state.

- [ ] **Step 6: Register the system at a bounded simulation cadence**

Use a low-frequency/dirty-driven update phase appropriate for simulation reads; do not run landscape work in UIUpdate.

- [ ] **Step 7: Verify and commit**

Run Park facility/landscape tests and `dotnet build ... -p:AreaApiCheck=true`, then commit.

---

### Task 5: Observe real leisure visits and maintain runtime ParkVisitSessions

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkVisitSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkVisitModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaState.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkVisitModelTests.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkSerializationTests.cs`

**Interfaces:**
- Consumes: vanilla `Citizen`, `Leisure`, `CurrentBuilding`, `TravelPurpose`, household relationships, `TouristHousehold`, `AreaMembershipIndex.GetAreas(building)` and facility relevance from Task 4.
- Produces runtime-only `ParkVisitSession` records plus aggregate active/recent/lifetime visitor counts and resident/tourist satisfaction sample streams.

- [ ] **Step 1: Add failing visit-state tests**

Pin these transitions:

```text
relevant leisure building -> start session
same primary Park building transition -> continue same session
brief leave + same Park within grace -> continue
leave beyond grace -> complete exactly one visit
unrelated worker/resident presence -> no visit
same-kind overlap -> primary Park only
```

- [ ] **Step 2: Add tourist/resident split tests**

A citizen whose household has `TouristHousehold` increments tourist aggregates; otherwise resident aggregates.

- [ ] **Step 3: Add load-seeding regression test**

A pre-existing active visitor reconstructed after load is marked `FeeSatisfied=true` and cannot generate an admission transaction until the seeded session ends.

- [ ] **Step 4: Implement staggered visit observation**

Follow the source simulation cadence rather than rendered frames. The hot path is:

```text
eligible citizen -> CurrentBuilding -> AreaMembershipIndex.GetAreas(building) -> primary Park
```

Never perform citizen × Park polygon tests in the visitor hot path.

- [ ] **Step 5: Complete sessions into aggregates**

On completion, write lifetime resident/tourist counts and rolling satisfaction state into authoritative `ParkAreaState`; keep active session entities/dictionaries runtime-only.

- [ ] **Step 6: Verify no duplicate same-kind accounting**

Tests must assert one visit, one occupancy owner, one future admission owner and one XP owner for overlapping Parks.

- [ ] **Step 7: Verify and commit**

Run `ParkVisitModelTests`, serialization tests and API-check build; commit as `feat(park): track real Park leisure visits`.

---

### Task 6: Compute Accessibility, Attractiveness, Variety, Maintenance, Comfort and overall Quality

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkQualitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualityModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkQualityModelTests.cs`

**Interfaces:**
- Consumes: Entrance list from Task 1, facility/landscape snapshots from Task 4, visitor/satisfaction/occupancy snapshot from Task 5, `ManagedAreaSettings.BudgetMultiplier`, vanilla Park maintenance where available.
- Produces `ParkQualitySnapshot` with six metric values, combined Quality, confidence/availability markers, comfortable capacity, occupancy ratio, warnings and derived `TourismContribution`.

- [ ] **Step 1: Add failing component-metric tests**

Pin initial composition rules:

- Accessibility approximately 40% valid baseline entrance, 20% additional/distributed entrances, 20% nearby transit, 20% internal pedestrian provision;
- Attractiveness approximately 60% facility attractiveness + 40% landscape;
- Variety approximately 70% facility/leisure-category diversity + 30% landscape-category diversity;
- Comfort uses occupancy/comfortable-capacity curve and does not receive a direct Level multiplier;
- Maintenance uses real Park-building maintenance where present plus Area management coverage.

- [ ] **Step 2: Add missing-source regression test**

Mark Landscape unavailable and assert overall Quality uses a neutral ~50 contribution with degraded confidence; assert Quality does not increase merely because the metric disappeared.

- [ ] **Step 3: Implement cheap Accessibility inputs**

Use Entrance validity/count/distribution, nearby transit proximity and Park-local path provision. Do not issue repeated full pathfind requests per Park.

- [ ] **Step 4: Implement Quality recomputation at lower frequency/dirty-driven cadence**

No per-frame UI-driven scoring. Suspended Parks retain useful last/current diagnostics but do not generate normal active gameplay rewards.

- [ ] **Step 5: Compute future Tourism contribution without mutating vanilla tourism**

Use Level, Quality, tourist volume, tourist satisfaction and attractiveness only as a derived exported metric.

- [ ] **Step 6: Verify and commit**

Run Park Quality tests and API-check build; commit `feat(park): compute Park quality and tourism contribution`.

---

### Task 7: Implement Park budget, management expense and Level-2 admission transactions

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkFinanceSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFinanceModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaState.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkFinanceModelTests.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkVisitModelTests.cs`

**Interfaces:**
- Consumes: `ManagedAreaSettings.BudgetMultiplier`, active visit-start events from Task 5, Park Level, household `Resource.Money`, city `PlayerMoney`, Park management need inputs.
- Produces recent/lifetime admission income, recent/lifetime Area management expense and net Park Area cash effect.

- [ ] **Step 1: Write failing finance rules**

Assert:

- Level 1 fee control is unavailable and no fee is collected;
- Level 2+ permits the configured non-negative fee;
- one `ParkVisitSession` is charged at most once;
- free Park charges zero;
- requested fee never drives household money below zero;
- collected city revenue exactly equals the household deduction;
- insufficient-funds behavior matches `ComputeCollectibleAdmission` from Task 3;
- member-building vanilla revenue/upkeep is not included in Park Area transaction totals.

- [ ] **Step 2: Write management-cost tests**

Budget range starts with the approved approximately 50–150% service-style target. Higher budget increases Area management expense and management coverage with diminishing benefit; it must not directly increase unrelated Quality dimensions.

- [ ] **Step 3: Implement low-frequency management settlement**

Subtract Park Area administrative/grounds-management overhead from city money at a simulation-time interval; never re-charge member-building vanilla upkeep.

- [ ] **Step 4: Implement admission settlement at first genuine visit of a session**

Move actual money household → city and mark the session attempted/satisfied so an error or frame repeat cannot double-charge.

- [ ] **Step 5: Keep price out of vanilla destination selection**

Apply admission price only to real payment, finance statistics and Value-for-money satisfaction inputs. Do not alter `LeisureSystem` search/pathfinding.

- [ ] **Step 6: Verify and commit**

Run Park Finance + Visit tests and API-check build; commit `feat(park): add Park budget and admission finance`.

---

### Task 8: Implement XP, promotions, lifecycle hooks and complete Park persistence/load recovery

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkProgressionSystem.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkProgressionModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaState.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkProgressionModelTests.cs`
- Extend: `tests/CS2AreaSystem.Tests/ParkSerializationTests.cs`

**Interfaces:**
- Consumes: completed-visit aggregates, Satisfaction, Quality, Maintenance, Landscape, Variety, tourist lifetime count, lifecycle Active/Suspended state.
- Produces updates to shared `ManagedAreaProgress.Level/Experience`, promotion checklist state and a `ParkGameplayFacade` used by `ParkAreaModule.BuildPanel/HandleAction`.

- [ ] **Step 1: Write promotion tests with approved initial targets**

Assert:

```text
Lv1 -> Lv2: XP 1,000; Quality >= 40; 250 lifetime visitors
Lv2 -> Lv3: XP 4,000; Quality >= 55; 1,500 visitors; Variety >= 45; Landscape >= 40
Lv3 -> Lv4: XP 12,000; Quality >= 70; 7,500 visitors; recent Satisfaction >= 65; Maintenance >= 60
Lv4 -> Lv5: XP 30,000; Quality >= 82; 25,000 visitors; any 2 prestige objectives
Prestige objectives: Landscape >= 80; Variety >= 75; tourist lifetime visitors >= 5,000
```

Promotion Quality threshold must hold for the configured short rolling period (initial target roughly one in-game day); one-tick spikes do not promote.

- [ ] **Step 2: Write no-demotion and suspended-state tests**

Once earned, Level never falls. Suspended Park gains no normal operational XP/admissions but retains progression and lifetime stats.

- [ ] **Step 3: Implement visit XP and capped active Quality-maintenance XP**

Visit XP is satisfaction-adjusted and never negative. Passive Quality XP requires genuine recent visitor activity and is capped so fast-forward waiting cannot dominate progression.

- [ ] **Step 4: Ensure Park authoritative state exists on Park Areas**

Create/add `ParkAreaState` during Park module creation/activation workflow without adding it to Industrial/Tourism Areas. Preserve existing state on suspend/reactivate.

- [ ] **Step 5: Implement load recovery**

After Core load/membership readiness: validate Entrances → rebuild facility snapshot → queue landscape scan → seed current leisure visitors as fee-satisfied → recompute Quality when inputs are ready → resume finance/progression.

- [ ] **Step 6: Add `ParkGameplayFacade`**

Expose only selected-Park read snapshots and validated setters/actions needed by UI: budget, admission fee, entrance list/add/remove request, and current structured panel model inputs.

- [ ] **Step 7: Verify and commit**

Run all Park C# tests and API-check build; commit `feat(park): add Park progression and load recovery`.

---

### Task 9: Deliver the complete Park management UI, localization, warnings and runtime verification

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementViewModel.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Modify: `UI/src/bindings.ts`
- Modify: `UI/src/ParkPanel.tsx`
- Modify: `UI/src/AreaInfoSection.tsx`
- Modify: `UI/src/AreaSystem.module.scss`
- Extend: `UI/src/ParkPanel.test.tsx`
- Extend: `UI/src/AreaInfoSection.test.tsx`
- Extend: `tests/CS2AreaSystem.Tests/NativeAreaLocalizationTests.cs`
- Modify: `scripts/runtime-smoke-checklist.md`

**Interfaces:**
- Consumes: `ParkGameplayFacade`, generic structured panel contract, live-selection/stable-ID action guard.
- Produces five Park tabs: Overview, Visitors, Finances, Landscape, Progress.

- [ ] **Step 1: Write UI tests for all five tabs**

Pin required user-visible content:

- Overview: lifecycle, Level/XP, Quality, six metric bars, active/recent visitors, entrance count, warnings/next action;
- Visitors: active/recent/lifetime, resident/tourist split, combined/resident/tourist satisfaction, comfortable capacity, occupancy/crowding, peak where provided;
- Finances: budget slider, Level-2+ admission slider, recent/lifetime admission income, management expense, net Area cash effect, explicit note that vanilla building upkeep/revenue is separate;
- Landscape: Landscape Score and category contributions/diversity/clutter warning;
- Progress: Level, XP progress, next thresholds, promotion checklist, Lv5 prestige objectives.

- [ ] **Step 2: Add multi-Entrance management UI tests**

Render all valid Entrances with add/remove actions. Removal sends an opaque current-link key and C# resolves it only against the selected Park's current anchor links; stale/arbitrary entity references are rejected.

- [ ] **Step 3: Add overlap-owner and degraded-confidence warnings**

When the selected Park is not primary for same-kind overlapping exclusive statistics, show an explanatory warning. When a Quality source is degraded/unavailable, show low-confidence/unavailable state rather than silently presenting a stronger score.

- [ ] **Step 4: Implement Park panel document generation**

`ParkAreaModule.BuildPanel(...)` emits the five tabs through the generic contract. `HandleAction(...)` accepts only the approved setters/actions and delegates to `ParkGameplayFacade` after the Core live-selection guard.

- [ ] **Step 5: Add en-US and ja-JP localization**

Cover tab names, all six metrics, financial labels/tooltips, landscape categories, warnings, promotion terms, entrance actions, confidence/unavailable text and Local/Neighborhood/City/Major/Landmark Park level names.

- [ ] **Step 6: Update runtime smoke checklist**

Add the 20 runtime scenarios from the approved spec, including lifecycle/Entrance loss, real visitors vs unrelated presence, tourist split, overlap ownership, Level-2 paid visit money transfer, higher-price Value penalty without destination-choice change, budget/Maintenance behavior, save/load no duplicate ticket, landscape anti-spam, and large-city profiling.

- [ ] **Step 7: Run complete automated verification**

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -ApiCheck
```

Expected: source guards, offline contracts, NUnit tests, mod build, Vitest tests and UI build all exit 0.

- [ ] **Step 8: Review runtime-only items without fabricating results**

Mark smoke-checklist items that require the actual game as **pending runtime verification** unless they were genuinely executed in Cities: Skylines II 1.6.2f1. Do not claim runtime success from unit/API-check builds alone.

- [ ] **Step 9: Commit**

```bash
git add src/CS2AreaSystem/Modules/Park src/CS2AreaSystem/Core/UI UI/src tests/CS2AreaSystem.Tests scripts/runtime-smoke-checklist.md
git commit -m "feat(park): complete Park Area v1 gameplay"
```

---

## Final implementation verification

After Task 9, run from a clean working tree:

```powershell
git status --short
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -ApiCheck
```

Expected before integration review:

- working tree contains only intentional plan/progress artifacts, preferably clean;
- all automated checks exit 0;
- Park does not introduce a Harmony dependency;
- no code path expands Core global membership to trees/props/paths;
- no Park code modifies vanilla destination selection, citizen happiness, land value, residential demand or city-wide tourism attractiveness;
- `Park Policies` / ordinances and Events remain absent from v1 implementation;
- runtime-only smoke items are clearly separated from automated evidence.

Then use `superpowers:verification-before-completion` before any claim that Park v1 is complete, followed by `superpowers:requesting-code-review` for the whole implementation branch before merge/integration.
