# Phase 2 B2 Manual Industrial Company Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player explicitly assign an eligible Industrial company to one managed Industrial Area, overriding geometric primary ownership for that company only.

**Architecture:** Persist company Entity references in an Industrial-Area-owned buffer and rebuild a runtime reverse index after load. An assignment is valid only while the company is an eligible non-office/non-commercial Industrial company and its rented building is geometrically related to the target Industrial Area. Observation consumes a single resolver: manual assignment wins; otherwise Core primary ownership wins.

**Tech Stack:** C# 9, Unity ECS, Phase 1 AreaMembershipIndex + semantic registry, existing Industrial observation/panel contracts, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §B2

## Global Constraints

- Baseline implementation is PR #82 HEAD or newer.
- One live company has at most one effective manual Industrial owner.
- Explicit ownership never changes Core building membership or the rented building's primary Area.
- Assignment candidates must be Industrial-eligible; offices/commercial companies are rejected.
- A company can only be assigned to an Industrial Area containing its rented building geometrically, including non-primary overlap membership.
- Entity references are serialized with game-supported entity-reference serialization; no raw index/version persistence outside that contract.
- Assignment state is authoritative; reverse indexes/candidate lists are runtime-only.
- All actions use selected Area stable-ID guards and resolve opaque company keys against current candidates.

## Review Focus

- Duplicate persisted assignment to two Areas resolves by lowest Area StableId; loser is shown invalid and may be cleaned, never double-counted.
- Company changes renter/building after assignment: assignment becomes invalid before the next observation owns it.
- Assigned company sits in a non-primary overlapping building: target Area observes that company but does not steal unrelated companies in the same building.
- Company is deleted while UI still has its key: action is rejected without touching another reused Entity.
- Load rebuild completes before observation: no cycle mixes geometric and stale manual ownership.

---

### Task 1: Persistent assignment buffer and reverse index

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialCompanyAssignment.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialCompanyAssignmentIndex.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialCompanyAssignmentTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialSerializationTests.cs`

**Interfaces:**
- `IndustrialCompanyAssignment : IBufferElementData, ISerializable` with schema version and `Entity Company`.
- `IndustrialCompanyAssignmentIndex.Rebuild(...)`, `TryGetOwner(Entity company, out ulong areaId)`, `Revision`, `Clear()`.
- Duplicate valid claims: lowest Area StableId wins deterministically.

- [ ] Write failing serialization/duplicate/load tests.
- [ ] Run focused tests and confirm RED.
- [ ] Implement the buffer/index and load-generation reset.
- [ ] Run focused tests and confirm GREEN.
- [ ] Commit: `feat(industrial): persist manual company ownership`

### Task 2: Eligibility and resolved-owner service

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialCompanyOwnership.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationContracts.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationReader.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialCompanyOwnershipTests.cs`

**Interfaces:**
- `IndustrialCompanyOwnership.Resolve(company, building, geometricRelations, manualIndex, eligibility) -> IndustrialCompanyOwnerResolution`.
- Result includes `AreaStableId`, `Manual`, `Valid`, and invalid reason.
- Manual assignment is valid only when the rented building currently has a geometric relation to that Area and the company remains Industrial-eligible.

- [ ] Write RED tests for manual override, office/commercial rejection, moved renter, and geometric fallback.
- [ ] Implement resolver using typed Phase 1 semantic/classifier data.
- [ ] Confirm GREEN and commit: `feat(industrial): resolve manual company ownership`

### Task 3: Observation integration without stealing building ownership

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationAggregator.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialRuntimePerformanceTests.cs`

**Interfaces:**
- Cycle source includes normal primary buildings plus non-primary member buildings containing companies manually assigned to the Area.
- Company rows are filtered through `IndustrialCompanyOwnership`; unrelated renters in that same building remain owned by their own resolver result.
- Assignment-index revision participates in observation generation/restart.

- [ ] Add RED overlap/restart/performance tests.
- [ ] Implement bounded candidate inclusion and company filtering.
- [ ] Confirm no second whole-world scan and GREEN.
- [ ] Commit: `feat(industrial): observe resolved company owners`

### Task 4: Guarded UI assignment/removal

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSnapshot.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialGameplayFacade.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPanelBuilder.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialLocalization.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- Test: `tests/CS2AreaSystem.Tests/IndustrialCompanyAssignmentActionTests.cs`
- Test: `UI/src/IndustrialPanel.test.tsx`

**Interfaces:**
- Observation publishes a bounded ownership candidate list (max 64 current companies) with opaque company key, display label, auto/manual owner status and eligibility.
- Module actions: `assignCompanyHere`, `clearCompanyAssignment`.
- Handler re-resolves the key against the current snapshot and current renter/building relation before writing the buffer.

- [ ] Add RED stale-key/UI tests.
- [ ] Implement Companies/Ownership tab and actions.
- [ ] Confirm GREEN and commit: `feat(industrial): manage company ownership from area panel`

### Task 5: Migration, guards and verification

**Files:**
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`

- [ ] Full C#, API build, UI, source guards and `git diff --check`.
- [ ] Add runtime cases for overlap override, company move/delete, save/load and bounded profiling.
- [ ] Commit: `docs: verify manual industrial company ownership`
