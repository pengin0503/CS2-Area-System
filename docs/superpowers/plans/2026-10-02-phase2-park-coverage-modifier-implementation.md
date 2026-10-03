# Phase 2 C6 Park Coverage Modifier Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify and, only if supported, add a bounded Park-only operating modifier through the 1.6.2f1 `ModifiedServiceCoverage` magnitude/capacity path, with immediate neutral restoration and a runtime-proof gate.

**Architecture:** First prove the native writer, consumer and serialization contracts. Only then keep a per-building baseline/ownership record for current Park-owned eligible service buildings and apply the Area factor between native baseline production and its actual consumer. Restore neutral/original behavior on disable, ownership change, save, load or system failure. This feature ships disabled unless live-game ordering and restoration are verified.

**Tech Stack:** C# 9, Unity ECS, Park membership/primary owner, verified 1.6.2f1 service-coverage components, source/runtime guards, NUnit/NUnitLite.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §C6

## Global Constraints

- Do not touch `m_Range`.
- Do not patch/replace the vanilla coverage system.
- Exact `ModifiedServiceCoverage` field/component contract must be re-verified against supplied 1.6.2f1 sources before product code is written.
- Modifier applies only to current primary-owned Park-eligible service buildings.
- Initial Area factor derives from Park budget: 50% budget => 0.90, 100% => 1.00, 150% => 1.10, linear and clamped [0.90,1.10].
- If Enhanced Maintenance is effective, apply its existing management-funding multiplier to the factor input but clamp final factor to 1.10.
- Pure factor: `clamp(1 + 0.20 × (effectiveBudget - 1), 0.90, 1.10)`, where `effectiveBudget = normalizedBudgetMultiplier × effectivePolicyManagementFundingMultiplier`; use the existing Park budget normalization and neutral defaults for unavailable policy effects.
- No saved temporary vanilla override; runtime state rebuilds after load.
- Inspect the native serializer as well as the simulation writer: runtime-only mod bookkeeping alone does not prevent a modified vanilla component from being saved. Restore before serialization and reapply only after a fresh verified native baseline.
- There is no supported enable control in a production build until the runtime release gate passes. A disabled prototype must never schedule the writer, even if its code and pure model compile.
- Disable/removal/failure must restore neutral behavior in the same or next valid writer pass.
- Release gate: static/API tests are insufficient; in-game runtime verification is mandatory.

## Review Focus

- Repeated mod passes without a new native write never compound. Another native writer changes the value: fresh baseline wins; an external conflicting value is not overwritten with a stale stored baseline.
- Building changes primary Park owner: old modifier is removed before new owner factor applies.
- Load while modifier was active: no serialized temporary value survives as authoritative mod state.
- Component/prefab becomes ineligible or disappears: no stale Entity write.
- Registration/order failure: C6 disables itself while Park/Core remain operational.

---

### Task 1: Re-verify native coverage contract

**Files:**
- Create: `docs/verification/park-coverage-modifier-contract.md`
- Test: `tests/CS2AreaSystem.Tests/ParkCoverageApiContractTests.cs`

- [ ] Inspect supplied 1.6.2f1 managed/toolchain sources for exact component fields, every writer, the real coverage consumer/job dependencies, update cadence/phase and component serialization path.
- [ ] Record an ordering that places the mod after fresh native baseline production and before the consuming coverage calculation. `UpdateAfter` alone does not prove that a native job has completed or that its consumer has not already run.
- [ ] Prove a fresh-baseline signal and a reversible save hook; equality with the last modded value alone cannot distinguish an identical vanilla rewrite from no rewrite. Preserve a native/external write on restoration if it no longer matches this modifier's last write.
- [ ] Pin the verified public types/fields in API-contract tests.
- [ ] If freshness, consumer ordering, non-accumulating instance writes or save restoration cannot be proven, stop C6 before Task 2 and document BLOCKED; a speculative writer must not be added merely to finish the phase.
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
- Create: `src/CS2AreaSystem/Modules/Park/ParkCoverageModifierSaveSystem.cs` (only if Task 1 verifies its native save hooks)
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkCoverageModifierSystemTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- Process only bounded current Park members/revision changes; no whole-world scan.
- Store runtime baseline identity/value and verified freshness evidence sufficient to write `vanillaBaseline × factor`, never previous modded value × factor.
- Register the writer between the verified producer/consumer with correct jobs dependencies. Register save restore before native serialization; post-save application requires a fresh baseline, not a stale pre-save cache.
- Restore tracked instances on disable/preload/save/ownership loss; restore only values still owned by this modifier. OnStopRunning/registration failure must handle runtime cleanup rather than assuming disabled systems will receive another OnUpdate.

- [ ] RED repeated-pass/no-fresh-write, ownership/load/save/order/failure/external-write tests and disabled-build-no-write test.
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
