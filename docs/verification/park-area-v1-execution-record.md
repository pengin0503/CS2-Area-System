# Park Area v1 execution record — 2026-09-30

Native / executing-plans in one session; no per-task implementer subagents. Base: `main` at `1b16e5b` (spec + plan commits on top of `9275438`). Spec `docs/superpowers/specs/2026-09-30-park-area-v1-design.md` and plan `docs/superpowers/plans/archive/2026-09-30-park-area-v1-implementation.md` were read in full and not modified.

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

## Foundation + Phase 1 — 2026-10-02

This update supersedes the historical Park entrance-only activation and runtime-only trend assumptions above. Park now requires Park Maintenance Headquarters plus dedicated Path crossing or a registered explicit Entrance. Old leisure Entrance links remain invalid/removable. A6 adds read-only pedestrian connectivity; A7 adds transient budget/fee previews; B1 integrates registered asset semantics into anchors, visits and Quality; C1 persists 168 hourly buckets and seeds the latest 24 distinct hours after load.

Final automated evidence and review decisions are in [the Foundation + Phase 1 execution record](foundation-phase1-execution-record.md). Runtime and official Unity postprocessing/deployment remain **NOT RUN**; use cases F02–F07 and P01–P08 in [the smoke checklist](../../scripts/runtime-smoke-checklist.md). The original v1/Wave A records above describe their dated revisions rather than current prerequisites.

## Phase 2 C5 Park festival — 2026-10-03

Branch `claude/phase2-c5-se4lwg`, stacked on the verified C4 head `c59f063` (PR #87). Plan: `docs/superpowers/plans/2026-10-02-phase2-park-events-implementation.md`.

Adds one internal Park event, the Park festival, started from a new **Events** tab in the Park panel:

- Cost: half a day of the Park's current management cost (rounded, minimum ¢1), charged once through Area Finance as `AreaFinanceKind.ParkEvent` (Area management account). Only one request per Park can be pending; the festival starts only when the payment settles, and starts at the amount actually paid. A zero or cancelled settlement clears the request without starting anything.
- Duration: one in-game day (262,144 frames). Saved in `ParkEventState` (schema 1) with a 16-entry history buffer; the saved end frame is never extended or replayed after load.
- Effects while the Park is Active: comfortable-capacity share ×0.85, completed-visit satisfaction +5 (clamped), visit XP ×1.25 (composed with policy XP). A suspended Park keeps the calendar running but gets neutral effects. No vanilla attraction, leisure, pathfinding or citizen data is written.
- The module registers only when Area Finance and Park gameplay registered; a failure disables it and its effects.

Commits after `c59f063`: `4440978` events model/serialization, `06cca7a` finance settlement, `961a2b9` effects, `dea9cbb` panel, `be602dd` warning-count fix, `ff750be` review fixes, then this record.

Automated verification (final, after the review fixes):

- `pwsh -NoProfile -File scripts/verify.ps1 -ApiCheck`: exit 0 — NUnit 1197/1197, Vitest 113/113, source guards and offline contracts.
- `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release`: 0 warnings / 0 errors.
- `node scripts/test-offline-contracts.mjs`: passed. `git diff --check`: clean.

Final whole-branch review (fresh reviewer): no Critical/Important findings as labelled. Two Minor findings were re-graded and fixed test-first in `ff750be`: a suspended Park now still shows its outstanding festival payment, and a started festival recomputes Park Quality at once. Deferred minors: the "unavailable" warning can show for a brand-new Park until its event state is added on the next ModificationEnd; the XP tooltip does not mention that the +5 satisfaction also raises visit XP; the satisfaction adjustment is outside the per-listener `try`; a history write failure after the state write would leave a festival without a history row; the panel's start decision does not check quarantine (the action does).

Runtime: **NOT RUN.** In-game checks C501–C507 in [the smoke checklist](../../scripts/runtime-smoke-checklist.md) have not been performed.

## Phase 2 C6 Park coverage operating modifier — 2026-10-03

Branch `claude/phase2-c6-se4lwg`, based on the verified C5 head `a146d06` (merged to main as #89). Plan: `docs/superpowers/plans/2026-10-02-phase2-park-coverage-modifier-implementation.md`.

**API gate: proven, not BLOCKED.** `docs/verification/park-coverage-modifier-contract.md` proves the following from the supplied 1.6.2f1 `Game.dll` and Entities 1.3.10 sources:

- the exact fields;
- every native writer, including the native recompute from prefab data on each pass;
- consumer ordering (`ServiceCoverageSystem` before `ParkAISystem`) and job completion;
- the fresh-baseline signal: chunk change version equals `ParkAISystem.LastSystemVersion`;
- the serializer restore hook (before `SerializerSystem`).

A fresh reviewer re-checked each step against the decompiled sources.

**Implementation:**
- **`ParkCoverageModifierModel`:** computes the factor `clamp(1 + 0.20 × (budget × EM funding − 1), 0.90, 1.10)`. It applies the factor to capacity and magnitude only; `m_Range` is untouched. Its tracker restores a building only while the value is still the mod's write.
- **`ParkCoverageModifierSystem`:** runs directly after `ParkAISystem` at the same interval, inheriting its offset.
- **`ParkCoverageModifierSaveSystem`:** restores the mod's values before the serializer.
- **Finances tab:** shows the factor and a "not applied yet" note.
- **Offline guards:** no `m_Range` writes; only the modifier touches `ModifiedServiceCoverage`; the gate stays closed.

**Shipped disabled.** `ParkCoverageModifierGate.RuntimeVerified = false`, so the writer is created with `Enabled = false` and never runs in this build. Phase 2 is **not** complete.

Commits after `a146d06`:

| Commit | Change |
| --- | --- |
| `565d728` | contract |
| `71eb327` | model |
| `ecd8b99` | writer and save restore |
| `21c7abc` | panel, guards, checklist |
| (this commit) | review rulings and docs |

Automated verification:
- `scripts/verify.ps1 -ApiCheck` exits 0.
  - NUnit: 1239/1239.
  - Vitest: 113/113.
  - Source guards and offline contracts pass, with negative checks for the gate and for `m_Range`.
- Release API-check build: 0 warnings.
- `git diff --check`: clean.
- The test project keeps its 6 pre-existing CS8632 warnings.

Review rulings:
- Ownership loss, suspension and budget changes take effect at the next native pass (≤1024 frames).
- `Shutdown()` must be wired to an off switch by whatever change opens the gate.

Deferred minors:
- The system tests live in the model test file.
- System behaviour is covered only by source guards.
- There is a theoretical bitwise-equality coincidence on restore.

Runtime: **NOT RUN.** The release gate C601–C606 in [the smoke checklist](../../scripts/runtime-smoke-checklist.md) has not been performed, and the gate must stay closed until it passes.

## Runtime verification addendum — 2026-10-04

**PASS — user-confirmed.** The user reports all Park v1, Phase 2 C5 Festival and Phase 2 C6 coverage-modifier in-game checks completed on Cities: Skylines II 1.6.2f1 and behaved normally. The original NOT RUN statements above remain as implementation-time history; they are not rewritten as automated evidence. Exact local save/build metadata was not supplied in this chat.

Runtime-confirmation Issues #30, #50, #53 and #55 were closed as completed. C6 is therefore release-eligible; the readiness branch opens `ParkCoverageModifierGate.RuntimeVerified` and adds an Options off switch.
