# Pre-Tourism Wave A1-A5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add runtime trends, safe diagnostic focus actions, Industrial process visibility/bottleneck targets, and bounded Park landscape diagnostics without changing vanilla simulation.

**Architecture:** Keep the first implementation wave read-only. Add one reusable fixed-capacity trend primitive in Core; Park and Industrial store only module-owned runtime series. Diagnostic actions travel through the existing guarded `moduleAction` path, resolve an opaque target key against the *current* module snapshot, re-check entity existence/current Area ownership, then call the existing selected-info focus callback. Industrial process and bottleneck target data are collected inside the existing budgeted observation cycle. Park landscape diagnostics are produced inside the already sliced landscape scan and retain only bounded grid/representative evidence.

**Tech Stack:** C# 9, Unity ECS / Cities: Skylines II 1.6.2f1 public APIs, NUnit, React/TypeScript, Vitest, existing CS2-Area-System panel/action/localization contracts.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md`

## Global Constraints

- Target Cities: Skylines II **1.6.2f1**.
- Wave A1-A5 is read-only with respect to vanilla simulation and authoritative Park/Industrial saved state.
- Do not enable any policy currently marked `EffectUnavailable`.
- Do not change Core's building-only geometric membership contract.
- Same-kind overlap uses Core's deterministic primary-owner relation.
- Runtime trends are fixed-capacity and reset at load generation; no history persistence in this wave.
- Diagnostic target actions must reject stale Area IDs, stale snapshots, deleted targets and targets no longer relevant to the selected Area.
- Park spatial diagnostics stay dirty-driven/sliced under the existing landscape work budget; no retained per-object history.
- Do not add ExtraLib/Harmony dependencies.
- Tourism Area gameplay remains untouched.

## Review Focus

- **Load boundary:** old-city trend samples and diagnostic targets must disappear when the module runtime generation resets; Task 1/2/5 tests pin this.
- **Stale UI target:** a button rendered from an old panel must not focus an entity after membership/diagnostics change or entity deletion; Task 3/5 tests pin this.
- **Partial/delayed Industrial observation:** process/bottleneck presentation must preserve current stale/partial semantics rather than turn unavailable data into zero/current data; Task 4/5 tests pin this.
- **Park edit during sliced scan:** landscape tally, grid evidence and representative targets must be discarded together when the boundary revision changes; Task 6 tests pin this.
- **Large dense Park:** diagnostics must retain O(1) grid/top-N state rather than one record per prop/tree and must not increase the existing per-frame candidate budget; Task 6 tests pin this.

---

## File Structure

New focused files:

- `src/CS2AreaSystem/Core/Diagnostics/RuntimeTrendSeries.cs` — fixed-capacity runtime-only time series.
- `src/CS2AreaSystem/Core/Modules/AreaDiagnosticTargets.cs` — opaque entity target key formatting/resolution.
- `src/CS2AreaSystem/Modules/Industrial/IndustrialProcessDiagnostics.cs` — process relation DTO/key/aggregation helpers.
- `src/CS2AreaSystem/Modules/Park/ParkLandscapeDiagnostics.cs` — bounded grid evidence and diagnostic evaluator.

Existing files remain owners of their current responsibilities:

- Park/Industrial runtime snapshots own trend instances.
- ParkQualitySystem and Industrial observation publication record trend samples.
- IndustrialObservationReader/Aggregator gather process/target evidence inside the existing observation budget.
- ParkLandscapeSystem gathers bounded spatial evidence inside the existing sliced scan.
- Park/Industrial panel builders only format module data.
- AreaManagementUISystem remains the only place that supplies the live selection-focus callback.

### Task 1: Fixed-capacity runtime trend primitive

**Files:**
- Create: `src/CS2AreaSystem/Core/Diagnostics/RuntimeTrendSeries.cs`
- Create: `tests/CS2AreaSystem.Tests/RuntimeTrendSeriesTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct RuntimeTrendSample { public long Bucket { get; } public double Value { get; } }`
  - `public sealed class RuntimeTrendSeries`
  - `RuntimeTrendSeries(int capacity)`
  - `void Record(long bucket, double value)`
  - `bool TryLatest(out RuntimeTrendSample sample)`
  - `bool TryPrevious(out RuntimeTrendSample sample)`
  - `int CopyTo(List<RuntimeTrendSample> into)`
  - `void Clear()`
  - `int Count { get; }`
- Semantics:
  - recording the same bucket replaces that bucket's value;
  - newer buckets append and evict the oldest at capacity;
  - an older-than-latest bucket is ignored;
  - non-finite values are ignored;
  - `CopyTo` returns oldest -> newest without allocating internally.

- [ ] **Step 1: Write failing trend-series tests**

Add tests named:

- `SameBucketReplacesWithoutGrowing`
- `CapacityEvictsOldestInOrder`
- `OlderBucketAndNonFiniteValuesAreIgnored`
- `LatestPreviousAndClearAreStable`
- `CopyToReusesCallerList`

Assert capacity 3 with buckets 10/11/12/13 yields 11/12/13 and no count growth for duplicate bucket 13.

- [ ] **Step 2: Run the focused tests and confirm RED**

Run:

`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true --filter RuntimeTrendSeriesTests`

Expected: FAIL because `RuntimeTrendSeries` does not exist.

- [ ] **Step 3: Implement the primitive**

Implement a preallocated ring buffer in `RuntimeTrendSeries.cs`; no LINQ or temporary collections in `Record/TryLatest/TryPrevious`.

- [ ] **Step 4: Re-run focused tests**

Expected: PASS.

- [ ] **Step 5: Commit**

Commit: `feat(core): add bounded runtime trend series`

### Task 2: Park and Industrial runtime trend capture + compact panel rows

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- Create: `tests/CS2AreaSystem.Tests/RuntimeTrendIntegrationTests.cs`

**Interfaces:**
- Add `public sealed class ParkTrendSnapshot` to Park runtime code with six `RuntimeTrendSeries` members:
  - `Visitors`, `Quality`, `Satisfaction`, `Occupancy`, `AdmissionIncome`, `ManagementExpense`.
- Add `public readonly ParkTrendSnapshot Trends` to `ParkRuntimeSnapshot`.
- Extend `ParkRollingWindow` with `long CurrentBucket(uint frame)` so trend capture reads the current hour's admission/expense/visit amount without changing existing 24-hour `Sum` semantics.
- Park capture bucket: `frame / ParkRuntimeSnapshot.RecentBucketFrames`.
- A Park bucket is recorded only once after Quality exists; repeated ParkQuality updates in the same hour replace the same bucket.
- Add `public sealed class IndustrialTrendSnapshot` with:
  - `Production`, `Logistics`, `Employment`, `Efficiency`, `Performance`, `ProductiveFacilities`.
- Add `public readonly IndustrialTrendSnapshot Trends` to `IndustrialRuntimeSnapshot`.
- Industrial bucket is `IndustrialObservationResult.HourBucket`; record only a published result that is Active and not Delayed. For metrics without `HasValue`, do not record a numeric sample.
- Panel presentation uses latest-vs-previous delta only; no chart in Wave A.
- Add localization keys:
  - `CS2AS.Trend.Up`, `CS2AS.Trend.Down`, `CS2AS.Trend.Flat`, `CS2AS.Trend.New`, `CS2AS.Trend.Tip`.

- [ ] **Step 1: Add failing Park trend integration tests**

Pin:
- two Quality evaluations in one hour produce one sample;
- next hour produces previous/latest samples;
- `CurrentBucket` returns only the active hour amount, while `Sum` still returns the last-day sum;
- clearing/recreating `ParkRuntimeStore` yields empty trend state.

- [ ] **Step 2: Add failing Industrial trend integration tests**

Pin:
- active non-delayed result records available metrics;
- unavailable metric is skipped;
- delayed/inactive results do not become new trend samples;
- `IndustrialRuntimeStore.Clear()` drops all trend state.

- [ ] **Step 3: Run focused C# tests and confirm RED**

Run the filters:
`RuntimeTrendIntegrationTests|ParkPanelBuilderTests|IndustrialObservationTests|IndustrialPanelBuilderTests`.

- [ ] **Step 4: Implement Park/Industrial trend capture**

Keep capture at existing publication/evaluation points; do not add a new per-frame system.

- [ ] **Step 5: Add compact trend rows to Park/Industrial panels**

For each existing headline metric, add one adjacent row only when at least one trend sample exists. Format delta with invariant numeric text and a localized direction key/tool tip; do not infer a percentage when the underlying metric is a raw count.

- [ ] **Step 6: Add localization and panel tests**

Assert all emitted keys exist in en-US/ja-JP and an unavailable/one-sample series displays `New` rather than a fabricated delta.

- [ ] **Step 7: Run focused tests**

Expected: PASS.

- [ ] **Step 8: Commit**

Commit: `feat: add runtime trends to Park and Industrial panels`

### Task 3: Guarded diagnostic target focus contract

**Files:**
- Create: `src/CS2AreaSystem/Core/Modules/AreaDiagnosticTargets.cs`
- Modify: `src/CS2AreaSystem/Core/Modules/AreaPanelContext.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- Modify: `tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs`
- Create: `tests/CS2AreaSystem.Tests/AreaDiagnosticTargetTests.cs`

**Interfaces:**
- Produces:
  - `public static class AreaDiagnosticTargetKey`
  - `string Format(Entity target)`
  - `bool TryResolve(string key, IEnumerable<Entity> allowedTargets, out Entity target)`
- Extend `AreaPanelContext`:
  - `public Action<Entity> Focus { get; }`
  - constructor becomes `AreaPanelContext(World world, EntityManager entities, Action<Entity> focus = null)`.
- `AreaManagementUISystem` supplies `selection.Focus` when creating the action context.
- Target key syntax is internal/opaque; tests care only about round-trip and rejection.
- `TryResolve` succeeds only when the parsed entity exactly equals an entity in `allowedTargets`; parsing a raw Entity key alone is never authorization.

- [ ] **Step 1: Add failing key-resolution tests**

Assert:
- valid current candidate resolves;
- same Index with different Version fails;
- malformed/empty key fails;
- valid-looking key absent from the current allowed list fails.

- [ ] **Step 2: Add failing panel-context/action tests**

Pin that the existing stable-ID/live-selection guard still runs before provider action dispatch, and that a supplied Focus callback can be invoked by a provider only after that guard.

- [ ] **Step 3: Run focused tests and confirm RED**

Filter: `AreaDiagnosticTargetTests|AreaPanelModelTests`.

- [ ] **Step 4: Implement key and context changes**

Preserve the existing two-argument `AreaPanelContext` call sites through the optional callback parameter.

- [ ] **Step 5: Run focused tests**

Expected: PASS.

- [ ] **Step 6: Commit**

Commit: `feat(core): add guarded diagnostic focus targets`

### Task 4: Industrial process relation observation and display

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialProcessDiagnostics.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationReader.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialObservationReaderTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialProcessDiagnosticsTests.cs`

**Interfaces:**
- Extend `IndustrialCompanyRead` with blittable process fields copied from `IndustrialProcessData`:
  - input1 resource/amount;
  - input2 resource/amount;
  - output resource/amount.
- Add managed DTO:
  - `public readonly struct IndustrialProcessKey` — equality over input/output resources and recipe amounts.
  - `public sealed class IndustrialProcessSummary` with `IndustrialProcessKey Process`, `int Companies`, `double ActualOutputPerDay`.
- Add `List<IndustrialProcessSummary> Processes` to `IndustrialObservationResult`.
- The mapper carries a process relation only for eligible producer tenants with valid `IndustrialProcessData`.
- The aggregator groups equal recipes; never merges recipes solely because they share an output resource.
- Panel ordering: descending `ActualOutputPerDay`, then deterministic resource IDs.
- Display each process as a small group of localized resource-title rows: zero/one/two input rows followed by one output row. Do not concatenate raw `Resources.TITLE[...]` localization keys into an unlocalized sentence.
- Add module localization keys for role/tooltips only:
  - `CS2AS.Industrial.Process.Input`
  - `CS2AS.Industrial.Process.Output`
  - `CS2AS.Industrial.Process.Companies`
  - `CS2AS.Industrial.Process.ActualPerDay`
  - `CS2AS.Industrial.Process.Tip`.

- [ ] **Step 1: Write failing reader/mapper tests**

Use synthetic `IndustrialProcessData` facts to assert both inputs, output and integer recipe amounts survive reader -> mapper without changing activity/flow semantics.

- [ ] **Step 2: Write failing grouping tests**

Assert:
- same recipe aggregates company count/output rate;
- same output with different input recipe remains two rows/groups;
- extractor with no material input produces an output-only process group;
- office/commercial tenants still produce no Industrial process summary.

- [ ] **Step 3: Run focused tests and confirm RED**

Filter: `IndustrialProcessDiagnosticsTests|IndustrialObservationReaderTests|IndustrialObservationTests|IndustrialPanelBuilderTests`.

- [ ] **Step 4: Implement DTO/read/aggregation**

Use data already read within the current observation job. Do not add a second EntityManager scan.

- [ ] **Step 5: Implement panel presentation/localization**

Keep current stale/delayed panel semantics: a stale Current result may show its process rows only with the same stale context/note as the rest of the tab; never relabel it as a current score.

- [ ] **Step 6: Run focused tests**

Expected: PASS.

- [ ] **Step 7: Commit**

Commit: `feat(industrial): show observed input-output processes`

### Task 5: Industrial bottleneck target collection and focus buttons

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Modify: `scripts/test-offline-contracts.mjs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPerformanceModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- Create: `tests/CS2AreaSystem.Tests/IndustrialDiagnosticTargetTests.cs`
- Test: `UI/src/ModulePanel.test.tsx`

**Interfaces:**
- Add `public Entity Target` to `IndustrialBottleneck`; the pure performance model leaves it `Entity.Null`.
- Add module-owned bounded target evidence to `IndustrialCycleAccumulator`; select at most one representative Entity per bottleneck key during existing `Apply`.
- Representative rules:
  - WorkforceShortage: lowest fill ratio among understaffed facilities;
  - LowEfficiency: lowest observed efficiency;
  - LowProfitability: lowest observed profitability;
  - InputStarvation: first/worst producer whose input state is Affected;
  - StorageSaturation: first/worst facility whose output state is Affected;
  - InsufficientCargo: non-operational cargo facility when available, otherwise affected-output producer;
  - LowActiveProducerShare: an idle producer.
- After `FindAllBottlenecks`, attach only the current accumulator's candidate for that bottleneck key.
- New action constant: `IndustrialGameplayFacade.FocusDiagnosticTarget = "focusDiagnosticTarget"`.
- New method:
  - `public static bool HandleAction(Entity area, string action, string value, in AreaPanelContext context)`
  - handles only `focusDiagnosticTarget`; all other Industrial-specific actions remain false.
- Resolution requirements before `context.Focus(target)`:
  1. current runtime snapshot exists;
  2. key resolves against targets in `Current.AllBottlenecks`;
  3. target entity still exists;
  4. Core membership currently contains `(selected Area StableId, Industrial, IsPrimaryForType=true)` for that building.
- Panel emits a `button` item after a bottleneck suggestion only when `Target != Entity.Null`; `Value=AreaDiagnosticTargetKey.Format(Target)`.
- Update offline contract: Industrial remains simulation-read-only, but the single whitelisted focus action and Button presentation are allowed; sliders/mutating actions remain forbidden.

- [ ] **Step 1: Add failing representative-target tests**

Pin every rule above, including deterministic tie behaviour (first observed facility wins).

- [ ] **Step 2: Add failing action safety tests**

Assert no focus when:
- action name differs;
- snapshot changed and target disappeared;
- target Entity was deleted;
- target is no longer primary-owned by the Area;
- current Area stable ID/action guard is stale (existing Core dispatcher test).

Assert exactly one focus callback for a valid current target.

- [ ] **Step 3: Add failing panel/UI tests**

Assert button appears only for target-bearing bottlenecks and `ModulePanel` sends the target key as the existing moduleAction value.

- [ ] **Step 4: Run focused tests and confirm RED**

Filter C#: `IndustrialDiagnosticTargetTests|IndustrialObservationTests|IndustrialPanelBuilderTests|IndustrialPerformanceModelTests`.
Run UI: `cd UI && npm test -- --run ModulePanel.test.tsx`.

- [ ] **Step 5: Implement target evidence/action/panel**

Do not perform any extra world scan on button click beyond existence/current membership validation.

- [ ] **Step 6: Update source/offline guards**

Replace the old blanket "Industrial module has no actions/buttons" assertion with:
- only `focusDiagnosticTarget` accepted;
- no `SetComponentData/AddComponent/RemoveComponent` in IndustrialGameplayFacade;
- no sliders;
- no other Industrial button action string.

- [ ] **Step 7: Run focused tests + offline contracts**

Expected: PASS.

- [ ] **Step 8: Commit**

Commit: `feat(industrial): focus facilities behind bottlenecks`

### Task 6: Bounded Park landscape diagnostics

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkLandscapeDiagnostics.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkLandscapeSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFacilityClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- Create: `tests/CS2AreaSystem.Tests/ParkLandscapeDiagnosticTests.cs`
- Test: `UI/src/ModulePanel.test.tsx`

**Interfaces:**
- Add `public sealed class ParkLandscapeDiagnostic`:
  - `string Key`
  - `double Severity`
  - `Entity Target`
- Add `IReadOnlyList<ParkLandscapeDiagnostic> Diagnostics` to `ParkLandscapeSnapshot`.
- Add `ParkLandscapeDiagnosticCollector` with a fixed **4 × 4** grid (16 cells) across the Park AABB.
- Collector retains per cell only:
  - whether the cell received classified landscape evidence;
  - category bit mask;
  - one representative Entity for Amenity and Lighting;
  - one representative Entity for clutter/density evidence.
- It does not retain per-object lists.
- Evaluation rules:
  - category shortage when the existing awarded category score is below **50% of that category's weight**; severity is normalized missing share;
  - clutter diagnostic exactly when `ParkLandscapeResult.Cluttered`, severity = `ClutterPenalty / MaxClutterPenalty`;
  - sparse Amenity/Lighting coverage only when at least **8** grid cells contain any classified landscape evidence and that category appears in fewer than **25%** of evidenced cells;
  - emit at most **4** diagnostics, sorted severity desc then key;
  - if no representative Entity is reliable, Target stays null and the warning remains non-clickable.
- Required keys:
  - `LowVegetation`, `LowAmenities`, `LowDecoration`, `LowLighting`, `LowPaths`, `Cluttered`, `SparseAmenities`, `SparseLighting`.
- Add Park action constant `ParkGameplayFacade.FocusDiagnosticTarget = "focusDiagnosticTarget"`.
- Park focus action resolves only against current `snapshot.Landscape.Diagnostics`, verifies target existence, and verifies the entity remains within/relevant to the current Park scan evidence before invoking `context.Focus`. For standalone landscape props that are not Core building members, relevance check is current Park geometry containment plus `Transform/Static` and no Deleted/Temp/Owner; for building targets (if any later), use current membership.
- Landscape panel displays diagnostic warning/note rows and a focus button only when Target exists.

- [ ] **Step 1: Add failing pure evaluator tests**

Pin:
- below-half-weight category emits shortage;
- exactly half does not;
- clutter severity uses existing max penalty;
- sparse coverage is suppressed below 8 evidenced cells;
- 8+ cells with <25% Amenity/Lighting coverage emits sparse warning;
- result caps at 4 in deterministic order.

- [ ] **Step 2: Add failing collector bounded-state tests**

Feed thousands of synthetic objects and assert collector state remains 16 cells/top-N only (no per-object collection), with deterministic representative target.

- [ ] **Step 3: Add failing sliced-scan invalidation test**

Extend the existing boundary-edit test so a partial scan with populated diagnostic grid is invalidated and none of its diagnostics reaches the snapshot after revision change.

- [ ] **Step 4: Add failing focus action tests**

Pin valid target, deleted target, moved-outside-geometry target and stale old-snapshot key.

- [ ] **Step 5: Run focused C#/UI tests and confirm RED**

Filter: `ParkLandscapeDiagnosticTests|ParkFacilityClassifierTests|ParkPanelBuilderTests`.
Run ModulePanel UI test.

- [ ] **Step 6: Implement bounded collection/evaluation in the existing sliced scan**

Update diagnostics during the same `Classify` callback that feeds `ParkLandscapeTally`; do not increase `ObjectsPerFrame` or `TilesPerFrame`.

- [ ] **Step 7: Implement panel/action/localization**

Keep diagnostics advisory only: do not modify Park Quality inputs or activation.

- [ ] **Step 8: Run focused tests**

Expected: PASS.

- [ ] **Step 9: Commit**

Commit: `feat(park): add bounded landscape diagnostics`

### Task 7: Fixtures, runtime checklist, and full Wave A1-A5 verification

**Files:**
- Modify: `tests/CS2AreaSystem.Tests/ParkPanelFixtureTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/IndustrialPanelFixtureTests.cs`
- Modify generated: `UI/src/__fixtures__/park-panel.json`
- Modify generated: `UI/src/__fixtures__/industrial-panel.json`
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/park-area-v1-execution-record.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`
- Modify: `README.md` only if its explicit feature summary would otherwise be false.

**Interfaces:**
- No new production interface.
- Runtime checklist additions:
  - trend rollover/load reset;
  - bottleneck button focuses the correct current Industrial facility and rejects a demolished/moved one;
  - Industrial process rows match the selected company's visible vanilla recipe/resource roles where comparable;
  - Park landscape diagnostics change after adding/removing scenery and remain responsive in a dense Park;
  - no new frame spike beyond the existing Park landscape scan budget.

- [ ] **Step 1: Regenerate panel fixtures through the C# fixture tests**

Use the repository's existing `UPDATE_UI_FIXTURES=1` flow; do not hand-edit generated fixture JSON.

- [ ] **Step 2: Run the complete C# test assembly**

Primary:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true`

If the known host/environment issue prevents VSTest from starting, use the repository's existing NUnitLite runner for the same built assembly and record that limitation; do not report the blocked VSTest command as PASS.

- [ ] **Step 3: Run production API compilation**

`dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release`

Expected: 0 errors.

- [ ] **Step 4: Run UI tests/build**

`cd UI && npm test -- --run`

`cd UI && npm run build`

Expected: PASS.

- [ ] **Step 5: Run repository guards**

`node scripts/test-offline-contracts.mjs`

`pwsh -NoProfile -File scripts/verify-source.ps1`

`pwsh -NoProfile -File scripts/test-source-guards.ps1`

`git diff --check`

Expected: PASS, or record an environment-specific blocker exactly.

- [ ] **Step 6: Review Wave A1-A5 against the approved spec**

Confirm:
- no saved schema change;
- no vanilla simulation mutation;
- no policy availability change;
- no Tourism gameplay;
- no new full-world scan;
- Industrial focus action is the only new Industrial panel action;
- Park diagnostics are advisory and bounded.

- [ ] **Step 7: Commit verification/docs**

Commit: `docs: add Wave A1-A5 runtime verification cases`

- [ ] **Step 8: Open a PR against current main for review**

PR description must list:
- exact base SHA used at implementation start;
- task commits;
- test commands/results;
- runtime checks still NOT RUN;
- explicit note that A6/A7 and Waves B-D are not included.

