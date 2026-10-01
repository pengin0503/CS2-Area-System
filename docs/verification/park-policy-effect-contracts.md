# Park policy effect contracts (1.6.2f1)

Source of truth for the Park policy effects of the Area Policy System (design §9, §11; plan Task 7). The vanilla members below were read from the decompiled 1.6.2f1 `Game.dll` supplied with the project. Nothing here has been verified in game; the runtime checks are listed at the end and in `area-policy-system-runtime.md`.

## Summary

| ID | Policy | Class | Status | Effect |
|---:|---|---|---|---|
| 2001 | Enhanced Maintenance | Area-internal | Available | Park management funding ×1.25: the real Area Management expense and the Maintenance coverage both rise by 25% |
| 2002 | Night Events | Vanilla simulation | EffectUnavailable | No verified safe path (below) |
| 2003 | Quiet Operations | Vanilla simulation | EffectUnavailable | No verified safe path (below) |
| 2004 | Landscape Protection | Area-internal | Available | Vegetation and decoration effective counts ×1.25 in the landscape model; comfortable capacity ×0.85 |
| 2005 | Premium Services | Area-internal | Available | Fair admission fee ×1.3 in value for money |
| 2006 | Major Event Attraction | Vanilla simulation | EffectUnavailable | No verified safe path (below) |

The magnitudes are the named constants in `ParkPolicyEffects` and are stated in each policy description (`ParkLocalization`). `ParkPolicyEffectTests` pins them.

## Area-internal effects

Every effect changes a Park model input. Park Quality stays the plain weighted six-metric model (`ParkQualityModel.ComputeQuality`); no policy adds points after it. The neutral value of every factor is exactly the pre-policy behaviour, and an invalid factor (NaN, infinite, ≤ 0, or a capacity share above 1) falls back to neutral.

### 2001 Enhanced Maintenance

- `ParkFinanceModel.ComputeManagementCoverage(budget, fundingMultiplier) = ClampBudget(budget) × fundingMultiplier`.
- `ParkFinanceModel.ComputeManagementCostPerDay(need, budget, fundingMultiplier) = need × coverage`, so every point of coverage is paid for with real money.
- `ParkFinanceSystem` uses this cost for the Area Management expense (shared Area finance, Service Upkeep).
- `ParkQualityEvaluator` uses the same coverage for the Maintenance metric (`ComputeMaintenance`). Coverage above 1 keeps the existing diminishing returns of `CoverageScore`.
- The policy's own upkeep (`AreaPolicyUpkeep`, 300 + 20/ha + 25/facility per day) is billed separately as Policy Upkeep, as every policy is (design §13). The extra 25% management money is the effect itself, not the policy's upkeep.

### 2004 Landscape Protection

- `ParkLandscapeModel.ComputeLandscape(inputs, scenicCredit)` multiplies the effective counts of Vegetation and Decoration before the same fill curve and category caps. Amenities, Lighting, Paths, Diversity, clutter and object counts are unchanged.
- The scan (`ParkLandscapeSystem`) keeps the raw result. The Quality evaluator derives the effective result from the scanned inputs when the policy is in effect and stores it as `ParkQualitySnapshot.EffectiveLandscape`.
- The Landscape metric, Attractiveness, Accessibility path provision, visit experience and the Landscape tab all read that result.
- `ParkQualityModel.ComputeComfortableCapacity(inputs, usableShare)` multiplies the comfortable capacity by 0.85: part of the Park is closed to visitors. A share above 1 is rejected, so a policy can never manufacture capacity.
- The landscape credit reaches Level and Landmark landscape checks through the Landscape metric. That is the intended direction ("improves landscape-oriented quality").

### 2005 Premium Services

- `ParkFinanceModel.ComputeValueForMoney(fee, quality, fairFeeMultiplier)` scales the fair fee `4 + 36·q^1.5` by 1.3.
- The fee charged is still the player's fee; only how visitors judge it changes.
- Value for money feeds the visit experience (`ParkVisitExperience.ValueForMoney`), so satisfaction changes only through real visits. Quality is untouched directly.

### Runtime wiring

- `ParkPolicyEffectReader` asks `AreaPolicySystem.IsEffective(area, id)` for 2001, 2004 and 2005 only. It is used by `ParkQualitySystem` and `ParkFinanceSystem`.
- `IsEffective` already covers Level locks, Suspended Areas, conflicts and effect availability.
- A failing read keeps that Park on the neutral baseline and logs once per system (failure isolation).
- Effects are read on every evaluation and settlement, never cached, so turning a policy off removes it at the next Quality evaluation and the next management settlement. There is no stored residue.

## Vanilla candidates examined and rejected

The design allows a vanilla-effect policy only with a documented, safe, observable and removable path (design §9.2). None of the candidates below meets that bar, so 2002, 2003 and 2006 stay visible but `EffectUnavailable`. They are not effective and not billed. Their descriptions keep the direction only, and no visitor or noise state is faked in Park inputs.

### Attractiveness (Night Events, Major Event Attraction)

- **Where the value comes from.** `Game.Buildings.AttractivenessProvider.m_Attractiveness` is recomputed by `Game.Simulation.AttractionSystem` on each building's update frame. The inputs are `AttractionData` from the prefab and upgrades, `Efficiency` and the Park maintenance state. There is no per-building, district or city modifier channel for it: `BuildingModifierType` has only `ParkingFee`, and `DistrictModifierType` has no attractiveness or leisure member.
- **Why writing it is unsafe.** A write would race the vanilla writer every update and be overwritten on the building's next update frame.
- **Why it is out of scope.** The value is consumed by `TourismSystem`, which sums the squared attractiveness into the city tourism attractiveness. The design keeps Park effects away from vanilla tourism in v1, and Tourism policies are not implemented.
- **Visitor choice.** `CitizenPathfindSetup.SetupAttractionJob` uses the same value for tourist destination choice. Resident leisure targets (`SetupLeisureTargetJob`) are weighted by the prefab's `LeisureProviderData` and service availability. Changing either would mean editing shared prefab data, which changes every instance of the prefab, or changing pathfinding.
- **Vanilla events.** `Game.Events.CalendarEvent` / `CalendarEventLaunchSystem` events are city-wide citizen-targeted happenings chosen from prefab data, not location-bound park attendance. They are not a path for Park visitor pressure.

### Noise (Quiet Operations, Night Events)

- **The candidate channel.** `Game.Buildings.PollutionEmitModifier` (fields `m_GroundPollutionModifier`, `m_AirPollutionModifier`, `m_NoisePollutionModifier`; `pollution += modifier × pollution`) is a per-instance component. It is added to every building whose prefab has the `Pollution` component, and it is serialized with the building.
- **The only vanilla writer.** `BatteryAISystem` writes it, and only on installed battery upgrades.
- **Where it applies, and where it does not.** `BuildingPollutionAddSystem.BuildingPolluteJob` applies the building's own modifier after `GetBuildingPollution`, so the noise map would change. The selected-building pollution section (`PollutionSection`) and the pollution info-view colours (`ObjectColorSystem`) call `GetBuildingPollution`, which applies only the upgrades' modifiers. The vanilla UI for the building would therefore not show the change.
- **Residue.** The component is saved with the building. A policy-written value would stay in the save after the policy is removed or the mod is uninstalled unless it was restored before every save.
- **Unknown size of the effect.** Park prefabs' noise values are asset data, not in the decompile, so it is unproven that Park members emit noise at all. Where they do not, the policy would do nothing.
- **Visitor pressure.** The "reduces visitor pressure" half of Quiet Operations has no path at all (see Attractiveness above).

Ruling: no adapter writes `PollutionEmitModifier` for Parks in this release. The channel and its constraints (save-time restore, UI mismatch) are recorded here for the Industrial adapters (plan Task 8) and for any later Park work.

## Runtime checks (not run; in-game verification needed)

1. Enable Enhanced Maintenance on an Active Park with the budget slider at 100%. Expected:
   - The Finances tab's management cost per day rises by 25%.
   - The Area Management expense rises by the same amount at the next settlement.
   - The Maintenance metric reflects 125% coverage.
   - Disabling it restores both at the next settlement and evaluation.
2. Enable Landscape Protection. Expected:
   - The Landscape tab's Vegetation and Decoration rows rise, the other rows are unchanged, and comfortable capacity drops by 15%.
   - Disabling it restores the scan values at the next Quality evaluation.
3. Enable Premium Services on a Level 4 Park with a high fee. Expected:
   - Value for money rises.
   - The fee collected per visit is unchanged.
4. Suspend the Park (remove its entrance). Expected: none of the effects apply and no policy upkeep is billed. Restore the entrance and the effects resume.
5. Night Events, Quiet Operations and Major Event Attraction show as unavailable in the policy panel and cannot be enabled.
