# Industrial Area v1 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans in Native mode. The user selected Opus 5.5 as the implementer; implement tasks inline without Task implementation subagents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Industrial Area v1 as a low-overhead management and diagnosis layer over Cities: Skylines II's real industrial simulation, with transparent Production, Logistics, Employment, Efficiency, bottleneck diagnosis and permanent progression.

**Architecture:** Keep Industrial gameplay isolated under `Modules/Industrial`. Core continues to own geometry, membership, anchors and generic selected-info UI; Industrial adds a module-local classifier, public-ECS observation adapter, derived runtime snapshots, pure score/progression models and a read-only five-tab panel. The observation adapter is the only layer allowed to know exact 1.6.2f1 industrial/company component layouts; all scoring and UI code consumes module-owned DTOs.

**Tech Stack:** C# / .NET 8 API-check tests, Cities: Skylines II 1.6.2f1 managed APIs, Unity ECS, NUnit 4, React/TypeScript UI, Vitest, existing PowerShell verification pipeline.

**Spec:** `docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md`

**Revision:** User-approved eight-point refinement, 2026-10-01. This revision supersedes the original XP, logistics, streak, SupportingWorkplace, global-revision and continued-HQ-policy decisions. Product code is not changed by this documentation revision.

## Global Constraints

- Target Cities: Skylines II **1.6.2f1**.
- Industrial v1 observes the real vanilla economy; it must not create a parallel production/resource/employment simulation.
- Do not Harmony-patch or replace vanilla production, transfer, cargo, pathfinding, tax or demand systems.
- Do not write vanilla `Resources`, `BuildingEfficiency`, `Profitability`, taxes, demand or cargo routing for Industrial gameplay.
- Core membership remains building-only. Do not add company, worker, prop, path or resource entities to `AreaMembershipSystem`.
- Same-kind Industrial overlap credits each exclusive economic observation only to Core's deterministic primary Industrial membership.
- Cross-kind overlap remains independent.
- Initial HQ assignment requires a supported industrial building. An accepted, existing valid HQ inside the boundary keeps the Area Active regardless of vacancy/operating health; physical loss/out-of-boundary suspends without resetting state. No HQ-health debuff in v1.
- Level never demotes and does not directly buff production, logistics capacity, employee efficiency, profitability, taxes or demand.
- Industrial v1 has no budget, fees, subsidies, policies or ordinances.
- Missing optional data must be shown as unavailable, never silently converted to a misleading zero.
- If one top-level performance component is unavailable, renormalize the other three weights; if two or more are unavailable, Industrial Performance is unavailable.
- UI tabs are exactly: `Overview | Production | Logistics | Employment | Progress`.
- UI has no Industrial-specific write action in v1; only existing Core Area/HQ actions remain interactive.
- Heavy observation is hourly and bounded/round-robin, initially 128 building reads per simulation update in total; never scan all city companies every frame.
- Restart partial work only for that Area's relevant structural generation, never the global membership revision.
- Daily XP cap is 240 game-day XP, hourly maximum 10, one missing XP input is reweighted and two missing inputs grant zero.
- No Area-size XP multiplier. Existing facility/employee gates represent scale.
- Streak qualification uses completed-day time-weighted activity >=80 with >=18 valid observed game-hours; no sample-count shortcut.
- Ordinary office-only jobs are excluded. Warehouse/cargo presence yields no direct Logistics points.
- Persist only the minimal day/XP accumulators needed for deduplication and streak history; keep current/rolling score caches runtime-only.
- Public 1.6.2f1 ECS components/prefab data are the supported integration surface. Uncertain field semantics must be pinned by the API-contract task before they are used.
- A raw inventory delta by itself must never be labeled or scored as production throughput.

## Review Focus

1. **HQ tenant/health changes:** accepted HQ vacancy/low health keeps Area Active; assignment filtering, physical removal and Park entrance behavior retain their own rules (Task 1).
2. **Partial data:** one missing Performance/XP input reweights; two missing XP inputs give a visible zero-XP reason; no source invents throughput or healthy flow from inventory/cargo counts (Tasks 3–5).
3. **Temporal history:** 17.99-hour coverage and a brief spike fail; midnight intervals split; pause/retry/same-hour reload cannot duplicate XP or covered time; a skipped day breaks the streak (Tasks 2, 5).
4. **Unrelated structural change:** another Area/Park edit does not restart this Area; local ownership transfer invalidates both affected Industrial Areas; generation mixing never publishes (Task 4).
5. **Scale and attribution:** healthy small/large/infrastructure-free districts retain matching normalized scores; generic offices and multi-role/multi-tenant duplicates do not inflate jobs, flow or XP (Tasks 1, 3, 4).

---

### Task 1: Industrial observation contracts, facility classification and Headquarters policy

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationContracts.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialApiContractTests.cs`
- Modify: `src/CS2AreaSystem/Core/Anchors/AreaAnchorService.cs`
- Modify: `src/CS2AreaSystem/Core/Anchors/IAreaAnchorPolicy.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs`
- Modify: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Create: `docs/verification/industrial-observation-contracts.md`

**Interfaces:**
- Produces:
  - `[Flags] public enum IndustrialFacilityRole { None=0, Extractor=1, Processor=2, Storage=4, Cargo=8, SupportingWorkplace=16 }`
  - `public struct IndustrialFacilityTraits`
  - `public struct IndustrialEntityObservation`
  - `public static IndustrialFacilityRole IndustrialFacilityClassifier.Classify(in IndustrialFacilityTraits traits)`
  - `public static bool IndustrialFacilityClassifier.IsHeadquartersEligible(in IndustrialFacilityTraits traits)`
  - `public static IndustrialFacilityTraits IndustrialFacilityReader.ReadAnchor(Entity entity, EntityManager entities)`
  - Optional Core `IAreaExistingAnchorPolicy.AcceptsExistingAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities)`; only Industrial opts in.
  - `AreaAnchorService(IAreaAnchorStore store, Func<Entity,bool> valid, Func<Entity,Entity,AreaAnchorRole,bool> eligible, Func<Entity,Entity,AreaAnchorRole,bool> existingEligible = null)`; existing predicate defaults to assignment predicate for backward compatibility.
- Consumes: existing Core `AnchorAreaModule`, `AreaAnchorRole.Headquarters`, building membership and 1.6.2f1 public game references.

- [ ] **Step 1: Write classifier/HQ tests that define the approved roles**

Test cases must assert:
- extractor only -> `Extractor`;
- processing/industrial company -> `Processor`;
- storage -> `Storage`;
- recognizable cargo-capable facility, working or disabled -> `Cargo`; operational status is a separate observation;
- supporting employment inside an eligible industrial/cargo/storage facility -> descriptive `SupportingWorkplace`; an ordinary office-only workplace -> `None`;
- temporary tenant vacancy/disabled operating health does not reject an industrial building with proven eligible static capability;
- one facility may expose multiple role flags;
- generic office-only, residential, park/attraction and unrelated civic building -> no Industrial specialized role and not HQ-eligible;
- extractor/processor/storage/cargo -> HQ-eligible;
- a modded building exposing the same supported industrial traits is treated identically to vanilla;
- unknown traits -> safe `None`.

Add assignment-versus-existing-link tests to AreaAnchorServiceTests: stricter initial eligibility rejects ordinary offices; after successful assignment, vacancy/poor profitability retains the valid HQ, while deletion/out-of-boundary invalidates it. Legacy callers and Park continue using their original predicate for both assignment and validation.

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

If no public member has defensible productive-activity/throughput semantics, pin that source as unavailable; never substitute inventory delta.

Write `industrial-observation-contracts.md` with source type/member, consumer/system evidence, unit/normalizer, entity attribution, clock conversion and availability decision for each metric. Compare the supplied vanilla UI calculation where relevant. A compile-time type test proves API shape only; it cannot prove production, efficiency or profitability semantics. Keep uncertain sources unavailable pending runtime validation.

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

Override `AcceptsAnchor` for initial HQ assignment only; catch trait-reading failures and reject safely.

Implement `IAreaExistingAnchorPolicy` on Industrial. After Core checks current area, building validity, role and membership, `AcceptsExistingAnchor` returns true for an accepted HQ link without consulting current tenant/health traits. Wire separate assignment/existing predicates through AreaCoreServicesSystem and AreaAnchorService. Assign uses `eligible`; GetValidAnchors/GetAnchorLinks use `existingEligible`. Modules not opting in, especially Park, retain the original strict behavior. HQ health is an observation, with no v1 debuff.

- [ ] **Step 5: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialFacilityClassifierTests|IndustrialApiContractTests|AreaAnchorServiceTests"`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial src/CS2AreaSystem/Core/Anchors src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs tests/CS2AreaSystem.Tests/IndustrialApiContractTests.cs tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs docs/verification/industrial-observation-contracts.md
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
  - `IndustrialRuntimeSystem.OnUpdate()` performs collect/prune/state initialization in ModificationEnd only; it does not perform economic observation.
- Consumes: `ManagedArea`, `ManagedAreaStableId`, shared `ManagedAreaProgress`, `AreaLoadCoordinatorSystem`, Core membership index.

- [ ] **Step 1: Write serialization tests**

`IndustrialAreaState` schema v1 stores only:
- `SchemaVersion`;
- `LifetimePeakEmployees`;
- `XpDay`;
- `XpGrantedToday`;
- `LastSettledXpHour` (long, -1 initially);
- `ActivityDay`;
- `ActivityWeightedSum` (double);
- `ActivityObservedHours` (double);
- `LastCreditedActivityHour` (long, -1 initially);
- `HighThroughputStreakDays`;
- `HighThroughputLastDay`.

`CreateDefault(uint currentDay)` initializes XpDay/ActivityDay to current game day, last-hour fields to -1 and HighThroughputLastDay to uint.MaxValue. Zero observed hours means no coverage. Use the exact field order above. Repeated reads/saves never reset a day accumulator.

Do **not** duplicate Level/Experience; they remain in shared `ManagedAreaProgress`.

Tests assert exact field order/round trip, schema rejection, preservation of partial-day weighted sums/coverage/hour deduplication, and absence of current scores/raw-window caches in saved state. Day accumulators are authoritative history, not derived current metrics.

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
- ordered bottleneck records (key/severity/evidence/affected count/observed-hours/suggestion);
- per-metric availability reason and observed/eligible coverage;
- observation cycle ID, game-hour bucket, start/end simulation times and Area generation tokens;
- runtime interpolation baseline and rolling six-hour flow intervals (never serialized).

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
- Create: `tests/CS2AreaSystem.Tests/IndustrialScoreExampleTests.cs`
- Create: `docs/verification/industrial-score-examples.json`
- Create: `docs/verification/industrial-score-examples.md`

**Interfaces:**
- Produces:
  - `IndustrialMetric` exposes `Available`, normalized `Value`, `IndustrialMetricStatus` (Available/Partial/Unavailable/NotApplicable/WarmingUp/Stale), and observed/eligible coverage; only Available/Partial enter scoring.
  - `IndustrialDailyActivityObservation` exposes hour bucket, start/end game time, validated normalized activity, Active/currentness and Area generation.
  - `IndustrialProductionModel.Compute(in IndustrialProductionInputs) -> IndustrialMetric`
  - `IndustrialLogisticsModel.Compute(in IndustrialLogisticsInputs) -> IndustrialMetric`
  - `IndustrialEmploymentModel.Compute(in IndustrialEmploymentInputs) -> IndustrialMetric`
  - `IndustrialPerformanceModel.ComputeEfficiency(IndustrialMetric buildingEfficiency, IndustrialMetric profitability) -> IndustrialMetric`
  - `IndustrialPerformanceModel.ComputePerformance(IndustrialMetric production, IndustrialMetric logistics, IndustrialMetric employment, IndustrialMetric efficiency) -> IndustrialMetric`
  - `IndustrialPerformanceModel.FindBottlenecks(in IndustrialBottleneckInputs inputs) -> IReadOnlyList<IndustrialBottleneck>`; each item has key, normalized severity, affected-facility count, observed-hours and evidence/suggestion keys.
- Consumes: normalized semantic inputs from Task 1/4; no ECS types.

- [ ] **Step 1: Write score tests with exact weights**

Pin:
- Production = productive activity **65%** + active share **25%** + diversity **10%**. Validated activity is required; renormalize missing optional active-share/diversity inputs.
- Logistics = observed input continuity **50%** + observed output/dispatch continuity **50%**; no points for storage/cargo counts or breadth.
- Employment = aggregate workplace fill **75%** + arithmetic mean of eligible per-facility fill percentages **25%**.
- Efficiency = building efficiency **70%** + profitability **30%** when both exist.
- Industrial Performance = Production **35%** + Logistics **25%** + Employment **20%** + Efficiency **20%**.

Use spec §12.1 source normalization before score clamping. Preserve raw >100% vanilla efficiency in UI. NaN/infinity/invalid denominators become unavailable. Per-company efficiency/profitability and per-producer flow use weight clamp(validJobs,1,100), with partial coverage flagged when jobs are unknown.

- [ ] **Step 2: Write availability and Review Focus numeric tests**

Assert:
- one missing Performance component renormalizes the remaining three original weights;
- two missing Performance components -> Performance unavailable;
- one missing Efficiency source -> the available source is 100% of Efficiency;
- both missing Efficiency sources -> Efficiency unavailable;
- zero valid job capacity produces Employment NotApplicable with Available=false, rather than divide-by-zero;
- negative capacities are invalid/unavailable, not silently converted to healthy zero-capacity inputs;
- one unavailable or NotApplicable Logistics channel reweights the available one; no available applicable channel -> unavailable;
- Logistics requires >=4 observed game-hours per channel in its rolling six-hour window; no symptoms with no coverage is WarmingUp/Unavailable, never 100;
- ordinary offices are excluded; multi-role jobs count once;
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

Each warning contains evidence, affected-facility count, observation period and a concise suggestion. No in-boundary cargo terminal is not sufficient evidence for InsufficientCargo.

- [ ] **Step 4: Add executable calculation examples before implementing models**

Create the JSON/Markdown examples from spec §12.2. `IndustrialScoreExampleTests` reads the committed JSON and calls the real pure models, asserting scores with tolerance 1e-5 and exact rounded XP. Assert healthy small/large equivalence, Logistics 100 without dedicated storage/cargo, Logistics 62.5 for 50%/25% blocked shares, a confirmed stopped district, and one/two missing inputs. These are semantic-input fixtures; explicitly label them synthetic.

Expected initially: FAIL because models are absent. The XP assertions are completed in Task 5 rather than introducing a second XP implementation.

- [ ] **Step 5: Implement the pure models**

Do not read ECS state here. The models receive already-normalized semantic inputs and return availability-aware scores.

- [ ] **Step 6: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialPerformanceModelTests|IndustrialScoreExampleTests"`

At Task 3 validate score assertions; Task 5 adds/validates XP assertions against IndustrialProgressionModel.

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/Industrial*Model.cs tests/CS2AreaSystem.Tests/IndustrialPerformanceModelTests.cs tests/CS2AreaSystem.Tests/IndustrialScoreExampleTests.cs docs/verification/industrial-score-examples.json docs/verification/industrial-score-examples.md
git commit -m "feat(industrial): add transparent performance scoring"
```

---

### Task 4: Low-frequency ECS observation and primary-area aggregation

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationReader.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationSystem.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Modify: `src/CS2AreaSystem/Core/Membership/AreaMembershipIndex.cs`
- Modify: `tests/CS2AreaSystem.Tests/AreaMembershipPolicyTests.cs`

**Interfaces:**
- Produces:
  - `IndustrialObservationReader.TryRead(Entity building, EntityManager entities, out IndustrialEntityObservation observation)`
  - `IndustrialObservationAggregator.Apply(IndustrialRuntimeSnapshot snapshot, in IndustrialEntityObservation observation)`
  - `IndustrialObservationAggregator.Finalize(IndustrialRuntimeSnapshot snapshot)`
  - `IndustrialObservationSystem` updating approximately once per in-game hour, round-robin/bounded per update.
  - `AreaMembershipIndex.GetAreaRevision(ulong areaStableId) -> ulong` as a module-neutral per-Area membership/ownership token.
  - Industrial-local structure generation for relevant tenant/role changes, separate from ordinary economic values.
- Consumes:
  - Task 1 contracts/classifier;
  - Task 2 runtime store;
  - Task 3 scoring models;
  - Core `AreaMembershipIndex`.

- [ ] **Step 1: Add and test per-Area membership/ownership revision**

In AreaMembershipPolicyTests, pin GetAreaRevision(areaStableId):
- unknown/untouched Area returns 0;
- effective member/relation/primary-owner changes advance only affected Area tokens;
- unchanged normalized memberships and no-op removals do not advance;
- owner transfer advances both old/new affected Areas, including primary flags changing while member lists stay equal;
- other Areas' membership edits leave this Area token unchanged;
- unrelated Park membership changes leave Industrial ownership tokens unchanged;
- Clear/load invalidates cycles and clears tokens; removed-Area tokens are removed after deletion cleanup.

Compare normalized old/new relations before mutation. Advance a token once per effective relation set change, unchecked increment skipping zero; use equality, never numeric ordering. Retain a token for a temporarily empty but existing Area, preventing accidental reuse. Keep Core unaware of Industrial semantics.

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter AreaMembershipPolicyTests`

Expected: PASS after the minimal Core token change.

- [ ] **Step 2: Write pure aggregation tests**

Assert:
- a building contributes only to its primary Industrial Area;
- changing primary ownership removes it from the old snapshot on the next full observation cycle and credits it once to the new one;
- cross-kind memberships do not affect Industrial ownership;
- multi-role facilities contribute to each relevant diagnostic dimension without duplicating jobs/employees;
- inactive/disabled producers do not count as active productive facilities;
- cargo placement/count changes alone do not change Logistics;
- healthy industrial flow using external cargo/no dedicated warehouse can score 100;
- generic office-only jobs never contribute;
- another Area/Park change does not restart this Area's cycle;
- local boundary/ownership/tenant-structure change invalidates its cycle; ordinary stock/employee changes do not;
- over-one-game-hour cycle duration becomes delayed/stale and supplies no XP/coverage.

- [ ] **Step 3: Write throughput/data-fidelity tests**

Using the exact API source selected in Task 1:
- productive activity must follow the validated public signal;
- raw inventory delta alone cannot make productive activity available;
- transfers/consumption that change inventory without validated production must not be reported as production;
- if the selected production signal is unavailable for an entity/asset, throughput remains unavailable while other metrics continue.

- [ ] **Step 4: Implement company/tenant resolution in `IndustrialObservationReader`**

Start from Core's building member. Resolve only companies/tenants attributable to that building using the 1.6.2f1 public building/company relationship selected by the API-contract test. Never broaden Core membership itself to company entities.

Aggregate a building with multiple relevant tenants once for building-level values and per-company only for company-specific observations.

- [ ] **Step 5: Implement normalized observation mapping**

Map public state into Task 1 DTOs:
- active producer/utilization;
- validated productive activity/throughput;
- resource activity;
- diagnostic storage utilization and validated input/output flow-symptom durations;
- diagnostic cargo function/mode (not a direct score);
- eligible industrial jobs/employees with facility/company deduplication;
- efficiency;
- profitability.

Document and test adapter conversions against spec §12.1 and Task 1's observation-contract ledger. A raw member's existence is not semantic evidence. Implement rolling six-hour flow windows with >=4-hour coverage. Unknown demand/context cannot turn empty/full stock into a confirmed symptom. Treat unsupported inputs as unavailable. Do not expose raw game structs beyond this reader.

- [ ] **Step 6: Implement low-frequency observation scheduling**

Initial cadence: one complete Industrial observation cycle per in-game hour.

Requirements:
- no city-wide company query every frame;
- use Industrial Area membership to enumerate relevant building members;
- process fairly across Areas with an initial total cap of 128 dynamic building reads per simulation update; bound tenant traversal and avoid per-frame full GetMembers/ToArray allocations;
- capture GetAreaRevision(areaId) and Industrial-local relevant structure generation; discard/restart only the affected Area if either changes before publish;
- use existing membership/native change evidence to invalidate relevant tenant/role structure; do not city-scan or restart on ordinary employee/inventory/efficiency fluctuation;
- unrelated Area/Park changes cannot invalidate the cycle; transfer of primary Industrial ownership invalidates both affected Areas;
- include start/end simulation time; a cycle taking >one game-hour is stale/delayed and supplies no XP or daily coverage;
- suspended Areas do not publish a fresh operating snapshot;
- after load, no stale pre-load snapshot survives;
- UI reads only the last completed snapshot.

- [ ] **Step 7: Finalize snapshots through Task 3 models**

Finalize counts, normalized component inputs, scores and top-three bottlenecks only after a complete coherent observation cycle.

Update `LifetimePeakEmployees` only from completed Active-Area observations.

- [ ] **Step 8: Run focused + API-check tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "IndustrialObservationTests|IndustrialApiContractTests|IndustrialPerformanceModelTests"`

Then:
`dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug -p:AreaApiCheck=true`

Expected: PASS, with direct compile proof against supplied 1.6.2f1 managed references.

- [ ] **Step 9: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/IndustrialObservation*.cs src/CS2AreaSystem/Core/Membership/AreaMembershipIndex.cs tests/CS2AreaSystem.Tests/AreaMembershipPolicyTests.cs tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs
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
  - `IndustrialProgressionModel.ComputeHourlyXp(in IndustrialProgressInputs inputs, bool active, int grantedToday)` (three-of-four renormalization)
  - `IndustrialProgressionModel.AccumulateDailyActivity(in IndustrialDailyActivityObservation observation, ref IndustrialAreaState state)`
  - `IndustrialProgressionModel.FinalizeActivityDay(uint completedDay, ref IndustrialAreaState state)`
  - `IndustrialProgressionModel.UpdateHighThroughputStreak(uint completedDay, bool qualifies, ref IndustrialAreaState state)`
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
  - seven consecutive completed game days, each with >=18 valid observed hours and time-weighted productive activity average >=80.

A missing required promotion metric remains visibly unmet; the one-input XP fallback does not fabricate a missing Production or waive promotion gates.

- [ ] **Step 2: Write no-demotion, suspend and XP tests**

Use spec §14.1:
- Active + fresh coherent completed hourly observation + at least one independently confirmed operating producer;
- Production/Employment/Logistics/Performance original weights 0.35/0.25/0.20/0.20;
- at least three of four inputs; one missing renormalizes, two missing -> zero with reason;
- roundAwayFromZero(reweightedScore/10), clamp 0..10; no minimum award;
- daily cap 240, reset grant count on game-day change only;
- no scale multiplier, stale/Suspended awards or missing-hour catch-up;
- persist LastSettledXpHour; retries/reassignment/same-hour save/load cannot award twice;
- saturating long XP; no level demotion.

Named test assertions:
- AllAvailable100 -> hourly 10; 24 eligible distinct full-day hourly slots -> 240; 25th/cap-exhausted -> 0.
- MissingProduction(E=60,L=80,F=80) -> weighted score 72.307692..., award 7; independent active-producer confirmation still required.
- EachSingleMissingInput -> three remaining original weights renormalized.
- TwoMissingInputs, NoConfirmedActiveProducer, StaleSnapshot, Suspended -> 0.
- SameHourAfterReload/Retry -> no additional award; next distinct hour can earn remaining cap.
- AllZero -> 0; score 45 -> 5 (round away from zero).
- HealthySmall/HealthyLarge -> identical XP for identical quality.

Complete the XP assertions in IndustrialScoreExampleTests using the real progression model.

- [ ] **Step 3: Write coverage-aware daily-average streak tests**

Use double accumulators and the game calendar pinned by Task 1.
- <=one-hour gaps bracketed by valid observations contribute the previous activity value * elapsedGameHours; first observation adds no earlier coverage.
- >one-hour gaps, unavailable/stale/Suspended intervals add no coverage; interval spanning midnight splits between the two days.
- completed day with exactly 18 covered hours and mean exactly 80 qualifies; 17.99 hours does not; mean 79.99 does not.
- a brief 100 spike with low remainder does not qualify; changing sample density at identical covered values/time does not change mean.
- finalize each day once; seven consecutive qualifying days satisfy prestige; missing/unqualified day resets.
- same-hour reload cannot duplicate interval coverage; persist partial-day sum/hours/last-credit hour and streak; after load use new endpoints, never bridge the load gap.
- relevant structural-generation change resets only interpolation/window baseline, preserving authoritative covered time already earned.
- day zero and multiday jumps are handled without sentinel collisions.

- [ ] **Step 4: Implement pure progression model**

Keep all thresholds/constants in `IndustrialProgressionModel`. Return a checklist consumed by UI; do not embed promotion rules in the panel.

- [ ] **Step 5: Implement progression system**

Consume each coherent completed observation once:
- independently settle the game-hour XP bucket and eligible daily-coverage interval;
- update peak employees from current Active observations;
- finalize each completed calendar day once, including days crossed while Suspended, with insufficient/missing coverage breaking the streak;
- award XP only if eligible and below cap, then apply at most one promotion;
- preserve Level/XP while Suspended; do not accumulate activity there;
- load/generation change clears interpolation baseline and stale snapshots, while authoritative accumulators remain.

- [ ] **Step 6: Run focused tests**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter IndustrialProgressionModelTests`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Modules/Industrial/IndustrialProgression*.cs tests/CS2AreaSystem.Tests/IndustrialProgressionModelTests.cs tests/CS2AreaSystem.Tests/IndustrialScoreExampleTests.cs
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

Unavailable data is never rendered as 0. Show distinct Unavailable/NotApplicable/WarmingUp/Stale states, partial coverage and reason. Show hourly XP/day cap 240, blocked-XP reason, streak day average/covered hours (required 18), and no bonus from warehouse/cargo placement. All diagnostic statistics are available at every Level. Existing HQ health may be displayed without applying a debuff.

Bottleneck payload shows evidence/affected count/window/suggestion. Explain unobserved external cargo information without claiming no route exists.

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
- unavailable/not-applicable/warming-up/stale/coverage wording;
- daily XP cap, insufficient XP-input reason, daily-average/18-hour coverage wording;
- accepted-HQ operating health does not suspend Area and has no v1 debuff;
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
- Verify/update: `docs/verification/industrial-observation-contracts.md`, `industrial-score-examples.json`, `industrial-score-examples.md`
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
- approved spec/plan and executable calculation-example/observation-contract documents exist;
- no ordinary-office-only eligibility or cargo-presence score path is introduced.

Explicit forbidden Industrial source patterns include production/logistics system replacement or direct mutation paths for resources, taxes/demand, pathfinding/cargo routing, `BuildingEfficiency` and `Profitability`.

- [ ] **Step 2: Register Industrial systems with local failure isolation**

Add `RegisterIndustrialSystems(UpdateSystem updateSystem)` in `Mod.cs`, mirroring Park's isolation.

Register:
- `IndustrialRuntimeSystem` in ModificationEnd, after Core membership/lifecycle updates, to collect/prune Areas and attach missing authoritative Industrial state/progress;
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

- [ ] **Step 4: Add all 27 runtime smoke scenarios from spec §22.4**

Copy the approved scenarios into `scripts/runtime-smoke-checklist.md`, keeping each as an unchecked runtime-only item.

Create `docs/verification/industrial-area-v1-execution-record.md` with:
- source/API verification section;
- runtime section explicitly marked **NOT RUN**;
- space for measured small/large-Area wall time/allocations, observation completion delay/coverage, ownership churn and balance notes;
- explicit live-field semantic validation evidence separate from compile/model example evidence.

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
