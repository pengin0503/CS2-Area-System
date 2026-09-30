# Area Core integration review — 2026-09-30

## Scope and progress ledger

- Reviewed `main` at `a68ba77f050a6d870bf71c6ef4e94c638a5a1b73` and `fix/area-core-offline-review` at `2a314140e0a2926e86520b4b9e76b57dc65bcd05` against the approved spec, plan, and Issues #1–#11.
- Imported current remote snapshots through the GitHub connector and verified each blob/tree hash. Native git transport was unavailable. Original implementation worktree and branch were preserved.
- Created an isolated integration worktree. Merged the current `main` into the repair branch without conflicts. Both branches had already restored exactly the same approved spec/plan; the merge therefore preserves the repair branch's source snapshot and both histories.
- Read-only fresh-context review identified one Important regression: native recreation is not ordinary vertex editing. Independently confirmed this against the supplied 1.6.2f1 DLL's AreaToolSystem implementation. No implementation-task subagents were used.
- Created Issue #12 for UI test import resolution and Issue #13 for the recreation regression; corrected their source-level causes before preparing the PR. Native runtime acceptance for #13 remains open.

Ruling: The ObjectToolSystem recreate sequence applies to temporary Lot creation, whereas these boundaries are permanent Space Areas — clear recreation state, select the prefab and normal native Edit mode, activate AreaToolSystem, then focus the selected area — if native pointer selection does not target the expected boundary, game smoke will reveal it; the mod does not claim an entity-locked editor or rewrite native input.

Ruling: A list of shipped 1.6.2f1 icon URIs verified from the supplied UI reference is a conservative fallback policy, not an actual runtime availability check — retain the branch's shared resolver but keep Issue #8 open — if a listed resource is missing at runtime, this whitelist cannot detect the failure.

Ruling: A branch fix is not a main-branch resolution, and unavailable game tests must not be reported as passed — only fully confirmed automatic fixes are linked for closure on merge; partial/native acceptance stays open — otherwise important incomplete behavior could disappear from tracking.

## Verification evidence

Baseline after the clean main merge: C# **63/63** and API compilation passed; UI suite failed before collection because Vite could not resolve `cs2/l10n`. This is the RED evidence for #12, not a passing suite. Adding only the test alias/boundary restored **8/8** UI tests and webpack build; production still externalizes the public game API.

The new edit-workflow test initially failed with the workflow absent. A focused command without the global API profile first failed with NETSDK1005; that environment/profile failure was excluded from RED evidence, then the correct profile was used. A mutation restoring `setRecreation(area)` failed with the actual unwanted entity versus Entity.Null. Omitting reverse-index pruning failed both the existing cleanup test and the new shared-anchor/repeated-deletion test. All three expected failures were inspected; both mutations were reverted before final verification.

Final `scripts/verify.ps1 -ApiCheck`: source/provenance guards, negative guard fixtures, official/API target-evaluation contracts, offline contracts, **65/65 NUnit tests**, **8/8 UI tests**, fresh net8 API C# build and webpack UI build, exit **0**. The final tests include 100 repeated deletions sharing a surviving area's anchor and verify only that surviving area remains invalidated.

Known output: C# emits MSB3245 for the existing nonexistent `Unity.Jobs` assembly reference (Issue #10). Vite reports its CJS Node API deprecation and npm reports the environment's http-proxy setting. These are not test failures and have not been concealed.

The additional net48 API compile passed with 0 errors and the same single Unity.Jobs warning, using Microsoft's official v4.8 reference pack. Its command explicitly selects both `TargetFramework=net48` and `TargetFrameworks=net48` for restore and sets `TargetFrameworkRootPath` to the pack's `build` directory. Initial supplemental attempts exposed missing reference-pack search configuration, a restore graph still targeting net8, and old obj files being included when a custom intermediate path was tried; those environment/probe failures were investigated, excluded from passing evidence, and resolved without changing game DLLs or official targets.

API compilation is not Unity postprocessing or a playable mod build. Official Windows postprocessing/deployment and every in-game smoke case are **NOT RUN**. No game save, city performance benchmark, real failure injection, input timing, or UI discovery result is claimed.

## Issue disposition before main integration

| Issue | Current branch assessment | Remaining condition / tracking |
|---|---|---|
| #1 completed Space selection | Unchanged | Missing user selection route remains a major functional limitation. Open. |
| #2 live selection validation | Unchanged | Mutation actions still use cached selectedInfo; pending cancellation validation remains. Open. |
| #3 per-type prefab/load recovery | Source-level fix present | Independent registration retries/readiness, no global construction gate on saved-area recovery. Native factory/AddPrefab failure-injection acceptance not run. Open pending that validation. |
| #4 toolbar retry duplication | Source-level fix for ordinary partial retries | Menu/category objects and attached-type state are retained. Native partial-registration/asset-attachment failure fixtures still need validation. Open. |
| #5 classifier getter isolation | Unchanged | Getter can still escape the module boundary. Open. |
| #6 panel recovery | Unchanged | React error boundary still has no reset/retry path. Open. |
| #7 reverse anchor cleanup | Confirmed source/service fix | Actual ForgetArea removes derived relations and preserves final validation; shared-anchor and 100 deletion repetitions pass, mutation detects regression. Closure on main merge. |
| #8 runtime icon availability | Partial | Unknown URI falls back and toolbar shares resolver, but listed-resource runtime loss is undetected. Open. |
| #9 localization | Partial | Public cs2/l10n, EN/JA entries and localized lifecycle/actions are present; readable missing-module-key fallback and explicit locale-change test are not completed. Open. |
| #10 nonexistent Unity.Jobs reference | Unchanged | Warning reproduced; no fake assembly supplied. Open. |
| #11 first-run CLI JSON | Unchanged | Validation used an initialized CLI home and banner suppression; fresh-profile parser robustness is not fixed. Open. |
| #12 UI test l10n import | Confirmed automatic fix | Test environment resolves game API boundary; all 8 UI tests and bundle build pass. Closure on main merge. |
| #13 Edit recreation regression | Source-level fix and workflow regression test | Native game pointer/vertex-edit/cancel/identity smoke remains. Open. |

## Integration boundary

This PR preserves both prior histories, the approved source-of-truth documents, and the offline-review improvements. It prepares a reviewed/tested source change for integration; it does not declare Area Core operational. `main` and branch deletion remain untouched by this preparation. The original execution ledger remains historical; this ledger records the current review and verification limits.

Detailed Park/Industrial/Tourism simulation is outside this review. Speculative prefab removal/native buffer corruption was considered and declined because no supported-lifecycle reproduction was established. No new code was copied/adapted from third-party repositories during these corrections; existing provenance remains intact.
