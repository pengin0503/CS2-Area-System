# Phase 2 C5 Park Internal Events Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a mod-owned Park event loop that changes only Park Area gameplay, finance and progression inputs without changing vanilla destination choice or attractiveness.

**Architecture:** Introduce a small event catalog and one active-event state per Park. Starting an event queues its real Area Finance cost; the event becomes active only after the expense settles. Active events apply bounded modifiers to Park-internal satisfaction/capacity/XP inputs and record a compact history.

**Tech Stack:** C# 9, Unity ECS, Area Finance listener, Park runtime/quality/progression, existing panel/action contracts, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §C5

## Global Constraints

- No vanilla destination-choice, attractiveness, citizen happiness, land value or demand writes.
- One active event per Park.
- Requires C4's requested/settled completion callback and finance boundary contracts; zero/aborted completion clears the pending event without starting it or charging again.
- Initial catalog contains one event: **Park Festival**.
- Park Festival duration: a full 262,144 simulation frames from positive settlement, not merely until the next midnight. Use the existing frames-per-day constant; persist StartFrame and EndFrameExclusive and use unsigned elapsed-frame arithmetic across frame-counter wrap.
- Requested event expense = max(1, round(50% of the Park's current daily management cost)), using round-to-even integer currency. Snapshot that cost at accepted start; an unavailable/stale cost estimate disables start until rebuilt.
- Internal effects while active: comfortable-capacity share ×0.85, completed-visit satisfaction sample +5 (clamped 0..100), visit XP ×1.25. Existing visit XP has saturation but no daily cap; preserve that behaviour and the separate passive XP daily cap. Do not apply the festival multiplier to passive XP or add a new cap.
- Event starts only after its Area Finance expense is actually settled; partial positive settlement starts the event and records the actual paid cost.
- Suspended Park has neutral effects while time continues; reactivation resumes only within the original interval. Area deletion clears pending/runtime state; history remains only while its owning Area exists.
- No automatic recurrence in Phase 2.

## Review Focus

- Start clicked twice while payment pending: one request only.
- Native lower city-money bound permits zero event expense: pending clears, no event starts and no history entry is created; negative money away from that bound still permits payment.
- Park suspends mid-event: effects stop immediately, but calendar time continues; the event still expires at its saved EndFrameExclusive.
- Save/load during pending or active event: cost/history finalize once at the boundary, or an unpaid request clears; active end frame and paid amount survive with no second charge.
- Event modifiers compose with existing policies without exceeding capacity/satisfaction/XP clamps.

---

### Task 1: Event catalog/state/model

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventCatalog.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventState.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventModel.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventHistoryEntry.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkEventModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkSerializationTests.cs`

**Interfaces:**
- Stable event ID `ParkFestival = 1`.
- `ParkEventState : IComponentData, ISerializable`: schema, ActiveEventId, StartFrame, EndFrameExclusive, LifetimeEvents, LifetimeEventExpense. PendingEventId belongs to the generation-scoped runtime pending map, not authoritative save data.
- `ParkEventHistoryEntry : IBufferElementData, ISerializable`: event ID, StartFrame, EndFrameExclusive, settled expense; retain the newest 16 entries and persist them because the Events tab exposes history after load.
- Unknown event IDs disable effects; old saves without event components receive neutral defaults. Expiry is determined on read as well as on the event update, so a stale snapshot cannot extend effects.

- [ ] RED catalog/duration/composition/serialization tests.
- [ ] Implement model/state.
- [ ] GREEN and commit: `feat(park): define internal park events`

### Task 2: Event finance start/settlement

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceKind.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceAccounts.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaBudgetViewModel.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventSaveSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkEventFinanceTests.cs`

**Interfaces:**
- Add expense `AreaFinanceKind.ParkEvent`.
- `RequestStart(area,eventId)` sets pending only after eligibility validation and queues exactly one expense.
- Completion listener consumes only requested ParkEvent entries matched to the current generation/Area pending map. Positive settlement starts only a still-existing eligible Active Park; if eligibility was lost, record the settled expense without starting effects/history. Zero/aborted outcome clears pending without history or automatic retry.
- A save flush settles queued finance, then finalizes event state before `SerializerSystem`. Preload completes old-city outcomes before reset; new city/load never requeues a pending event. Group event + save registration as a finance-dependent optional module and roll back partial registration without disabling base Park gameplay.
- New expense belongs to Area Management totals/details and Park recent expense/trends exactly once; existing management expense listeners must not also count it. Preserve enum values and existing accounts.

- [ ] RED pending/double-click/zero/abort/save-load/suspension/removed-Area tests and finance budget/registration-order tests.
- [ ] Implement finance lifecycle.
- [ ] GREEN and commit: `feat(park): settle event start through area finance`

### Task 3: Apply Park-internal effects

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualityModel.cs` (contains `ParkQualityEvaluator`)
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkVisitModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkVisitSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkProgressionSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkEventEffectTests.cs`

**Interfaces:**
- `ParkEventEffects` is a pure input object; no post-hoc Quality rewrite.
- Capacity, satisfaction sample and visit XP modifiers compose through existing model inputs and clamps.
- Compute capacity from `policy.CapacityShare × event.CapacityShare` once. Recompute/neutralize event-owned derived values on start, expiry and lifecycle changes instead of retaining the previous event snapshot.
- Apply +5 once to a completed visit with `HasSatisfaction=true`, before that same adjusted event reaches both `RecordCompletedVisit` and `IParkVisitListener.OnVisitCompleted`; unavailable samples remain unavailable. Restored/completed visits cannot receive the bonus twice.
- Apply ×1.25 once in the visit XP award together with the existing policy multiplier; do not feed it into `ParkProgressionModel.Step`'s passive award.

- [ ] RED active/suspended/policy-composition tests plus near-midnight duration, frame wrap, missing satisfaction, shared adjusted completion, expired cached Quality and unchanged passive XP cap.
- [ ] Implement effects.
- [ ] GREEN and commit: `feat(park): apply internal festival effects`

### Task 4: Panel/actions/history and verification

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkLocalization.cs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Test: `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- Test: `UI/src/ModulePanel.test.tsx`

- [ ] Add Events tab/status/start button/cost estimate/history.
- [ ] Stale action and active/pending guards.
- [ ] Full verification; ensure no vanilla attraction/pathfinding writes.
- [ ] Commit: `feat(park): manage park festivals from area panel`
