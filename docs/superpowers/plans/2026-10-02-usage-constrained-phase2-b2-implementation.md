# Usage-Constrained Phase 2 B2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Do not use subagent-driven-development for this run. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete Phase 2 B2 manual Industrial company assignment within a constrained model-usage budget, leaving enough quota for one whole-branch review and fixes.

**Architecture:** Execute the already-approved B2 plan without redesign. Persist manual company assignments on each Industrial Area, rebuild a runtime reverse index, resolve manual company ownership ahead of geometric primary ownership, integrate that resolver into the existing bounded observation path, and expose guarded assignment/removal actions in the Industrial UI.

**Tech Stack:** C# 9, Unity ECS, CS2 1.6.2f1 managed APIs, Phase 1 AreaMembershipIndex/semantic registry, NUnit/NUnitLite, React/TypeScript/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §B2

**Detailed implementation source:** `docs/superpowers/plans/2026-10-02-phase2-industrial-company-assignment-implementation.md`

## Usage Budget Policy

- This run implements **B2 only**.
- Do not start C4, C5, C6, Phase 3 or Tourism.
- Use Native execution with `superpowers:executing-plans`; do not spawn implementation/reviewer subagents per task.
- Read the spec and detailed B2 plan once at the start. Re-open only exact sections/files needed for a current task.
- Use the supplied 1.6.2f1 ZIPs only when an exact game API is uncertain. Do not scan all archives proactively.
- Prefer existing repository abstractions and tests over new general frameworks.
- Run focused tests after each task; run the complete suite only at the final verification gate.
- Commit every completed task immediately. Never begin the next task with uncommitted working changes.
- Perform exactly one whole-branch self-review after Task 5. Fix concrete findings, rerun affected tests, then run final verification.
- If work must stop unexpectedly, leave the branch in a committed state and create/update a concise checkpoint file before doing any further exploratory work.

## Baseline

- Work from the latest `main`.
- At plan creation, latest `main` is `f9a79e954f86f2fb24fc087d890946fe2f5edba3`.
- That SHA is informational only; execution-time latest `main` is authoritative.
- Foundation + Phase 1 are already merged and must not be rolled back.

## Global Constraints

- One live company has at most one effective manual Industrial owner.
- Manual assignment changes company ownership only; Core building membership and building primary ownership remain geometric.
- Only eligible Industrial companies may be assigned; offices/commercial companies remain excluded.
- A manually assigned company's rented building must currently belong geometrically to the target Industrial Area, including a non-primary overlap relation.
- Authoritative assignment state is persisted through game-supported Entity reference serialization; reverse indexes and candidate lists are runtime-only.
- Duplicate valid persisted claims resolve deterministically to the lowest Area StableId.
- Company move/delete/reuse invalidates stale assignment/action state before observation applies it.
- Assignment-index revision invalidates/restarts an in-flight observation cycle.
- Do not add a second whole-world company/building scan.
- No Harmony/ExtraLib, pathfinding mutation, economy mutation, tax changes or policy availability changes.

## Review Focus

- Duplicate saved claims never double-count one company.
- A company moving to another rented building loses an invalid manual assignment before the next completed observation.
- A manual assignment in an overlapping non-primary building includes only that company, not unrelated renters.
- A stale UI company key cannot target a reused Entity.
- Load rebuild finishes before observations consume manual ownership.

---

### Task 1 — Persistent assignment state and reverse index

Implement Task 1 from the detailed B2 plan exactly.

- [ ] Add RED serialization, duplicate-claim and load-rebuild tests.
- [ ] Implement `IndustrialCompanyAssignment` and `IndustrialCompanyAssignmentIndex`.
- [ ] Run only the focused assignment/serialization tests.
- [ ] Commit: `feat(industrial): persist manual company ownership`

**Checkpoint condition:** branch must be clean before Task 2.

### Task 2 — Company owner resolver

Implement Task 2 from the detailed B2 plan exactly.

- [ ] Add RED manual override, office/commercial rejection, moved-renter and geometric fallback tests.
- [ ] Implement `IndustrialCompanyOwnership.Resolve(...)`.
- [ ] Run only ownership + directly affected observation-contract tests.
- [ ] Commit: `feat(industrial): resolve manual company ownership`

**Durable midpoint:** Tasks 1–2 form a coherent committed ownership foundation. If the session becomes constrained, preserve this branch; do not start C4 or unrelated work.

### Task 3 — Bounded observation integration

Implement Task 3 from the detailed B2 plan.

- [ ] Add RED overlap, generation-restart and work-bound tests.
- [ ] Integrate resolved ownership into the existing observation cycle without a second full scan.
- [ ] Verify unrelated renters in an overlapping building are not stolen.
- [ ] Run focused Industrial observation/performance tests.
- [ ] Commit: `feat(industrial): observe resolved company owners`

### Task 4 — Guarded ownership UI

Implement Task 4 from the detailed B2 plan.

- [ ] Publish at most 64 current ownership candidates.
- [ ] Add `assignCompanyHere` and `clearCompanyAssignment` with current-snapshot/stable-ID/live-eligibility revalidation.
- [ ] Add the Companies/Ownership panel UI and stale-key tests.
- [ ] Run only affected C# and UI tests.
- [ ] Commit: `feat(industrial): manage company ownership from area panel`

### Task 5 — Migration, guards and final verification

Implement Task 5 from the detailed B2 plan.

- [ ] Update offline guards, runtime smoke checklist and Industrial execution record.
- [ ] Run one whole-branch self-review against §B2 and the five Review Focus cases.
- [ ] Fix only concrete B2 regressions/findings; do not expand scope.
- [ ] Run full C# tests with API check.
- [ ] Run Release API build.
- [ ] Run UI tests/build.
- [ ] Run offline/source guards and `git diff --check`.
- [ ] Record game/runtime checks as NOT RUN unless actually performed.
- [ ] Commit: `docs: verify manual industrial company ownership`

## Git / PR

- Create one feature branch from execution-time latest `main`.
- Keep the five task commits separate.
- Open one PR against latest `main`.
- Do not auto-merge.
- PR body must include base SHA, task commits, test/build/guard results, runtime NOT RUN items, and explicit statement that C4/C5/C6/Phase 3/Tourism are excluded.

## Stop Rule

After the B2 PR is open and verified, **stop implementation even if some weekly usage remains**. Reserve remaining usage for PR review findings, merge conflicts, or targeted fixes. Do not begin C4 in the same weekly-budget run.
