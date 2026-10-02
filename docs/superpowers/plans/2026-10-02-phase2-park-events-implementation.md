# Phase 2 C5 Park Internal Events Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a mod-owned Park event loop that changes only Park Area gameplay, finance and progression inputs without changing vanilla destination choice or attractiveness.

**Architecture:** Introduce a small event catalog and one active-event state per Park. Starting an event queues its real Area Finance cost; the event becomes active only after the expense settles. Active events apply bounded modifiers to Park-internal satisfaction/capacity/XP inputs and record a compact history.

**Tech Stack:** C# 9, Unity ECS, Area Finance listener, Park runtime/quality/progression, existing panel/action contracts, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §C5

## Global Constraints

- No vanilla destination-choice, attractiveness, citizen happiness, land value or demand writes.
- One active event per Park.
- Initial catalog contains one event: **Park Festival**.
- Park Festival duration: 1 in-game day.
- Requested event expense = max(1, round(50% of the Park's current daily management cost)).
- Internal effects while active: comfortable-capacity share ×0.85, completed-visit satisfaction sample +5 (clamped 0..100), visit XP ×1.25 before existing daily caps.
- Event starts only after its Area Finance expense is actually settled; partial positive settlement starts the event and records the actual paid cost.
- Suspended/deleted Park stops active effects immediately; earned history remains.
- No automatic recurrence in Phase 2.

## Review Focus

- Start clicked twice while payment pending: one request only.
- City cannot pay event cost: no event starts and no history entry is created.
- Park suspends mid-event: effects stop immediately, but calendar time continues; the event still expires at its saved EndDayExclusive.
- Save/load during active event: end frame/day and paid amount survive; no second charge.
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
- `ParkEventState : IComponentData, ISerializable`: schema, ActiveEventId, StartDay, EndDayExclusive, PendingEventId, LifetimeEvents, LifetimeEventExpense.
- `ParkEventHistoryEntry : IBufferElementData, ISerializable`: event ID, StartDay, EndDayExclusive, settled expense; retain the newest 16 entries and persist them because the Events tab exposes history after load.

- [ ] RED catalog/duration/composition/serialization tests.
- [ ] Implement model/state.
- [ ] GREEN and commit: `feat(park): define internal park events`

### Task 2: Event finance start/settlement

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkEventSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceKind.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkEventFinanceTests.cs`

**Interfaces:**
- Add expense `AreaFinanceKind.ParkEvent`.
- `RequestStart(area,eventId)` sets pending only after eligibility validation and queues exactly one expense.
- Finance listener activates only on a positive settled ParkEvent entry for that Area.

- [ ] RED pending/double-click/zero-settlement tests.
- [ ] Implement finance lifecycle.
- [ ] GREEN and commit: `feat(park): settle event start through area finance`

### Task 3: Apply Park-internal effects

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualityEvaluator.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkVisitModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkProgressionSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkRuntimeSnapshot.cs`
- Test: `tests/CS2AreaSystem.Tests/ParkEventEffectTests.cs`

**Interfaces:**
- `ParkEventEffects` is a pure input object; no post-hoc Quality rewrite.
- Capacity, satisfaction sample and visit XP modifiers compose through existing model inputs and clamps.

- [ ] RED active/suspended/policy-composition tests.
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
