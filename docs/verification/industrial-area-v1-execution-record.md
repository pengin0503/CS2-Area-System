# Industrial Area v1 execution record — 2026-10-01

Native / executing-plans in one session; no per-task implementer subagents. Base: `main` at `92e7b50` (approved spec and plan). Spec `docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md` and plan `docs/superpowers/plans/archive/2026-10-01-industrial-area-v1-implementation.md` were read in full and not modified (their blob hashes are pinned by `scripts/test-offline-contracts.mjs`).

## Environment

- API references: the supplied `CS2-All-Managed-1.6.2f1-fix` DLLs (`CSII_MANAGEDPATH`), toolchain `Mod.props`/`Mod.targets` from `CS2-toolchain-1.6.2f1`, UI reference `index.js` from `CS2-UI-reference-1.6.2f1`.
- Field semantics were traced in a decompilation of the supplied `Game.dll` (ILSpy 9.1): writers and consumers per metric are listed in [industrial-observation-contracts.md](industrial-observation-contracts.md).
- .NET 8 SDK 8.0.131 (Ubuntu package), PowerShell 7.4.6 (dotnet tool), Node 22.22.0.
- Not available: Windows game install, Unity Mod Project, ModPostProcessor run. Official postprocessing/deployment and every in-game check are **NOT RUN**.

## Task ledger

| Task | Commit | Evidence |
| --- | --- | --- |
| 1 Contracts, classifier, HQ policy | `418ea33` | classifier/HQ/API-contract tests RED (missing types) → GREEN; old "Industrial accepts any building" test replaced by the approved strict rule; suite 294/294 |
| 2 State and runtime store | `5d622f9` | serialization/runtime tests RED → GREEN (13) |
| 3 Pure scoring + examples | `329b129` | 26 model/example tests; mutation checks (swapped weights, warm-up threshold) caught; suite 333/333 |
| 4 Observation and aggregation | `ffab28e` | membership revision tests (16) and 22 observation tests; mutation checks (gap bridging, building de-duplication) caught |
| 5 Progression and XP | `cdecff7` | 27 progression tests + example XP; mutation checks (cap 250, banker's rounding, no midnight split, 17.5 h streak) caught; suite 393/393 |
| 6 Panel, localization, fixture | `6073c3e` | panel builder 17 tests; fixture RED (missing) → generated → GREEN; Vitest 66/66; Park fixture regenerated for the shared en-US dictionary |
| 7 Registration, guards, docs | `78332e7` | offline guards RED (missing registration/record) → GREEN; see below |

Corrections made while implementing (the code, not the approved rules, changed): `default(IndustrialMetric)` was "Available 0" and is now Unavailable; a vacant producer's flow channels are NotApplicable (not Unavailable, which would have added a partial-data label); the panel clock rounds to the minute (floating truncation showed 14:11 for 14:12); the Logistics tab lists every current flow diagnostic, not only those in the overall top three; the net8 API-check compiler cannot bind the game's `Span<float>` overload of `BuildingUtils.GetEfficiency`, so the job uses the `(Entity, ref BufferLookup<Efficiency>)` overload of the same product.

## Decisions recorded for review

- **Productive activity source.** No per-company accumulated production counter exists for processors in 1.6.2f1 (production is only aggregated city-wide). The activity numerator is the game's own displayed production rate (`CompanySection`: `GetCompanyProductionPerDay` with current efficiency, which is 0 while the `LackResources` factor is 0; extractors: `CompanyStatisticData.m_LastUpdateProduce`), the denominator vanilla's maximum-production term (efficiency 1, full staff). It is labeled as a sampled rate, never a lifetime counter. Raw inventory deltas are not used anywhere.
- **Flow symptoms.** Input starvation reconstructs the `ProcessingCompanySystem` condition (positive potential output, an input stock below one unit's requirement); output blockage uses the extractor free-storage cap and the processor weighted-storage limit. `CompanyNotifications` counters are unused because 1.6.2f1 never writes them.
- **Structure generation.** Tenant/role changes are taken from vanilla `RentersUpdated` events and member destruction/abandonment, collected every frame in ModificationEnd; ordinary `Updated` tags are not used because they fire for unrelated building changes and would restart cycles too often.
- **Profitability scale.** The vanilla byte is converted with the vanilla UI formula and mapped piecewise onto five 20-point bands whose edges are the vanilla tier thresholds; every byte value is tested against `CompanyUIUtils.GetProfitabilityKey`.
- **Rolling outputs.** The Level-5 "three meaningful outputs" objective averages per-output activity over the six-hour window; the Production diversity term uses the current observation.
- **Pre-v1 headquarters links.** Saves made before Industrial v1 could link any building as HQ. Because an accepted HQ is never re-classified (design §5.1), such a link stays valid while the building exists inside the Area; only new assignments require an industrial capability.
- **Failure paths found in self-review (fixed before the final run).** Structure tokens of deleted Areas were not pruned (the store was pruned first); reads handed out but dropped (load, transient not-ready, schedule failure) would have left a cycle waiting forever — such cycles are now rewound and read again (`IndustrialObservationScheduler.Abandon`), and reads are only handed out when the economy parameters needed to schedule them exist.
- **Sample timing tolerance.** Consecutive hourly endpoints up to 1 h + 1 game minute apart are bracketed (absorbs the 16-frame update jitter of the hour-boundary start); larger gaps are never bridged.

## Automated verification

Final run on the branch after the self-review fixes (parent `78332e7`, committed as "fix(industrial): recover dropped observation reads and prune structure tokens"):

- `pwsh scripts/verify.ps1 -ApiCheck`: **exit 0** — source/provenance guards, source-guard self-tests, MSBuild JSON tests, official/API build-contract evaluation, offline contracts (including the new Industrial guards: pinned spec/plan, registration, read-only facade/panel, no office eligibility, no cargo/storage presence term in Logistics, forbidden production/transfer/tax/demand/pathfinding systems and Resources/efficiency/profitability writes), NUnit **412/412**, net8 API-check build of the mod against the supplied 1.6.2f1 DLLs **0 warnings / 0 errors** (exit 0), Vitest **66/66** (7 files), webpack production UI build.
- Offline Industrial guard negative check: injecting an `EconomyUtils.AddResources` call into an Industrial source makes `test-offline-contracts.mjs` fail with "must not touch direct Resources/efficiency mutation" (file restored afterwards).
- Separate net48 compile of every `src/CS2AreaSystem` source file against the supplied DLLs with the Microsoft .NET Framework 4.8 reference assemblies (scratch project, LangVersion 9): **0 warnings / 0 errors**.
- C# tests added by Industrial v1: classifier/HQ (11), anchor service policy (2 new + 1 replacing the former permissive Industrial test), API contracts (11, including executed vanilla `GetProfitabilityKey`, `GetWorkplacesData`, `TimeSystem.GetDay`, `GetCompanyProductionPerDay`, `GetAdjustedLimitForWarehouse`), serialization (7), runtime store (6), scoring models (23), calculation examples (4, JSON fixture executed against the real models including XP), membership revision tokens (7), observation/aggregation/scheduling/data fidelity (24), progression/XP/streak (27), panel builder/localization/cache (17), generated UI fixture (1) — 140 net new C# tests over the 272-test baseline. React: Industrial panel (14 new; 52 → 66).

None of these compiles or tests runs Unity, the game simulation or the deployed UI. **In-game Industrial runtime tests are still unrun.**

## Runtime verification

**NOT RUN.** All 27 Industrial items in [the smoke checklist](../../scripts/runtime-smoke-checklist.md) are unchecked. Placeholders for measurements:

| Measurement | Small Area | Large Area |
| --- | --- | --- |
| Observation cycle wall time per update (ms) / allocations | not measured | not measured |
| Cycle completion delay (game h), Delayed cycles | not measured | not measured |
| Flow coverage hours reached | not measured | not measured |
| Ownership churn / restarts per hour | not measured | not measured |
| Balance notes (XP pace, gate reachability) | not measured | not measured |

Live-field semantic validation (separate from the compile and model-example evidence above): not performed — `m_LastUpdateProduce`, `GetCompanyProductionPerDay`, the `LackResources` factor, storage limits, `Profitability` and `RentersUpdated` behavior must be compared with the vanilla info sections in game.

## Pre-Tourism Wave A1–A5 (Industrial)

Branch `claude/pre-tourism-wave-a1-a5-l5pses`, base `c04ebf4cf586b4d46532662000dc650f62df0e2f`. Adds runtime trend rows (A1), the read-only bottleneck facility focus (A2/A3; the only Industrial panel action) and observed input→output process rows (A4), all from the existing budgeted observation cycle. Scores, XP, policies and saved state are unchanged.

- Automated: `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` 950/950; Release API-check build against the supplied 1.6.2f1 DLLs 0 warnings / 0 errors; Vitest 104/104; webpack production build; offline contracts, `verify-source.ps1`, `test-source-guards.ps1`; `pwsh scripts/verify.ps1 -ApiCheck` exit 0.
- Runtime: **NOT RUN.** See the "Pre-Tourism Wave A1–A5" section of [the smoke checklist](../../scripts/runtime-smoke-checklist.md).
- Not included: A6 (pedestrian connectivity), A7 (budget/fee what-if) and Waves B–D.

## Foundation + Phase 1 — 2026-10-02

This update supersedes the historical industrial-capability HQ eligibility and unchanged-HQ-validation statements above. Headquarters now uses stable office-building evidence; vacancy retains eligibility, while old extractor/factory/storage/cargo HQ links remain visible but invalid. Office/commercial observation exclusions remain enforced. B1 adds explicit registered asset roles, C1 persists 168 hourly buckets and seeds the newest 24 distinct hours after load, and C3 adds a read-only comparison of managed Industrial Areas with stale/unavailable data preserved.

Final automated evidence and review decisions are in [the Foundation + Phase 1 execution record](foundation-phase1-execution-record.md). Runtime and official Unity postprocessing/deployment remain **NOT RUN**; use F01/F02 and P05–P08 in [the smoke checklist](../../scripts/runtime-smoke-checklist.md). The original v1/Wave A records above describe their dated revisions rather than current prerequisites.

## Phase 2 B2 execution record — 2026-10-02

Execution-time latest `main`: `135ff11bfa762d6d0085c574a9ef5d800e3a50b4` (verified through GitHub). Native branch: `feat/phase2-b2-manual-company-assignment`. Followed `2026-10-02-usage-constrained-phase2-b2-implementation.md` and `2026-10-02-phase2-industrial-company-assignment-implementation.md` using Superpowers executing-plans, no task subagents. B2 only; C4/C5/C6, Phase 3 and Tourism are excluded.

### Task evidence

Each task followed RED → implementation → focused tests → commit, with the Task 4 correction noted below. Native commits are shown here; GitHub publication preserves all five task trees/messages but may assign different author metadata/SHA values (canonical published commits are in the PR).

| Task | Native commit | Evidence |
| --- | --- | --- |
| 1 Persistence/index | `b6ea5c5` | Missing new types RED; assignment/serialization/presentation focused 31/31 GREEN. Supported Entity reference serialization remaps both Company and rented Building; unknown schema rejects payload. |
| 2 Resolver | `dccc081` | Missing eligibility/resolver RED; ownership/reader/API contracts focused 26/26 GREEN. Manual priority, geometric fallback, lowest valid StableId, moved/dead/reused/ineligible company and office/commercial exclusion. |
| 3 Observation | `56c450e` | Missing generation/source/mapper contracts RED; manual/observation/performance/reader/classifier focused 57/57 GREEN. Exact Company filtering, separate overlap batches, unchanged shared 128 reads. |
| 4 UI/actions | `c6a10b4` | Missing actions/candidates and Companies fixture RED; C# 45/45 and affected UI 43/43 GREEN. An obsolete UI focus-only whitelist was initially missed at commit; fixed and Task 4 amended before Task 5. |
| 5 Guards/review | final task commit | Offline focus-only guards RED; new stale-coverage/candidate/focus contracts RED, candidate flag/cap and candidate-only count assertions RED → affected C# 84/84 GREEN. One whole-branch self-review; concrete B2 findings fixed. |

### Self-review fixes

- Candidate-only indexed overlap reads allow initial assignment when the geometric primary Area is suspended. They use the existing cached membership lists and shared budget, and never inflate member/ignored counts or metrics.
- Assignment changes, including automatic invalidation, discard flow/resource baselines and coverage, mark the last result stale, and prevent an unconsumed old cycle from being settled for XP. The assignment revision restarts in-flight and already-completed hourly generations.
- Current non-primary manual-owner bottlenecks can be focused after live ownership validation. Stale actions revalidate load/hour/revision, exact Entity version, renter Building, Area relation and eligibility.
- Re-observation retains saved claim removal flags; saved invalid/duplicate claims displace automatic candidate rows at the 64-row cap. Removing a losing duplicate never clears another Area's winning claim.
- Shared Park fixture regeneration exposed a pre-existing mismatch: its suspended case lacked completed entrance evidence while the UI expected the missing-entrance warning. Corrected only that fixture input; Park runtime behavior is unchanged.

### Final verification

Runtime: .NET SDK 8.0.408, NUnit/NUnitLite 4.2.2, PowerShell 7.4.6; supplied 1.6.2f1 managed/toolchain references used for API compilation. Six existing test-side CS8632 warnings; Release mod build has no warnings.

| Command | Result |
| --- | --- |
| `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` | **FAILED before test execution**, exit 1: VSTest `TestEngine.ShouldRunInProcess` NullReferenceException. API/test compilation succeeded. Standard runner is not verified in this process environment. |
| Temporary NUnitLite runner on the same API-check compiled test assembly, unfiltered, `--noresult` | **PASS 1039/1039**, one full C# run. No runner/project dependency changes committed. |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | **PASS**, exit 0, 0 warnings/errors. Initial launcher process-information failure was retried; no source change. |
| `cd UI && npm test -- --run` | Initial full run **107/108**; suspended Park fixture mismatch above. After fixture correction, affected Park test and C# fixture checks rerun below; no second full suite. |
| `cd UI && npm run build` | **PASS**, exit 0, webpack production build. |
| `node scripts/test-offline-contracts.mjs` | **PASS**, exit 0. B2 whitelist, exact renter/revision, module-buffer boundary and no second ownership query; existing economy/path/tax guards retained. |
| `pwsh -NoProfile -File scripts/verify-source.ps1` | **PASS**, exit 0. |
| `pwsh -NoProfile -File scripts/test-source-guards.ps1` | **PASS**, exit 0, clean/four forbidden patterns/provenance cases. |
| `git diff --check` | **PASS**, exit 0. |

Only focused verification follows the final-gate fixture correction. In-game B201–B208, save/load with real saves, runtime Entity reuse/migration, official ModPostProcessor/deployment, live frame profiling, and Foundation/Phase 1 gameplay regression: **NOT RUN**. See `scripts/runtime-smoke-checklist.md`; API compilation/unit tests do not establish playable readiness.

### Rulings and limits

1. The usage-constrained plan's one author self-review and focused-per-task checks override generic reviewer/full-suite skill steps. Risk: author blind spots remain for PR review.
2. Persist rented Building alongside Company to invalidate moves within the same Area across save/load. Risk: one additional Entity reference in the new schema; serializer remapping is covered.
3. Correct the baseline derived-anchor adapter `out int` to its interface's `out int?` so API compilation can proceed. No membership or primary ownership change; nullable display value is forwarded.
4. VSTest cannot start here; temporary NUnitLite executes the same compiled tests without repository test infrastructure changes. Exact requested command is reported failed rather than replaced silently.
5. Candidate-only overlap reads extend the source's primary-plus-assigned list so an active target does not depend on an active primary. No extra world query; risk is longer bounded cycles in dense overlaps, pending in-game profiling.
6. Shared localization fixture regeneration requires complete entrance evidence in its existing suspended Park example. Only fixture setup changes; post-fix validation is focused to respect the one-full-suite gate.

Post-gate fixture correction: `ParkPanelFixtureTests` **2/2 PASS** (regenerated fixture retains all existing Park payloads and adds only shared B2 localization); `cd UI && npm test -- --run src/ParkPanel.test.tsx` **13/13 PASS**. The other 95 UI tests passed in the single full run. Final `git diff --check` remains PASS.

## B2 PR follow-up — 2026-10-02

User requested remaining B2 work. Automated PR #83 review reported two P2 issues after initial completion; evaluated against actual code/spec, with no C4 work.

- Building efficiency / LackResources factor leaked into a non-primary manual company owner. Regression `ManualOwnerGetsCompanyProfitAndActivityButBuildingEfficiencyStaysPrimary` failed at manual efficiency = 25 (expected null); cleared building-owned efficiency/factor in the non-primary mapping, retaining Company activity/profit and primary Building efficiency. Affected C# tests **45/45 PASS** through the temporary NUnitLite runner; API-check test compilation passed. Review: `discussion_r4166676441`.
- Original full C# run remains 1039/1039 evidence for the initial branch; follow-up changes receive focused checks only, preserving the requested single full-suite run. Standard VSTest and game/deployment checks remain unverified / **NOT RUN** as recorded above.

- Assignment invalidation was global: `AssignmentChangeLeavesUnrelatedCycleAndCachedMembersUntouched` reproduced unrelated cycle ID changing 2 → 4. Added runtime-only per-Area assignment revisions and effective-owner deltas; compare old/new claims without clearing the index first. Only former/new resolved owners lose baselines/coverage and restart; add/remove uses the live geometric fallback, including moved/ineligible/deleted companies. A losing duplicate removal or unchanged rebuild leaves generations intact. Scoped store invalidation preserves unrelated flow coverage and progression endpoint. Regression/API contracts were RED before implementation; assignment/manual/runtime/performance/observation/action/progression focused checks **97/97 PASS**. Review: `discussion_r4166676432`.
- Follow-up Release API build **PASS**, 0 warnings/errors (includes production UI webpack build); offline contracts, source guards and guard regression checks **PASS**. `git diff --check` **PASS**. Added source guards for per-Area generations and scoped snapshot invalidation. No additional whole-world query, Core ownership change, UI feature, economy/path/tax/resource-enum change or full-suite rerun.
- Ruling: keep the global assignment revision for stale UI key rejection, but use per-Area revisions for metrics/cycle invalidation. Native refresh supplies the current live fallback owner over changed explicit claims only. Risk: live fallback/ownership churn behavior still needs the B201/B206 game checks, **NOT RUN**.

## Phase 2 C4 execution record — 2026-10-03

The execution-time latest `main` was `75e03c2`; this is the source of truth. The native branch is `claude/phase2-c4-se4lwg`.
- Plan: `2026-10-02-phase2-industrial-subsidies-implementation.md`, spec §C4 plus the 2026-10-03 clarifications. Followed with Superpowers executing-plans, inline, with one fresh-context whole-branch reviewer at the end.
- Scope: C4 only. B2 and its PR #83 fixes are kept unchanged. C5, C6, Phase 3, dedicated HQ/Gate assets and Tourism are excluded.
- Game API: every 1.6.2f1 member used was read in the supplied `CS2-All-Managed-1.6.2f1-fix.zip` decompile, not guessed. These are `EconomyUtils.AddResources`/`GetResources` and the `PayWageSystem.PayJob` company-money pattern; see `area-finance-contracts.md`.

### Task evidence

| Task | Commit | Evidence |
| --- | --- | --- |
| 0 Completion contract | `46d1443` | RED: missing request listener. GREEN: `AreaFinanceCompletionTests` covers zero, abort, partial mixed kind, once only, isolation, reset cancel, and preload record before reset. |
| 1 State, kind, allocation | `2779c8a` | `AreaFinanceKind.IndustrialSupport = 3` (4 reserved for C5); `IndustrialSupportState` schema 1; equal split with remainders to the earliest receivers, capped per company. The fixed-kind and budget guards were updated. |
| 2 Daily scheduling | `ae15654` | The complete roster comes from the bounded observation path (no new query). Partial, stale or inactive Areas and outstanding requests schedule nothing. The day is saved only after the enqueue is accepted. Saved-state validation added. |
| 3 Credit and save boundary | `02ac064` | The settled amount is credited after a live B2 revalidation. `IndustrialSupportCreditJob` is the only `AddResources(Resource.Money)` call, and the offline guard was narrowed to it. Credit is measured from the actual increase. The save flush runs after `AreaFinanceSaveSystem`. NUnit 1109/1109. |
| 4 UI and verification | `df581b8` | Support tab: a 0–5,000 / 250-step slider routed through the enabled `IndustrialSupportSystem`, plus receiver, request, stage and lifetime rows in en-US and ja-JP. Offline guards whitelist exactly one support slider/action. UI fixtures were regenerated (Park fixture: new localization keys only). |
| Docs | `7c08aa5` | `area-finance-contracts.md`: completion contract, company-money path, preload order. |
| Review fixes | `40b0ce2` | Found by the fresh reviewer; each fix went RED → GREEN. |

The review fixes in `40b0ce2`:
- Scheduling waits for `AreaFinanceSystem.Started`, so a save right after load cannot store a day it never paid.
- A credit job that was already scheduled when a later exception hit is completed and recorded.
- A saved state with settled above requested is invalid.

### Final verification (head `40b0ce2`)

Tools: .NET SDK 8.0.131, PowerShell 7.4.6, Node 22.22.0, and the supplied 1.6.2f1 managed and toolchain references.

| Command | Result |
| --- | --- |
| `pwsh -NoProfile -File scripts/verify.ps1 -ApiCheck` | **PASS**, exit 0. Source, policy and target guards; offline contracts; NUnit **1129/1129**; Vitest **110/110**; UI build. The six existing test-side CS8632 warnings are unchanged. |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | **PASS**, exit 0, 0 warnings / 0 errors. |
| `node scripts/test-offline-contracts.mjs` | **PASS**. Negative checks failed as expected for: a second `AddResources(Resource.Money)` writer, the support setter without `Enabled`, and the facade without routing. |
| `git diff --check 75e03c2..HEAD` | **PASS**. |

### Runtime

The following are **NOT RUN**:
- In-game cases C401–C407 (`scripts/runtime-smoke-checklist.md`);
- official ModPostProcessor/deployment;
- a real save/load;
- live frame profiling.

API compilation and unit tests do not show that the mod is ready to play.

### Rulings and limits

- **A roster entry no longer owned makes the whole roster Stale.** Payment waits for the next complete cycle the same day.
- **The setting snaps to the 250 step,** with midpoints rounding away from zero.
- **The setting write lives in `IndustrialSupportSystem.TrySetPerCompany`,** so the facade itself never writes.
- **Failures are not retried.** A credit job that fails to schedule is recorded as fully unallocated. Preload drops settled but uncredited old-city work. Neither case is retried.
- **At the native lower money bound, the batch's queue order decides which Area/kind is shortened.** The accounting remains exact.
- **Deferred minors:**
  - No single end-to-end test covers an owner change between days; it is covered piecewise.
  - Counters are still written onto an Area quarantined mid-session. Quarantine is load-time only today.

## Runtime verification addendum — 2026-10-04

**PASS — user-confirmed.** The user reports all Industrial v1, Phase 2 B2 manual-company-assignment and Phase 2 C4 support-payment in-game checks completed on Cities: Skylines II 1.6.2f1 and behaved normally. The original NOT RUN statements above remain as implementation-time history; they are not rewritten as automated evidence. Exact local save/build metadata was not supplied in this chat.

Runtime-confirmation Issue #43 was closed as completed. The Phase 3 D1/D2 plans may rely on the verified Phase 2 ownership/support baseline.

## Phase 3 C2 dedicated-asset cutover — 2026-10-04

Runtime: **NOT RUN.** Phase 3 C2 replaces the Foundation office Headquarters with the `AS_IndustrialHeadquarters` explicit anchor. Implementation-time automated evidence only; the in-game cases are C201–C208 in [the smoke checklist](../../scripts/runtime-smoke-checklist.md). The earlier PASS addendum above covers the pre-C2 rules and is not carried over to C2.

- Old office HQ links are kept, shown as invalid/removable and never converted; the Area is Suspended until the dedicated asset is assigned. Automated: `DedicatedContentStateTests`, `DedicatedAnchorCutoverTests`.
- Without the installed asset package the summary and warnings say which `AS_` asset is not installed.
- No serialized schema changed (`AreaAnchorLink` v1, `IndustrialAreaState` v1 are unchanged).

## Phase 3 D2 reference factory — execution record 2026-10-04

The execution-time latest `main` was `108bf47`. Branch: `claude/project-thread-3jfpsi-d2`. It is stacked on the C2 branch,
because the factory uses C2's built-in content provider `CS2AreaSystem.BuiltInContent`. D2 does not depend on D1.
- Plan: `2026-10-02-phase3-custom-factory-content-implementation.md`. Followed with Superpowers executing-plans, inline,
  with one fresh-context whole-branch reviewer at the end.
- Scope: D2 only. There is no new `Resource`, no economy change and no score exception.

### Decision (Task 1)

No recipe is authored. `AS_FurnitureFactory` is an Industrial, non-office signature building that manufactures only
vanilla Furniture. The game places a vanilla Furniture company in it (`PropertyProcessingSystem`), and that company's own
`IndustrialProcessData` is what runs and is observed. Vanilla `EconomyUtils.IsProducedFrom` makes Furniture from Timber
alone. The company prefab's amounts are prefab data that are not in the supplied managed assemblies, so they are not pinned.
Details are in `docs/verification/custom-factory-process-contract.md`.

### Task evidence

| Task | Commit | Evidence |
| --- | --- | --- |
| 1 Process contract | `bc0f571` | API/pure-function tests on 1.6.2f1: Furniture is a single non-office, non-extractor bit made only from Timber; editor→runtime mapping; empty allowed input = any recipe; prefab field shapes. RED: missing contract type. |
| 2 Content provider | `dda0570` | `AS_FurnitureFactory` is optional content of the shared provider and registered as `IndustrialProducer` only when the runtime prefab matches the contract (10 mismatch cases rejected); absent → nothing changes and nothing is reported missing; reload rebinds. RED: missing facts type. Asset authoring **BLOCKED** (no source model). |
| 3 Generic observation | `b1bf916` | Identical reads in the factory and a vanilla building give identical processes, resources, bottlenecks, scores and comparison; B2 manual ownership works. Proof over unchanged code, so GREEN from the start. |
| 4 Guards and checklist | see `git log` (docs: verify custom factory) | Offline guard: no new `Resource`/`ResourceInEditor` enum, no Resource bits ≥ 41 or economy-array growth, no factory/Furniture reference outside the two contract files, read-only discovery (mutation runs failed as expected). Runtime cases D201–D206. |

### Runtime

The following are **NOT RUN**, and asset authoring is **BLOCKED**:
- asset import and packaging;
- in-game cases D201–D206.

The first release cannot ship the reference factory until a model exists and these cases pass.

### Verification (before review)

| Command | Result |
| --- | --- |
| `pwsh -NoProfile -File scripts/verify.ps1 -ApiCheck` | **PASS**, exit 0. NUnit **1343/1343**, Vitest **113/113**, guards, UI build. |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | **PASS**, 0 warnings / 0 errors. |
