# CS2 Area System — Area Core, Park Area v1 and Industrial Area v1

Targets Cities: Skylines II **1.6.2f1**. Area Core source implements Park, Industrial and Tourism management boundaries using native `AreaToolSystem`, `SpacePrefab` (an `AreaPrefab`) and native toolbar/asset-menu data. Areas never replace the vanilla simulation: Park and Industrial observe it, and Tourism is a boundary only.

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

The [offline CI workflow](.github/workflows/verify.yml) runs on pushes to main and pull requests. It checks the UI tests/build, offline contracts, source guards and MSBuild output parser without game DLLs. C# tests and API compilation still require local game references and `./scripts/verify.ps1 -ApiCheck`; CI does not verify in-game behavior.

## Run and required game verification

Use an official-toolchain build in a fresh 1.6.2f1 session. Open **Areas → Area Types**, select one of the three kinds and use native area controls. Completed areas are not selectable with the vanilla default tool, so open **Managed areas** (top right) and pick an area. The game's own selected-info panel opens, like for a district: its header shows and renames the area and offers locate, and the area section below shows type, status, entrance/headquarters and progress with Edit/Delete. For Industrial, assign an office building inside the area as Headquarters. For Park, assign a Park Maintenance facility as Headquarters and connect a dedicated pedestrian Path from outside to inside the boundary; both requirements must be met. Assignment shows a hint with Cancel until an eligible building is clicked. The Headquarters and Entrance groups are separate; explicit Entrance assignment is available only when a semantic provider registers an eligible gate asset. Tourism requires no anchor and is explicitly labeled boundary only: tourism gameplay is not implemented. Reassign facilities through the same flow. A management boundary does not place or clear normal city assets.

## Park Area v1

A Park activates with a valid Park Maintenance Headquarters **and** at least one entrance: a dedicated pedestrian Path crossing from outside to inside its boundary, or an explicitly registered Entrance building. Ordinary road sidewalks do not count as automatic entrances. Detected Path evidence is rebuilt after load and network/boundary edits; it does not force visitor routing. Old leisure/attraction Entrance links remain visible and removable but no longer activate a Park by themselves. The Park observes real vanilla leisure: citizens doing leisure at its park/leisure/attraction buildings become visits (residents and tourists separately). Overlapping Parks count a visit only in the Park with the smaller stable ID. Park Quality (0–100) combines Visitor Satisfaction 25%, Attractiveness 20%, Accessibility 15%, Variety 15%, Maintenance 15% and Comfort 10%; landscape (vegetation, rest amenities, decoration, lighting, footpaths) is analyzed inside the Park only, with diminishing returns for repeated objects. Visits and strong Quality earn XP; five levels (Local, Neighborhood, City, Major, Landmark Park) never demote. The Park budget (50–150%) pays an area-management cost from city money and drives Maintenance; from Level 2 an admission fee moves real household money to the city once per visit (never below zero). The selected-info panel shows Overview, Visitors, Finances, Landscape and Progress tabs. v1 does not change citizens' destination choice, happiness, land value, demand or vanilla tourism, and has no Park events. Available Area policies retain their existing effects and upkeep; unavailable policies remain locked. The React Park tests read `UI/src/__fixtures__/park-panel.json`, generated from the real C# payload by `ParkPanelFixtureTests`; after changing the Park panel or its strings, run the C# tests once with `UPDATE_UI_FIXTURES=1` and commit the updated fixture. Park runtime checks are listed in [the smoke checklist](scripts/runtime-smoke-checklist.md) and have **not been run**.

## Industrial Area v1

An Industrial Area is drawn with the native area tool like the other types and stays **Suspended** until an ordinary office building inside it is assigned as its **headquarters** (no custom asset is needed). Factory, extractor, warehouse and cargo capability alone does not qualify; legacy links remain visible as invalid until replaced. Once accepted, the headquarters keeps the Area **Active** through tenant vacancy, bankruptcy or poor operating health; only demolition, leaving the boundary or removing the link suspends it, and reassignment keeps Level/XP. The Area observes the real vanilla companies inside it once per game hour (at most 128 building reads per update, round-robin over Areas, in a scheduled job): each building is credited only to Core's primary Industrial Area for it, offices are never counted, and nothing in the economy is modified. Four 0–100 components are shown with their availability: **Production** (65% productive activity — the game's own current production rate divided by the company's rate at full staff and 100% efficiency — 25% active producers, 10% output diversity), **Logistics** (50% input continuity, 50% output/dispatch continuity over the last six game hours, at least four observed hours per channel; warehouses or cargo terminals give no points by themselves), **Employment** (75% job fill, 25% staffing balance) and **Efficiency** (70% vanilla building efficiency, 30% vanilla profitability). Industrial Performance combines them 35/25/20/20 and up to three bottlenecks are listed with their evidence. Missing data is shown as unavailable, never as zero. Five permanent levels (Local Industry, Industrial District, Production Hub, Regional Industrial Center, Industrial Powerhouse) are earned with hourly XP (at most 10 per game hour and 240 per game day, no Area-size multiplier) plus facility/employee/score gates; Level never demotes and gives no production, cargo, efficiency, profitability, tax or demand bonus. Industrial has no subsidies; existing available Area policies remain unchanged and unavailable policies stay locked. The selected-info panel shows Overview, Production, Logistics, Employment and Progress tabs; the React tests read `UI/src/__fixtures__/industrial-panel.json`, generated by `IndustrialPanelFixtureTests` (rerun the C# tests with `UPDATE_UI_FIXTURES=1` after changing the panel or its strings). Field semantics and the calculation examples are documented in [the observation contracts](docs/verification/industrial-observation-contracts.md) and [the score examples](docs/verification/industrial-score-examples.md). The Industrial runtime checks in [the smoke checklist](scripts/runtime-smoke-checklist.md) have **not been run**: in-game behavior, live field semantics and performance remain unverified.

## Foundation + Phase 1

The Landscape tab adds read-only pedestrian connectivity diagnostics. A Complete scan can identify disconnected facilities and focus them; a Partial or Unknown scan does not declare a facility disconnected. The Finances tab offers budget/fee what-if estimates: candidate sliders do not change settings or money; only the explicit apply action changes the selected setting. Estimates hold current visitor volume and Quality constant and do not predict demand.

Park and Industrial retain up to **168 hourly history buckets** (seven game days) per Area, with the latest 24 seeded into runtime trends after load. Industrial Overview compares managed Industrial Areas using current metrics and marks suspended, delayed or old observations as stale. A versioned semantic registry lets providers explicitly register eligible mod asset roles; typed vanilla facts remain the default. These additions preserve existing policies, Level/XP, finance and primary ownership. Phase 2, Phase 3 and Tourism gameplay are not included.

Automated API builds are not playable MODs. The [Foundation + Phase 1 runtime checklist](scripts/runtime-smoke-checklist.md#foundation--phase-1--cities-skylines-ii-162f1-runtime-verification) remains **NOT RUN** and includes activation, network edits, save/load, provider removal and performance checks.

Behaviour options (initial expansion of the managed area list, camera focus, panel side, delete confirmation, whether managed areas may overlap) are in **Options → CS2 Area System**.

The **Managed areas** selection button remains available when the initial-expansion option is off; the list starts collapsed and can be opened at any time. Changing the option applies once, and manual opening/closing is preserved until the option changes or a new session starts.

Follow [the twelve runtime checks](scripts/runtime-smoke-checklist.md). They have **not been run in this workspace**; runtime readiness, native menu/tool behavior, real save/load and UI deployment need game verification before declaring Area Core operational.

Unknown schema-v1 TypeIds retain their identity and become ModuleUnavailable. Unsupported binary component schemas are explicitly rejected before interpreting fields or overwriting a save. Membership is derived and is never serialized.

Copied/adapted GPL-3.0 portions and exact destinations are recorded in [THIRD_PARTY.md](THIRD_PARTY.md).

## License

This project is licensed under **GPL-3.0-only**. See [LICENSE](LICENSE) and [THIRD_PARTY.md](THIRD_PARTY.md) for third-party provenance. Game DLLs and media are not included.
