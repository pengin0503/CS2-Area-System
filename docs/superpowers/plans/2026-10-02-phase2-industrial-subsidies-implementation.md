# Phase 2 C4 Industrial Support Payments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add auditable Industrial Area support payments that debit real city money exactly once and credit only companies currently resolved to that Area.

**Architecture:** The player sets a per-company daily support amount. Once per in-game day, the system snapshots eligible resolved companies, queues one Area Finance expense, and only after Area Finance reports the actually settled amount distributes that amount across still-live receivers in deterministic order. Requested amounts never directly create company money.

**Tech Stack:** C# 9, Unity ECS, B2 ownership resolver, Area Finance listener/save boundary, Game.Economy `Resources` + verified 1.6.2f1 money-resource helper, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §C4

## Global Constraints

- Requires B2 merged.
- Add `AreaFinanceKind.IndustrialSupport` as an expense and update all fixed-kind guards/tests.
- Initial setting: `DailySupportPerCompany` from 0 to 5,000 currency units, step 250, default 0.
- Eligible receivers: live Industrial companies whose resolved owner is the Area; office/commercial companies remain excluded.
- Daily request = eligible company count × configured per-company amount, saturating.
- Actual receiver credit total must be <= the Area Finance settled `IndustrialSupport` amount for that Area.
- If receivers disappear before settlement, distribute only to remaining live eligible receivers; undistributable settled money is recorded as unallocated support, never replayed.
- No native tax setters, profitability writes, demand mutation or production mutation.
- Use only a verified public 1.6.2f1 resource-money write path; if the helper contract differs, stop and document the blocker rather than guess.

## Review Focus

- City can pay only half the request: receiver credits total exactly the settled half.
- Company deletion between request and settlement: no exception/replay; unallocated remainder is recorded.
- Save after city debit but before pending receiver job completes: save flush completes credit/ledger exactly once.
- Load on the same in-game day: LastScheduledDay prevents duplicate scheduling.
- B2 owner changes before next day: next payout follows the new resolved owner only.

---

### Task 1: Support state, finance kind and pure allocation

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportState.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportModel.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceKind.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceModelTests.cs`

**Interfaces:**
- `IndustrialSupportState : IComponentData, ISerializable`: schema, DailySupportPerCompany, LastScheduledDay, LifetimeRequested, LifetimeSettled, LifetimeCredited, LifetimeUnallocated.
- Pure deterministic allocation by stable Entity ordering; total credits never exceed settled amount.

- [ ] RED tests for saturation, partial settlement and zero receivers.
- [ ] Implement state/model + finance kind.
- [ ] GREEN and commit: `feat(industrial): define area support payments`

### Task 2: Daily scheduling through Area Finance

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportSystemTests.cs`

**Interfaces:**
- One request per Area/day when Active, setting > 0, and at least one eligible resolved company.
- Snapshot receiver Entity list and request ID in runtime-only pending state; persistent state stores only dedupe/accounting values.
- Queue `finance.EnqueueExpense(areaId, IndustrialSupport, requested)`.

- [ ] RED daily-dedupe/ownership tests.
- [ ] Implement scheduling.
- [ ] GREEN and commit: `feat(industrial): schedule daily company support`

### Task 3: Settled receiver credit and save boundary

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportSettlement.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportSaveSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportSettlementTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportSaveBoundaryTests.cs`

**Interfaces:**
- `IndustrialSupportSystem : IAreaFinanceListener` consumes only settled entries with `IndustrialSupport`.
- Revalidate receivers with B2 ownership before credit.
- Schedule/perform verified `Resource.Money` additions with proper vanilla job dependencies.
- Save system flushes any settled-but-not-yet-credited work before serializer.

- [ ] RED partial/deleted/save tests.
- [ ] Implement settlement and exact-once flush.
- [ ] GREEN and commit: `feat(industrial): settle support to company money`

### Task 4: UI and verification

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialLocalization.cs`
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- Test: `UI/src/IndustrialPanel.test.tsx`

- [ ] Add slider and requested/settled/credited/unallocated rows with stale-action guards.
- [ ] Narrow the old Industrial “no Resources writes” source guard so only the dedicated subsidy settlement file may use the verified Money write path.
- [ ] Full verification and runtime checklist.
- [ ] Commit: `feat(industrial): expose support payment controls`
