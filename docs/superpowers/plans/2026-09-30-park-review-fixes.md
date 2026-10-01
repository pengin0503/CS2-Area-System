# Park review fixes implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix issues #31–35 without changing the Park feature scope.
**Architecture:** Keep observation and transfers scheduled. Use pure models for regression tests, invalidate sliced scans on boundary changes, and settle pending finance records before serialization.
**Tech Stack:** C# 9, Unity ECS, game 1.6.2f1 references, NUnit, TypeScript/Vitest.
**Spec:** GitHub issues https://github.com/pengin0503/CS2-Area-System/issues/31 through /35 (accepted by the user's request to fix them).

## Global Constraints

- No Harmony patches or reads of vanilla private event queues.
- Do not add main-thread synchronization per visit or per transaction.
- Preserve save schemas and existing UI contracts.
- Game execution is unavailable here; compile against supplied game DLLs and disclose runtime verification limits.

## Review Focus

- A Leisure target can be a tenant rather than the current building; preserve vanilla provider resolution.
- Boundary edits may retain the same AABB and happen during a sliced scan or while paused.
- Singleton prefab variations must not reset diminishing returns for a dominant prefab.
- Saving may occur between money transfer and the next finance update, including queued admissions.
- Saturated city money and unlimited-money mode must book only real changes in the stored balance.

### Task 1: Per-prefab landscape returns (#33)

**Files:** ParkLandscapeModel.cs, ParkLandscapeClassifier.cs, landscape/quality/panel tests under existing Park paths.
**Interfaces:** Tally.ToInputs produces exact prefab frequencies; EffectiveCount consumes them. Aggregate-only test inputs will be made explicit.
- [x] Write a regression comparing 100 + 19 singleton trees with 19 groups of 6 + one group of 5; expect effective counts 33.7468 and 119.
- [x] Run filtered landscape tests; expect regression FAIL on equal effective counts.
- [x] Store frequencies and sum min(n,5)+sqrt(max(0,n-5)) per prefab. Missing frequencies receive conservative single-prefab credit.
- [x] Run filtered tests; expect PASS; commit.

### Task 2: Boundary revisions (#32)

**Files:** ParkLandscapeClassifier.cs, ParkLandscapeSystem.cs, ParkFacilityClassifierTests.cs.
**Interfaces:** Scheduler.BoundaryChanged(ulong), RevisionOf(ulong), IsCurrent(ulong,ulong); ActiveScan captures the revision.
- [x] Test immediate requeue at the same frame and invalidation of an active revision; ordinary object dirtying must preserve it; forgetting invalidates it.
- [x] Run tests; expect missing API FAIL.
- [x] Increment revisions on area update signals and changed bounds, cancel obsolete/deleted scans before publishing.
- [x] Run scheduler tests; expect PASS; commit.

### Task 3: Resolved leisure observation (#31)

**Files:** ParkVisitModel.cs, ParkVisitSystem.cs, ParkVisitModelTests.cs.
**Interfaces:** ParkLeisureObservation.TryResolveProvider resolves tenant/current-building provider from typed facts and a non-null Leisure target.
- [x] Test null target and absent provider rejection, valid tenant resolution, building fallback despite a distinct destination, inactive/unavailable provider rejection.
- [x] Run tests; expect missing API FAIL.
- [x] Read Leisure plus public provider components in ObserveJob; admit only resolved usable providers. Keep scheduling and target membership map.
- [x] Run visit tests and API compile; expect PASS; commit.

### Task 4: Saturating finance (#35)

**Files:** ParkFinanceModel.cs, ParkFinanceSystem.cs, ParkTransactions.cs, finance/transaction tests.
**Interfaces:** Collectible admission accepts city stored balance; management amount is capped by the lower bound. Ledger.Resolve consumes actual long managementPaid instead of bool.
- [x] Test city 1,999,999,990 fee 60 transfers 10; lower balance -1,999,999,990 pays 10 of 60; test sequential admissions, zero capacity, full-range expense and unlimited flag preservation using actual PlayerMoney.Add.
- [x] Run tests; expect missing API FAIL.
- [x] Bound transfers before household debit; apply large expenses in safe int chunks; record actual expense with deterministic FIFO allocation.
- [x] Run finance/transaction tests; expect PASS; commit.

### Task 5: Save finance boundary (#34)

**Files:** ParkFinanceSaveBoundary.cs (new), ParkFinanceSystem.cs, Mod.cs, ParkFinanceSaveBoundaryTests.cs (new).
**Interfaces:** Save boundary completes/records the existing batch, schedules queued requests, then completes/records them; gated by current ready load generation.
- [x] Test real ledger and serialized ParkAreaState match transferred money before the next simulation update, repeated saves do not duplicate counters, queued and completed batches both record, stale load generation performs no transfer.
- [x] Run tests; expect missing API FAIL.
- [x] Register a save system before Game.Serialization.SerializerSystem in Serialize phase; invoke save-only draining without generating new management charges.
- [x] Run save tests; expect PASS; commit.

## Final verification

- [x] Run scripts/verify.ps1 -ApiCheck, complete C#/UI suites and builds; expected exit 0.
- [x] Review the complete branch with a fresh reviewer, fix important findings with regressions, then rerun affected and complete suites.
Integration: recheck remote main, create a feature branch/commit with remote ancestry, and open a PR referencing all five issues. Do not merge without authorization.

## Execution record (2026-10-01)

- All five tasks completed with regressions. Prefab regression failed numerically before the fix; newly introduced model interfaces failed before implementation.
- Full `scripts/verify.ps1 -ApiCheck`: 272 C# tests, 52 UI tests, source guards, offline contracts, target evaluation, C# build and UI build passed.
- Additional .NET Framework 4.8 API compile: `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -p:TargetFramework=net48 -p:TargetFrameworks=net48`. This checks supplied game APIs; it does not perform Unity postprocessing or produce a verified playable mod.
- Fresh read-only review found one Important issue: save draining outside FinanceSystem.Update could reuse a stale ECS dependency. The save boundary now completes tracked jobs before scheduling queued transfers. The lifecycle regression fails without that completion and passes with it; native job timing still requires game verification.
- Decision: read stored city money via its public getter on a copy with unlimited disabled, preserving the city's flag. This relies on the verified 1.6.2f1 getter semantics and must be rechecked if that API changes.
- Decision: commercial-only admission attribution remains excluded by the existing Park v1 design; broadening that scope would require a separate change.
- No deferred minor review findings. In-game observation, paused boundary editing and save/load timing remain unverified in this environment.
