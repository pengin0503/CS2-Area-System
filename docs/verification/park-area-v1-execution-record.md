# Park Area v1 execution record — 2026-09-30

Native / executing-plans in one session; no per-task implementer subagents. Base: `main` at `1b16e5b` (spec + plan commits on top of `9275438`). Spec `docs/superpowers/specs/2026-09-30-park-area-v1-design.md` and plan `docs/superpowers/plans/2026-09-30-park-area-v1-implementation.md` were read in full and not modified.

## Environment

- API references: the supplied `CS2-All-Managed-1.6.2f1-fix` DLLs (`CSII_MANAGEDPATH`), toolchain `Mod.props` from `CS2-toolchain-1.6.2f1`. Game APIs were checked against a decompilation of the supplied `Game.dll`, `Colossal.UI.Binding`, `Colossal.Mathematics`, `Colossal.Collections`; UI exports against the supplied 1.6.2f1 `index.js`.
- .NET 8 SDK (Ubuntu package), PowerShell 7.4 (dotnet tool), Node 22.
- Not available: Windows game install, Unity Mod Project, ModPostProcessor run. Official postprocessing/deployment and every game check are **NOT RUN**.

## Task ledger

| Task | Commit | Evidence |
| --- | --- | --- |
| 1 Anchor eligibility + multi-Entrance | `61d5f65` | policy/enumeration/order tests RED (missing types) → GREEN; suite 109/109 |
| 2 Structured module panel contract | `14f613e` | C# DTO/dispatcher/link-key tests RED → GREEN (115); UI ModulePanel RED → GREEN, slider resend bug found by test and fixed; UI 39/39 |
| 3 Park state + pure models | `6bba128` | 60+ model tests RED → GREEN; two test-arithmetic errors corrected (see below); suite 171 |
| 4 Facility classifier + landscape | `3335f9c` | classifier/tally/scheduler RED → GREEN; API build 0/0 |
| 5 Visit observation | `bd0f0bc` | cadence/defaults/recorder RED → GREEN; tracker transition/overlap/seeding tests from Task 3 |
| 6 Quality | `aa0231c` | evaluator tests RED → GREEN (193) |
| 7 Finance | `1687ec9` | admission plan/elapsed/suspended tests RED → GREEN (201) |
| 8 Progression + facade + load | `08391fa` | step/facade tests RED → GREEN (209) |
| 9 Park UI, localization, checklist | `08a4462` | builder/localization tests RED → GREEN (220); ParkPanel UI RED (check kind) → GREEN (46) |
| Review fixes | `4aec2f1`, `1285d5d` | sliced landscape scan (slice test RED → GREEN), panel cache (RED → GREEN) |

Test corrections made during Task 3 (the tests, not the rules, were wrong): the settlement test used 0.4/day twice and expected 1 unit (0.8 < 1), now 0.6/day; the 24-slice settlement did not cover a whole day because 262144 is not divisible by 24, now 16 slices; the size-normalization test gave a 16 ha park a single decoration and lamp prefab, now scaled prefab variety.

## Final automated verification

- `scripts/verify.ps1 -ApiCheck`: exit 0 — source/provenance guards, MSBuild JSON tests, build-contract tests, offline contracts, NUnit 222/222, net8 API-check build 0 warnings/0 errors, Vitest 46/46, webpack production build.
- Separate net48 API compile of the committed source against the supplied DLLs (Microsoft .NET Framework 4.8 reference assemblies): 0 warnings/0 errors.
- Neither compile is a playable mod; see `scripts/runtime-smoke-checklist.md` (Park section, 20 items, all NOT RUN).

## Review follow-up (issues #20–#29)

A whole-branch review filed #20–#30; all except #30 (in-game verification, cannot be done here) were fixed in the requested order, each RED → GREEN:

| Issue | Commit | Change |
| --- | --- | --- |
| #20 | `78866a0` | money transfers in a scheduled `IJob`; visit observation in an `IJobChunk`; `Renter`-free entrance/visit traits (sync behavior verified in the supplied `Unity.Entities.dll`) |
| #21 | `9024688` | ineligible/demolished entrances listed and removable; explanatory summary |
| #24 | `4ba776e` | no satisfaction samples before the first Quality evaluation; unevaluated Parks evaluated first |
| #22 | `f9ab7e8` | Decoration only for vanilla PropsPark/PropsStone menu props |
| #23 | `4c99ad1` | transit counted as distinct access points and modes |
| #25 | `f0602bd` | unpaid admission as a rolling day, shown in Finances |
| #26 | `116995f` | no duplicate Level/XP rows (`ShowsProgress`) |
| #27 | `8af6705` | triangle-bounds prefilter for landscape point queries |
| #29 | `41c0e72` | footpaths reaching entrances (6% of Accessibility) |
| #28 | `1ae1b94` | C#-generated UI fixture, Park offline contracts, dispatcher tests |

Final `scripts/verify.ps1 -ApiCheck`: exit 0 — NUnit 250/250, Vitest 52/52, guards/contracts, net8 API build 0/0. Separate net48 API compile 0/0. Runtime items (including new item 21 and the prop-menu/transit/job assumptions) remain NOT RUN (#30).

## Pre-Tourism Wave A1–A5 (Park)

Branch `claude/pre-tourism-wave-a1-a5-l5pses`, base `c04ebf4cf586b4d46532662000dc650f62df0e2f`. Adds runtime trend rows (A1) and bounded landscape diagnostics (A5): a fixed 4×4 evidence grid fed by the existing sliced scan, at most four advisory findings, and a read-only camera focus re-validated against the current scan, the object and the Park geometry. Scan budget, Quality inputs, activation and saved state are unchanged.

- Automated: `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` 950/950; Release API-check build against the supplied 1.6.2f1 DLLs 0 warnings / 0 errors; Vitest 104/104; webpack production build; offline contracts, `verify-source.ps1`, `test-source-guards.ps1`; `pwsh scripts/verify.ps1 -ApiCheck` exit 0.
- Runtime: **NOT RUN.** See the "Pre-Tourism Wave A1–A5" section of [the smoke checklist](../../scripts/runtime-smoke-checklist.md).
- Not included: A6 (pedestrian connectivity), A7 (budget/fee what-if) and Waves B–D.
