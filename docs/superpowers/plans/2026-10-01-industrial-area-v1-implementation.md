# Industrial Area v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Industrial Area v1 as a low-overhead management and diagnosis layer over Cities: Skylines II's real industrial simulation, with transparent Production, Logistics, Employment, Efficiency, bottleneck diagnosis and permanent progression.

**Architecture:** Keep Industrial gameplay isolated under `Modules/Industrial`. Core continues to own geometry, membership, anchors and generic selected-info UI; Industrial adds a module-local classifier, public-ECS observation adapter, derived runtime snapshots, pure score/progression models and a read-only five-tab panel. The observation adapter is the only layer allowed to know exact 1.6.2f1 industrial/company component layouts; all scoring and UI code consumes module-owned DTOs.

**Tech Stack:** C# / .NET 8 API-check tests, Cities: Skylines II 1.6.2f1 managed APIs, Unity ECS, NUnit 4, React/TypeScript UI, Vitest, existing PowerShell verification pipeline.

**Spec:** `docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md`

## Global Constraints

- Target Cities: Skylines II **1.6.2f1**.
- Industrial v1 observes the real vanilla economy; it must not create a parallel production/resource/employment simulation.
- Do not Harmony-patch or replace vanilla production, transfer, cargo, pathfinding, tax or demand systems.
- Do not write vanilla `Resources`, `BuildingEfficiency`, `Profitability`, taxes, demand or cargo routing for Industrial gameplay.
- Core membership remains building-only. Do not add company, worker, prop, path or resource entities to `AreaMembershipSystem`.
- Same-kind Industrial overlap credits each exclusive economic observation only to Core's deterministic primary Industrial membership.
- Cross-kind overlap remains independent.
- A valid Headquarters is required for Active Industrial Areas; loss of the HQ suspends the Area without resetting Level, XP or lifetime state.
- Level never demotes and does not directly buff production, logistics capacity, employee efficiency, profitability, taxes or demand.
- Industrial v1 has no budget, fees, subsidies, policies or ordinances.
- Missing optional data must be shown as unavailable, never silently converted to a misleading zero.
- If one top-level performance component is unavailable, renormalize the other three weights; if two or more are unavailable, Industrial Performance is unavailable.
- UI tabs are exactly: `Overview | Production | Logistics | Employment | Progress`.
- UI has no Industrial-specific write action in v1; only existing Core Area/HQ actions remain interactive.
- Heavy observation must be low-frequency and incremental/round-robin; do not scan all city companies every frame.
- Public 1.6.2f1 ECS components/prefab data are the supported integration surface. Uncertain field semantics must be pinned by the API-contract task before they are used.
- A raw inventory delta by itself must never be labeled or scored as production throughput.

## Review Focus

1. **Building/company ownership ambiguity:** one building may host a company and expose several roles; classification must preserve multiple capabilities while selecting one display role, and HQ eligibility must reject generic offices/residential/civic buildings.
2. **Overlap changes after area edits:** a facility moving between primary/non-primary Industrial ownership must stop contributing to the old Area and contribute once to the new primary Area without duplicate XP/statistics.
3. **Unavailable production signal:** if 1.6.2f1 cannot provide a semantically valid throughput/activity source, Production's throughput component and dependent statistics must be unavailable rather than fabricated from inventory delta.
4. **Suspend/load stale data:** suspended Areas and freshly loaded saves must not earn XP or present stale snapshots as current; derived caches must rebuild before publishing fresh metrics.
5. **Bad numeric input:** NaN, infinity, negative capacities, zero job capacity, counter overflow and out-of-range vanilla values must clamp/degrade safely without producing NaN Industrial Performance.

---

### Task 1: Industrial observation contracts, facility classification and Headquarters policy

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationContracts.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialApiContractTests.cs`

**Interfaces:**
- Produces:
  - `[Flags] public enum IndustrialFacilityRole { None=0, Extractor=1, Processor=2, Storage=4, Cargo=8, SupportingWorkplace=16 }`
  - `public struct IndustrialFacilityTraits`
  - `public struct IndustrialEntityObservation`
  - `public static IndustrialFacilityRole IndustrialFacilityClassifier.Classify(in IndustrialFacilityTraits traits)`
  - `public static bool IndustrialFacilityClassifier.IsHeadquartersEligible(in IndustrialFacilityTraits traits)`
  - `public static IndustrialFacilityTraits IndustrialFacilityReader.ReadAnchor(Entity entity, EntityManager entities)`
- Consumes: existing Core `AnchorAreaModule`, `AreaAnchorRole.Headquarters`, building membership and 1.6.2f1 public game references.

- [ ] **Step 1: Write classifier/HQ tests that define the approved roles**

Test cases must assert:
- extractor only -> `Extractor`;
- processing/industrial company -> `Processor`;
- storage -> `Storage`;
- functional cargo station -> `Cargo`;
- relevant workplace with none of the specialized roles -> `SupportingWorkplace`;
- one facility may expose multiple role flags;
- generic office-only, residential, park/attraction and unrelated civic building -> no Industrial specialized role and not HQ-eligible;
- extractor/processor/storage/cargo -> HQ-eligible;
- a modded building exposing the same supported industrial traits is treated identically to vanilla;
- unknown traits -> safe `None`.

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialFacilityClassifierTests`

Expected: FAIL because Industrial contracts/classifier do not exist.

- [ ] **Step 2: Write API-contract tests against the supplied 1.6.2f1 references**

The compile-time contract test must directly reference the public types needed by the adapter:
- `Game.Companies.IndustrialCompany`
- `Game.Companies.ProcessingCompany`
- `Game.Companies.StorageCompany`
- `Game.Companies.WorkProvider`
- `Game.Companies.Employee`
- `Game.Companies.Profitability`
- `Game.Buildings.ExtractorFacility`
- `Game.Buildings.StorageProperty`
- `Game.Buildings.CargoTransportStation`
- `Game.Buildings.BuildingEfficiency`
- `Game.Buildings.Renter`
- `Game.Economy.Resources`

The test must also pin the exact public members actually selected for:
- company/tenant resolution;
- jobs/employees;
- efficiency;
- profitability;
- storage amount/capacity or equivalent pressure;
- cargo functionality/mode when available;
- productive activity/throughput when available.

If no public member has defensible production/throughput semantics, the contract test must instead pin that throughput source as unavailable; later code must not substitute raw inventory delta.

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialApiContractTests`

Expected before implementation: FAIL to compile until exact 1.6.2f1 members are selected.

- [ ] **Step 3: Implement the module-owned observation DTOs and classifier**

`IndustrialFacilityTraits` must carry only semantic facts used by classification/HQ policy, not raw ECS objects.

`IndustrialEntityObservation` must expose:
- facility entity;
- role flags;
- operating/available state;
- optional normalized productive-activity value;
- resource-activity samples, if supported;
- optional storage utilization/input pressure/output pressure;
- cargo functional flag/mode identity when supported;
- jobs and employees;
- optional building efficiency;
- optional profitability.

Use nullable/availability fields for optional observations rather than sentinel zero.

- [ ] **Step 4: Implement `IndustrialFacilityReader.ReadAnchor` and strict HQ eligibility**

Update `IndustrialAreaModule` constructor to accept an injectable trait reader like Park:
`IndustrialAreaModule(AreaAnchorService anchors, Func<Entity, EntityManager, IndustrialFacilityTraits> readTraits = null)`.

Override `AcceptsAnchor` so only `AreaAnchorRole.Headquarters` with `IsHeadquartersEligible` passes; catch observation exceptions and reject rather than breaking Core.

- [ ] **Step 5: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialFacilityClassifierTests|IndustrialApiContractTests|AreaAnchorServiceTests"`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs tests/CS2AreaSystem.Tests/IndustrialApiContractTests.cs
git commit -m "feat(industrial): classify facilities and headquarters"
```

---

### Task 2: Authoritative Industrial state and runtime store

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaState.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialSerializationTests.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialRuntimeModelTests.cs`

**Interfaces:**
- Produces:
  - `public struct IndustrialAreaState : IComponentData, ISerializable`
  - `public sealed class IndustrialRuntimeSnapshot`
  - `public sealed class IndustrialRuntimeStore`
  - `public readonly struct IndustrialAreaRef`
  - `IndustrialRuntimeSystem.CollectAreas(List<IndustrialAreaRef>)`
  - `IndustrialRuntimeSystem.EnsureIndustrialState(List<IndustrialAreaRef>)`
  - `IndustrialRuntimeSystem.PruneStore(List<IndustrialAreaRef>)`
- Consumes: `ManagedArea`, `ManagedAreaStableId`, shared `ManagedAreaProgress`, `AreaLoadCoordinatorSystem`, Core membership index.

- [ ] **Step 1: Write serialization tests**

`IndustrialAreaState` schema v1 stores only:
- `SchemaVersion`;
- `LifetimePeakEmployees`;
- `XpDay`;
- `XpGrantedToday`;
- `HighThroughputStreakDays`;
- `HighThroughputLastDay`.

Do **not** duplicate Level/Experience; they remain in shared `ManagedAreaProgress`.

Tests must assert exact write/read order, round-trip equality, unsupported schema rejection, and no derived metric fields in persisted state.

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialSerializationTests`

Expected: FAIL.

- [ ] **Step 2: Write runtime-store tests**

Assert:
- snapshots are keyed by stable ID;
- load clears runtime snapshots;
- pruning removes deleted Industrial Areas only;
- state initialization adds `IndustrialAreaState` and `ManagedAreaProgress` but no Industrial budget/settings component;
- existing state/progress is never replaced;
- suspended Areas retain authoritative state;
- a fresh load has no publishable current metrics until observation rebuilds them.

- [ ] **Step 3: Implement state, snapshot/store and runtime host**

`IndustrialRuntimeSnapshot` must remain derived/runtime-only and include:
- stable ID;
- last successful observation frame/day;
- facility counts by role;
- active productive facility count;
- jobs/employees/vacancies;
- resource activity collection;
- storage/cargo/flow observations;
- component scores and final Performance;
- ordered bottleneck keys;
- availability/currentness flags.

- [ ] **Step 4: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialSerializationTests|IndustrialRuntimeModelTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/IndustrialAreaState.cs src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs tests/CS2AreaSystem.Tests/IndustrialSerializationTests.cs tests/CS2AreaSystem.Tests/IndustrialRuntimeModelTests.cs
git commit -m "feat(industrial): add persistent state and runtime snapshots"
```

---

### Task 3: Pure Production, Logistics, Employment, Efficiency and Performance models

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialProductionModel.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialLogisticsModel.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialEmploymentModel.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialPerformanceModel.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialPerformanceModelTests.cs`

**Interfaces:**
- Produces:
  - `public struct IndustrialMetric { public bool Available; public float Value; }`
  - `IndustrialProductionModel.Compute(in IndustrialProductionInputs) -> IndustrialMetric`
  - `IndustrialLogisticsModel.Compute(in IndustrialLogisticsInputs) -> IndustrialMetric`
  - `IndustrialEmploymentModel.Compute(in IndustrialEmploymentInputs) -> IndustrialMetric`
  - `IndustrialPerformanceModel.ComputeEfficiency(IndustrialMetric buildingEfficiency, IndustrialMetric profitability) -> IndustrialMetric`
  - `IndustrialPerformanceModel.ComputePerformance(IndustrialMetric production, IndustrialMetric logistics, IndustrialMetric employment, IndustrialMetric efficiency) -> IndustrialMetric`
  - `IndustrialPerformanceModel.FindBottlenecks(...) -> IReadOnlyList<string>`
- Consumes: normalized semantic inputs from Task 1/4; no ECS types.

- [ ] **Step 1: Write score tests with exact weights**

Pin:
- Production = productive activity **65%** + active-producer/utilization **25%** + diversity **10%**.
- Logistics = storage health **35%** + cargo access **25%** + flow continuity **25%** + breadth/resilience **15%**.
- Employment = workplace fill **75%** + staffing balance **25%**.
- Efficiency = building efficiency **70%** + profitability **30%** when both exist.
- Industrial Performance = Production **35%** + Logistics **25%** + Employment **20%** + Efficiency **20%**.

All inputs are clamped to 0–100. NaN/infinity become unavailable, not zero.

- [ ] **Step 2: Write availability and Review Focus numeric tests**

Assert:
- one missing Performance component renormalizes the remaining three original weights;
- two missing Performance components -> Performance unavailable;
- one missing Efficiency source -> the available source is 100% of Efficiency;
- both missing Efficiency sources -> Efficiency unavailable;
- zero job capacity produces Employment unavailable rather than divide-by-zero;
- negative capacities/utilizations clamp safely;
- no result is NaN or infinite.

- [ ] **Step 3: Write diversity and bottleneck tests**

Use a deterministic diversity helper:
- 0 meaningful output classes -> unavailable/0 only when productive activity itself is available and zero;
- 1 meaningful class -> 50;
- 2 -> 75;
- 3 or more -> 100.

For Lv5 prestige, a resource is "meaningful" when its rolling activity is at least **20% of the dominant output resource**.

Bottleneck keys are ordered by severity and limited to the top three from:
`WorkforceShortage`, `LowEfficiency`, `LowProfitability`, `InputStarvation`, `StorageSaturation`, `InsufficientCargo`, `LowActiveProducerShare`.

- [ ] **Step 4: Implement the pure models**

Do not read ECS state here. The models receive already-normalized semantic inputs and return availability-aware scores.

- [ ] **Step 5: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialPerformanceModelTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/Industrial*Model.cs tests/CS2AreaSystem.Tests/IndustrialPerformanceModelTests.cs
git commit -m "feat(industrial): add transparent performance scoring"
```

---

### Task 4: Low-frequency ECS observation and primary-area aggregation

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationReader.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationSystem.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Modify only if needed for reusable read-only access: `src/CS2AreaSystem/Core/Membership/AreaMembershipIndex.cs`

**Interfaces:**
- Produces:
  - `IndustrialObservationReader.TryRead(Entity building, EntityManager entities, out IndustrialEntityObservation observation)`
  - `IndustrialObservationAggregator.Apply(IndustrialRuntimeSnapshot snapshot, in IndustrialEntityObservation observation)`
  - `IndustrialObservationAggregator.Finalize(IndustrialRuntimeSnapshot snapshot)`
  - `IndustrialObservationSystem` updating approximately once per in-game hour, round-robin/bounded per update.
- Consumes:
  - Task 1 contracts/classifier;
  - Task 2 runtime store;
  - Task 3 scoring models;
  - Core `AreaMembershipIndex`.

- [ ] **Step 1: Write pure aggregation tests**

Assert:
- a building contributes only to its primary Industrial Area;
- changing primary ownership removes it from the old snapshot on the next full observation cycle and credits it once to the new one;
- cross-kind memberships do not affect Industrial ownership;
- multi-role facilities contribute to each relevant diagnostic dimension without duplicating jobs/employees;
- inactive/disabled producers do not count as active productive facilities;
- cargo placement without functional state does not receive full cargo credit.

- [ ] **Step 2: Write throughput/data-fidelity tests**

Using the exact API source selected in Task 1:
- productive activity must follow the validated public signal;
- raw inventory delta alone cannot make productive activity available;
- transfers/consumption that change inventory without validated production must not be reported as production;
- if the selected production signal is unavailable for an entity/asset, throughput remains unavailable while other metrics continue.

- [ ] **Step 3: Implement company/tenant resolution in `IndustrialObservationReader`**

Start from Core's building member. Resolve only companies/tenants attributable to that building using the 1.6.2f1 public building/company relationship selected by the API-contract test. Never broaden Core membership itself to company entities.

Aggregate a building with multiple relevant tenants once for building-level values and per-company only for company-specific observations.

- [ ] **Step 4: Implement normalized observation mapping**

Map public state into Task 1 DTOs:
- active producer/utilization;
- validated productive activity/throughput;
- resource activity;
- storage utilization/input/output pressure;
- cargo function/mode;
- jobs/employees;
- efficiency;
- profitability.

Document unit conversion/normalization next to each adapter method and pin it with focused tests. Do not expose raw game structs beyond this reader.

- [ ] **Step 5: Implement low-frequency observation scheduling**

Initial cadence: one complete Industrial observation cycle per in-game hour.

Requirements:
- no city-wide company query every frame;
- use Industrial Area membership to enumerate relevant building members;
- process Areas or members in bounded batches/round-robin across updates;
- discard/restart an Area's partial cycle if its membership/boundary revision changes before publish;
- suspended Areas do not publish a fresh operating snapshot;
- after load, no stale pre-load snapshot survives;
- UI reads only the last completed snapshot.

- [ ] **Step 6: Finalize snapshots through Task 3 models**

Finalize counts, normalized component inputs, scores and top-three bottlenecks only after a complete coherent observation cycle.

Update `LifetimePeakEmployees` only from completed Active-Area observations.

- [ ] **Step 7: Run focused + API-check tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialObservationTests|IndustrialApiContractTests|IndustrialPerformanceModelTests"`

Then:
`dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug -p:AreaApiCheck=true`

Expected: PASS, with direct compile proof against supplied 1.6.2f1 managed references.

- [ ] **Step 8: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/IndustrialObservation*.cs tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs
git commit -m "feat(industrial): observe real industrial activity"
```

---

### Task 5: Permanent Industrial progression and rate-limited XP

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialProgressionModel.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialProgressionSystem.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialProgressionModelTests.cs`

**Interfaces:**
- Produces:
  - `IndustrialProgressInputs`
  - `IndustrialPromotionTarget`
  - `IndustrialPromotionCheck`
  - `IndustrialPromotionResult`
  - `IndustrialProgressionModel.EvaluatePromotion(int level, long experience, in IndustrialProgressInputs inputs)`
  - `IndustrialProgressionModel.ComputeHourlyXp(in IndustrialProgressInputs inputs, bool active, int grantedToday)`
  - `IndustrialProgressionModel.UpdateHighThroughputStreak(...)`
  - `IndustrialProgressionSystem`
- Consumes: completed Task 4 runtime snapshots and shared `ManagedAreaProgress`.

- [ ] **Step 1: Write exact promotion-gate tests**

Pin specification thresholds:

Level 1 -> 2:
- XP 1,000;
- Performance >= 45;
- active productive facilities >= 3;
- employees >= 50.

Level 2 -> 3:
- XP 5,000;
- Performance >= 55;
- active productive facilities >= 8;
- employees >= 200;
- Logistics >= 45.

Level 3 -> 4:
- XP 15,000;
- Performance >= 65;
- active productive facilities >= 20;
- employees >= 600;
- Production >= 60;
- functional cargo infrastructure **or** Logistics >= 70.

Level 4 -> 5:
- XP 35,000;
- Performance >= 75;
- employees >= 1,500;
- Production >= 70;
- any two prestige objectives:
  - Logistics >= 80;
  - Employment >= 85;
  - Efficiency >= 80;
  - >=3 meaningful output classes;
  - productive-activity component >=80 for 7 consecutive in-game days.

- [ ] **Step 2: Write no-demotion, suspend and XP tests**

Use an hourly XP rule:
- no XP unless Active, at least one active productive facility exists, and Production/Employment/Logistics/Performance needed for the calculation are available;
- hourly award = rounded weighted operating score `(Production*0.35 + Employment*0.25 + Logistics*0.20 + Performance*0.20) / 20`, clamped **1..6**;
- daily XP cap = **120**;
- a new in-game day resets only the daily grant counter;
- suspended Areas gain zero XP;
- level never decreases;
- long XP addition saturates at `long.MaxValue`.

These are initial balancing values and can be tuned only after runtime evidence, not silently during implementation.

- [ ] **Step 3: Write 7-day throughput streak tests**

A day qualifies only when the completed normalized productive-activity component is >=80.

Rules:
- first qualifying day -> streak 1;
- consecutive day -> increment;
- skipped/nonqualifying day -> reset;
- multiple hourly observations on one day cannot increment more than once;
- load/save preserves the streak fields;
- 7+ satisfies the prestige objective.

- [ ] **Step 4: Implement pure progression model**

Keep all thresholds/constants in `IndustrialProgressionModel`. Return a checklist consumed by UI; do not embed promotion rules in the panel.

- [ ] **Step 5: Implement progression system**

At most once per completed hourly Industrial observation:
- update peak employees;
- update throughput streak on day transition;
- award capped XP;
- evaluate and apply at most one level promotion;
- do nothing for Suspended Areas except preserve state/progress.

- [ ] **Step 6: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialProgressionModelTests`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/IndustrialProgression*.cs tests/CS2AreaSystem.Tests/IndustrialProgressionModelTests.cs
git commit -m "feat(industrial): add permanent industrial progression"
```

---

### Task 6: Industrial read-only panel, localization and generated UI fixture

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialGameplayFacade.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialLocalization.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialPanelFixtureTests.cs`
- Create: `UI/src/__fixtures__/industrial-panel.json`
- Create: `UI/src/IndustrialPanel.test.tsx`

**Interfaces:**
- Produces:
  - `IndustrialPanelModel`
  - `IndustrialGameplayFacade.Read(Entity area, in AreaPanelContext context)`
  - `IndustrialPanelBuilder.Build(IndustrialPanelModel model) -> AreaPanelDocument`
  - `IndustrialLocalization.Append(Dictionary<string,string> entries, bool ja)`
- Consumes: Tasks 2–5 runtime/progression snapshots; generic `AreaPanelDocument`/React `ModulePanel`.

- [ ] **Step 1: Write panel-builder tests for the exact five tabs**

Assert tab IDs/order:
`overview, production, logistics, employment, progress`.

`ShowsProgress = true` so Core's duplicate numeric level/XP rows remain hidden.

- [ ] **Step 2: Write content/availability tests**

Overview must show:
- level/XP;
- Industrial Performance;
- four component scores;
- active productive facilities;
- employees/jobs;
- top bottlenecks;
- last successful metric update.

Production must show role counts, active producer ratio, throughput/activity with a derived/sampled label when applicable, resource breakdown when available.

Logistics must show storage/cargo/flow details and score.

Employment must show jobs/employees/vacancies/fill and mismatch when available.

Progress must show every current promotion gate and the Level-5 prestige objectives.

Unavailable data renders `CS2AS.Industrial.Unavailable`; no unavailable metric is rendered as `0`.

- [ ] **Step 3: Implement the read-only facade and panel cache**

`IndustrialGameplayFacade` has **no setters/actions**. It only assembles a selected-Area read model.

Use the same short UI cache pattern as Park (250 ms) so selected-info rendering does not rebuild the document every frame.

`IndustrialAreaModule.HandleAction` remains false for Industrial-specific actions; Core HQ/Edit/Delete actions continue normally.

- [ ] **Step 4: Implement English/Japanese localization**

Add all emitted keys and tooltips, including:
- five level names;
- four metric names/weights;
- bottleneck messages;
- Active/HQ-required/HQ-invalid summaries;
- sampled/derived throughput note;
- unavailable/currentness wording;
- "Level does not directly increase production" note.

Call `IndustrialLocalization.Append` from `NativeAreaLocalization.Entries`.

- [ ] **Step 5: Add generated C# -> React fixture contract**

Mirror Park's fixture approach:
- C# fixture generator writes representative Active, Suspended, unavailable-metric, overlapping and max-level Industrial panel states;
- without `UPDATE_UI_FIXTURES=1`, stale committed fixture fails;
- React test renders the committed fixture using the generic module panel and asserts tabs, bars/checks/warnings and unavailable labels.

Generate:
`UPDATE_UI_FIXTURES=1 dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialPanelFixtureTests`

Then run without update to prove the committed fixture matches.

- [ ] **Step 6: Run C# and UI tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialPanelBuilderTests|IndustrialPanelFixtureTests|NativeAreaLocalizationTests"`

Then:
`cd UI && npm test -- --run && cd ..`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs tests/CS2AreaSystem.Tests/IndustrialPanelFixtureTests.cs UI/src/__fixtures__/industrial-panel.json UI/src/IndustrialPanel.test.tsx
git commit -m "feat(industrial): add management panel and localization"
```

---

### Task 7: System registration, offline guards, documentation and full verification

**Files:**
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `README.md`
- Create: `docs/verification/industrial-area-v1-execution-record.md`
- Modify as required by source/build contract coverage: `scripts/test-source-guards.ps1`

**Interfaces:**
- Produces the complete registered Industrial v1 feature and its verification record.
- Consumes all previous tasks.

- [ ] **Step 1: Write registration/source-contract tests first**

Offline/source guards must fail until:
- Industrial observation/progression systems are registered;
- Industrial code does not reference forbidden vanilla mutation/replacement systems;
- no Industrial UI action setters exist;
- Core membership remains building-only;
- Industrial panel fixture exists and is generated from C#;
- the approved spec and plan paths are present.

Explicit forbidden Industrial source patterns include production/logistics system replacement or direct mutation paths for resources, taxes/demand, pathfinding/cargo routing, `BuildingEfficiency` and `Profitability`.

- [ ] **Step 2: Register Industrial systems with local failure isolation**

Add `RegisterIndustrialSystems(UpdateSystem updateSystem)` in `Mod.cs`, mirroring Park's isolation.

Register:
- the modification-phase Industrial state/observation preparation needed to react to membership changes;
- `IndustrialObservationSystem` in GameSimulation;
- `IndustrialProgressionSystem` in GameSimulation after observation publication.

A registration exception logs an Industrial-specific error and leaves Area Core/Park/Tourism available.

- [ ] **Step 3: Update README gameplay summary**

Add an Industrial Area v1 section describing:
- native polygon area creation;
- HQ requirement;
- real production/logistics/employment/efficiency observation;
- five tabs;
- five levels;
- no direct buffs/policies in v1;
- runtime validation remains outstanding until tested in game.

- [ ] **Step 4: Add the 20 runtime smoke scenarios from spec §22.4**

Copy the approved scenarios into `scripts/runtime-smoke-checklist.md`, keeping each as an unchecked runtime-only item.

Create `docs/verification/industrial-area-v1-execution-record.md` with:
- source/API verification section;
- runtime section explicitly marked **NOT RUN**;
- space for measured large-Area overhead and balance notes.

- [ ] **Step 5: Run targeted source/offline guards**

Run:
`node scripts/test-offline-contracts.mjs`

Run:
`powershell -ExecutionPolicy Bypass -File scripts/test-source-guards.ps1`

Expected: PASS.

- [ ] **Step 6: Run the complete API-check verification pipeline**

Run:
`powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -ApiCheck`

Expected:
- source verification PASS;
- source guards PASS;
- offline contracts PASS;
- all C# tests PASS;
- C# API-check build PASS;
- net48/API compatibility checks in the existing build contract PASS;
- all UI tests PASS;
- UI build PASS.

Do not claim Unity runtime correctness from this command.

- [ ] **Step 7: Update the execution record with exact verification evidence**

Record:
- commit under test;
- C# test count;
- UI test count;
- build exit codes;
- API-check outcome;
- explicit statement that in-game Industrial runtime tests are still unrun.

- [ ] **Step 8: Commit final integration/documentation**

```bash
git add src/CS2AreaSystem/Mod.cs scripts README.md docs/verification/industrial-area-v1-execution-record.md
git commit -m "feat(industrial): integrate Industrial Area v1"
```

- [ ] **Step 9: Final branch verification before PR/merge**

Re-run:
`powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -ApiCheck`

Then inspect:
`git status --short`
`git log --oneline --decorate -n 10`

Expected: verification exit 0 and clean working tree. Runtime remains explicitly unverified until the user can test in Cities: Skylines II.
