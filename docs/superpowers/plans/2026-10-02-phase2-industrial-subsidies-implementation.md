# Phase 2 C4 Industrial Support Payments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add auditable Industrial Area support payments that debit real city money exactly once and credit only companies currently resolved to that Area.

**Architecture:** The player sets a per-company daily support amount. Once per in-game day, the system snapshots eligible resolved companies, queues one Area Finance expense, and only after Area Finance reports completion distributes the actually settled amount across still-live receivers in deterministic order. The completion contract also reports zero/aborted requests so pending work cannot become stuck. Requested amounts never directly create company money.

**Tech Stack:** C# 9, Unity ECS, B2 ownership resolver, Area Finance listener/save boundary, Game.Economy `Resources` + verified 1.6.2f1 money-resource helper, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §C4

## Global Constraints

- Requires B2 merged.
- B2 is already present in reviewed main `13d4a0a`; preserve its scoped assignment revisions and consume its live owner resolver.
- Add `AreaFinanceKind.IndustrialSupport` as an expense and update all fixed-kind guards/tests.
- Initial setting: `DailySupportPerCompany` from 0 to 5,000 currency units, step 250, default 0.
- Eligible receivers: live Industrial companies whose resolved owner is the Area; office/commercial companies remain excluded.
- Daily request = eligible company count × configured per-company amount, saturating.
- Actual receiver credit total must be <= the Area Finance settled `IndustrialSupport` amount for that Area.
- Preserve the current city-money rule: negative balances still incur expenses; partial/zero settlement occurs at the native `-PlayerMoney.kMaxMoney` saturation boundary, not at a balance of zero.
- Receiver collection covers all eligible companies through the existing bounded observation path; the 64-row UI ownership candidate list is not a payment roster. Never add a second whole-world scan or increase the shared per-update read budget.
- At most one outstanding support request per Area; snapshot the setting with the roster, and do not schedule another day until that request completes. No catch-up payouts for days skipped while inactive, incomplete or loading.
- Divide the settled amount evenly among revalidated snapshot receivers in ascending Entity Index/Version order, giving integer remainders to earlier receivers. Each receiver's allocation is capped at the snapshotted per-company amount; deleted/ineligible receivers are never replaced by newly appearing companies. Any excess is unallocated.
- Native receiver Money saturation is accounted from the actual buffer increase, never the requested addition. Completed payouts obey `settled = credited + unallocated`; no refunds or replay.
- If receivers disappear before settlement, distribute only to remaining live eligible receivers; undistributable settled money is recorded as unallocated support, never replayed.
- No native tax setters, profitability writes, demand mutation or production mutation.
- Use only a verified public 1.6.2f1 resource-money write path; if the helper contract differs, stop and document the blocker rather than guess.

## Review Focus

- Native lower money bound permits half the request: with unchanged receivers and sufficient native receiver headroom, credits total exactly the settled half. A negative treasury away from the bound pays the full request.
- Company deletion between request and settlement: no exception/replay; unallocated remainder is recorded.
- Save after city debit but before pending receiver job completes: save flush completes credit/ledger exactly once.
- Load on the same in-game day: LastScheduledDay prevents duplicate scheduling.
- B2 owner changes before next day: next payout follows the new resolved owner only.

---

### Task 0: Completion notification and finance lifecycle prerequisite

**Files:**
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceRuntime.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceSaveSystem.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceRuntimeTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportSaveBoundaryTests.cs`

**Interfaces:**
- Add `IAreaFinanceRequestListener.OnAreaFinanceCompleted(AreaFinanceBatch requested, AreaFinanceBatch settled, uint frame)` and `AddRequestListener` / `RemoveRequestListener` to the runtime/system. Preserve existing `IAreaFinanceListener` behaviour.
- Notify the requested batch exactly once on both `CompleteApply` and `AbortApply`; aborted/zero amounts have an empty settled batch. Requested entries identify all completed Area/kind pairs even when positive settlement omitted them.
- Module-local pending maps are keyed by load generation + Area StableId + kind, with at most one outstanding request per pair; no general persistent transaction framework is needed.
- At save, Area Finance flush runs before support receiver flush, which runs before `SerializerSystem`. At preload/world teardown, complete and account already-applied old-city batches and receiver jobs against their original generation before reset/cleanup; cancel unapplied work without charging or replay. Never resolve old Entities against the next city or make boundary completion conditional on new-city `Ready`.

- [ ] RED tests: zero settlement, aborted scheduling, mixed-kind partial batch, duplicate completion, listener isolation and old-city applied debit before reset.
- [ ] Implement this minimal extension and pin save/preload ordering; retain the existing policy/Park finance contracts.
- [ ] GREEN and commit: `feat(finance): report completed area requests including zero outcomes`.

### Task 1: Support state, finance kind and pure allocation

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportState.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportModel.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceKind.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceAccounts.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaBudgetViewModel.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceModelTests.cs`

**Interfaces:**
- `IndustrialSupportState : IComponentData, ISerializable`: schema, DailySupportPerCompany, LastScheduledDay, LifetimeRequested, LifetimeSettled, LifetimeCredited, LifetimeUnallocated.
- Pure allocation follows the cap/remainder rules above; use saturating counters and a schema/default that cannot confuse an old save or day zero with an already-paid day.
- Add the new expense to `AreaManagement`, budget totals/details, localization and fixed-kind guards; keep existing enum numeric values unchanged and reserve a distinct value for C5 `ParkEvent`.

- [ ] RED tests for saturation, partial settlement and zero receivers.
- [ ] Implement state/model + finance kind.
- [ ] GREEN and commit: `feat(industrial): define area support payments`

### Task 2: Daily scheduling through Area Finance

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialSupportSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSupportSystemTests.cs`

**Interfaces:**
- One request per Area/day when Active, setting > 0, and at least one eligible resolved company.
- Snapshot receiver Entity list and accepted-request correlation in runtime-only pending state; persistent state stores only dedupe/accounting values.
- Collect a complete deduplicated roster during existing Industrial observation, independently of UI truncation. Stamp it with load/membership/Area assignment revisions. Revalidate current ownership before scheduling; partial or stale evidence queues nothing. Commit `LastScheduledDay` only after `EnqueueExpense` accepts the request.
- Daily zero/inactive/partial cases may become eligible later that same day; once a request is accepted, same-day failure or zero settlement does not trigger an automatic retry.
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
- `IndustrialSupportSystem : IAreaFinanceRequestListener` consumes completed requested `IndustrialSupport` entries, including zero/aborted outcomes, and clears pending state exactly once.
- Revalidate receivers with B2 ownership before credit.
- Schedule/perform verified `Resource.Money` additions with proper vanilla job dependencies.
- Save system flushes any settled-but-not-yet-credited work before serializer.
- Consume each completion before exposing another request; isolate per-receiver failures, account unapplied amounts as unallocated and continue other receivers. Complete/record a job that actually ran even if registration failed; never queue its credit again. Pin these cases with regression tests.
- Register support + its save flush as one finance-dependent optional module; registration failure disables support while Industrial observation/Core/Park remain usable. Do not silently allow a slider to accept unsupported support requests.

- [ ] RED partial/deleted/save tests, including >64 companies, moved/manual owners, receiver Money headroom, same-day load, empty settlement and rejected enqueue.
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
