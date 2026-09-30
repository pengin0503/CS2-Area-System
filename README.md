# CS2 Area System — Area Core

Targets Cities: Skylines II **1.6.2f1**. Area Core source implements Park, Industrial and Tourism management boundaries using native `AreaToolSystem`, `SpacePrefab` (an `AreaPrefab`) and native toolbar/asset-menu data. Detailed visitor, production and tourism simulation is outside this implementation.

`LotPrefab` is editor-only for native new-area drawing in 1.6.2f1. `SpacePrefab` initializes editable non-clearing geometry and passes the gameplay creation gate. An additive ECS system reuses public native validation helpers on updated managed previews: only permitted managed-area overlap errors become warnings, while invalid shapes, city limits and vanilla-area conflicts stay blocking. Neither `ToolSystem.ignoreErrors` nor native systems are replaced. Actual game input, rendering and warning/barrier timing still require the runtime checks below.

## Build and verification

Install the official game Modding Toolchain and set its `CSII_*` environment variables. Game DLLs/toolchain/media are not bundled. Install Node 18+ and .NET SDK, then:

```powershell
cd UI
npm ci
cd ..
./scripts/verify.ps1
```

Default mod build targets **net48**, imports official `Mod.props` / `Mod.targets`, runs Unity postprocessing, then deploys to the configured local Mods directory. UI dependencies are mandatory for deployment; the current UI is rebuilt and copied after the toolchain's clean deployment step.

For portable unit/API checks using supplied game DLLs:

```powershell
./scripts/verify.ps1 -ApiCheck
```

This profile targets net8, uses the actual game references, and deliberately skips Unity analyzers/postprocessing/deployment. Its assembly is **not a playable build**. A net48 API compilation also checks framework compatibility; neither compilation proves in-game behavior.

## Run and required game verification

Use an official-toolchain build in a fresh 1.6.2f1 session. Open **Areas → Area Types**, select one of the three kinds and use native area controls. Completed areas are not selectable with the vanilla default tool, so open **Managed areas** (top right, above the management panel) and pick an area to select it and open its management panel. For Park/Industrial, choose Assign entrance/headquarters, then select an existing vanilla building inside that area. Tourism requires no anchor. Reassign facilities through the same flow. A management boundary does not place or clear normal city assets.

Behaviour options (managed area list, camera focus, panel side, delete confirmation, whether managed areas may overlap) are in **Options → CS2 Area System**.

Follow [the twelve runtime checks](scripts/runtime-smoke-checklist.md). They have **not been run in this workspace**; runtime readiness, native menu/tool behavior, real save/load and UI deployment need game verification before declaring Area Core operational.

Unknown schema-v1 TypeIds retain their identity and become ModuleUnavailable. Unsupported binary component schemas are explicitly rejected before interpreting fields or overwriting a save. Membership is derived and is never serialized.

Copied/adapted GPL-3.0 portions and exact destinations are recorded in [THIRD_PARTY.md](THIRD_PARTY.md).
