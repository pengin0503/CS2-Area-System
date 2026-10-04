# Phase 2 Program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Phase 2 as four sequential, independently reviewable features: B2 manual Industrial company ownership, C4 Industrial support payments, C5 Park internal events, and C6 the bounded Park coverage modifier.

**Architecture:** Phase 2 builds on execution-time latest `main`. B2 establishes the company-owner resolver consumed by C4; C5 stays Park-internal; C6 is last because it writes vanilla coverage state and therefore requires verified API contracts and runtime proof before release.

**Tech Stack:** C# 9, Unity ECS, CS2 1.6.2f1 managed APIs, Area Finance, Phase 1 semantic registry/history infrastructure, NUnit/NUnitLite, React/TypeScript/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md` and `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md`

## Global Constraints

- Execution starts only after PR #82 is merged (or from an equivalent descendant containing all Foundation + Phase 1 commits).
- At execution start, latest `main` is the source of truth; never reset newer work to the planning baseline.
- No Harmony/ExtraLib dependency, no pathfinding interception, no new Resource enum, no global economy rebalance.
- Existing `EffectUnavailable` policies stay unavailable unless their own verified adapter requirements are separately satisfied.
- Core building membership remains geometric and building-only.
- Manual Industrial assignment changes company ownership only; it never moves a building between Areas.
- Every real-money operation settles through Area Finance and uses the amount actually applied to city money.
- Save/load must not replay assignment, support, event expense or vanilla modifier work.
- Foundation/Phase 1 runtime verification remains required and must not be claimed PASS from static/API tests.

## Review Focus

- A company appears in two persisted manual-assignment buffers: exactly one deterministic owner wins and the loser is inert/repairable.
- Native city-money saturation permits only part of the support request: companies receive no more than the Area Finance settled amount. A negative treasury alone never blocks spending.
- Save occurs between support/event request and settlement: no duplicate charge or duplicate receiver credit after load.
- Park event ends while the Park is Suspended/deleted: all event-owned runtime effects stop without corrupting persistent history.
- Coverage writer ordering changes or native component disappears: modifier disables safely and restores neutral behavior instead of writing stale values.

---

## Execution order

### Review baseline — 2026-10-03

- Reviewed `main`: `13d4a0a9de6fe2fd62ad47617f91d2fc25bb9f47` (informational, never a reset target).
- Foundation, Phase 1 and B2, including the PR #83 follow-up fixes, are present in this baseline. B2 product code and its execution record take precedence over its unchecked historical plan steps; do not implement B2 again.
- Remaining implementation order is **C4 → C5 → C6**. Phase 3 dedicated `AS_` HQ/Gate content and Tourism remain separate.
- Use Native `superpowers:executing-plans` for the Opus handoff. Read this program, the current feature plan and its spec sections; read other plans only at the next feature boundary.
- The old `usage-constrained-phase2-b2` plan applied to a finished B2-only run; its stop rule does not govern this run.
- C4 owns the minimal Area Finance completion/save-boundary extension also consumed by C5. Do not start C5 against the old positive-only settlement callback.
- Keep one feature branch/PR per feature. Finish the C4 branch first, then base C5 on its verified commit; base C6 on the verified C5 commit. Later PRs may initially target their predecessor branch and must be retargeted to `main` after that predecessor merges. Do not auto-merge or mark implementation merged merely because a PR exists.
- C6 may finish this handoff as **BLOCKED** (API/baseline contract unsupported) or **IMPLEMENTED, DISABLED, RUNTIME NOT RUN**. Neither state means Phase 2 gameplay is complete.

1. **B2 manual Industrial company assignment**
   - Plan: `docs/superpowers/plans/2026-10-02-phase2-industrial-company-assignment-implementation.md`
   - Produces the resolved company-owner service used by C4.
   - Already implemented in the review baseline; validate its presence and consume it.
2. **C4 Industrial subsidies/support**
   - Plan: `docs/superpowers/plans/2026-10-02-phase2-industrial-subsidies-implementation.md`
3. **C5 Park internal events/festivals**
   - Plan: `docs/superpowers/plans/2026-10-02-phase2-park-events-implementation.md`
4. **C6 Park coverage operating modifier**
   - Plan: `docs/superpowers/plans/2026-10-02-phase2-park-coverage-modifier-implementation.md`

Each feature should use its own branch/PR. Do not combine C6 with another feature: its runtime-risk and rollback boundary are intentionally isolated.

## Phase 2 completion gate

- [ ] All four feature PRs merged to latest `main`.
- [ ] Full C# / UI / guard suites pass after the final merge.
- [ ] Runtime checklist contains separate cases for B2/C4/C5/C6.
- [ ] C6 remains unreleased/disabled if its live-game writer ordering and restoration tests are not proven.
- [ ] Update the roadmap to mark Phase 2 complete only after those gates.

## Automated verification at each feature boundary

- Focused RED → GREEN tests per task; update serialized-state tests, localization, generated fixtures and fixed finance-kind contracts with the feature that changes them.
- `pwsh -NoProfile -File scripts/verify.ps1 -ApiCheck` with the supplied 1.6.2f1 managed/toolchain paths configured; `npm ci` in `UI` if dependencies are absent.
- `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` and `git diff --check`.
- If the environment cannot execute VSTest, use the documented NUnitLite route on the same compiled test assembly and report the exact commands/counts; a successful test-assembly build is not a test run.
- Update each module execution record with base SHA, task commits, actual automated results and remaining runtime checks. Official Unity postprocessing/deployment and game checks stay **NOT RUN** unless actually performed.

## Completion update — 2026-10-04

- B2, C4 and C5 are merged to `main`; C6 implementation is merged but was intentionally runtime-gated.
- The user reports every current game-side verification item, including C601–C606, completed on Cities: Skylines II 1.6.2f1 and behaved normally.
- Runtime-confirmation Issues #30, #38, #43, #44, #50, #53, #54 and #55 were closed as completed. Performance Issues remain independent follow-up work.
- The Phase 3 readiness branch opens the verified C6 gate and adds a live Options off switch. Its GitHub Offline verification passed after an observed contract-test RED→GREEN cycle.
- Once the readiness branch is merged to latest `main`, Phase 2 satisfies the prerequisite for `docs/superpowers/plans/2026-10-02-phase3-program-implementation.md`. Phase 3 product implementation itself is not part of this readiness change.
