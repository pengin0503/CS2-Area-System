# Phase 2 C6 Park Coverage Modifier Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a bounded Park-only operating modifier through the verified 1.6.2f1 `ModifiedServiceCoverage` magnitude/capacity path, with immediate neutral restoration and a runtime-proof gate.

**Architecture:** Keep a per-building baseline/ownership record only for current Park-owned eligible service buildings, apply the Area factor after the relevant vanilla coverage writer, and restore neutral/original behavior on disable, ownership change, load or system failure. This feature ships disabled unless live-game ordering and restoration are verified.

**Tech Stack:** C# 9, Unity ECS, Park membership/primary owner, verified 1.6.2f1 service-coverage components, source/runtime guards, NUnit/NUnitLite.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §C6

## Global Constraints

- Do not touch `m_Range`.
- Do not patch/replace the vanilla coverage system.
- Exact `ModifiedServiceCoverage` field/component contract must be re-verified against supplied 1.6.2f1 sources before product code is written.
- Modifier applies only to current primary-owned Park-eligible service buildings.
- Initial Area factor derives from Park budget: 50% budget => 0.90, 100% => 1.00, 150% => 1.10, linear and clamped [0.90,1.10].
- If Enhanced Maintenance is effective, apply its existing management-funding multiplier to the factor input but clamp final factor to 1.10.
- No saved temporary vanilla override; runtime state rebuilds after load.
- Disable/removal/failure must restore neutral behavior in the same or next valid writer pass.
- Release gate: static/API tests are insufficient; in-game runtime verification is mandatory.

## Review Focus

- Another vanilla writer updates the component after our last pass: next pass recomputes from current vanilla baseline rather than compounding.
- Building changes primary Park owner: old modifier is removed before new owner factor applies.
- Load while modifier was active: no serialized temporary value survives as authoritative mod state.
- Component/prefab becomes ineligible or disappears: no stale Entity write.
- Registration/order failure: C6 disables itself while Park/Core remain operational.

---

### Task 1: Re-verify native coverage contract

**Files:**
- Create: `docs/verification/park-coverage-modifier-contract.md`
- Test: `tests/CS2AreaSystem.Tests/ParkCoverageApiContractTests.cs`

- [ ] Inspect supplied 1.6.2f1 managed/toolchain sources for exact component fields, owning vanilla system and update phase.
- [ ] Pin the verified public types/fields in API-contract tests.
- [ ] If the verified contract does not support non-accumulating per-instance magnitude/capacity modification, stop C6 and document BLOCKED.
- [ ] Commit: `docs: verify park coverage modifier api`

### Task 2: Pure factor/baseline model

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkCoverageModifierModel.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkCoverageModifierModelTests.cs`

- [ ] RED tests for budget interpolation, policy composition, clamping, neutral restore and non-compounding.
- [ ] Implement pure model.
- [ ] GREEN and commit: `feat(park): define coverage operating modifier`

### Task 3: Runtime writer and restoration

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkCoverageModifierSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkCoverageModifierSystemTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- Process only bounded current Park members/revision changes; no whole-world scan.
- Store runtime baseline identity/value sufficient to write `vanillaBaseline × factor`, never previous modded value × factor.
- Register after the verified vanilla writer.
- Restore tracked instances on disable/load/ownership loss.

- [ ] RED ownership/load/order/failure tests.
- [ ] Implement writer.
- [ ] GREEN and commit: `feat(park): apply bounded coverage modifier`

### Task 4: UI diagnostics, guards and runtime release gate

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkLocalization.cs`
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/park-area-v1-execution-record.md`

- [ ] Show current factor/eligible count and “runtime verification required” state where applicable.
- [ ] Add guards forbidding `m_Range`, global coverage mutation and unsupported writers.
- [ ] Run full automated verification.
- [ ] Perform/record live-game enable→change budget→disable→save/load→delete/overlap tests before marking release-ready.
- [ ] Commit: `docs: verify park coverage operating modifier`
