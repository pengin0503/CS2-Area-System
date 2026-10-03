# Scoped issue regressions — 2026-10-03

## Scope and source of truth

Only open Issues #84, #85, #91, #92, #93, #94 and #95 were repaired. Latest main was obtained at task start: `903683771c566b6a8d05e6000d5a8bb5a113601e`, tree `e8d00edf79ca4767f5aabc7cfb6b765ec6bb830f`. GitHub connector retrieval restored all 421 file hashes and the exact signed main commit locally. Main was rechecked before publication and had not changed.

Superpowers systematic-debugging → test-driven-development → verification-before-completion; Native/executing-plans, one independent whole-branch reviewer, no task subagents. Each Issue still existed on main, received a failing regression, minimum repair, focused GREEN and a separate commit. PR #86 was not merged; only its #84/#85 repair/test changes were ported into current code. Current C4 roster logic was preserved.

No Core Building membership/primary changes, second whole-world scan, performance Issue, other gameplay-confirmation Issue, save schema change or automatic merge. The 64-row UI limit, 128 shared reads and existing load/hour/revision/entity/renter guards remain intact.

## Issue evidence and canonical commits

| Issue | Root cause and change | RED → focused GREEN | Canonical GitHub commit |
| --- | --- | --- | --- |
| #84 | `Record` excluded every Null Company. Allow only saved claims, force invalid, retain exact selected Area/Company/Building removal and disabled assignment. | `NullCompanySavedClaimIsVisibleRemovableAndNeverAssignable(false/true)` both failed at zero rows; **28/28** assignment/manual ownership tests PASS. Ordinary Null observations remain excluded; other Area/live Company claims survive. | `cbb010bcff1002c175ee3cabf694ff8dc9cc53ab` |
| #85 | Candidate publication used `cycle.HourBucket`. Pass current OnUpdate bucket through Consume only to candidate publication; cycle/aggregation/roster identities remain unchanged. | Three imported Native wiring guards failed; offline contracts then PASS, **64/64** ownership/observation tests PASS, including old hour-key rejection. | `dac53c730f5e9f8dd4a4752ee72997ed3eb07d94` |
| #91 | Complete delayed roster omitted from stale predicate. `roster.Delayed` now rejects support before daily request. | `CompleteButDelayedRosterIsStaleAndCannotScheduleSupport` failed Request vs Stale; **75/75** Industrial support tests PASS, retaining day/ownership/partial/failed/inactive/token rules. | `cd49b15b8f0ac731100bc8ecebf5597feffee0b1` |
| #92 | Festival trusted settled ManagementCostKnown/PerDay despite changed settings. Park finance and Festival UI/action share live budget, effective policy and latest available runtime management inputs. Estimate reads do not settle or change cached accounting. | Six failures across live-cost/shared-path/native tests. Budget/policy changes and new visits before next settlement produce hand-calculated Festival costs **648 / 540 / 810**, identically displayed/requested. Missing landscape/facilities and expired visitor inputs covered. **126/126** focused finance/event/policy tests PASS. | `e6ee2afcbc3b4820dcbed3e3e4bf465c62241d84` |
| #93 | Finance batch frame also became Festival start. Record receives separate accounting and processing clocks; only event start uses current simulation frame. | `DelayedSettlementStartsAFullDayAtProcessingFrameAndKeepsFinanceFrame` caught native batch-frame forwarding. Processing at 20,000 starts interval **[20,000, 282,144)**, last frame active and end exclusive inactive; finance expense keeps batch frame. **110/110** event/Area finance tests PASS. | `b022537595bd7631f2357bf7c3a6f9c6352b7643` |
| #94 | Visit frame was discarded; satisfaction used dispatch time and XP current runtime time. Runtime-only EventFrame now stamps start/switch/expiry; both effects use that frame. Existing bounded history resolves intervals even after normal expiry or a newer Festival. | Seven RED cases: Festival starts or expires between completion/dispatch, satisfaction and XP, tracker timestamps, interval/activity bounds, both native readers. **111/111** visit/progression/event tests PASS. Historical helper has a distinct name so existing `Effects(default, ...)` callers stay unambiguous. | `719007cbcddeeb41e14cb41c3b22fa97e50b741d` |
| #95 | Toolbar publish ran after AddPrefab/TryGetEntity before readiness. Remove both early publish calls; publication is gated by readiness after IsPrefabReady. Toolbar relationships still precede AddPrefab; saved-Area fallback still registers. | Three RED cases: registered-but-unready publication/version, toolbar-failure fallback, compiled native call sites. **29/29** Core/prefab/toolbar tests and offline contracts PASS. | `7fc41d5722a30ed35f1452de6ce3173531e9c048` |

Connector commit author metadata differs from Native metadata. The seven canonical task trees and messages were checked against their Native commits before creating the branch; all tree SHAs match.

## Major changed files

- Industrial: `IndustrialCompanyAssignmentActions.cs`, `IndustrialObservationSystem.cs`, `IndustrialSupportScheduling.cs`; assignment/action and support tests; `scripts/test-offline-contracts.mjs`.
- Park cost: `ParkFinanceModel.cs`, `ParkFinanceSystem.cs`, `ParkEventSystem.cs`; `ParkLiveManagementCostTests.cs`, `ParkEventFinanceTests.cs`.
- Park timing: `ParkVisitModel.cs`, `ParkVisitSystem.cs`, `ParkProgressionSystem.cs`, `ParkEventModel.cs`, `ParkEventSystem.cs`; `ParkDeferredVisitEventTests.cs`, event tests.
- Core: `AreaPrefabRegistrationSystem.cs`, `AreaTypeReadiness.cs`; `AreaToolbarPublicationTests.cs`.
- Plan: [scoped repair plan](../superpowers/plans/2026-10-03-scoped-issue-regressions.md).

## Final verification

Supplied CS2 1.6.2f1 fixed Managed ZIP and official toolchain ZIP used for compilation. SDK 8.0.425 / runtime 8.0.31, NUnit/NUnitLite 4.2.2. Linux `npm.cmd` was a scratch-only forwarding wrapper to npm; no runner/environment/dependency files were added to the repository. Initial baseline was C# **1239/1239** through NUnitLite and UI **113/113**.

| Requested / alternative check | Actual result |
| --- | --- |
| `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` | **FAIL before test execution**, exit 1: VSTest `TestEngine.ShouldRunInProcess` NullReferenceException, line 625. Actual mod/test compilation succeeds; six existing test-side CS8632 warnings. This is not a passing test command. |
| `dotnet tests/CS2AreaSystem.Tests/bin/Debug/net8.0/runner.dll --noresult` (temporary NUnitLite runner, same compiled assembly) | **PASS 1258/1258**, no failed/skipped/warnings. 19 added NUnit cases beyond baseline. No repository runner/dependency change. |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | **PASS**, exit 0, **0 warnings / 0 errors**. API output only, no deployment. |
| `cd UI; npm.cmd test -- --run` | **PASS 113/113**, nine test files. |
| `cd UI; npm.cmd run build` | **PASS**, webpack production build. |
| `pwsh scripts/verify.ps1 -ApiCheck` | **FAIL**, exit 1 at `test-build-contract.ps1`: dotnet CLI Process.GetStat process/namespace error, “Official target initialization failed.” Source guards and MSBuild JSON parser checks passed before the stop. Not a passing aggregate script. |
| Direct MSBuild `BuildGetFullPaths` and `BuildAreaApiCheckPaths` + actual repository `ConvertFrom-MSBuildPropertyOutput` parser | **PASS**, native NeedBuild=true/nonempty DeployDir; API NeedBuild=false/empty DeployDir. Both real target evaluations exit 0. This checks path contracts without performing deployment. |
| `node scripts/test-offline-contracts.mjs` | **PASS**. |
| `pwsh scripts/verify-source.ps1` | **PASS**. |
| `pwsh scripts/test-source-guards.ps1` | **PASS**, clean/forbidden-pattern/provenance cases. |
| `pwsh scripts/test-msbuild-json.ps1` | **PASS**. |
| `git diff --check` and `git diff --check main...HEAD` | **PASS**. |

Some plain-pipe or nested .NET launches also hit known process/PID namespace lookup failures before compiling. Focused runs used direct MSBuild and the existing NUnitLite alternative; environment launch failures were distinguished from expected regression assertion failures. ECS integration was checked with existing Native wiring contracts and compiled call-site checks alongside real model tests; no actual Unity ECS/game execution is claimed.

## Whole-branch review and decisions

One independent, read-only review covered `9036837..f258363`, all seven fixes and surrounding integration. **No Critical / Important / Minor findings.** No extra review fix pass or out-of-scope polish was needed. The reviewer did not rerun tests; final suite evidence above is the implementer's independently executed validation.

Decisions recorded during Native execution:

1. The user supplied approved repair steps and authorized implementation/PR, so no additional plan approval was requested. A new isolated checkout/branch was used; shared main stays unchanged. Cost if incorrect: review/adjust implementation through the PR.
2. ECS cannot execute in the standalone .NET runner; repository Native wiring guards plus numeric model tests cover integration. Cost if incorrect: actual ECS callback/game behavior remains a runtime verification gap.
3. Performance benchmarking was excluded by the user; no performance Issues changed. Cost: no measured city-scale performance evidence.
4. Native gameplay confirmation was excluded; no game tests or official deployment. Cost: native runtime behavior remains unverified.
5. Existing activity/lifecycle gating for deferred visits was retained; the change corrects event time only. Cost: existing suspension/reactivation gating behavior remains unchanged.
6. Existing 16-entry event history and save schema were retained. Cost: events older than that bounded history do not gain an expanded replay contract.

Deferred Minor findings: **none**. Known remaining environment problems are standard VSTest launch and the aggregate verify script's process lookup. All other Issues remain untouched; none were closed by hand. The repair PR targets main and must remain unmerged for review.
