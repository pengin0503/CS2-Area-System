# Post-B2 code audit — 2026-10-02

PR #83 was merged on explicit user instruction after checking its current head, successful Offline CI and resolved review threads. The resulting latest main is `40c3057eed612192f6a4edf43b2f1f98f86b3d9f`, tree `49e3ea5c2315825fbb9cc4847c52a0e28ad89f53`; the exact remote commit and tree were restored locally before implementation. Native branch: `fix/post-b2-code-audit`.

Used Superpowers executing-plans with [the repair plan](../superpowers/plans/2026-10-03-post-b2-code-audit-fixes.md), no task subagents. One bounded audit covered existing B2 candidate/action, saved-state, observation/index and Core action boundaries. Existing runtime-confirmation issues remain open. No C4 or new feature work, Core membership/primary changes, economy/tax/pathfinding changes, Harmony/ExtraLib, Resource enum or additional whole-world scan.

## Issues and task evidence

| Issue | Result | Native commit / RED → GREEN |
| --- | --- | --- |
| [#84](https://github.com/pengin0503/CS2-Area-System/issues/84) | Saved Null Company references appear only as invalid, removable claims. Ordinary Null observations remain excluded; assign rejects and removal matches the exact selected Area/Company/Building. | `25ec102cd8b71043c8b27eb058861bf4cd073d7e`: two test cases failed at row count 0 before implementation, covering present/missing Building. Focused action/index/manual ownership/panel tests **58/58 PASS**. Other Area and live Company claims survive removal. |
| [#85](https://github.com/pengin0503/CS2-Area-System/issues/85) | Live-revalidated company candidates use the consumption-time hour. Observation cycle identity, aggregation hour and scheduler stamps remain unchanged. | `112453a2e6f4646797005937b35139d5f16017dc`: three Native wiring guards failed before implementation; guards then passed. Focused candidate/action/manual/reader/observation/performance tests **63/63 PASS**, retaining stale hour-key rejection. |

Each task completed RED → implementation → focused checks → commit before the next task. API-check test compilation passed for both tasks. No new 1.6.2f1 API was introduced or uncertain; no additional ZIP source investigation was necessary.

One whole-branch author review checked exact saved-claim cleanup, stale hour keys, unchanged cycle stamps, 64 rows and the shared 128-read budget. No additional concrete regression or deferred minor finding was identified. This is bounded review evidence, not an exhaustive proof.

## Final verification

SDK 8.0.408, NUnit/NUnitLite 4.2.2, PowerShell 7.4.6; API compilation uses supplied 1.6.2f1 managed/toolchain references. Six existing test-side CS8632 warnings; Release mod build has zero warnings/errors. Full suites ran once at this repair branch's final gate.

| Command | Result |
| --- | --- |
| `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` | **FAILED before test execution**, exit 1: VSTest `TestEngine.ShouldRunInProcess` NullReferenceException, line 625. Test/API compilation succeeded. |
| Temporary NUnitLite runner, same compiled assembly, unfiltered `--noresult` | **PASS 1046/1046**, zero failed/skipped. Runner/dependency changes were not committed. |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | **PASS**, exit 0, zero warnings/errors. |
| `cd UI && npm test -- --run` | **PASS 108/108**, nine files. |
| `cd UI && npm run build` | **PASS**, production webpack build. |
| `node scripts/test-offline-contracts.mjs` | **PASS**. |
| `pwsh -NoProfile -File scripts/verify-source.ps1` | **PASS**. |
| `pwsh -NoProfile -File scripts/test-source-guards.ps1` | **PASS**, clean/forbidden-pattern/provenance cases. |
| `git diff --check` and `git diff --check main...HEAD` | **PASS**. |

Game save/load with missing Entity references, delayed-cycle company UI, Entity reuse/migration, live profiling, Foundation/Phase 1 gameplay regression and official postprocessing/deployment: **NOT RUN**. See B209/B210 and B201–B208 in [the runtime checklist](../../scripts/runtime-smoke-checklist.md). Standard VSTest remains unverified in this process environment; NUnitLite success does not erase that failed requested command.

## Rulings

1. Explicit user authorization allowed merging PR #83 with its previously disclosed VSTest limitation after fresh CI/review/head checks. Cost: standard-runner and game/deployment evidence remain incomplete.
2. Explicit request to create issues and begin repair allowed immediate Native plan execution without another approval pause or task agents. Cost: the author review has no independent human reviewer until the follow-up PR.
3. The audit repaired confirmed #84/#85 only and left runtime-confirmation issues open. Cost: this is not an exhaustive repository audit; C4 remains outside scope.

Publish one follow-up PR with `Fixes #84` and `Fixes #85`; leave it unmerged for review. Its canonical commits preserve these Native task trees and messages, although connector author metadata can produce different commit SHAs.
