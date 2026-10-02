# Phase 2 Program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Phase 2 as four sequential, independently reviewable features: B2 manual Industrial company ownership, C4 Industrial support payments, C5 Park internal events, and C6 the bounded Park coverage modifier.

**Architecture:** Phase 2 builds on Foundation + Phase 1 from PR #82 HEAD `229ba45283f3f75806bfcc2438e6c9ab65602c58`. B2 establishes the company-owner resolver consumed by C4; C5 stays Park-internal; C6 is last because it writes verified vanilla coverage state and therefore requires runtime proof before release.

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
- City money is only partially available for support: companies receive no more than the Area Finance settled amount.
- Save occurs between support/event request and settlement: no duplicate charge or duplicate receiver credit after load.
- Park event ends while the Park is Suspended/deleted: all event-owned runtime effects stop without corrupting persistent history.
- Coverage writer ordering changes or native component disappears: modifier disables safely and restores neutral behavior instead of writing stale values.

---

## Execution order

1. **B2 manual Industrial company assignment**
   - Plan: `docs/superpowers/plans/2026-10-02-phase2-industrial-company-assignment-implementation.md`
   - Produces the resolved company-owner service used by C4.
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
