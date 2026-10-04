# Foundation + Phase 1 execution record — 2026-10-02

Repository: `pengin0503/CS2-Area-System`. Target: Cities: Skylines II **1.6.2f1**.

Implementation start base and freshly checked `main`: `3aad310c71ef46eea2e442ba5d26de175ae3c4a5`. Resume checkpoint: `feat/foundation-phase1` at `b9a78d5fcacbb60ae7dee6d12f5f8a98e9039348`, tree `c2db1e6e8a9d5cb6bead40f4ea4ac8259a93402a`. The checkpoint already contained Tasks 1–12 and generated Task 13 fixtures. They were restored, not reimplemented. Both remote refs matched the checkpoint when work resumed.

Native / Superpowers executing-plans. Approved source of truth:

- [Design](../superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md)
- [Implementation plan](../superpowers/plans/archive/2026-10-02-foundation-phase1-implementation.md)

The approved design/plan were not rewritten. Foundation and Phase 1 remain one branch and one PR. No automatic merge; Phase 2, Phase 3 and Tourism gameplay are excluded.

## Saved task commits

| Task | Scope | GitHub commit |
| --- | --- | --- |
| 1 | Contextual activation and multiple roles | `2d9c6efa971713de5118a4cae206c8653e511780` |
| 2 | Multi-role anchor UI | `bf14e1c492659eb44ea5e6f69a62f7c0220d277d` |
| 3 | Industrial office Headquarters | `9b12fc403916e53da483f9bf58685862d371338c` |
| 4 | Park Maintenance Headquarters and Entrance | `581e0f200843ab0fb5d34dc2ac6edfc8b0732950` |
| 5 | Dedicated Path boundary crossings | `049fa6455813c7af2c6dd5bc3cc7d8950080154d` |
| 6 | Lifecycle, old links and Accessibility integration | `788ccb9286fa7630ce201322c5c7c3029624e22f` |
| 7 | B1 semantic registry | `61cd8facbd02488e053448a46d17f5974cef9414` |
| 8 | Semantic classifiers and role integration | `7afa4f8630126bcb495fc00ff5f2b77345e173b5` |
| 9 | A6 pedestrian connectivity | `b97487ad87ac6e0a32f2bf89cf09e7ac9c19ea67` |
| 10 | A7 budget/fee what-if | `7736859278aeb7221c4fd696799e3559a6a200db` |
| 11 | C1 bounded persistent history | `f40e0a33482860b844b19d2df75a9207087421ee` |
| 12 | C3 Industrial comparison | `30d4241e4badd478a23fc877db3c2744092917ae` |
| 13 checkpoint | Generated fixtures, not final completion | `b9a78d5fcacbb60ae7dee6d12f5f8a98e9039348` |

Final follow-up commits are named `fix: close Foundation Phase 1 review gaps` and `docs: add Foundation and Phase 1 runtime verification cases`. Their remote SHAs are listed in the PR; GitHub API-created commits have different author metadata from local commits, and their trees must match before advancing the branch ref.

## Environment and verification

.NET SDK 8.0.425, VSTest 17.11.1, PowerShell and cached Node dependencies; the supplied 1.6.2f1 managed DLLs and toolchain props were used. The existing sandbox self-proc compatibility preload was retained. No game binaries or machine paths were committed. The checkpoint's previous environment remained available; fresh output was generated against the restored remote tree and review fixes.

| Final command | Result |
| --- | --- |
| `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` | PASS: **1007/1007**, failed 0, skipped 0; actual VSTest execution |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | PASS: **0 warnings / 0 errors**; API compile only |
| `cd UI && npm test -- --run` | PASS: **107/107**, 9 files |
| `cd UI && npm run build` | PASS: webpack production |
| `node scripts/test-offline-contracts.mjs` | PASS |
| `pwsh -NoProfile -File scripts/verify-source.ps1` | PASS |
| `pwsh -NoProfile -File scripts/test-source-guards.ps1` | PASS |
| `git diff --check` and base-to-HEAD whitespace check | PASS |
| Official Unity postprocessing/deployment | **NOT RUN** |
| Every game/runtime check | **NOT RUN** |

The first `dotnet test` invocation returned exit 0 after restore without test execution and was not counted as PASS. Explicit SDK MSBuild `-t:VSTest` then ran 1000/1000 before fixes; subsequent CLI invocations executed correctly, including the final exact command above. NUnitLite fallback was not needed. Fresh C# recompilation emits six existing test-side CS8632 nullable-context warnings; the Release MOD API build has zero warnings.

Fixtures were retained from the checkpoint, then regenerated through the C# fixture tests with `UPDATE_UI_FIXTURES=1` when the shared Headquarters instruction changed. JSON was not hand-edited. Focused fixture/review tests passed 27/27 before the full suite.

New offline guards reject persistent Entity identities in module hourly history and mutation of the vanilla network by connectivity diagnostics. Injected negative controls each produced the expected failing guard, were restored, and clean guards passed again. These are source safety checks, not proof of runtime classification or performance.

## Whole-branch review and one fix pass

A fresh independent reviewer examined the entire `3aad310c..b9a78d5` implementation against the approved spec/plan and all five Review Focus scenarios. No Critical findings. Three Important findings were confirmed, plus misleading Park HQ guidance regraded from Minor to Important. The fixes were implemented in one pass with observed RED/GREEN regression runs:

| Finding | Fix and evidence |
| --- | --- |
| Full Park quality reader lost prefab identity, ignoring registry semantics | Reuse lightweight typed facts before full reads. Compiled integration call test failed before fix and passed after; executed allow/deny/removal aggregation test passed. Live native EntityManager behavior remains F07 NOT RUN. |
| Repeatedly overflowing quadtree tiles could grow an unbounded scan queue and starve other Parks | Limit total tile queries and pending tiles to 4096; cap exhaustion ends incomplete without publishing activation evidence. Perpetual-overflow regression failed before fix and passed after, including processing a second Park. Existing per-update work bounds remain. |
| Duplicate/out-of-order loaded hours displaced distinct history/trends | Shared bounded hourly normalization merges available channels per bucket, sorts before retention/seeding, and validates schemas before mutation. Park and Industrial duplicate/ordering tests each failed before fix and passed after. |
| Park assignment banner instructed the user to select an office | Generic localized guidance now explains Park Maintenance for Park and office for Industrial. UI test consumes the C#-generated dictionary; failed office-only guidance, passed after regeneration. |

Deferred Minor: `AreaManagementUISystem.BuildAnchorGroups` does not locally catch exceptions from extension module assignment/derived-anchor methods. Current built-in implementations are not known to throw on their validated input; external-module hardening was not added in this finish pass. This is not a claim that third-party modules are runtime verified.

## Spec audit

| Requirement | Evidence and remaining limit |
| --- | --- |
| Foundation activation and old saves | Multiple role contracts; typed office/maintenance eligibility; invalid old links visible/removable; contextual activation and integration tests. Actual assignment/load remains F01–F03 NOT RUN. |
| No road-sidewalk automatic Entrance | Entrance detector accepts only dedicated Pathway evidence; normal road pedestrian lanes are used only by connectivity. Detector tests passed; actual net classification remains F04 NOT RUN. |
| Incompatible partial evidence | Coordinator revision/geometry tests discard stale partial scans; complete compatible net evidence is retained until replacement. Boundary/net lifecycle remains F05/F06 NOT RUN. |
| No saved Net Entity | Entrance/connectivity are runtime-only; history entries contain only schema/hour/availability/metric values; guards and serialization tests passed. Real load rebuild remains F06 NOT RUN. |
| A6 read-only diagnostic | Bounded graph scan and Complete/Partial/Unknown reporting; stale focus validation; no network/pathfinding writes. Native graph/focus remains P01/P02 NOT RUN. |
| A7 explicit-only mutation | Pure estimate, transient candidate state and facade setters; preview tests and guards passed. Money/demand assumptions remain P03/P04 NOT RUN. |
| B1 typed defaults and isolated providers | Priority/provider-ID deterministic resolution, provider removal/cache invalidation, explicit role overrides; classifier/registry tests passed. Actual custom assets remain F07 NOT RUN. |
| C1 seven-day capacity | 168 unique retained buckets and 24 distinct latest-hour seed; schema validation and merge/order tests passed. Save flush/remapping remains P05 NOT RUN. |
| C3 read-only comparison | Managed Industrial filtering, stable ordering, delayed/suspended/age-based stale flags and unavailable metrics. Live agreement remains P06 NOT RUN. |
| A1–A5, policies, finance and ownership retained | Full automated regression suite passed. Policy catalogs and finance sources unchanged by this branch; no unavailable policy enabled. Runtime regression remains P08 NOT RUN. |
| No forbidden dependency/gameplay | Source/policy/provenance/offline guards passed; Tourism changes only migrate the activation signature, no gameplay. No Harmony/ExtraLib dependency or new Resource enum. |

## Implementation rulings

1. Reuse existing generated fixtures until a code/string change requires regeneration. Risk: a fixture mismatch if the checkpoint were stale; the current generator equality tests catch it. The final instruction change did require regeneration, which was performed.
2. Native EntityManager is unavailable in the portable API environment. Use compiled reader integration and executed pure classification/aggregation tests, with F07 required in the game. Risk: live native reads remain unproven until that check.
3. A loaded buffer with more than 168 physical rows is rejected before native reading/overwriting, while supported buffers are fully normalized. Risk: an already oversized corrupt history remains unavailable and untouched until separately repaired; gameplay failure isolation remains in place. This avoids arbitrarily scanning corrupted save buffers during normal updates.

Task 13's remaining checklist, execution-record updates, truthful README, guards, audit and final automated verification are complete. Reviewable integration is one PR against the freshly checked main. The [runtime procedures](../../scripts/runtime-smoke-checklist.md) are **all NOT RUN**; this record does not declare the MOD playable or runtime verified.

## Runtime verification addendum — 2026-10-04

**PASS — user-confirmed.** The user reports every Foundation + Phase 1 F01–F07/P01–P08 in-game check completed on Cities: Skylines II 1.6.2f1 and behaved normally. The original NOT RUN statements above remain as implementation-time history; they are not rewritten as automated evidence. Exact local save/build metadata was not supplied in this chat.

This clears the Foundation/Phase 1 runtime prerequisite used by Phase 2 and the Phase 3 program.
