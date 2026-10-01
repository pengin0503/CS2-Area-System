# Industrial Area v1 — observation contracts (Cities: Skylines II 1.6.2f1)

Evidence base: the supplied `CS2-All-Managed-1.6.2f1-fix` `Game.dll`, decompiled with ILSpy 9.1, and the supplied 1.6.2f1 UI `index.js`. Each source below names the vanilla **writer** (the system that sets the value) and **consumer** (a vanilla system or UI section that interprets it). A public member's existence alone was not accepted as meaning.

`IndustrialApiContractTests` pins every selected member by compilation/reflection, and runs the pure vanilla functions that the normalizations depend on (`CompanyUIUtils.GetProfitabilityKey`, `EmploymentData.GetWorkplacesData`, `TimeSystem.GetDay`, `EconomyUtils.GetCompanyProductionPerDay`, `StorageLimitData.GetAdjustedLimitForWarehouse`). **These tests prove API shape and formula agreement only. None of the live in-game semantics below has been runtime-validated; everything is NOT RUN in game** (see `industrial-area-v1-execution-record.md`).

Attribution for every metric: Core's building member → only that building's own components, its prefab, and the companies in its `Game.Buildings.Renter` buffer that carry `Game.Companies.CompanyData` (the vanilla `CompanyUIUtils.HasCompany` rule). Exclusive metrics are credited only to Core's primary Industrial Area for that building. Core membership is never extended to companies.

## Classification (static capability; survives vacancy)

| Role | Building / prefab evidence | Tenant evidence | Vanilla writer/consumer |
|---|---|---|---|
| Extractor | `ExtractorProperty`; `Game.Buildings.ExtractorFacility` or prefab `ExtractorFacilityData` (extractor upgrade/sub-building) | `Game.Companies.ExtractorCompany` | `BuildingProperties.AddArchetypeComponents` adds `ExtractorProperty` when `m_AllowedManufactured` contains an extractor resource; `CompanySection.GetIndustrialType` |
| Processor | `IndustrialProperty` and prefab `BuildingPropertyData.m_AllowedManufactured` containing a non-office, non-extractor resource | `ProcessingCompany` + `IndustrialCompany`, not `ExtractorCompany`, `OfficeCompany`, `CommercialCompany` or `ServiceAvailable` | `BuildingProperties`; `ProcessingCompany` prefab adds `OfficeCompany` for office output; `CompanySection.GetIndustrialType` |
| Storage | `StorageProperty`, or `IndustrialProperty` + `m_AllowedStored` | `Game.Companies.StorageCompany` | `BuildingProperties`; `StorageSection` |
| Cargo | `Game.Buildings.CargoTransportStation` or prefab `CargoTransportStationData` | — | `StorageSection` (`StorageType.Cargo`) |
| SupportingWorkplace | descriptive only: Storage/Cargo facility without a producer role that offers jobs | — | — |

Offices (`OfficeProperty`/`OfficeCompany`), residential, commercial, parks and civic buildings receive no role and are never HQ-eligible. `ExtractorFacility.m_Flags.Working` is an animation state written by `ExtractorFacilityAISystem` and is **not** used as a production signal. `CompanyNotifications.m_NoInputCounter`/`m_NoCustomersCounter` have no writer in 1.6.2f1 and are **not** used.

## Metrics

| Metric | Source (type.member) | Vanilla writer → consumer | Unit / normalizer | Availability decision |
|---|---|---|---|---|
| Jobs | `WorkProvider.m_MaxWorkers` on each industrial tenant (or the cargo/storage building itself) → `EmploymentData.GetWorkplacesData(max, SpawnableBuildingData.m_Level, WorkplaceData.m_Complexity).total` | `IndustrialAISystem`/`WorkProviderSystem` → `EmployeesSection` | count; test proves `.total == m_MaxWorkers` | Available; negative → invalid/unavailable; zero excluded |
| Employees | `Employee` buffer length on the same entity | `EmployeesSection` | count | Available |
| Vacancy by education level | jobs per level (`GetWorkplacesData`) − employees by `Employee.m_Level` | `FreeWorkplaces.Refresh` (m_Level is the workplace level) | count per level | Diagnostic only |
| Building efficiency | `Game.Buildings.Efficiency` buffer on the building → `BuildingUtils.GetEfficiency(DynamicBuffer<Efficiency>)` | production systems set factors → `EfficiencySection`, `CompanySection` | multiplier ×100 (1.0 = 100%, raw >100 kept; only scoring clamps) | Available when the buffer exists; `Game.Buildings.BuildingEfficiency` is an **empty tag** with no value |
| Profitability | `Profitability.m_Profitability` (byte) | `CompanyProfitabilitySystem`: `clamp(Δtotal worth/100, −127, 128) + 127` → `ProfitabilitySection.OnProcess`: `profit = round(byte·2 − 255)` → `CompanyUIUtils.GetProfitabilityKey` | piecewise-linear 0–100 using the vanilla tier edges −182/−64/32/128 as 20/40/60/80 (`IndustrialSourceNormalization.ProfitViability`); test checks all 256 bytes land in their vanilla tier band | Available for industrial producer/storage companies; it is a worth-change momentum, not absolute profit |
| Productive activity (processor) | actual: `EconomyUtils.GetCompanyProductionPerDay(GetEfficiency(buffer), true, employees, IndustrialProcessData, …)` — exactly `CompanySection.QueryCompanyData`; reference: `GetCompanyProductionPerDay(1f, WorkProvider.m_MaxWorkers, level, true, WorkplaceData, IndustrialProcessData, …)` — the capacity term of `GetCompanyMaxProfitPerDay` | `ProcessingCompanySystem` sets the `LackResources` factor to 0 when it produced nothing, so the displayed rate is 0 while input-limited | `100 · actual / reference` (units/day of the same output) | **Derived sampled rate**, not an accumulated counter; unavailable when reference ≤ 0 or values non-finite |
| Productive activity (extractor) | actual: `CompanyStatisticData.m_LastUpdateProduce` (last update amount × `kCompanyUpdatesPerDay` 256); reference as above | written by `ExtractorCompanySystem`; shown by `CompanySection` | same | Derived sampled rate (last company update) |
| Resource activity | per output resource: Σ weighted activity of producers of that resource | — | unit-free (capacity-normalized), so resources are comparable for the 20%-of-dominant rule | Derived |
| Input starvation (input channel) | process has an input with `m_Amount > 0`; potential rate `GetCompanyProductionPerDay(GetEfficiencyExcludingFactor(buffer, LackResources), …) > 0`; and some input stock `Resources` < `input.m_Amount / output.m_Amount` (cannot make one unit) | same condition `ProcessingCompanySystem.UpdateProcessingJob` uses to produce 0; corroborated by `GetEfficiencyFactor(buffer, LackResources) == 0` (shown in `EfficiencySection`) | affected / not affected per sample | NotApplicable for extractors and input-free processes |
| Output blockage (output channel) | extractor: storage limit (company `StorageLimitData` + `Attached` parent upgrades) − `GetTotalStorageUsed(resources)` ≤ 0; processor: weighted stored amount ≥ company `StorageLimitData.m_Limit` | extractor: `ExtractorCompanySystem` caps production at free storage; processor: `BuyingCompanySystem` stops buying when weighted storage is exhausted, `StorageSection` status `Full` | affected / not affected per sample | Available when a limit exists; otherwise Unavailable. Full stock is a symptom, never asserted as the root cause |
| Storage amount/capacity | warehouse: company prefab `StorageLimitData.GetAdjustedLimitForWarehouse(SpawnableBuildingData, BuildingData)`; cargo/service storage: building prefab `StorageLimitData` combined with `InstalledUpgrade`; stored: Σ `Resources.m_Amount` with `ResourceData.m_Weight > 0`, clamped to capacity | `StorageSection` | units, utilization % | Diagnostic only; no score points |
| Cargo operating | `CargoTransportStation` facility with building efficiency > 0 and not `BuildingOption.Inactive` | `BuildingUtils.CheckOption` | flag | Diagnostic only. Cargo **mode** is Unavailable in v1 (no single public mode member) |
| Operating producer | producer facility whose sampled actual rate > 0 | — | flag | Placement/capacity alone never confirms operation |

## Clock

`IndustrialGameClock` uses `TimeData.m_FirstFrame`, `TimeData.TimeOffset` and `TimeSystem.kTicksPerDay`: `gameHours = ((frame − firstFrame)/kTicksPerDay + timeOffset)·24`, `day = floor(gameHours/24)` — the same formula as `TimeSystem.GetDay` (test compares both). Hour buckets are `floor(gameHours)`.

## Not used (and why)

- Raw inventory deltas: transfers, buying and consumption change `Resources` without production.
- City-level production statistics (`CityProductionStatisticSystem`, `ProcessingCompanySystem.m_ProducedResources`): not attributable to a building.
- `CompanyNotifications` counters: no writer in 1.6.2f1.
- `ExtractorFacility` animation flags.
- Cargo route/terminal usage: not attributable; external cargo is reported as unknown.
