# Area Core requirements audit — 2026-09-30

This file describes the **current repository state** after the Area Core implementation was committed to `main` and the subsequent offline-review repair work. The detailed execution ledger in `area-core-execution-record.md` is historical and intentionally preserves the branch/state that existed when each task originally ran.

## Current implementation status

Area Core source is present on `main`. The original implementation verification recorded **56/56 C# tests**, **7/7 UI tests**, source/provenance guards, a fresh UI bundle, and net8/net48 API compilation. Those results predate the repair below and are historical evidence only; they must not be treated as a fresh verification of the repaired sources.

A post-merge review found issues that can be corrected without running Cities: Skylines II. The repair is developed on `fix/area-core-offline-review` and includes regression coverage for each source-level defect:

| Requirement / defect | Repair |
|---|---|
| Approved spec/plan remain readable source-of-truth documents | Restored the exact approved UTF-8 blobs from `bd732ae5f10628bb4f28739a293e978268466ce0`. |
| Edit Area targets the selected native area | Management UI now follows the native edit sequence: `recreate = area` → `prefab` → `Mode.Edit` → activate `AreaToolSystem`. |
| One failed area prefab must not disable unrelated types | Prefab readiness is tracked per TypeId; missing/unregistered prefabs retry; ready types can attach to the toolbar independently. |
| Existing saves must recover independently of construction-menu readiness | `AreaLoadCoordinatorSystem` no longer waits for every managed prefab to be ready; native loaded search geometry remains the recovery gate. |
| Missing icon must not be accepted merely because its URI is non-empty | Production icon selection uses the 1.6.2f1 game-media URIs verified for Park, Industrial, Tourism, and the fallback icon, with fallback for unverified URIs. |
| Deleted areas must not leave reverse anchor entries | `AreaAnchorService.ForgetArea` removes reverse entries; deletion queues one final lifecycle validation. |
| Management panel must use the game's localization path | Panel strings, area types, summaries, lifecycle states, and actions now flow through `cs2/l10n`; English and Japanese entries are supplied by the C# localization source. |
| Rename field must reflect backend name changes | React name state resynchronizes from `area.name`. |
| UI failure should not flood the log every frame | Repeated management-UI update failures are logged once until a successful update occurs. |
| Offline regressions should be reproducible | `scripts/test-offline-contracts.mjs` is part of `scripts/verify.ps1`, and focused C#/UI regression tests cover readiness, anchor pruning, icon fallback, localization, and rename synchronization. |

## Verification boundary

The repaired branch still requires a fresh Windows/game-toolchain run before any claim that it builds or works in-game:

```powershell
cd UI
npm ci
cd ..
./scripts/verify.ps1 -ApiCheck
./scripts/verify.ps1
```

The first command set exercises source guards, offline contracts, NUnit tests, UI tests/build, and API compilation. The second additionally requires the official Windows CS2 modding toolchain/postprocessor/deployment environment.

After a playable build is produced, all cases in `scripts/runtime-smoke-checklist.md` remain mandatory. In particular, native menu/tool input, actual area editing/rendering, overlap warning/barrier ordering, real save/load reconstruction, deployed UI discovery, and city-scale performance cannot be established from source inspection alone.

## Scope boundary

Detailed Park visitor simulation, Industrial production/progression, and Tourism scoring remain outside Area Core v1 exactly as defined by the approved specification. The repair does not invent those deferred gameplay systems and does not introduce Harmony or modify the shipped game UI bundle.
