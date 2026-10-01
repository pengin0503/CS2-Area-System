# CS2 Area System — Area Policy / Ordinance System Design

**Date:** 2026-10-01  
**Target game version:** Cities: Skylines II 1.6.2f1  
**Repository:** `pengin0503/CS2-Area-System`  
**Status:** Approved design specification; implementation not started

## 1. Purpose

Add a reusable Area policy/ordinance subsystem to CS2 Area System.

The subsystem must feel like the base game's District policy UI and behavior while remaining compatible with the existing Area Core, Park Area, and Industrial Area architecture. It must also provide a stable foundation for future Area modules such as Campus, Business, Transport Hub, Commercial, and other management areas.

Tourism Area policy effects are not part of the first implementation. Tourism remains boundary-only until its gameplay layer is designed separately.

## 2. Design goals

The system is successful when:

1. Policies are configured independently per managed Area instance.
2. Basic policies are available from Level 1 and stronger/specialized policies unlock across Levels 2–5.
3. There is no global limit on the number of simultaneously enabled policies.
4. Explicitly opposing policies cannot be active at the same time.
5. Every enabled policy has a maintenance cost.
6. Policy maintenance and Area-generated income/expenses use the vanilla city finance model instead of a separate mod wallet.
7. Policy UI uses the vanilla District policy interaction model and, where possible, the actual vanilla policy UI components.
8. Park/Industrial gameplay remains module-owned; Area Core never accumulates Park- or Industrial-specific effect logic.
9. Policies that modify vanilla simulation only ship after their real 1.6.2f1 effect path has been verified in-game.
10. Failure in one policy or adapter does not disable unrelated Areas or the vanilla selected-info panel.

## 3. Explicit non-goals

This design does not include:

- a separate policy point/currency system;
- a fixed number of policy slots;
- city-wide bulk policy application;
- Tourism gameplay policies;
- a complete redesign of the existing Park or Industrial panels;
- a custom floating policy window;
- replacement of vanilla pathfinding, production, tourism, or service systems;
- double-charging vanilla building upkeep or double-counting vanilla service fees;
- fake UI-only finance entries that are not backed by real city accounting.

## 4. Architecture

The selected architecture is:

**shared common policy foundation + Area-specific policy definitions/effects.**

Dependency direction:

```text
Area Core
  Policy registry
  Per-Area policy state
  Unlock/conflict rules
  Persistence
  Policy UI view models/actions
  Shared Area Finance integration
       |
       +--> Common policy handlers
       |
       +--> Park policy adapter
       |
       +--> Industrial policy adapter
       |
       +--> future Area module adapters
```

Area Core owns generic policy mechanics. Modules own semantics.

Area Core must not contain switches such as “if Park then modify satisfaction” or “if Industrial then modify production”. A module registers policy definitions and effect handlers through explicit contracts.

## 5. Policy identity and registry

Each policy has a stable, non-reused integer `PolicyId`.

Policy ID allocation:

- 1000–1999: common policies
- 2000–2999: Park
- 3000–3999: Industrial
- later modules receive dedicated ranges

Initial reserved IDs:

| ID | Policy |
|---:|---|
| 1001 | Focused Management |
| 2001 | Enhanced Maintenance |
| 2002 | Night Events |
| 2003 | Quiet Operations |
| 2004 | Landscape Protection |
| 2005 | Premium Services |
| 2006 | Major Event Attraction |
| 3001 | Enhanced Safety Audits |
| 3002 | Stronger Environmental Regulation |
| 3003 | Production Priority |
| 3004 | Logistics Priority |
| 3005 | 24-Hour Operations |
| 3006 | Restricted Operating Hours |
| 3007 | Advanced Automation |

Once a PolicyId is used in a released save, it must never be reassigned to a different policy.

A policy definition contains at least:

- PolicyId;
- localization keys for name, description, lock reason and effect summary;
- icon URI;
- supported Area type(s);
- minimum Area Level;
- mandatory upkeep definition;
- optional conflict group;
- effect class;
- module/effect handler identity;
- UI metadata required by the vanilla policy presentation.

## 6. Per-Area state

Policies are configured per Area instance, not per Area type and not city-wide.

Example:

```text
Park Area A: Night Events ON
Park Area B: Night Events OFF
Industrial Area C: Production Priority ON
Industrial Area D: Environmental Regulation ON
```

Changing one Area never changes another Area's policy state.

Persisted policy state is conceptually:

```text
ManagedAreaPolicyState
  SchemaVersion

ManagedAreaPolicyEntry[]
  PolicyId
  DesiredEnabled
```

Only authoritative user choice is persisted. Derived state is recomputed after load:

- unlocked/locked;
- currently effective;
- conflict status;
- current upkeep;
- effect magnitude;
- display state.

Unknown PolicyIds found in a save are retained as disabled/inert records in the serialized policy buffer so that re-installing a missing module restores the user's prior selection. Unknown policies produce no effect and no upkeep.

## 7. Unlocking and availability

Policy unlocks use existing Area Levels 1–5.

- Level 1: basic policies may already be available.
- Levels 2–5: progressively stronger or more specialized policies unlock.
- Unlocking does not automatically enable a policy.
- All policies default OFF when first available.
- There is no artificial slot limit at any Level.

If a future module ever allows Area Level to decrease, policies whose minimum Level is no longer met retain `DesiredEnabled=true` but become inactive until the requirement is met again. Current Park and Industrial progression remains permanent-level progression.

## 8. Activation, conflicts, and suspension

Policy ON/OFF applies immediately.

A successful toggle changes authoritative state during the action and takes effect on the next relevant simulation update. Maintenance starts or stops from the same simulation-time boundary.

Conflicts are explicit, not inferred from labels or categories.

Examples:

- Park: Night Events vs Quiet Operations
- Industrial: Environmental Regulation vs Production Priority
- Industrial: 24-Hour Operations vs Restricted Operating Hours

Enabling a policy in a conflict group atomically disables the conflicting active policy or policies in that same Area before enabling the new policy. There must be no simulation frame with both conflicting effects or both upkeep charges active.

There is no global concurrent-policy cap.

When an Area is `Suspended`:

- `DesiredEnabled` is preserved;
- policy effects pause;
- policy upkeep pauses;
- UI shows that the policy remains configured but is currently suspended.

When the Area returns to `Active`, eligible policies resume automatically.

## 9. Effect model

Policies use two effect classes.

### 9.1 Area-internal effects

These affect values owned by the relevant Area module, for example:

- Area XP;
- Park maintenance effectiveness;
- Park value-for-money behavior;
- Park landscape/capacity trade-offs;
- other module-owned derived gameplay values.

They must modify the real model inputs/results defined by that module rather than adding unexplained hidden score bonuses.

### 9.2 Vanilla-simulation effects

These intentionally affect supported vanilla simulation values such as:

- production/efficiency;
- pollution/environmental burden;
- noise;
- operating activity;
- logistics behavior;
- other verified public ECS/data paths.

Every vanilla-affecting policy must use a dedicated Effect Adapter. The generic policy system must not directly know vanilla production, traffic, noise, or pollution internals.

A vanilla-effect policy may ship only when:

1. the exact 1.6.2f1 source/target and units are documented;
2. the write/update path is verified to be safe;
3. enabling the policy changes the real simulation value in-game;
4. the existing observation layer can observe the resulting change where relevant;
5. disabling the policy restores/removes the modifier without stale residue.

If a safe effect path cannot be verified, the policy is omitted/disabled for that release rather than replaced by a fake score modifier.

## 10. Initial common policy

### 10.1 Focused Management — Level 2

Direction:

- increases Area XP earned from existing qualifying XP awards by 10%;
- never bypasses existing earning requirements;
- never bypasses existing daily XP caps;
- does not create XP while the Area is otherwise ineligible to earn XP.

This is an Area-internal policy.

## 11. Initial Park policies

Park policy effects integrate with the existing Park quality, maintenance, visitor, satisfaction, admission, landscape, comfort/capacity and progression models.

| ID | Min Level | Policy | Direction | Conflict |
|---:|---:|---|---|---|
| 2001 | 1 | Enhanced Maintenance | Higher real Park management spend improves effective maintenance/management coverage | — |
| 2002 | 2 | Night Events | Raises visitor/attractiveness pressure with higher crowding/noise burden | Quiet Operations |
| 2003 | 2 | Quiet Operations | Reduces visitor pressure while favoring satisfaction/quiet operation | Night Events |
| 2004 | 3 | Landscape Protection | Improves landscape-oriented quality at the cost of some usable capacity | — |
| 2005 | 4 | Premium Services | Reduces the value-for-money penalty associated with higher admission fees | — |
| 2006 | 5 | Major Event Attraction | Strong visitor/attractiveness increase with strong crowding burden and higher upkeep | — |

Exact tuning magnitudes are implementation tuning constants, not hidden architectural behavior. They must be named constants, documented, testable, and visible through policy descriptions where player-relevant.

No policy may directly add arbitrary Park Quality points after the normal quality calculation merely to simulate an effect that should have changed an underlying input.

## 12. Initial Industrial policies

Industrial policies must respect the observation-first architecture. Where a policy changes vanilla production behavior, Industrial Performance changes because the existing observation layer sees changed real values—not because the policy adds points directly.

| ID | Min Level | Policy | Direction | Conflict |
|---:|---:|---|---|---|
| 3001 | 1 | Enhanced Safety Audits | Favors stable/safe operation over extreme utilization where a verified vanilla path exists | — |
| 3002 | 2 | Stronger Environmental Regulation | Reduces environmental burden with a modest production/efficiency trade-off | Production Priority |
| 3003 | 2 | Production Priority | Raises production/efficiency with increased environmental burden | Environmental Regulation |
| 3004 | 3 | Logistics Priority | Favors real input/output flow and reduces attributable logistics blockage where a verified path exists | — |
| 3005 | 4 | 24-Hour Operations | Raises operating activity with higher noise and operating cost | Restricted Operating Hours |
| 3006 | 4 | Restricted Operating Hours | Reduces night/noise burden with lower total productive activity | 24-Hour Operations |
| 3007 | 5 | Advanced Automation | Raises facility productivity/efficiency while reducing labor demand where a verified path exists | — |

Industrial Performance, Production, Logistics, Employment and Efficiency formulas remain authoritative. A policy does not directly add or subtract points from these scores.

Policies whose safe vanilla adapter cannot be verified in 1.6.2f1 remain unavailable rather than pretending to work.

## 13. Policy upkeep model

Every enabled and effective policy has maintenance cost. No active policy is free.

Each definition provides:

```text
BaseDailyUpkeep > 0
PerHectare >= 0
PerEligibleFacility >= 0
PerActivityUnit >= 0
ActivityBasis = explicit module-owned metric or None
```

Daily upkeep:

```text
AreaHectares = AreaSquareMeters / 10000

DailyUpkeep =
  BaseDailyUpkeep
  + PerHectare * AreaHectares
  + PerEligibleFacility * EligibleFacilityCount
  + PerActivityUnit * ActivityUnits
```

The result is clamped to a non-negative finite range and rounded using one documented finance rounding rule.

Only scaling inputs guaranteed by the owning module may have non-zero coefficients. A policy must not depend on an occasionally unavailable observation source for mandatory billing. If a source cannot be guaranteed, its coefficient is zero and the policy uses other stable bases.

Exact currency coefficients are balancing constants selected during implementation/runtime tuning. The architectural requirements are:

- base upkeep is always positive;
- formula inputs are explicit;
- there are no hidden Level multipliers;
- there are no nonlinear hidden multipliers;
- the UI shows the current estimated upkeep;
- policy effect strength is not reduced merely because all policies now have upkeep.

## 14. Time settlement

Policy upkeep is simulation-time proportional.

Turning a policy on midway through a game day does not charge a full day immediately. Turning it off stops future accumulation from that boundary.

Conceptually:

```text
settledCost = DailyUpkeep * activeSimulationFrames / FramesPerDay
```

Fractional carry may be retained internally to avoid systematic rounding loss, but it is derived finance settlement state rather than policy gameplay state.

Conflict replacement is one atomic state transition so old/new policies never overlap for billing.

No wall-clock timing is used.

## 15. Shared Area Finance layer

Area System must not have a separate wallet.

All Area-generated expenses and income are integrated into the city's real finance model.

### 15.1 Expenses

Target presentation:

```text
City Expenses
  -> Service Upkeep
      -> Area Management
```

Area Management includes:

- policy upkeep;
- Park Area management cost;
- future Area-module operating expenses.

Vanilla building upkeep already charged by the game must never be booked again by Area System.

Expenses continue to exist when city money is negative, matching vanilla city spending behavior. Area System does not maintain unpaid-debt records and does not disable policies merely because the city balance is negative.

### 15.2 Income

Target presentation:

```text
City Income
  -> Service Fees
      -> Area Service Fees
```

Area Service Fees includes:

- Park admission revenue actually collected;
- future Area-specific service charges;
- future policy-generated service income only when it represents a real supported service transaction.

For Park admissions, only the amount actually collected from households is city income.

### 15.3 1.6.2f1 integration constraint

`ExpenseSource` and `IncomeSource` are fixed enums. Area System therefore must not invent new top-level enum values.

The preferred integration is a dedicated Area System service-accounting prefab/data path using the vanilla service accounting structures, including where applicable:

- `ServicePrefab`;
- `CollectedCityServiceUpkeepData`;
- `CollectedCityServiceFeeData`;
- `CityServiceBudgetSystem`;
- vanilla service-fee/upkeep accounting.

The implementation must prove in-game that Area Management and Area Service Fees appear under the intended Service Upkeep / Service Fees presentation and affect real totals.

If the public 1.6.2f1 service-prefab path cannot create the required distinct detail line because of fixed `CityService` or UI assumptions, use a narrowly scoped, version-pinned finance presentation adapter at the budget-detail boundary while keeping the underlying monetary transaction in the real vanilla finance system. A UI-only fake amount is forbidden.

### 15.4 Park finance migration

The current Park finance implementation directly manipulates city `PlayerMoney` and caps management spending against the stored city-money lower bound.

The shared Area Finance work must migrate Park management expense and city-side admission revenue to the common vanilla-aligned finance path.

Household admission debit still uses real household money and must remain transactionally consistent with the city-side collected amount.

## 16. Policy UI

The policy UI must look and behave like vanilla District policies.

It appears inside the selected managed Area's normal selected-info panel. Do not create a separate floating policy window.

Preferred reusable 1.6.2f1 UI components include the vanilla policy section/components discovered in the game UI/runtime, such as:

- `PoliciesSection`;
- `Policy`;
- `CompactPolicy`;
- policy toggle/slider prefab structures where appropriate.

The existing `UI/src/vanilla.tsx` runtime-module lookup pattern is the preferred integration style: reuse real vanilla modules when available and retain a safe fallback if a game update changes module paths.

Each policy entry displays:

- icon;
- localized name;
- localized description/effect summary;
- current ON/OFF state;
- current estimated upkeep;
- lock requirement if unavailable;
- suspended/effect-unavailable state when applicable.

Locked policies remain visible but disabled.

A configured policy on a Suspended Area is shown as configured ON but temporarily suspended, not silently rewritten OFF.

Existing Park/Industrial UI cleanup is out of scope. Only the new policy surface must meet the vanilla District-policy standard in this project.

## 17. UI and command authority

React/UI is not authoritative.

C# builds the policy view model and validates every action.

A policy toggle action includes at least:

- Area stable ID;
- PolicyId;
- desired state.

C# re-validates:

- Area exists;
- Area stable ID matches;
- policy definition exists;
- policy supports that Area type;
- Level requirement;
- lifecycle rules;
- conflict transition;
- adapter availability.

UI never computes conflicts, unlocks, actual upkeep, or effect strength on its own.

## 18. Failure isolation

Failure behavior is fail-safe.

Examples:

- unknown PolicyId: retain inert record, no effect/upkeep;
- deleted Area: reject action without affecting other Areas;
- missing module: its policy effects remain inert;
- failed vanilla Effect Adapter: disable only that policy effect, log diagnostics, keep Area simulation alive;
- failed policy UI component: contain failure inside the policy/Area section using the existing boundary pattern;
- unavailable finance presentation hook: continue real finance accounting and report the display integration failure; never fabricate totals.

For the same Area stable ID + PolicyId + failure class, log the first occurrence per load generation and suppress repeats until the load generation changes or that failure clears and later recurs.

## 19. Data flow

Normal toggle:

```text
Player toggles vanilla-style policy control
  -> UI sends Area stable ID + PolicyId + desired state
  -> C# validates Area/policy/unlock
  -> C# applies atomic conflict transition
  -> persisted DesiredEnabled state changes
  -> effective policy set is recomputed
  -> module/internal or vanilla effect adapter observes new effective state
  -> Area Finance accrues upkeep from the same boundary
  -> UI view model refreshes
```

Load:

```text
Deserialize Area + policy IDs
  -> rebuild policy registry references
  -> retain unknown IDs inertly
  -> recompute unlock/effective/conflict state
  -> restore adapters only for Active eligible Areas
  -> resume finance settlement without back-charging unobserved time
```

## 20. Testing

### 20.1 Pure policy-model tests

Cover at least:

- Level 1–5 unlock rules;
- policies default OFF;
- no global simultaneous-policy cap;
- conflict replacement is atomic;
- different Areas are independent;
- Suspended Area preserves DesiredEnabled but has no effective policy/upkeep;
- reactivation restores eligible configured policies;
- unknown IDs remain inert;
- Focused Management respects existing XP eligibility and daily caps;
- upkeep formula and simulation-time proration;
- zero/NaN/infinite invalid inputs cannot produce invalid finance values.

### 20.2 Persistence tests

Cover:

- ON/OFF save/load;
- conflict state save/load;
- unknown PolicyId round-trip;
- old saves with no policy component initialize safely;
- deleted Areas leave no active policy runtime state;
- module removal/re-add preserves inert IDs and restores the configured state when the policy definition returns.

### 20.3 Finance tests

Cover:

- every effective policy has positive upkeep;
- negative city balance does not suppress Area expense;
- no Area debt ledger exists;
- Park building vanilla upkeep is not double-booked;
- Park admission city income equals the amount actually debited/collected;
- partial-day settlement is deterministic;
- conflict swaps do not double-charge;
- save/load does not back-charge elapsed wall-clock or unobserved simulation time.

### 20.4 UI tests

Cover:

- Park and Industrial policy lists;
- locked/unlocked/ON/OFF/suspended/effect-unavailable states;
- upkeep display;
- atomic conflict toggle behavior;
- localization;
- vanilla module lookup failure fallback;
- a policy section failure does not take down selected-info.

### 20.5 1.6.2f1 runtime verification

Runtime validation is required before claiming the feature complete.

Verify:

1. policy UI visually and behaviorally matches vanilla District policy interaction;
2. Area Management appears under Service Upkeep as intended;
3. Area Service Fees appears under Service Fees as intended;
4. totals are real city-finance totals, not UI-only numbers;
5. expenses continue while city balance is negative;
6. every enabled vanilla-effect policy changes the actual intended simulation path;
7. Industrial observation sees the resulting real value changes instead of receiving direct policy score bonuses;
8. Park effects modify their intended underlying module inputs;
9. conflict switching is immediate with no one-frame double effect/charge;
10. save/load restores configuration and effective behavior;
11. Suspended -> Active resumes configured policies correctly.

## 21. Compatibility and versioning

Target is Cities: Skylines II 1.6.2f1.

Vanilla-facing integrations are isolated behind adapters so future game updates do not force module-wide rewrites.

Version-sensitive adapters include:

- vanilla policy UI module resolution;
- vanilla simulation-effect targets;
- service-accounting/finance-detail integration.

If a future game update invalidates an adapter, disable that adapter/policy safely and preserve user policy configuration.

## 22. Implementation boundaries

Expected new/changed responsibility areas include:

```text
src/CS2AreaSystem/Core/Policies/
  policy IDs/definitions
  registry
  persisted per-Area policy state
  conflict/unlock/effective-state model
  UI facade/view models/actions

src/CS2AreaSystem/Core/Finance/
  Area finance aggregation
  vanilla service upkeep/fee adapter
  settlement model

src/CS2AreaSystem/Modules/Park/
  Park policy definitions/effect handler
  migration to shared finance

src/CS2AreaSystem/Modules/Industrial/
  Industrial policy definitions/effect adapters

UI/src/
  vanilla policy component lookup
  selected-info policy section
  bindings/types/tests
```

Exact filenames are an implementation-plan concern. Responsibilities and dependency direction in this specification are fixed.

## 23. Scope order

Recommended implementation order:

1. generic policy registry/state/conflict/unlock model;
2. per-Area persistence and UI bindings;
3. shared Area Finance and Park finance migration;
4. vanilla-style policy UI;
5. common + Area-internal Park policy effects;
6. individually verified Industrial/Park vanilla-effect adapters;
7. runtime finance/UI/effect verification;
8. Tourism/future Area policies only in later design cycles.

## 24. Final design decisions

The approved design fixes these rules:

- shared policy foundation + module-specific policies;
- policy effects may be Area-internal or verified vanilla-simulation effects;
- Level 1 basic policies and Level 2–5 stronger policies;
- no simultaneous-policy slot limit;
- explicit opposing policies are mutually exclusive;
- enabling a conflict atomically replaces the old policy;
- every active/effective policy has maintenance cost;
- policy effect strength is not reduced merely because upkeep exists;
- all Area finance uses vanilla city accounting principles;
- expenses target Service Upkeep -> Area Management;
- income targets Service Fees -> Area Service Fees;
- negative city balance does not stop Area spending;
- policies are configured independently per Area;
- toggles take effect immediately;
- policy configuration is preserved through Area suspension;
- policy UI follows the vanilla District policy interaction model;
- existing Park/Industrial panel cleanup remains out of scope;
- Tourism policies remain out of scope for the first implementation.
