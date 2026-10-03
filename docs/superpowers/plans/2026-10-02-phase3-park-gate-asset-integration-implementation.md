# Phase 3 C2 Dedicated Area System Asset Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the completed Park Headquarters, Industrial Headquarters and Park Gate models as required Area System content, make those three prefabs the authoritative Headquarters/Entrance sources, and make the Park Gate physically connectable to pedestrian paths.

**Architecture:** C2 packages three normal placeable Building prefabs through the verified CS2 1.6.2f1 content pipeline, using two authoritative source archives: `CS2_AS_IndustrialHeadquarters.zip` for Industrial HQ and `CS2_AS_ParkSet_HQ_Gate.zip` for both Park HQ and Park Gate. The standalone `CS2_AS_ParkHeadquarters.zip` is not an implementation source. A built-in content provider resolves the exact Area System-owned prefab identities and registers `ParkHeadquarters`, `IndustrialHeadquarters` and `ParkEntrance` semantics each load generation. C2 then retires the temporary Foundation-era office/Park Maintenance/path-Entrance eligibility; the gate's pedestrian passage is authored in the asset itself rather than implemented with a runtime pathfinding patch.

**Tech Stack:** C# 9, Unity ECS, Phase 1 `AreaAssetSemanticRegistry`, CS2 1.6.2f1 asset/prefab pipeline, existing multi-role anchor UI, NUnit/NUnitLite.

**Spec:** `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md` §2/§3/§4.5/§5 and Phase 3 C2

## Global Constraints

- Required authored prefab identifiers are exactly:
  - `AS_ParkHeadquarters`
  - `AS_IndustrialHeadquarters`
  - `AS_ParkGate`
- Authoritative source archives are exactly:
  - `CS2_AS_IndustrialHeadquarters.zip` => `AS_IndustrialHeadquarters`
  - `CS2_AS_ParkSet_HQ_Gate.zip` => `AS_ParkHeadquarters` + `AS_ParkGate`
- `CS2_AS_ParkHeadquarters.zip` is a superseded standalone copy and must not be used as a C2 source.
- All future Area System-owned building/model identifiers introduced by this content pack must begin with `AS_`.
- After C2, only registered `AS_ParkHeadquarters` satisfies Park Headquarters, only registered `AS_IndustrialHeadquarters` satisfies Industrial Headquarters, and only registered `AS_ParkGate` satisfies Park Entrance.
- Ordinary office HQ, Park Maintenance HQ and automatic dedicated-Path Entrance are Foundation-era compatibility mechanisms only and are retired by C2; they do not remain activation fallbacks.
- Do not identify assets by localized/display names, substring matching, or DLC-like names. Resolve only the exact internal identifiers owned by the Area System content package.
- Runtime prefab `Entity` values are never serialized as authoritative IDs; rediscover and re-register the authored prefabs each load generation.
- The Park Gate must contain an editor-authored pedestrian passage/subnetwork through the opening, with both ends configured to connect/snap to external pedestrian paths using the verified 1.6.2f1 asset workflow.
- Do not intercept or rewrite citizen pathfinding to force gate traversal.
- 3D models contain only building-fixed elements. Do not add benches, vegetation, streetlights or other scenery that can be supplied by game props.
- Preserve Area StableId, Level/XP, finance, history, policies and Industrial company ownership when old anchors become invalid after C2.
- Do not silently convert an old office/Park Maintenance/path source into a dedicated AS-prefixed anchor.

## Review Focus

- Old save with an office Industrial HQ, Park Maintenance HQ and path Entrance: state loads, those sources no longer satisfy activation, and reassignment to AS-prefixed assets is required.
- One of the three required asset prefabs is absent: semantic registration for that role is absent, the affected Area remains Suspended, and no vanilla/path fallback is fabricated.
- Asset package reload/upgrade recreates prefab entities: provider unregisters stale generation data and registers current runtime entities.
- Park Gate connects to pedestrian paths on both sides and pedestrians can traverse the authored gate path without a code-level routing hook.
- Future DLC adds similarly named HQ/Gate content: it cannot qualify because only exact `AS_` internal identifiers owned by this content pack are registered.

---

### Task 1: Define the three-asset content contract

**Files:**
- Create: `docs/content/area-system-built-in-assets-contract.md`
- Create: `tests/CS2AreaSystem.Tests/AreaBuiltInAssetContractTests.cs`
- Modify: `scripts/test-offline-contracts.mjs`

**Interfaces:**
- Content package logical ID: `AS_AreaSystemAssets`.
- Exact prefab IDs:
  - `AS_ParkHeadquarters` => `AreaAssetSemantic.ParkHeadquarters`
  - `AS_IndustrialHeadquarters` => `AreaAssetSemantic.IndustrialHeadquarters`
  - `AS_ParkGate` => `AreaAssetSemantic.ParkEntrance`
- Provider ID remains `CS2AreaSystem.BuiltInContent`.

- [ ] **Step 1: Write failing contract tests**
  - assert all three required IDs begin with `AS_`;
  - assert IDs are unique;
  - assert each ID maps to exactly its intended semantic;
  - assert no contract permits localized/display-name or substring fallback;
  - assert no contract permits office/Park Maintenance/path Entrance fallback after C2.

- [ ] **Step 2: Run focused tests and confirm RED**

- [ ] **Step 3: Write the contract and source guard**

- [ ] **Step 4: Run focused tests and confirm GREEN**

- [ ] **Step 5: Commit**
  - `docs: define dedicated Area System asset contract`

### Task 2: Import/package the completed 3D assets

**Files:**
- Create: `assets/area-system/README.md`
- Modify: `docs/content/area-system-built-in-assets-contract.md`
- Add only repository-approved source/metadata files produced from the two authoritative source archives: `CS2_AS_IndustrialHeadquarters.zip` and `CS2_AS_ParkSet_HQ_Gate.zip`.

Before authoring, inspect the supplied/installed 1.6.2f1 asset toolchain and record the exact verified source, import and output paths/formats. Treat the ParkSet archive as authoritative for both Park HQ and Park Gate, including their shared texture/material references; do not mix in files from standalone `CS2_AS_ParkHeadquarters.zip`. Do not invent an FBX/material/package layout not supported by that toolchain.

- [ ] **Step 1: Rename authored internal prefab/model identifiers before final import**
  - Park HQ => `AS_ParkHeadquarters`
  - Industrial HQ => `AS_IndustrialHeadquarters`
  - Park Gate => `AS_ParkGate`
  - update any Park Gate -> Park HQ shared texture/material references to the renamed paths without duplicating the atlas unnecessarily.

- [ ] **Step 2: Import all three as normal placeable Building prefabs**
  - use one Area System-owned asset/content package;
  - keep generated/editor cache artifacts out of git unless repository policy explicitly requires them.

- [ ] **Step 3: Author pedestrian connectivity for `AS_ParkGate`**
  - place the verified editor-supported invisible pedestrian path/subnetwork through the model's central passage;
  - configure front and rear endpoints as connectable/snap-compatible with external pedestrian paths;
  - keep the path aligned inside the gate opening and collision-free;
  - do not add a code pathfinding override.

- [ ] **Step 4: Verify each asset in the editor**
  - LODs;
  - materials/textures;
  - collision;
  - footprint/placement;
  - road/path access as applicable;
  - gate pedestrian path continuity and both external connection points.

- [ ] **Step 5: Verify/package content dependency**
  - record the verified 1.6.2f1 packaging/dependency mechanism that causes Area System's required asset package to be installed/loaded with the code mod;
  - fail visibly rather than substituting vanilla assets when required content is unavailable.

- [ ] **Step 6: Commit**
  - `assets: add dedicated Area System headquarters and gate content`

### Task 3: Register the three built-in prefab semantics

**Files:**
- Create: `src/CS2AreaSystem/Core/Semantics/AreaBuiltInContentSemanticProvider.cs`
- Create: `src/CS2AreaSystem/Core/Semantics/AreaBuiltInContentSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaBuiltInContentSemanticProviderTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- `AreaBuiltInContentSemanticProvider.ProviderId == "CS2AreaSystem.BuiltInContent"`.
- Provider resolves only the exact authored identifiers from Task 1.
- Register:
  - Park HQ prefab => Allow `ParkHeadquarters`;
  - Industrial HQ prefab => Allow `IndustrialHeadquarters`;
  - Park Gate prefab => Allow `ParkEntrance`.
- Missing content registers nothing for that role.
- On load generation change, unregister prior provider registrations and rediscover current prefab entities before activation/assignment uses them.

- [ ] **Step 1: Add failing exact-ID/present/absent/reload tests**

- [ ] **Step 2: Confirm RED**

- [ ] **Step 3: Implement provider and lifecycle system**

- [ ] **Step 4: Confirm GREEN and verify provider runs before anchor activation/assignment state is evaluated**

- [ ] **Step 5: Commit**
  - `feat: register dedicated Area System asset semantics`

### Task 4: Cut Headquarters and Entrance eligibility over to dedicated assets

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialFacilityClassifier.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkConnectivitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Delete after references are removed: `src/CS2AreaSystem/Modules/Park/ParkEntranceSystem.cs`
- Delete after references are removed: `src/CS2AreaSystem/Modules/Park/ParkPathEntranceDetector.cs`
- Delete after references are removed: `src/CS2AreaSystem/Modules/Park/ParkEntranceEvidence.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkFacilityClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialFacilityClassifierTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkActivationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaSemanticAdapterTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`
- Modify/remove obsolete path-Entrance tests and guards.

**Interfaces:**
- HQ classifiers use registered semantic truth with vanilla default `false` for `ParkHeadquarters` and `IndustrialHeadquarters`.
- Park activation requires a valid Headquarters anchor and a valid explicit Entrance anchor; derived `ParkEntranceEvidence` is no longer an activation source.
- Park Connectivity starts from valid explicit gate anchor(s), not derived boundary crossings.
- Park Quality entrance count/placement inputs use valid explicit entrance anchors.
- Remove `ParkEntranceSystem` scheduling and path-Entrance stores once all consumers are switched.

- [ ] **Step 1: Add failing cutover tests**
  - ordinary office => not Industrial HQ after C2;
  - Park Maintenance => not Park HQ after C2;
  - dedicated Path crossing without gate => not Park Entrance after C2;
  - the exact three registered prefabs qualify;
  - same-looking/unregistered buildings do not qualify.

- [ ] **Step 2: Confirm RED**

- [ ] **Step 3: Switch classifiers, activation, quality and connectivity to explicit AS-prefixed anchors**

- [ ] **Step 4: Remove obsolete path-Entrance runtime scheduling/state/code and update guards**

- [ ] **Step 5: Run focused/full tests and confirm GREEN**

- [ ] **Step 6: Commit**
  - `feat: require dedicated Area System headquarters and gate`

### Task 5: Existing-save behavior and UI/runtime verification

**Files:**
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/park-area-v1-execution-record.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkSerializationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSerializationTests.cs`
- Test: `UI/src/AreaInfoSection.test.tsx`

- [ ] **Step 1: Pin old-save cutover behavior**
  - old office HQ / Park Maintenance HQ links do not become dedicated assets;
  - old path-derived Entrance state is not authoritative after C2;
  - affected Area becomes Suspended until the correct AS-prefixed asset is assigned;
  - Area StableId, Level/XP, finance, policies, history and Industrial company ownership survive unchanged.

- [ ] **Step 2: Add clear missing-required-content / missing-HQ / missing-Gate UI states**

- [ ] **Step 3: In-game verify all three asset assignments**
  - Park activates only with `AS_ParkHeadquarters` + `AS_ParkGate`;
  - Industrial activates only with `AS_IndustrialHeadquarters`;
  - deleting/moving/replacing the required anchor suspends the Area as expected.

- [ ] **Step 4: In-game verify gate path behavior**
  - external pedestrian Path can connect at both configured gate endpoints;
  - a continuous pedestrian route can pass through the gate;
  - no code-level pathfinding interception is present.

- [ ] **Step 5: Verify missing/remove/reinstall/save/load lifecycle**
  - missing asset content does not crash load;
  - no vanilla/path fallback is accepted;
  - reinstall/reload rediscovers current prefab entities and allows reassignment.

- [ ] **Step 6: Run full C# suite, production API build, UI tests/build, offline guards, source guards and `git diff --check`**

- [ ] **Step 7: Commit**
  - `docs: verify dedicated Area System asset cutover`
