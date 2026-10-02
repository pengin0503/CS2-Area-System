# Phase 3 C2 Optional Park Gate Asset Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Support an optional dedicated Park Gate building as an explicit Park Entrance while preserving dedicated-Path boundary crossings as a fully valid fallback.

**Architecture:** The asset is a normal placeable Building and is classified only through the Phase 1 semantic registry (`ParkEntrance`). Code never checks its display name. The built-in provider registers the prefab when present; missing content simply leaves path-based Entrance behavior unchanged.

**Tech Stack:** C# 9, Unity ECS, Phase 1 `AreaAssetSemanticRegistry`, CS2 1.6.2f1 asset/prefab pipeline, existing multi-role anchor UI, NUnit/NUnitLite.

**Spec:** `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md` §4.5 / Phase 3 C2

## Global Constraints

- Gate is optional and never required for old or new Parks.
- Gate counts as an explicit `AreaAnchorRole.Entrance`; path-derived Entrances continue to satisfy activation.
- No display-name heuristic.
- Gate asset must provide a valid Building entity and normal pedestrian/road access expected by CS2; do not implement routing interception.
- 3D model contains only building-fixed elements: gate structure, walls/panels, service door/window if needed, fixed signage housing, fixed electrical/utility elements. Do not model benches, vegetation, lamps or replaceable props.
- No Industrial HQ custom asset in C2.

## Review Focus

- Asset missing: no exception and path Entrance still activates Park.
- Asset removed after save with a persisted anchor: link is invalid/removable, path evidence may keep Park active.
- Two Gates plus path crossings: counts/presentation are deterministic and not duplicated as the same source.
- Semantic provider reload/unregister: registry revision invalidates eligibility immediately.
- Asset upgrade/version changes prefab identity: registration is rebuilt, no saved runtime prefab Entity.

---

### Task 1: Pin the Gate asset contract

**Files:**
- Create: `docs/content/park-gate-asset-contract.md`
- Create: `tests/CS2AreaSystem.Tests/ParkGateAssetContractTests.cs`

- [ ] Define required Building/prefab traits and semantic registration contract.
- [ ] Pin “optional, no name heuristic, path fallback preserved” in tests/guards.
- [ ] Commit: `docs: define optional park gate asset contract`

### Task 2: Built-in semantic provider

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkBuiltInSemanticProvider.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSystem.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkBuiltInSemanticProviderTests.cs`

**Interfaces:**
- Provider ID `CS2AreaSystem.BuiltInContent`.
- Register/unregister the located prefab with `AreaAssetSemantic.ParkEntrance`; no registration when content is absent.
- Runtime prefab Entity is rediscovered/re-registered each load generation.

- [ ] RED present/absent/reload tests.
- [ ] Implement provider.
- [ ] GREEN and commit: `feat(park): register optional gate entrance content`

### Task 3: Asset authoring/import handoff

**Files:**
- Create: `assets/park-gate/README.md`
- Modify: `docs/content/park-gate-asset-contract.md`

The code plan does not invent an FBX/material/package layout. Before authoring, inspect the actual verified 1.6.2f1 asset toolchain, record the exact source/import/output paths it requires in the contract, and only then create those files.

- [ ] Record the verified toolchain's exact asset paths/formats in the contract.
- [ ] Build/import the Gate as a Building using that verified pipeline.
- [ ] Keep prop-replaceable scenery external.
- [ ] Verify LOD/material/collision/footprint and placement.
- [ ] Commit only asset metadata/source allowed by repository policy; generated/import cache files stay out unless the repository policy explicitly requires them.

### Task 4: UI/runtime compatibility and verification

**Files:**
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/park-area-v1-execution-record.md`
- Test: `tests/CS2AreaSystem.Tests/ParkActivationTests.cs`
- Test: `UI/src/AreaInfoSection.test.tsx`

- [ ] Explicit Gate assignment + path fallback tests.
- [ ] Install/remove/save/load runtime checks.
- [ ] Full build/UI/guards.
- [ ] Commit: `docs: verify optional park gate integration`
