# Phase 3 D2 Custom Factory Existing-Resource Content Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add custom Industrial factory content that uses only existing vanilla resources and automatically participates in generic Industrial observation/semantics without score special cases.

**Architecture:** First pin one verified vanilla-compatible `IndustrialProcessData` recipe from 1.6.2f1 sources, then author one reference factory asset using that process and register only semantic role hints through B1. Observation must see the asset through the same process/company reader used for vanilla factories.

**Tech Stack:** C# 9, Unity ECS, CS2 1.6.2f1 prefab/asset pipeline, `IndustrialProcessData`, vanilla `Resource`, Phase 1 semantic registry and Industrial observation, NUnit/NUnitLite.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §D2

## Global Constraints

- No new Resource enum members, dense-index extensions or economy-overhaul arrays.
- First release ships exactly one reference custom factory asset.
- Its recipe must use Resource values and process semantics verified from supplied 1.6.2f1 sources before asset authoring.
- Prefer an already-valid vanilla process relationship; do not invent unsupported economic resources or transfer behavior.
- No factory-specific branches in Production/Logistics/XP scoring.
- Semantic registry may mark the prefab `IndustrialProducer`; observation still reads actual `IndustrialProcessData`.
- 3D model follows project rule: building-fixed elements only; benches/plants/streetlights/etc. remain vanilla props.

## Review Focus

- Custom asset absent: saves and observation of vanilla companies remain unaffected.
- Recipe prefab loads but company lacks required runtime buffers: metric becomes unavailable, no exception/fake production.
- Asset update changes prefab identity: semantic registration rebuilds on load.
- Reference factory in overlapping Areas follows Phase 2 resolved company ownership.
- Import pipeline changes process amounts/resources: API/content contract test catches mismatch before release.

---

### Task 1: Verify and freeze one process recipe

**Files:**
- Create: `docs/verification/custom-factory-process-contract.md`
- Create: `tests/CS2AreaSystem.Tests/CustomFactoryProcessContractTests.cs`

- [ ] Inspect supplied 1.6.2f1 managed/toolchain/prefab references and select one real supported `IndustrialProcessData` recipe using existing Resource values.
- [ ] Record exact input1/input2/output Resource IDs/amounts and required workplace/storage prefab data.
- [ ] Add API/content contract tests for those exact values/types.
- [ ] If no safely authorable process is verified, mark D2 BLOCKED rather than inventing one.
- [ ] Commit: `docs: pin custom factory process contract`

### Task 2: Reference factory asset and semantic registration

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialBuiltInContentProvider.cs`
- Create: `assets/industrial-reference-factory/README.md`
- Create/update actual asset source/import files according to the verified content pipeline.
- Test: `tests/CS2AreaSystem.Tests/IndustrialBuiltInContentProviderTests.cs`

**Interfaces:**
- Provider ID `CS2AreaSystem.BuiltInContent`.
- Register the prefab as `IndustrialProducer`; additional role bits only when verified.
- Runtime prefab Entity is rediscovered per load; no saved prefab Entity.

- [ ] RED absent/present/reload tests.
- [ ] Author/import asset with pinned process.
- [ ] Implement semantic provider.
- [ ] GREEN and commit: `feat(industrial): add reference factory content`

### Task 3: Generic observation integration proof

**Files:**
- Test: `tests/CS2AreaSystem.Tests/CustomFactoryObservationTests.cs`
- Modify: `docs/verification/industrial-observation-contracts.md` only to document the generic proof.

- [ ] Build a test fixture using the same reader/mapper contracts as vanilla producer observations.
- [ ] Assert process appears in A4 process rows, resource totals, bottlenecks and C3 comparison without a custom-factory conditional.
- [ ] Assert Phase 2 manual ownership override works for its company.
- [ ] Commit: `test(industrial): prove generic custom factory observation`

### Task 4: Runtime/import verification

**Files:**
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`
- Modify: `scripts/test-offline-contracts.mjs`

- [ ] Source guard forbids new Resource enum/economy-array extensions and factory-name special cases.
- [ ] Import the asset with official/verified pipeline.
- [ ] In game, confirm production process, company resources, observation, overlap/manual ownership, save/load and deletion.
- [ ] Full automated tests/build/UI/guards.
- [ ] Commit: `docs: verify custom factory existing-resource content`
