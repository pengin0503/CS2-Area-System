# Phase 3 D1 Industrial Area Supply Contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let managed Industrial Areas define persisted resource supply agreements and display advisory capacity/compliance from existing observations without changing vanilla transfers or routing.

**Architecture:** A contract is owned by its source Area and references a destination Area StableId, one existing vanilla Resource and a target units/day. Runtime evaluation uses the already-observed source output rates and destination process-input demand; it estimates potential coverage only and never claims or enforces a physical shipment.

**Tech Stack:** C# 9, Unity ECS, Industrial observation/process summaries, Phase 2 ownership resolver, stable Area IDs, structured panel UI, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §D1

## Global Constraints

- Industrial Areas only in the first version.
- No pathfinding, transfer-request, cargo-vehicle, market-price or native demand mutation.
- Resource must be one existing non-Money, non-NoResource vanilla Resource bit.
- Source and destination must be distinct live managed Industrial Areas.
- Contract status is explicitly labeled advisory/estimated.
- Persist configuration only; derived source capacity/destination need/status are runtime-only.
- Max 32 contracts per source Area.

## Review Focus

- Destination deleted/recreated with another StableId: old contract is invalid, never silently rebound.
- Source/destination observation delayed or partial: status is Unknown/Partial, never zero-fulfilled.
- Resource no longer appears in either observation: contract remains configured but reports unavailable.
- Duplicate resource/destination contract creation: update existing contract rather than create ambiguous duplicates.
- 32-contract cap and many Areas do not introduce all-Area × all-Resource per-frame scans.

---

### Task 1: Persistent contract schema/model

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupplyContract.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupplyContractModel.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupplyContractTests.cs`

**Interfaces:**
- `IndustrialSupplyContract : IBufferElementData, ISerializable`: schema, ContractId, DestinationStableId, Resource (ulong), TargetPerDay, Enabled.
- Contract IDs are monotonic per source Area; store next ID in `IndustrialSupplyContractState : IComponentData, ISerializable`.
- Max 32; same destination+resource is unique.

- [ ] RED serialization/validation/cap tests.
- [ ] Implement schema/model.
- [ ] GREEN and commit: `feat(industrial): persist area supply contracts`

### Task 2: Advisory evaluation from observation

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupplyContractEvaluator.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupplyContractEvaluatorTests.cs`

**Interfaces:**
- Source capacity = current observed `IndustrialResourceTotal.ActualPerDay` for contract resource.
- Destination estimated input need = sum for current observed process summaries consuming that resource: `ActualOutputPerDay × inputAmount / outputAmount` when recipe amounts are valid.
- Potential = min(TargetPerDay, SourceCapacity, DestinationNeed).
- Status: Unknown, PartialData, NoObservedSupply, NoObservedNeed, Available, Constrained.
- Never equate Potential with actual shipped volume.

- [ ] RED full/partial/delayed/invalid-recipe tests.
- [ ] Implement pure evaluator.
- [ ] GREEN and commit: `feat(industrial): evaluate advisory supply contracts`

### Task 3: Generic select control and guarded contract actions

**Files:**
- Modify: `src/CS2AreaSystem/Core/Modules/AreaPanelModels.cs`
- Modify: `UI/src/bindings.ts`
- Modify: `UI/src/ModulePanel.tsx`
- Create: `tests/CS2AreaSystem.Tests/AreaPanelSelectTests.cs`
- Modify: `UI/src/ModulePanel.test.tsx`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialGameplayFacade.cs`

**Interfaces:**
- Add generic `select` panel item with bounded `AreaPanelOption { Value, Label, LabelKey }`.
- Module actions create/update/toggle/remove contracts using stable contract IDs and current Area StableId guard.
- Resource options come only from verified existing Resource values observed/allowed by the game contract; destination options from current managed Industrial Areas.

- [ ] RED serialization/UI/stale-action tests.
- [ ] Implement generic select + actions.
- [ ] GREEN and commit: `feat(core): add bounded panel select control`

### Task 4: Contracts tab and lifecycle

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialLocalization.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupplyContractSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`

- [ ] Display configuration and advisory metrics/status.
- [ ] Mark deleted destination invalid and allow removal.
- [ ] Re-evaluate on relevant observation/contract generation changes, not every frame.
- [ ] Commit: `feat(industrial): manage advisory supply contracts`

### Task 5: Guards/runtime verification

**Files:**
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`

- [ ] Guard against pathfinding/transfer/price/demand mutation.
- [ ] Full tests/build/UI/guards.
- [ ] Game checks for delete/load/delayed observation/performance.
- [ ] Commit: `docs: verify advisory industrial supply contracts`
