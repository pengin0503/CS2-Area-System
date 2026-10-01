# Industrial policy effect contracts (1.6.2f1)

Source of truth for the Industrial policy effects of the Area Policy System (design §9.2, §12; plan Task 8). Every candidate below was read in the decompiled 1.6.2f1 `Game.dll` supplied with the project. "Member exists" and "semantics proven" are kept apart. Each candidate names who writes the value, who consumes it, and what removing an effect would have to restore. Nothing here has been verified in game.

## Result

**No Industrial policy has a verified safe vanilla path in 1.6.2f1.** All seven policies (3001–3007) stay visible in the policy panel as `EffectUnavailable`. They cannot be enabled, are never effective and are never billed, and the Industrial scores are untouched.

The adapter framework is in place for when a path is verified:

- `IIndustrialPolicyAdapter`: `PolicyId`, `IsSupported`, `Apply`, `Remove`.
- `IndustrialPolicyEffectController`: applies and removes on effective-state transitions, is idempotent, and starts clean on each load. A failure disables only that Area and policy for the load and is reported once per Area + policy + failure class.
- `IndustrialPolicyEffectSystem`: GameSimulation, before `IndustrialObservationSystem`.
- The shipped adapter set (`IndustrialPolicyAdapters.Create()`) is empty, so the system returns immediately.

| ID | Policy | Required direction (design §12) | Verdict |
|---:|---|---|---|
| 3001 | Enhanced Safety Audits | Stable/safe operation over extreme utilization | Unavailable: fire hazard has no per-building or Area channel |
| 3002 | Environmental Regulation | Environmental burden down, modest production/efficiency trade-off | Unavailable: a pollution channel exists, but the production trade-off has none |
| 3003 | Production Priority | Production/efficiency up, environmental burden up | Unavailable: no production/efficiency channel |
| 3004 | Logistics Priority | Real input/output flow improvement | Unavailable: no logistics control path |
| 3005 | 24-Hour Operations | Operating activity up, noise/operating burden up | Unavailable: no operating-hours channel |
| 3006 | Restricted Operating Hours | Night/noise burden down, productive activity down | Unavailable: noise channel exists, the activity trade-off has none |
| 3007 | Advanced Automation | Productivity up, labor demand down | Unavailable: workers are AI-controlled; no productivity channel |

A policy whose required trade-off cannot be implemented is not shipped as its beneficial half alone. Pollution-down without the production cost would be a different, strictly better policy than the one the design defines.

## Candidates

### Building efficiency (production, productivity)

- **Target.** The `Game.Buildings.Efficiency` buffer: `EfficiencyFactor m_Factor` (byte), `float m_Efficiency`, serialized per element. The product of all entries (`BuildingUtils.GetEfficiency`) scales company production in `ProcessingCompanySystem` and `ExtractorCompanySystem` (`EconomyUtils.GetCompanyProductionPerDay`). Industrial observation already reads it.
- **Writers.** Each factor is owned by one vanilla system through `BuildingUtils.SetEfficiencyFactor(buffer, factor, value)`. Examples:
  - `WorkProviderSystem` writes `NotEnoughEmployees` and `EmployeeHappiness`.
  - `ProcessingCompanySystem.UpdateEfficiencyFactors` writes `CityModifierIndustrialEfficiency`, `SpecializationBonus` and `LackResources`.
  - `BuildingStateEfficiencySystem` writes `Destroyed`, `Abandoned` and `Disabled`.
  - `SetEfficiencyFactors`, which clears the buffer, is called only by the editor's `AnimationControlsPanel`.
- **Why rejected.** `EfficiencyFactor` has 32 members plus `Count` and no slot a mod can own (pinned by `IndustrialApiContractTests`).
  - Writing a vanilla factor races its owner, which rewrites it on its next update. It also shows a false cause in `EfficiencySection`, for example "Specialization bonus".
  - An out-of-range factor value would persist in the save after the mod is removed, permanently changing the building. It would also appear in `EfficiencySection` without a label.
  - `CityModifierType.IndustrialEfficiency` is city-wide, not per Area.
- **Removal requirement for any future use.** The factor must be removed from every member building before each save and on policy removal.

### Workforce (labor demand)

- **Target.** `Game.Companies.WorkProvider.m_MaxWorkers` on each company.
- **Writer.** `IndustrialAISystem` raises or lowers it every company update from profitability, stock and fitting workers, clamped to `[kMinimumEmployee, fitting workers]`. `PropertyProcessingSystem` resets it on move-in, and `ExtractorAISystem` adjusts it for extractors.
- **Why rejected.** A policy write would fight the company AI and be overwritten. Lowering workers also lowers production through `GetCompanyProductionPerDay(employees)`, which is the opposite of "Advanced Automation".

### Pollution (environmental burden, noise)

- **Target.** `Game.Buildings.PollutionEmitModifier`:
  - Fields: `m_GroundPollutionModifier`, `m_AirPollutionModifier`, `m_NoisePollutionModifier`.
  - Formula: `pollution += modifier × pollution`, pinned executable in `IndustrialApiContractTests`.
  - It is added by the `Pollution` prefab component to every building prefab that has it, and it is serialized with the building.
- **Writer.** In vanilla, only `BatteryAISystem`, and only on installed battery upgrade entities. A building's own modifier is otherwise free.
- **Consumers.**
  - `BuildingPollutionAddSystem.BuildingPolluteJob` applies the building's own modifier after `GetBuildingPollution`, so the ground, air and noise maps change.
  - The selected-building `PollutionSection` and the pollution info-view colours (`ObjectColorSystem`) call `GetBuildingPollution`, which applies only the upgrades' modifiers. They would not show the change on the building itself.
- **Constraints any future adapter must meet.**
  - Record the value it replaced.
  - Restore that value on removal, and before every save. The restore must run in the Serialize phase before `SerializerSystem`, and the effect is re-applied on the next simulation update, so an uninstalled mod leaves no residue.
  - Leave alone a value another writer changed since.
  - Accept that the building's own pollution panel does not show the change.
- **Why not shipped.** For 3002 and 3006 the required production or activity trade-off has no channel (above). For 3003 and 3005 the pollution increase is only the burden half of the policy.

### Fire hazard (Enhanced Safety Audits)

- **Target.** `EventHelpers.FireHazardData` computes building fire hazard from prefab data, `DistrictModifierType.BuildingFireHazard` on the building's vanilla `CurrentDistrict`, and city modifiers.
- **Why rejected.** Area System Areas are `SpacePrefab` instances, not vanilla Districts (`ManagedAreaLifecycleSystem`), so district modifiers never reach Area members. `LocalModifierType` (crime, forest fire, wellbeing, health) comes from service-building prefabs, not per-building writes. No per-building fire-hazard modifier exists.

### Logistics, operating hours

- **Logistics.** Cargo dispatch, trucks and pathfinding (`ResourceBuyerSystem`, `CitizenPathfindSetup`, cargo station AIs) have no per-building priority or flow modifier. Changing pathfinding costs would affect all traffic, not one Area.
- **Operating hours.** Evening and night shift probabilities are prefab data (`WorkplaceData.m_EveningShiftProbability` and `m_NightShiftProbability`), shared by every instance. No per-company operating-hours state exists.

### Modifier enums

`BuildingModifierType` has only `ParkingFee`. `DistrictModifierType` has no production, efficiency, pollution or logistics member. Both are pinned in `IndustrialApiContractTests`, so a game update that adds a usable member fails the test and prompts a re-check.

## Observation boundary

- Industrial Performance, Production, Logistics, Employment and Efficiency come only from `IndustrialObservationSystem` reading vanilla state.
- `IndustrialPolicyAdapterTests` pins that the score producers take no policy input and that the adapter layer has no access to Industrial score types. A future adapter can change a score only by changing vanilla state that observation then reads.

## Runtime checks (not run; in-game verification needed)

1. All seven Industrial policies show as unavailable in the policy panel, cannot be turned on, and add no Policy Upkeep.
2. Industrial scores of an Area are unchanged by the presence of the policy section.
