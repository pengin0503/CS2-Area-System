# Scoped issue regressions implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Implement inline; one fresh whole-branch review at the end.

**Goal:** Fix only open #84, #85, #91, #92, #93, #94 and #95 against latest main.

**Architecture:** Preserve bounded Industrial ownership observations and finance identities. Refresh Festival costs through the Park management calculation and carry causal simulation frames through settlement and visit effects. Publish toolbar types only after prefab readiness.

**Tech Stack:** C# 9, Unity ECS / CS2 1.6.2f1, NUnit/NUnitLite, React/Vitest, PowerShell contracts.

**Spec:** User's issue-specific requirements; existing `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` for B2/C4/C5 context.

**Base:** Remote main `903683771c566b6a8d05e6000d5a8bb5a113601e`, obtained at start and restored with identical commit and tree. No merge of PR #86.

## Global constraints

- Only the seven named Issues; no gameplay-confirmation or performance Issue changes.
- Preserve Core Building membership/primary, 64 candidate rows, 128 shared reads, load/hour/revision/entity/renter guards and no additional world scan.
- Ordinary Null Company observations stay excluded; only exact saved invalid claims may be removed.
- Each Issue: confirm root cause on main, RED, minimal fix, focused GREEN, separate commit.
- No save-schema change, official deployment, game test or automatic merge. One PR.
- Requested commands must be attempted and failures disclosed; use the previously documented temporary NUnitLite fallback if VSTest cannot execute.

## Review focus

- Missing references versus live Entity reuse: removal cannot mutate another claim.
- Candidate consumption time versus cycle identity: newer UI keys must not alter support roster stamps.
- Current budget/policy/runtime inputs versus cached costs: panel and start must share computation without enqueue side effects.
- Delayed settlement and deferred visits: event boundaries and XP must use causal frames, including expiry and historical lookup.
- Registered-but-unready prefabs and toolbar failures: no premature publication; saved Areas still register.

### Task 1: #84 saved invalid claim cleanup

**Files:** `IndustrialCompanyAssignmentActions.cs`, `IndustrialCompanyAssignmentActionTests.cs`.
**Interfaces:** Existing `Record` and `TryApply` unchanged.
- [ ] Import PR #86 Null-company regression only; verify both cases fail with zero rows.
- [ ] Port only saved-claim exception and force invalid state.
- [ ] Run assignment/manual ownership/panel tests, preserving exact cleanup and stale guards; commit #84.

### Task 2: #85 consumption-hour publication

**Files:** `IndustrialObservationSystem.cs`, `scripts/test-offline-contracts.mjs`.
**Interfaces:** `Consume(long currentHourBucket)` receives current OnUpdate bucket; aggregate cycle stamps unchanged.
- [ ] Port PR #86 Native wiring regression; verify old cycle-hour publication fails.
- [ ] Pass current bucket only to candidate publication.
- [ ] Run wiring and ownership/observation focused tests; commit #85.

### Task 3: #91 delayed roster

**Files:** `IndustrialSupportScheduling.cs`, `IndustrialSupportSystemTests.cs`.
**Interfaces:** Existing `Decide` result becomes Stale for complete delayed roster.
- [ ] Add complete-but-delayed regression expecting Stale and no receivers/request; observe RED.
- [ ] Add Delayed to stale predicate.
- [ ] Run all Industrial support tests; commit #91.

### Task 4: #92 current Festival cost

**Files:** `ParkFinanceSystem.cs`, `ParkEventSystem.cs`, `ParkFinanceModel.cs`, Park finance/event tests.
**Interfaces:** Shared live Park management calculation consumed by settlement and `StartInputs`; no money writes during reads.
- [ ] Add budget/policy-change regressions after settlement before next settlement; expect current half-cost despite known stale snapshot. Verify RED.
- [ ] Share current runtime management inputs and calculation between finance and event UI/action.
- [ ] Run Park finance/event/policy tests; commit #92.

### Task 5: #93 settlement processing frame

**Files:** `ParkEventSystem.cs`, Park event finance tests.
**Interfaces:** Finance callback batch frame retains accounting identity; start uses current runtime simulation frame.
- [ ] Add delayed settlement regression expecting full 262144-frame duration from completion and unchanged accounting frame; observe RED.
- [ ] Record start at current simulation frame, keeping expense records tied to finance frame.
- [ ] Run event finance/save tests; commit #93.

### Task 6: #94 visit event frame

**Files:** `ParkVisitModel.cs`, `ParkVisitSystem.cs`, `ParkProgressionSystem.cs`, event/visit tests.
**Interfaces:** Runtime-only `ParkVisitEvent` observation/completion frame; both satisfaction and visit XP consume it.
- [ ] Add Festival start/expiry between observation and dispatch regressions and tracker timestamp cases; verify RED.
- [ ] Carry event frame through all tracker emission paths and effects, retaining historic Festival interval after expiration.
- [ ] Run visit/progression/event focused tests; commit #94.

### Task 7: #95 readiness before toolbar publication

**Files:** `AreaPrefabRegistrationSystem.cs`, Core prefab tests.
**Interfaces:** Relationships prepared before AddPrefab; publication after `IsPrefabReady` succeeds.
- [ ] Add registered-not-ready regression checking no publication/version bump; observe RED.
- [ ] Move publish to readiness-gated path, preserving fallback registration.
- [ ] Run prefab/toolbar/readiness tests; commit #95.

### Final gate

- [ ] One fresh whole-branch review; Critical/Important findings only enter one RED→GREEN fix pass. Minor findings deferred.
- [ ] Attempt requested dotnet test, Release API build, npm.cmd test/build, pwsh verify.ps1 -ApiCheck and git diff --check. Record exact results and any NUnitLite fallback.
- [ ] Commit execution record; publish one PR with issue/commit/test mapping, leaving it unmerged.
