# Post-B2 code audit fixes implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Native execution, no task subagents. The user requested immediate issue creation and repair; no separate plan approval is required.

**Goal:** Repair confirmed Issues #84 and #85 after merging PR #83.

**Architecture:** Keep the existing bounded candidate set and exact saved-claim removal guard. Publish live-revalidated ownership candidates with consumption time while preserving observation cycle identities and aggregation stamps.

**Tech Stack:** C# 9, Unity ECS / Cities: Skylines II 1.6.2f1, NUnit/NUnitLite, Node source contracts.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §B2 and GitHub Issues #84 / #85.

**Base:** latest main after PR #83 merge: `40c3057eed612192f6a4edf43b2f1f98f86b3d9f`, verified exact commit/tree locally and on GitHub.

## Global constraints

- Existing features only; no C4/C5/C6, Phase 3 or Tourism.
- No Core Building membership/primary, economy/tax/pathfinding/policy/resource-enum changes; no Harmony/ExtraLib.
- Preserve 64 candidate rows, 128 shared reads and exact Area/Company/Building/load/hour/revision stale guards; no second whole-world scan.
- Each issue: RED → minimal implementation → focused checks → commit. Whole-branch author review once; full suites only at the final gate.
- Standard VSTest launcher failed in this environment in the B2 run; execute compiled NUnit assembly through the temporary NUnitLite runner if still blocked, without repo dependency changes. Record exact command results honestly; game checks NOT RUN.

## Review focus

- Null Company is removable only as an exact saved invalid claim; normal Null observations never appear as assignable candidates.
- Null Company and/or Building must not permit assigning/reusing another live Entity.
- Candidate publication uses the current consumed hour, independent of cycle start hour; old UI key expires next hour.
- Aggregation CycleId/HourBucket remains unchanged even when ownership UI candidates use newer current time.
- Area switching, duplicate claims, capacity, native selection and automatic-owner observations preserve existing guards.

### Task 1 — #84 saved invalid Null claim cleanup

**Files:** `IndustrialCompanyAssignmentActions.cs`, `IndustrialCompanyAssignmentActionTests.cs` under their existing source/test directories.
**Interfaces:** Keep `Record(Entity, Entity, IndustrialCompanyOwnerResolution, string, bool hasClaimHere=false, bool invalid=false)` and `TryApply` signatures.

- [ ] Add `NullCompanySavedClaimIsVisibleRemovableAndNeverAssignable`: ordinary Null is skipped; saved Null claim gets one invalid row; assign rejects, wrong Area rejects, exact clear removes only the selected saved claim; other company and other Area claims survive.
- [ ] Build API-check tests; run that method alone. Expected RED: saved row count is 0 rather than 1.
- [ ] Allow Null only when `hasClaimHere`, force invalid state, preserve exact claim removal/action guards and capacity.
- [ ] Run assignment actions/index/manual ownership/panel focused tests; expected zero failures.
- [ ] Commit `fix(industrial): expose null saved company claims for removal`.

### Task 2 — #85 consumption-hour candidate publication

**Files:** `src/CS2AreaSystem/Modules/Industrial/IndustrialObservationSystem.cs`, `scripts/test-offline-contracts.mjs`.
**Interfaces:** Private `Consume(long currentHourBucket)` is called from OnUpdate with its current `bucket`; `ObserveCompanyCandidate` gets that same hour. Cycle/request identity and publication stamps remain untouched.

- [ ] Add a Native wiring contract requiring current-bucket Consume and prohibiting candidate publication with `cycle.HourBucket`; run offline contracts. Expected RED on original wiring.
- [ ] Pass current bucket into Consume and only the candidate publication path; keep cycle/token checks and aggregator state.
- [ ] Run offline contracts plus candidate/action/manual/reader/observation focused tests. Existing hour-key test must still reject old keys.
- [ ] Commit `fix(industrial): publish company candidates at consumption time`.

### Final gate / publication

- [ ] One focused branch review; fix concrete regressions only.
- [ ] One full C# suite, Release API build, UI tests/build, offline/source guards and whole-branch diff check. No runtime claims.
- [ ] Update execution record, preserve RED/GREEN evidence and rulings.
- [ ] Publish a follow-up PR targeting latest main with `Fixes #84` / `Fixes #85`. Do not merge it without user instruction; existing runtime-only issues remain open.
