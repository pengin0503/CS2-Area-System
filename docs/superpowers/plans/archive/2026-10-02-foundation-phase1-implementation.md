# Foundation + Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Headquarters/Entrance Foundation and the complete approved Phase 1 in one continuous branch/PR: revised activation/anchors, Park path entrances, A6/A7, B1, C1 and C3.

**Architecture:** Replace the single-required-anchor assumption with a generic activation context and multi-role anchor presentation. Industrial uses an office Headquarters; Park requires Park Maintenance Headquarters plus Entrance evidence, with the 1.6.2f1 baseline using dedicated `PathwayData` boundary crossings because no safe vanilla Park-gate Building is verified in the supplied sources. Phase 1 then builds on that Foundation: bounded pedestrian connectivity, pure what-if previews, a versioned semantic registry, bounded persistent history, and read-only Industrial Area comparison.

**Tech Stack:** C# 9, Unity ECS, Cities: Skylines II 1.6.2f1 public managed APIs, NUnit/NUnitLite, React/TypeScript, Vitest, existing CS2-Area-System panel/persistence/source-guard infrastructure.

**Spec:** `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md`

## Global Constraints

- Work from the latest `main`; the design-start baseline was `716f9906a1b2f4773eb140e84ee892bc57398c4f`, but it is not a fixed checkout target.
- Foundation + Phase 1 are one implementation program: one work branch and one PR, with task-sized commits and no approval gate between Foundation and Phase 1.
- Target Cities: Skylines II **1.6.2f1**.
- A1-A5 are already merged and must remain intact.
- Industrial Headquarters = ordinary office Building; extractor/processor/storage/cargo capability alone no longer qualifies.
- Park activation = valid Park Maintenance Headquarters **AND** at least one valid Entrance evidence.
- No safe vanilla Park-gate Building is verified by the supplied 1.6.2f1 sources; therefore dedicated `PathwayData` boundary crossing is the baseline Entrance source.
- Ordinary road sidewalks do not satisfy the automatic Entrance requirement.
- Entrance evidence does not force visitor routing and must not patch pathfinding.
- Path-derived Entrance evidence is runtime-only and never serializes Net entities.
- No ExtraLib/Harmony dependency.
- No currently `EffectUnavailable` policy becomes available.
- No Tourism gameplay is added.
- Same-kind primary-owner semantics, stale-action guards, module failure isolation and Area finance accounting remain unchanged.
- Use the supplied 1.6.2f1 ZIPs only when exact API semantics are uncertain; do not guess game APIs or reread the archives wholesale.
- RED -> minimal GREEN -> focused verification -> commit for every task.

## Review Focus

- **Load before path rescan finishes:** Park must not fabricate an Entrance; no saved Net Entity may satisfy activation. Task 5 tests this.
- **Boundary edit during a sliced Entrance/connectivity scan:** discard incompatible partial evidence and revalidate lifecycle deterministically. Tasks 5 and 7 test this.
- **Old saves with legacy HQ/Entrance links:** preserve XP/Level/finance/history, expose invalid links, never silently convert them. Task 6 tests this.
- **Semantic provider conflict/removal:** deterministic winner, cache invalidation and vanilla fallback must be immediate and provider-isolated. Task 9 tests this.
- **History buffer corruption/duplicate hour after save-load:** reject unsupported schema, deduplicate by bucket and stay bounded. Task 11 tests this.

---

### Task 1: Generic activation context and multi-role anchor contract

**Files:**
- Create: `src/CS2AreaSystem/Core/Modules/AreaActivationContext.cs`
- Modify: `src/CS2AreaSystem/Core/Modules/IAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/Anchors/IAreaAnchorPolicy.cs`
- Modify: `src/CS2AreaSystem/Core/Modules/AnchorAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/Anchors/AreaActivationSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaMemberClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/TourismAreaModuleTests.cs`
- Test: `tests/CS2AreaSystem.Tests/TourismBoundaryContractTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- Produce `public readonly struct AreaActivationContext` with:
  - `World World { get; }`
  - `EntityManager Entities { get; }`
  - constructor `AreaActivationContext(World world, EntityManager entities)`.
- Change `IAreaModule.ValidateActivation` to:
  - `bool ValidateActivation(Entity area, in AreaActivationContext context)`.
- Change `IAreaAnchorPolicy` to expose:
  - `IReadOnlyList<AreaAnchorRole> AnchorRoles { get; }`
  - `bool CanAssignAnchor(Entity area, AreaAnchorRole role, EntityManager entities)`
  - existing `AcceptsAnchor(...)`.
- `AnchorAreaModule` constructor becomes `AnchorAreaModule(AreaTypeId kind, AreaAnchorService anchors, params AreaAnchorRole[] roles)`; default activation remains “all declared persisted roles have a valid anchor” only for subclasses that do not override activation.
- `AreaCoreServicesSystem.EvaluateAnchor` authorizes a role only when it is present in `AnchorRoles`; assignment additionally requires `CanAssignAnchor(...)`. Persisted-link validation does not call `CanAssignAnchor`, so old links remain visible/validatable.
- `AreaActivationSystem` passes a live `AreaActivationContext`.

- [ ] **Step 1: Add failing contract/lifecycle tests**
  - multiple declared roles are recognized;
  - an undeclared role is rejected;
  - assignment-disabled role rejects new assignment but does not hide persisted links;
  - Tourism with no roles remains Active.

- [ ] **Step 2: Run focused tests and confirm RED**
  - `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true --filter "AreaAnchorServiceTests|AreaMemberClassifierTests|TourismAreaModuleTests|TourismBoundaryContractTests|ModSystemOrderTests"`

- [ ] **Step 3: Implement the context/interface migration**

- [ ] **Step 4: Run focused tests and confirm GREEN**

- [ ] **Step 5: Commit**
  - `feat(core): support contextual multi-role area activation`

### Task 2: Multi-role anchor management UI

**Files:**
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementViewModel.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Modify: `UI/src/bindings.ts`
- Modify: `UI/src/AreaInfoSection.tsx`
- Test: `tests/CS2AreaSystem.Tests/AreaManagementViewModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs`
- Test: `UI/src/AreaInfoSection.test.tsx`

**Interfaces:**
- Add `AreaAnchorGroupView`:
  - `string Role`
  - `bool AssignmentSupported`
  - `IReadOnlyList<AreaAnchorView> Anchors`
  - `int? DerivedCount`
  - `string DerivedStatusKey`.
- Replace the single-role presentation assumption with `AreaManagementViewModel.AnchorGroups`.
- Add generic UI trigger `assignAnchor(stableId, role)`; parse only defined `AreaAnchorRole` values and re-run current policy/role guards.
- Keep remove-anchor keys role-scoped through the group that owns them.
- Pending assignment continues to store one `pendingRole`.

- [ ] **Step 1: Add failing C#/UI tests for two groups**
  - Park-style view renders Headquarters and Entrance independently;
  - only `AssignmentSupported=true` group shows assign/change;
  - invalid legacy link remains removable;
  - stale stable ID cannot begin assignment.

- [ ] **Step 2: Run C#/UI focused tests and confirm RED**

- [ ] **Step 3: Implement view/binding/UI changes**

- [ ] **Step 4: Run focused tests and confirm GREEN**

- [ ] **Step 5: Commit**
  - `feat(core): present and assign multiple area anchor roles`

### Task 3: Industrial office Headquarters

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`

**Interfaces:**
- `IndustrialFacilityClassifier.IsHeadquartersEligible(in IndustrialFacilityTraits traits)` returns true for a Building with stable office-property evidence (`HasOfficeProperty`); `OfficeCompany` alone is not required for continued validity.
- Industrial declares only `AreaAnchorRole.Headquarters`.
- `CanAssignAnchor(... Headquarters ...)` is true.
- Existing Headquarters links are revalidated against office-building eligibility; old factory/extractor/storage/cargo HQ links become invalid rather than being silently converted.
- Industrial observation continues to exclude office companies from Production/Logistics.

- [ ] **Step 1: Add failing eligibility/migration tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement office-HQ semantics and copy**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat(industrial): use office buildings as headquarters`

### Task 4: Park Headquarters and Entrance evidence model

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkEntranceEvidence.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFacilityClassifierTests.cs`
- Create: `tests/CS2AreaSystem.Tests/ParkActivationTests.cs`

**Interfaces:**
- Extend `ParkFacilityTraits` with `bool HasParkMaintenance` and `Entity Prefab`.
- `ParkFacilityReader` sets `HasParkMaintenance` from `Game.Buildings.ParkMaintenance`.
- Add `ParkFacilityClassifier.IsHeadquartersEligible(in ParkFacilityTraits traits)`.
- Add:
  - `enum ParkEntranceEvidenceStatus : byte { Unknown, Complete }`
  - `readonly struct ParkEntrancePoint { float X, Z; Entity Edge; }`
  - `sealed class ParkEntranceEvidence` with `Status`, `Revision`, bounded `IReadOnlyList<ParkEntrancePoint> Points`, and `Count`.
  - `sealed class ParkEntranceEvidenceStore` with `TryGet(ulong, out ParkEntranceEvidence)`, `Set`, `Remove`, `Clear`.
- Park declares `Headquarters` and `Entrance`; Headquarters assignment is supported. Until Task 9 semantic registration is integrated, explicit Entrance assignment is disabled.
- Park activation is false unless Headquarters is valid and Entrance evidence is complete with Count > 0 (or, after Task 10, a valid explicit semantic Entrance exists).

- [ ] **Step 1: Add failing Park HQ/activation tests**
  - Park Maintenance qualifies as HQ;
  - leisure/attraction alone does not;
  - HQ only is Suspended;
  - Entrance evidence only is Suspended;
  - both are Active.

- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement model/classifier/module changes**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat(park): require maintenance headquarters and entrance evidence`

### Task 5: Dedicated pedestrian-Path boundary Entrance system

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkEntranceSystem.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkPathEntranceDetector.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSystem.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkPathEntranceDetectorTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`
- Modify: `scripts/test-offline-contracts.mjs`

**Interfaces / constants:**
- A dedicated automatic Entrance candidate is a live `Game.Net.Edge` + `Game.Net.Curve` whose `PrefabRef.m_Prefab` has `Game.Prefabs.PathwayData`.
- Road edges without `PathwayData` never qualify, even if their sublanes include pedestrians.
- `ParkPathEntranceDetector` samples each Bezier at **16 segments / 17 points**; every inside↔outside transition yields a candidate crossing.
- Merge crossing candidates within **8 metres** deterministically (lowest Edge Index/Version wins).
- Query Park AABB with **16 m margin**.
- `ParkEntranceSystem` work budget: **2048 candidate edges/update**, changed-net fanout cutoff **512**, fallback reconciliation every **3 in-game hours** (`ParkRuntimeSystem.FramesPerDay / 8`).
- Boundary edits clear old evidence to Unknown immediately; ordinary net dirties retain the last complete snapshot until the replacement scan completes.
- A successful completed scan atomically replaces the evidence and calls `AreaCoreServicesSystem.Anchors.InvalidateArea(area)` when Count/status changes.
- Register this system independently of Area Finance and **before `AreaActivationSystem`** in `ModificationEnd`.

- [ ] **Step 1: Add failing pure detector tests**
  - crossing PathwayData edge => 1 Entrance;
  - road/sidewalk edge => 0;
  - inside-only/outside-only => 0;
  - two physical crossings => 2;
  - nearby duplicate crossings collapse deterministically.

- [ ] **Step 2: Add failing scheduling/state tests**
  - load clears store;
  - boundary revision drops partial work and old evidence;
  - net edit replaces atomically;
  - work never exceeds budget.

- [ ] **Step 3: Confirm RED**
- [ ] **Step 4: Implement detector/system/scheduling**
- [ ] **Step 5: Confirm GREEN + offline guard**
- [ ] **Step 6: Commit**
  - `feat(park): detect dedicated path entrances at area boundaries`

### Task 6: Foundation integration, migration and Accessibility

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkQualityModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkSerializationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSerializationTests.cs`

**Interfaces:**
- `ParkQualitySystem` uses current path Entrance points/count for EntranceCount, spread and entrance/path accessibility inputs.
- Panel summaries distinguish missing HQ, missing Entrance, both missing, and active.
- Legacy Park leisure/attraction Entrance links and old Industrial industrial-facility HQ links stay visible as invalid links and never erase Level/XP/finance/state.
- No state schema migration invents an anchor or path entity.

- [ ] **Step 1: Add failing integration/migration tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement integration and localization**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat: integrate revised headquarters and park entrance lifecycle`

### Task 7: B1 versioned asset semantic registry

**Files:**
- Create: `src/CS2AreaSystem/Core/Semantics/AreaAssetSemanticRegistry.cs`
- Create: `src/CS2AreaSystem/Core/Semantics/AreaAssetSemantic.cs`
- Create: `tests/CS2AreaSystem.Tests/AreaAssetSemanticRegistryTests.cs`

**Interfaces:**
- `[Flags] enum AreaAssetSemantic : ushort`:
  - `ParkEntrance`
  - `ParkHeadquarters`
  - `ParkLeisure`
  - `ParkAttraction`
  - `IndustrialHeadquarters`
  - `IndustrialExtractor`
  - `IndustrialProducer`
  - `IndustrialStorage`
  - `IndustrialCargo`.
- `readonly struct AreaAssetSemanticPatch`:
  - `AreaAssetSemantic Allow`
  - `AreaAssetSemantic Deny`
  - `int Priority`.
  - overlapping Allow/Deny bits are rejected.
- `AreaAssetSemanticRegistry`:
  - `bool Register(string providerId, Entity prefab, AreaAssetSemanticPatch patch)`
  - re-registering same provider+prefab replaces atomically;
  - `bool Unregister(string providerId, Entity prefab)`
  - `int UnregisterProvider(string providerId)`
  - `bool Resolve(Entity prefab, AreaAssetSemantic semantic, bool vanillaDefault)`
  - `bool HasRegisteredAllow(AreaAssetSemantic semantic)`
  - `long Revision { get; }`.
- Conflict winner per semantic bit: highest Priority; ties by ordinal `providerId`; winner's Allow/Deny overrides the vanilla default. No winner => vanilla default.
- Resolution cache key includes registry Revision; provider failures/invalid registrations do not remove other providers.

- [ ] **Step 1: Add failing lifecycle/conflict/cache tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement registry**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat(core): add versioned area asset semantic registry`

### Task 8: Wire semantic registry into Foundation classifiers

**Files:**
- Modify: `src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFacilityClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`

**Interfaces:**
- `AreaCoreServicesSystem.Semantics` owns one registry per World/load lifecycle.
- Typed vanilla facts are passed as `vanillaDefault`; semantic overrides only documented bits.
- Registered `ParkEntrance` Allow enables explicit Entrance assignment for that prefab; path Entrance remains valid simultaneously.
- Registered Park HQ / Industrial HQ semantics may explicitly allow or deny assignment.
- Role-hint semantics are exposed to Industrial readers/classifiers without changing Core geometric membership or office/commercial exclusion rules unless an explicit documented semantic bit says so.

- [ ] **Step 1: Add failing override/fallback tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement semantic resolution adapters**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat: apply registered semantics to area facility roles`

### Task 9: A6 bounded Park pedestrian-connectivity diagnostic

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkConnectivitySystem.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkConnectivityModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Create: `tests/CS2AreaSystem.Tests/ParkConnectivityTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`

**Interfaces / constraints:**
- Graph uses live `Game.Net.Edge.m_Start/m_End`, node `ConnectedEdge` buffers and edge `SubLane` buffers.
- An edge is pedestrian-capable when at least one `SubLane.m_SubLane` has `Game.Net.PedestrianLane`; dedicated `PathwayData` is therefore included, but road sidewalks may participate in **connectivity** even though they cannot create automatic Entrances.
- Seeds are current explicit Entrance buildings (when any) plus Task 5 path Entrance edges.
- Targets are current Park-relevant member facilities.
- Result status: `Unknown`, `Partial`, `Complete`; incomplete work must never be reported as disconnected.
- Work is dirty-driven and bounded: **4096 traversed edges/update**, **20,000 edges max per Park scan**. Reaching the hard cap publishes Partial.
- No citizen/pathfinding calls and no route mutation.
- Diagnostic targets use the existing `AreaDiagnosticTargetKey` and focus contract.

- [ ] **Step 1: Add failing pure graph tests**
  - connected, disconnected, partial-cap, nonpedestrian edge exclusion, multiple entrances.
- [ ] **Step 2: Add stale-focus and dirty-restart tests**
- [ ] **Step 3: Confirm RED**
- [ ] **Step 4: Implement graph reader/system/panel diagnostics**
- [ ] **Step 5: Confirm GREEN**
- [ ] **Step 6: Commit**
  - `feat(park): add bounded pedestrian connectivity diagnostics`

### Task 10: A7 Park budget/fee what-if preview

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkWhatIfPreview.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFinanceModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFinanceModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- Test: `UI/src/ModulePanel.test.tsx`

**Interfaces:**
- `readonly struct ParkWhatIfResult` with estimated ManagementCost, Maintenance contribution/coverage, ValueForMoney, EstimatedNet and explicit availability flags.
- `ParkWhatIfPreview.Evaluate(ParkPanelModel current, float candidateBudget, int candidateFee)` is pure and performs no ECS writes, money transactions or persistence.
- Preview actions update only module UI preview state keyed by selected StableId; existing `setBudget` / `setFee` remain the only commit actions.
- All preview labels include an “estimate/preview” localization marker.

- [ ] **Step 1: Add failing pure preview/no-mutation tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement preview and UI**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat(park): add budget and fee what-if preview`

### Task 11: C1 bounded persistent history

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkHistoryEntry.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkHistorySystem.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialHistoryEntry.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialHistorySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Create: `tests/CS2AreaSystem.Tests/AreaHistorySerializationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/RuntimeTrendIntegrationTests.cs`

**Interfaces / retention:**
- Persist **168 hourly buckets (7 in-game days)** per Area.
- One buffer element per hour per module, not six independent buffers.
- `ParkHistoryEntry : IBufferElementData, ISerializable` stores schema, HourBucket, six A1 values plus availability mask.
- `IndustrialHistoryEntry : IBufferElementData, ISerializable` stores schema, HourBucket, six A1 values plus availability mask.
- Duplicate HourBucket replaces the existing entry; older out-of-order buckets are ignored; append evicts oldest beyond 168.
- Older saves with no buffer are valid.
- Unsupported element schema reports through existing `ComponentSchema` behavior.
- On runtime rebuild/load, seed the A1 `RuntimeTrendSeries` from the newest **24** persisted hourly entries; do not duplicate the current hour when observation resumes.

- [ ] **Step 1: Add failing serialization/bounds/load-seed tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement history entries/systems**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat: persist bounded park and industrial history`

### Task 12: C3 Industrial Area comparison

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaComparison.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialAreaComparisonTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`

**Interfaces:**
- Compare managed Industrial Areas only.
- Include current available Production, Logistics, Employment, Efficiency, Performance and Performance latest-vs-previous trend.
- Exclude quarantined/deleted Areas; show Suspended/delayed snapshots as stale, never as current zero.
- Sorting is deterministic: Performance descending for Areas with a current value, then StableId ascending; unavailable rows follow.
- Comparison is read-only and never affects economy, XP, ownership or ranking outside this panel.
- Add a `comparison` tab using existing Row/Note/Warning panel primitives; do not introduce a new generic table control solely for this feature.

- [ ] **Step 1: Add failing comparison/filter/order tests**
- [ ] **Step 2: Confirm RED**
- [ ] **Step 3: Implement comparison read model/tab**
- [ ] **Step 4: Confirm GREEN**
- [ ] **Step 5: Commit**
  - `feat(industrial): compare managed industrial areas`

### Task 13: Fixtures, guards and full Foundation + Phase 1 verification

**Files:**
- Modify: `tests/CS2AreaSystem.Tests/ParkPanelFixtureTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/IndustrialPanelFixtureTests.cs`
- Generated: `UI/src/__fixtures__/park-panel.json`
- Generated: `UI/src/__fixtures__/industrial-panel.json`
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/park-area-v1-execution-record.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `README.md` only if the existing feature summary would otherwise become false.

**Runtime checklist additions:**
- Industrial office HQ assignment and legacy HQ invalidation.
- Park Maintenance HQ + dedicated Path crossing activation matrix.
- ordinary road sidewalk does not activate Park.
- path deletion/boundary edit suspends after bounded rescan.
- save/load rebuilds path Entrance without serialized Net identity.
- semantic custom Entrance can coexist with path baseline.
- connectivity Complete/Partial/Disconnected behavior.
- what-if preview never changes money/settings before explicit commit.
- 7-day history survives save/load and stays bounded.
- Industrial comparison matches current Area metrics.
- profiling confirms no whole-city per-frame net scan.

- [ ] **Step 1: Regenerate fixtures via `UPDATE_UI_FIXTURES=1`; do not hand-edit fixture JSON**
- [ ] **Step 2: Run complete C# suite**
  - `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true`
  - if the known VSTest host failure occurs, run the repository NUnitLite runner for the same assembly and record the VSTest blocker; never call the blocked command PASS.
- [ ] **Step 3: Run production API build**
  - `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release`
- [ ] **Step 4: Run UI**
  - `cd UI && npm test -- --run`
  - `cd UI && npm run build`
- [ ] **Step 5: Run guards**
  - `node scripts/test-offline-contracts.mjs`
  - `pwsh -NoProfile -File scripts/verify-source.ps1`
  - `pwsh -NoProfile -File scripts/test-source-guards.ps1`
  - `git diff --check`
- [ ] **Step 6: Spec audit**
  - no pathfinding mutation;
  - no road-sidewalk Entrance;
  - no saved Net Entity;
  - no EffectUnavailable enablement;
  - no Tourism gameplay;
  - no A1-A5 regression;
  - Foundation and every Phase 1 item implemented.
- [ ] **Step 7: Commit verification/docs**
  - `docs: add Foundation and Phase 1 runtime verification cases`
- [ ] **Step 8: Open one PR against latest `main`**
  - PR body: exact base SHA, all task commits, automated results, runtime checks still NOT RUN, source/API assumptions, and explicit note that Phase 2/3 are excluded.
  - Do not auto-merge unless the user separately requests it.
