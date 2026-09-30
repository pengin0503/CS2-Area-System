# CS2 Area System — Park Area v1 Gameplay Design

**Date:** 2026-09-30  
**Target game version:** Cities: Skylines II 1.6.2f1  
**Repository:** `pengin0503/CS2-Area-System`  
**Core baseline:** `main` at `9275438ddb50242f94026477e8305d13417ba423`  
**Status:** Draft for final review; design only, no Park gameplay implementation authorized by this document

## 1. Purpose

Park Area is the first full gameplay module built on the mature Area Core.

The goal is not to create a second, fake park simulation beside Cities: Skylines II. The player continues to build parks with normal roads, paths, vegetation, props, buildings, attractions, commercial/leisure facilities, and public transport. Park Area adds a management boundary that observes and aggregates the real city simulation, then adds Park-specific quality, progression, admission, operating-management, statistics, and future Area-to-Area integration.

The core gameplay loop is:

```text
Build and improve a real place
  -> real residents/tourists use leisure facilities inside it
  -> Park measures visitor experience and physical quality
  -> Quality / finances / progression react
  -> player fixes access, crowding, maintenance, variety, landscape or price
  -> Park grows in level and prestige
```

Visitor experience is the primary axis. Finances and progression are consequences and management tools, not the only objective.

## 2. Relationship to the Area Core specification

The original Area Core specification remains architectural background, but the current implementation is authoritative where it has improved beyond the document.

Important current facts:

- Park / Industrial / Tourism already share native Area geometry, lifecycle, stable identity, persistence, membership, anchors, toolbar integration and selected-info integration.
- The current Core uses the supported `SelectedInfoUISystem.AddMiddleSection` path instead of the older proposed floating management panel.
- Core membership intentionally indexes building-like entities only. Trees, props and other landscape objects are not global Area members.
- `AreaAnchorService` already permits multiple `Entrance` links while treating `Headquarters` as a single-role anchor.
- Park currently activates when at least one valid Entrance anchor exists.
- Current module UI contracts are sufficient for basic Summary / Tabs / Level / XP / Anchor display, but not yet for structured Park metrics, sliders and detailed tab content.

Park v1 should extend the Core only where a genuinely generic module capability is missing. It must not move Park-specific simulation into Core.

## 3. Success criteria

Park v1 is successful when all of the following are true:

1. The player can draw a Park Area using the existing native Area tool and activate it by assigning at least one eligible Entrance building.
2. Multiple entrances are supported and improve access without being required.
3. Park membership uses Core's existing building index; unrelated city-wide object types are not added to Core membership.
4. Landscape quality considers vegetation, amenities, decoration, lighting and paths using a Park-local low-frequency analysis.
5. Actual leisure visitors are observed from vanilla ECS state; Park does not replace or Harmony-patch `LeisureSystem`.
6. Resident and tourist visitors are separated using vanilla household state.
7. Same-kind overlapping Park Areas do not double-count visitors, fees or XP.
8. Park Quality is a 0–100 composite of six understandable metrics: Attractiveness, Visitor Satisfaction, Accessibility, Variety, Maintenance and Comfort/Crowding.
9. Park growth uses cumulative XP plus Quality and milestone requirements; Level does not fall after it is earned.
10. Budget changes affect Park management cost and Maintenance quality.
11. Admission pricing, once unlocked, transfers real household money into the city treasury rather than creating a separate Park currency.
12. The module does not directly buff citizen happiness, land value, residential demand or vanilla city-wide attractiveness in v1.
13. Park can expose a derived tourism contribution for a future Tourism Area module without directly modifying vanilla tourism in v1.
14. Save/load preserves authoritative Park state while rebuilding all derived caches.
15. Steady-state cost remains low enough that the mod is not a material simulation bottleneck in large cities.

## 4. Chosen design approach

### 4.1 Alternatives considered

Three broad designs were considered.

### A. Independent Park simulation

Park would maintain its own visitors, capacity, economy and attractiveness separate from vanilla systems.

Advantages:

- complete control;
- easy balancing;
- simple deterministic rules.

Rejected because it duplicates the city simulation, creates fake visitor counts and a parallel economy, and undermines the main reason Area System exists.

### B. Observe vanilla simulation and aggregate it into Park gameplay — chosen

Park reads public ECS state such as:

- `Game.Citizens.Leisure`
- `Game.Citizens.CurrentBuilding`
- `Game.Citizens.TravelPurpose`
- household membership / `TouristHousehold`
- `Game.Prefabs.LeisureProviderData`
- `Game.Buildings.AttractivenessProvider`
- `Game.Buildings.Park`
- Park-local spatial landscape data

Park then creates only the management layer around the observed behavior.

Advantages:

- visitor counts correspond to real simulated citizens;
- weather/time effects already influencing vanilla leisure naturally show up in attendance;
- no LeisureSystem replacement;
- lower compatibility risk;
- natural future integration with Tourism Area.

This is the baseline.

### C. Patch or replace vanilla leisure/pathfinding

This would provide direct control over destination choice, ticket-price elasticity and gate traversal.

Rejected for v1 because it creates the largest compatibility surface and conflicts with the Core's preference for observation and extension over global system replacement.

It remains a possible later research target only for features that cannot be achieved safely otherwise.

## 5. Park lifecycle and entrances

### 5.1 Lifecycle

Park uses the shared lifecycle:

```text
Created -> Suspended -> Active
                    \-> Suspended
Active/Suspended -> Deleted
```

A completed Park Area with no valid Entrance is `Suspended`.

A Park becomes `Active` when at least one valid Entrance exists.

If all entrances are later deleted or become invalid, the Park becomes `Suspended` but retains:

- Level;
- XP;
- admission settings;
- budget settings;
- lifetime statistics;
- progression milestones.

### 5.2 Multiple entrances

Park is not limited to one entrance.

Rules:

- one valid entrance is enough for activation;
- additional entrances improve Accessibility with diminishing returns;
- entrances may be added and removed independently;
- deleting one entrance does not suspend the Park while another valid entrance remains;
- no level requirement is imposed on adding entrances.

### 5.3 Entrance eligibility

The current Core only checks that an anchor is a Building inside the Area. Park v1 needs a stricter generic anchor-policy hook so modules can reject nonsensical anchors.

Park's baseline entrance eligibility should require:

1. the entity is a valid building member of the Park;
2. the entity is recognizable as a plausible Park/leisure/attraction access facility, preferably through typed components/prefab metadata such as `Park`, `LeisureProvider` or `AttractivenessProvider`;
3. when a stable supported access/connectivity check is available, the building has usable road or pedestrian access.

The exact component predicate is finalized during implementation against 1.6.2f1 APIs.

No Prop or arbitrary world point is an Entrance in Park v1.

### 5.4 Physical gate limitation

Entrance anchors are management/access anchors, not hard pathfinding gates.

Park v1 does not require citizens to physically pass through an Entrance entity before visiting another member facility. Enforcing that would require deeper pathfinding/gate changes and is deferred.

A future dedicated entrance asset may reuse vanilla visuals without requiring a new 3D model, but Park v1 does not depend on it.

## 6. Park facility model

Core continues to index all building-like members spatially. Park then filters those members into meaningful facility classes using actual game components/prefab data.

Primary useful sources include:

- `Game.Buildings.Park`
- `Game.Prefabs.ParkData`
- `Game.Buildings.LeisureProvider`
- `Game.Prefabs.LeisureProviderData`
- `Game.Buildings.AttractivenessProvider`

`LeisureProviderData` provides at least:

- leisure efficiency;
- leisure resource type;
- `LeisureType`.

Vanilla `LeisureType` categories include Meals, Entertainment, Commercial, CityIndoors, Travel, CityPark, CityBeach, Attractions, Relaxation and Sightseeing.

Park does not give arbitrary residential/industrial/office buildings a quality bonus merely because they are inside the boundary.

### 6.1 Facility relevance

Park v1 should distinguish conceptually between:

- core park facilities;
- leisure facilities;
- attractions;
- supporting leisure/commercial amenities;
- unrelated member buildings.

A building may satisfy multiple roles.

This classification remains Park-local. Core's `AreaMemberRole.Member` does not need to become a large cross-module enum.

## 7. Landscape analysis

### 7.1 Architecture

Landscape is deliberately separate from Core membership.

```text
Area Core membership
  -> building-like entities only
  -> incremental / city-wide infrastructure

Park Landscape Analyzer
  -> only active/relevant Park Areas
  -> vegetation / props / pedestrian paths
  -> spatial, low-frequency, derived cache
```

This preserves the performance decision already made in Core while allowing park-building gameplay to matter.

### 7.2 Landscape categories

Initial categories:

1. **Vegetation**
   - trees;
   - bushes/plants;
   - decorative vegetation where reliably classifiable.

2. **Rest & Amenities**
   - benches;
   - tables;
   - shelters;
   - other recognizable small amenities.

3. **Decoration**
   - statues;
   - fountains;
   - signs;
   - decorative props.

4. **Lighting / Utility**
   - lamps and recognizable small support objects.

5. **Paths**
   - pedestrian path/net presence inside the Park.

Category resolution should prefer typed prefab/component metadata. A curated vanilla mapping may be used where the game exposes no reliable semantic tag. Unknown props must not gain large diversity bonuses based on fragile name heuristics.

Future mod-asset registration is deferred; unknown assets may still contribute modestly to density where safe.

### 7.3 Density + diversity + diminishing returns

Landscape score must not be linear in object count.

The design uses four principles:

- useful density is rewarded up to a target range;
- category diversity is more valuable than repeating one category;
- prefab diversity inside one category receives only a smaller bonus;
- extreme object density receives a modest clutter penalty.

Conceptually:

```text
Landscape Score
  = Vegetation contribution
  + Amenities contribution
  + Decoration contribution
  + Lighting contribution
  + Path contribution
  + Diversity bonus
  - Excess/clutter penalty
```

The initial balancing target is approximately:

- Vegetation: 30%
- Rest & Amenities: 20%
- Decoration: 15%
- Lighting / Utility: 10%
- Paths: 15%
- Cross-category diversity: 10%

The exact values are tuning constants, not architectural contracts.

### 7.4 Anti-spam behavior

Repeated placement of the same prefab must have strongly diminishing value.

Examples:

- the first several trees materially improve vegetation quality;
- the 100th copy of the same tree contributes very little;
- adding a bench/path/decoration category is worth more than adding another tree variant when the Park is already vegetation-heavy.

Clutter penalties start only after a clearly excessive density so dense gardens and plazas are not punished for normal design choices.

### 7.5 Park size normalization

Absolute object count is never the scoring basis.

Targets scale sub-linearly with Park area so both compact neighborhood parks and large regional parks are viable. A conceptual scaling such as `area^0.75`, with small-park floors and large-park caps, is preferable to direct linear scaling.

Exact world-unit thresholds require runtime tuning.

### 7.6 Path score

Park v1 does not attempt a complete route-connectivity graph.

The first path score considers:

- pedestrian path length/density inside the Area;
- evidence that the Park is not pathless;
- path availability near Entrance areas where practical;
- over-dense path layouts with diminishing value.

Full loop/connectivity analysis is deferred.

## 8. Visitor tracking

### 8.1 Observation sources

Park v1 observes public ECS state rather than accessing `LeisureSystem`'s private `NativeQueue<LeisureEvent>`.

Useful vanilla components include:

- `Leisure.m_TargetAgent`
- `Leisure.m_LastPossibleFrame`
- `CurrentBuilding.m_CurrentBuilding`
- `TravelPurpose`
- household relationships
- `TouristHousehold`
- member-building prefab data

Vanilla `LeisureSystem` itself uses `CurrentBuilding`, `LeisureProviderData`, `TravelPurpose`, `TouristHousehold`, weather and time data. Park therefore should observe its outputs/state instead of duplicating its logic.

### 8.2 Park Visit Session

Only citizens currently participating in a relevant Park visit receive temporary tracking state.

Conceptual flow:

```text
Citizen has Leisure state
  -> CurrentBuilding is a relevant building
  -> Core AreaMembershipIndex returns one or more Areas
  -> choose primary Park membership for Park-exclusive statistics
  -> begin/continue ParkVisitSession

Citizen moves among relevant buildings in the same Park
  -> same session continues

Citizen leaves Park leisure activity
  -> short grace period
  -> session completes
  -> aggregate one completed visit
  -> discard per-citizen session state
```

Sessions are runtime-only and are not serialized.

### 8.3 Grace period

A short simulation-time grace period avoids splitting one park visit into many sessions when a citizen briefly transitions between buildings or path states.

If the citizen re-enters the same primary Park during the grace period, the same session continues.

### 8.4 What counts as a visit

A citizen is not counted simply for being inside the polygon.

The baseline requires evidence of a leisure visit associated with a relevant Park member building.

This avoids counting:

- employees at unrelated buildings;
- residents living inside an overlapping area;
- through traffic;
- citizens merely passing the boundary.

### 8.5 Resident vs tourist

Tourist classification uses the citizen's household and the vanilla `TouristHousehold` component.

Park tracks at least:

- current/rolling visitors;
- lifetime visitors;
- resident visitors;
- tourist visitors;
- resident satisfaction;
- tourist satisfaction.

### 8.6 Same-kind overlap

When a visitor's building belongs to multiple Park Areas, only Core's deterministic primary Park receives exclusive metrics:

- visit count;
- admission charge;
- satisfaction sample;
- XP;
- occupancy.

Non-primary overlapping Parks may still expose geometric/facility relationships, but do not double-count the person.

### 8.7 Cross-kind overlap

A Park inside a Tourism Area remains fully independent as a Park.

Future Tourism logic may consume Park's exported aggregate contribution rather than forcing Park to change how it counts its own visits.

## 9. Visitor Satisfaction

### 9.1 Principle

Visitor Satisfaction is an observed Park outcome, not a duplicate of Park Quality.

Each completed visit produces a satisfaction sample from the state experienced during that visit.

Initial conceptual weights:

- Facility experience: 30%
- Landscape experience: 20%
- Comfort/crowding: 20%
- Maintenance: 15%
- Value for money: 10%
- Accessibility context: 5%

These are initial tuning values.

### 9.2 Facility experience

Prefer actual visited provider data.

`LeisureProviderData.m_Efficiency` is a useful primary signal. An attraction or other recognized provider without that data may use an appropriate neutral/fallback contribution rather than being ignored entirely.

### 9.3 Landscape experience

Uses the current Park Landscape Score but cannot by itself produce perfect satisfaction.

### 9.4 Comfort/crowding

Uses occupancy relative to comfortable capacity, not raw visitor count.

### 9.5 Maintenance

Uses the current Maintenance score.

### 9.6 Value for money

A free Park receives no price penalty.

A paid Park is evaluated relative to its current quality/experience. High fees on mediocre parks reduce Value satisfaction. A high-quality Park tolerates a higher fee before the value score degrades.

Park v1 does **not** modify vanilla leisure target selection based on price. True price-elastic destination demand would require deeper intervention and is deferred.

### 9.7 Satisfaction smoothing and confidence

Recent satisfaction uses an exponentially weighted or equivalent bounded rolling aggregate.

A new Park with zero completed visits should not display a misleading hard zero. Use a neutral prior near 50 with low confidence, converging toward observed values as completed-visit samples accumulate.

Resident and tourist rolling satisfaction may be maintained separately in addition to the combined value.

## 10. Crowding and comfortable capacity

### 10.1 Capacity model

Comfortable capacity is an estimate, not a hard gate.

It derives from:

- Park area;
- internal pedestrian path provision;
- relevant leisure facility capacity proxies such as provider efficiency;
- amenity provision;
- limited level/management context where appropriate.

The model must use diminishing returns and remain cheap to update.

### 10.2 Occupancy curve

Conceptual behavior:

```text
< 60% of comfortable capacity
  -> essentially no crowding penalty

60–100%
  -> mild decline

100–150%
  -> meaningful crowding penalty

150%+
  -> severe but bounded penalty
```

Popularity is therefore rewarded until genuine over-capacity appears.

The UI may display both:

- `Comfort` (higher is better);
- occupancy/crowding details.

Park Quality uses Comfort as the positive metric.

## 11. Accessibility

Accessibility should not run expensive per-Park pathfinding jobs.

Initial components:

1. **Entrance validity / baseline access** — approximately 40%
2. **Multiple entrance distribution** — approximately 20%
3. **Nearby public transport access** — approximately 20%
4. **Internal pedestrian provision** — approximately 20%

### 11.1 Entrances

One valid entrance establishes baseline access.

Additional entrances improve the score with diminishing returns. Where inexpensive spatial information permits it, entrances spread around the Park are worth more than several anchors effectively stacked in one location.

### 11.2 Public transport

Transit contribution should use nearby stop/station proximity to entrances or the Park boundary. It must not issue repeated full pathfind queries.

### 11.3 Internal walking

Uses the Park-local path analysis from the Landscape Analyzer.

Per-citizen travel-time satisfaction is deferred from v1.

## 12. Attractiveness

Attractiveness combines real facility attractiveness with landscape quality.

Initial target:

```text
Attractiveness
  = ~60% facility attractiveness
  + ~40% landscape quality
```

Facility attractiveness comes primarily from actual `AttractivenessProvider.m_Attractiveness`, normalized with area-size scaling and diminishing returns.

This prevents a pile of identical attractions from increasing score indefinitely.

## 13. Variety

Variety rewards breadth rather than quantity.

Initial target:

```text
Variety
  = ~70% facility/leisure-category diversity
  + ~30% landscape-category diversity
```

Facility diversity uses `LeisureType` and other stable typed distinctions where available.

Cross-category diversity is more important than multiple prefabs of the same category.

`Travel`-type providers are primarily an access concept and should not receive full Park-facility diversity weight automatically.

## 14. Maintenance and Park budget

### 14.1 Use real vanilla Park condition where available

Vanilla Park buildings expose `Game.Buildings.Park.m_Maintenance`; their prefab data exposes `ParkData.m_MaintenancePool`.

Park v1 should use real member-building Park maintenance as one input rather than inventing all condition data.

### 14.2 Area management need

The Park management layer has additional management need derived from:

- effective Park size;
- visitor load;
- landscape/amenity burden;
- number of managed relevant facilities.

This is separate from the member buildings' normal vanilla upkeep, which the base game already charges.

### 14.3 Budget multiplier

Use the existing shared `ManagedAreaSettings.BudgetMultiplier`.

Initial user range should follow the familiar service-style pattern, approximately 50–150%, subject to implementation verification.

100% means the estimated management requirement is normally funded.

Below 100%:

- reduces management coverage;
- lowers Maintenance score when need exceeds funding;
- lowers visitor satisfaction indirectly.

Above 100%:

- improves resilience/coverage with diminishing returns;
- costs more;
- cannot push every other Quality dimension upward automatically.

### 14.4 Real city expense

Area management cost is paid from city money at a low-frequency simulation interval.

Do not create a separate Park wallet or currency.

Do not re-charge the vanilla upkeep of member facilities. Park's own area-management cost is additional administration/grounds-management overhead only.

## 15. Admission pricing and finances

### 15.1 Unlock

Admission pricing unlocks at Park Level 2.

Level 1 Parks are free.

The exact maximum fee and step size are balancing values to tune against the game's money scale.

### 15.2 Real transaction

Once enabled, admission is charged once per ParkVisitSession.

The preferred transaction path is:

```text
visitor household Resource.Money
  -> subtract admission actually payable
city PlayerMoney
  -> add the same amount
Park lifetime/recent admission statistics
  -> record the transaction
```

This avoids creating money from nothing and avoids a parallel Park currency.

Public CS2 mod code demonstrates `EconomyUtils.AddResources(Resource.Money, ...)` on household resources and `PlayerMoney.Add(...)` on the city entity without replacing the global economy system.

### 15.3 Insufficient funds

Park v1 must not force a household's resource buffer below zero merely to collect a Park ticket.

The safe baseline is to collect at most the available payable amount and record any uncollected portion as zero/partial revenue for that session. The visit is not forcibly cancelled in v1 because destination-selection intervention is deliberately out of scope.

### 15.4 Price and demand

One earlier concept was that a higher Park price would directly lower visitor demand.

After API review, this is intentionally **not** a Park v1 requirement. Altering vanilla leisure destination selection based on an Area-local fee would require invasive leisure/pathfinding changes.

Park v1 instead applies price to:

- real household payment;
- admission revenue;
- Value-for-money satisfaction;
- therefore Quality, XP efficiency and future tourism contribution.

Direct price elasticity in vanilla destination choice is deferred.

### 15.5 Finance reporting

Finance tab reports at least:

- current admission fee;
- recent admission income;
- lifetime admission income;
- recent Park area management expense;
- lifetime Park area management expense;
- recent net Area cash effect (`admission income - area management cost`).

Normal member-building revenues and vanilla building upkeep are **not** booked again as Park Area transactions. The UI should make this distinction clear to avoid double-counting.

## 16. Park Quality

Park Quality is a 0–100 composite.

Initial weights:

| Metric | Weight |
| --- | ---: |
| Visitor Satisfaction | 25% |
| Attractiveness | 20% |
| Accessibility | 15% |
| Variety | 15% |
| Maintenance | 15% |
| Comfort | 10% |

The exact weights are balance constants, but the six dimensions are the v1 conceptual contract.

### 16.1 Missing data

A temporary failure of one optional analyzer must not force Quality to zero.

Metrics have availability/confidence. When a genuinely optional source is unavailable, the remaining available metrics may be renormalized and the missing dimension shown as unavailable, while errors are logged/throttled.

Visitor Satisfaction is treated differently: before enough samples exist, a neutral low-confidence prior is used.

### 16.2 Suspended Park

A Suspended Park retains its most recent/derived information where safe but does not earn normal operational XP, charge new admissions or accrue normal active-visitor gameplay.

## 17. XP and progression

### 17.1 Principles

Progression rewards successful use, not object spam.

XP comes primarily from:

- completed real visits;
- satisfaction-adjusted visit quality;
- small capped rewards for sustaining strong Quality while actually receiving visitors;
- milestone bonuses.

No XP is awarded merely for placing trees, props or buildings.

### 17.2 Visit XP

Each completed visit awards a small base XP amount modified by satisfaction.

Low-satisfaction visits still provide some experience, but far less efficiently than successful visits.

### 17.3 Passive Quality XP

A small periodic Quality-maintenance reward may exist only when the Park is Active and has genuine recent visitor activity.

It is capped so waiting at high simulation speed cannot dominate progression.

### 17.4 Levels

Five levels:

1. Local Park
2. Neighborhood Park
3. City Park
4. Major Park
5. Landmark Park

Localized names may change without altering level semantics.

### 17.5 Initial promotion targets

These are **initial balancing targets**, not final values:

| Promotion | XP | Quality | Other requirement |
| --- | ---: | ---: | --- |
| Lv1 -> Lv2 | 1,000 | 40 | 250 lifetime visitors |
| Lv2 -> Lv3 | 4,000 | 55 | 1,500 lifetime visitors, Variety >= 45, Landscape >= 40 |
| Lv3 -> Lv4 | 12,000 | 70 | 7,500 visitors, recent Satisfaction >= 65, Maintenance >= 60 |
| Lv4 -> Lv5 | 30,000 | 82 | 25,000 visitors + any 2 prestige objectives |

Initial Lv5 prestige objectives:

- Landscape >= 80;
- Variety >= 75;
- tourist lifetime visitors >= 5,000.

Requiring any two preserves multiple viable Park styles: a strong local designed park can reach Landmark without being forced into a tourist-only build, while a tourism-heavy park also has a valid route.

### 17.6 Stability requirement

Promotion Quality thresholds should be maintained for a short rolling period rather than accepted from a one-tick spike. An initial target of roughly one in-game day is appropriate for tuning.

### 17.7 No demotion

Level never decreases after promotion.

If Quality later collapses:

- Level remains earned;
- operational condition/warnings worsen;
- XP gain falls;
- Satisfaction and finances may decline;
- future tourism contribution falls.

This preserves long-term progression while keeping neglect meaningful.

### 17.8 Unlock philosophy

Do not artificially lock essential diagnostics just to make every level grant a button.

- Lv1 exposes entrances, budget, Quality details and core visitor diagnostics.
- Lv2 unlocks admission pricing and the full finance controls.
- Lv3–Lv5 primarily represent prestige/progression, later analytics opportunities and future cross-module value rather than raw stat buffs.

Park Policies and Events are deliberately not required for v1 merely to fill level rewards.

## 18. Tourism contribution

Park computes a derived `TourismContribution`/prestige-style value from concepts such as:

- Level;
- Quality;
- tourist visit volume;
- tourist satisfaction;
- attractiveness.

Park v1 does not apply this directly to vanilla city tourism or city-wide attractiveness.

The value is an exported module metric intended for the future Tourism Area module. A Tourism Area overlapping a Park can consume it without rewriting Park's own simulation.

## 19. UI design

Park remains inside the vanilla Selected Info Panel through the current supported AreaInfoSection extension.

### 19.1 Required tabs

Initial tabs:

1. **Overview**
2. **Visitors**
3. **Finances**
4. **Landscape**
5. **Progress**

### 19.2 Overview

Shows:

- lifecycle state;
- Level / XP;
- Park Quality;
- six Quality metric bars;
- active/recent visitors;
- entrance count;
- primary warnings and next recommended action.

Common Edit/Delete actions remain Core-owned.

Entrance management should support add/remove/list rather than the current single-name presentation.

### 19.3 Visitors

Shows:

- active visitors;
- recent/period visitors;
- lifetime visitors;
- resident/tourist split;
- combined satisfaction;
- resident satisfaction;
- tourist satisfaction;
- estimated comfortable capacity;
- occupancy/crowding;
- peak occupancy where useful.

### 19.4 Finances

Shows:

- Park budget slider;
- admission fee slider when unlocked;
- recent/lifetime admission income;
- recent/lifetime Area management cost;
- recent net Area cash effect.

It must not imply that vanilla building upkeep is included when it is not.

### 19.5 Landscape

Shows:

- Landscape Score;
- vegetation contribution;
- amenities contribution;
- decoration contribution;
- lighting contribution;
- path contribution;
- diversity bonus;
- clutter/excess warning when relevant;
- facility/leisure diversity summary where useful.

### 19.6 Progress

Shows:

- current Level;
- XP progress;
- next Quality threshold;
- promotion checklist;
- Lv5 prestige objectives where applicable.

### 19.7 Generic Core UI extension

The current `IAreaPanelProvider` contract is too small for sliders and structured module content.

Park v1 should introduce a **module-neutral** structured panel contract rather than adding Park conditionals to `AreaManagementUISystem`.

The exact C# signatures are implementation-plan work, but the generic contract must support at least:

- labeled metrics/rows;
- progress/bars;
- warnings;
- numeric slider controls;
- buttons/actions;
- tab sections;
- localized labels/tooltips.

Module actions must retain the current stable-ID + live-selection validation model so stale UI cannot mutate a different Area.

Charts/sparklines are not required for Park v1.

## 20. Authoritative Park state and persistence

### 20.1 Existing shared persisted state

Reuse:

- `ManagedArea`
- `ManagedAreaStableId`
- `ManagedAreaProgress.Level`
- `ManagedAreaProgress.Experience`
- `ManagedAreaSettings.BudgetMultiplier`
- `AreaAnchorLink` Entrances

### 20.2 Park-specific persisted state

A schema-versioned Park component/state should persist only information that cannot be safely reconstructed, conceptually including:

```text
ParkAreaState
  AdmissionFee
  LifetimeVisitors
  LifetimeResidentVisitors
  LifetimeTouristVisitors
  LifetimeAdmissionRevenue
  LifetimeManagementExpense
  rolling Satisfaction aggregate state
  rolling ResidentSatisfaction aggregate state
  rolling TouristSatisfaction aggregate state
  promotion/milestone state if needed
```

Exact field layout is implementation-plan work.

### 20.3 Derived state — do not persist as authoritative

Do not serialize as authoritative:

- Core membership cache;
- Park facility snapshot;
- Landscape object cache;
- current Landscape Score;
- current Quality sub-scores;
- active visitor sessions;
- active visitor count;
- comfortable capacity;
- spatial/transit search results.

These rebuild after load.

### 20.4 Load behavior

```text
load Core authoritative state
  -> Core membership rebuild ready
  -> rebuild/validate Entrances
  -> seed Park facility snapshots
  -> queue Landscape scan
  -> seed current in-Park leisure visitors as pre-existing sessions
  -> rebuild Quality when dependencies are available
  -> resume normal progression/finance
```

Citizens already inside a Park immediately after load must not be charged a fresh admission simply because runtime sessions were not serialized. Seed them as pre-existing/fee-satisfied until they leave and start a genuinely new session.

## 21. Performance design

### 21.1 Forbidden patterns

Do not implement:

```text
every frame:
  every citizen x every Park polygon
```

or:

```text
every frame:
  every prop/tree x every Park
```

### 21.2 Visitor lookup path

Preferred hot path:

```text
Citizen with Leisure/CurrentBuilding
  -> current building entity
  -> AreaMembershipIndex.GetAreas(building)
  -> primary Park relation
```

This avoids repeated polygon containment tests for visitors.

### 21.3 Stagger visitor observation

Visitor tracking should follow the game's update-frame style where practical, processing staggered chunks rather than every eligible citizen every rendered frame.

The vanilla `LeisureSystem` itself updates on an interval of 16 simulation frames; Park should use a similarly bounded observation cadence rather than out-running the source simulation.

### 21.4 Landscape budget

Landscape scans are Park-local and scheduled with a strict work budget.

Triggers include:

- Park geometry changed;
- relevant spatial objects changed where detectable;
- load/rebuild;
- periodic fallback refresh.

Only a limited number of dirty Parks should perform an expensive spatial scan in one frame.

### 21.5 Metric cadence

Conceptual cadence tiers:

- visit/session observation: relatively frequent, staggered;
- occupancy aggregation: frequent but cheap;
- Quality recompute: lower frequency or dirty-driven;
- landscape analysis: low frequency / dirty-driven;
- finance settlement: in-game time interval;
- UI publication: selected Park only, publish-on-change.

Exact frame intervals are tuning values established with runtime profiling.

## 22. Error handling

Park should fail by feature, not take down Area Core.

Examples:

- landscape analyzer exception -> retain last good runtime snapshot or show that metric unavailable;
- missing optional prefab classification -> ignore or use conservative fallback;
- invalid entrance -> remove/ignore that entrance, suspend only when none remain;
- fee transaction failure -> collect nothing for that session, do not repeatedly double-attempt the same visitor;
- missing household resource buffer -> no charge, visitor tracking still completes;
- UI tab failure -> selected-info panel remains alive through the existing boundary;
- Park module failure -> geometry/editing/save should remain recoverable where Core can continue safely.

Warnings/logs that can repeat every update must be throttled.

## 23. Compatibility principles

- No Harmony baseline dependency.
- No replacement of `LeisureSystem`.
- No private `LeisureEvent` queue access.
- No direct modification of shared `LeisureProviderData` prefab values to implement Area-local pricing.
- No new global tree/prop membership index in Core.
- No direct v1 mutation of citizen happiness, land value, residential demand or vanilla city-wide tourism attractiveness.
- No copying of third-party implementation code without explicit compatible licensing and provenance tracking.

Public repositories and decompiled/reference material may be used to understand behavior, subject to the repository's existing third-party policy.

## 24. Testing strategy

### 24.1 Pure/offline tests

At minimum:

1. Quality weighted calculation.
2. neutral-prior Satisfaction behavior with low sample count.
3. Satisfaction convergence with completed visits.
4. Landscape density saturation.
5. same-prefab diminishing returns.
6. category diversity > repeated same-category diversity.
7. clutter penalty only beyond upper threshold.
8. small/large Park size normalization.
9. crowding curve across occupancy thresholds.
10. multiple entrances improve Accessibility with diminishing returns.
11. promotion thresholds and hold time.
12. earned Level never demotes.
13. visit XP depends on satisfaction but never becomes negative.
14. same-kind primary Park owns exclusive visit metrics.
15. admission charged at most once per visit session.
16. household payment cannot drive available money below zero.
17. resident/tourist aggregate separation.
18. Park state serialization round trip and schema validation.
19. module panel action validation rejects stale stable IDs/selections.
20. load-seeded pre-existing visits do not pay a duplicate ticket.

### 24.2 Runtime smoke tests

1. Draw Park with no Entrance -> Suspended.
2. Assign eligible Entrance -> Active.
3. Add second valid Entrance -> remains Active; Accessibility improves.
4. Delete first Entrance -> remains Active while second survives.
5. Delete all Entrances -> Suspended; Level/XP/stats preserved.
6. Add real Park/leisure/attraction buildings -> facility metrics change.
7. Add vegetation/props/paths -> Landscape changes without Core membership ballooning.
8. Spam one prefab -> score saturates rather than scaling linearly.
9. Real citizens perform leisure in member buildings -> visits count.
10. Workers/residents merely present for non-leisure reasons -> no Park visit.
11. Tourists classify separately from residents.
12. Same-kind overlapping Parks count one visit in the primary Park only.
13. Park inside future/placeholder Tourism relation can expose aggregate contribution without corrupting Park count.
14. Level 2 paid Park moves household money to city money once per visit.
15. Free Park charges nothing.
16. Higher fee lowers Value satisfaction without changing vanilla destination choice.
17. Budget changes affect management expense/Maintenance.
18. Save/load preserves authoritative Park state and rebuilds derived data.
19. Citizens already visiting at save/load are not immediately charged again.
20. Large-city profiling confirms no material per-frame all-citizen/all-object scan.

## 25. Explicitly deferred after Park v1

The following are intentionally outside Park v1 even if desirable later:

- direct price elasticity in vanilla leisure destination selection;
- hard gate traversal / citizens physically required to pass an Entrance;
- dedicated custom-model entrance assets;
- full internal path connectivity/loop analysis;
- per-citizen Park preferences or long-term visit history;
- direct city-wide happiness buffs;
- direct land-value or demand buffs;
- direct vanilla tourism-attractiveness modification;
- Park policies;
- scheduled Park events/festivals;
- deep commercial-spending attribution to Park Area;
- charging/reassigning vanilla member-building upkeep into Park accounting;
- arbitrary modded prop semantic classification without an explicit registration/config mechanism;
- persistent detailed time-series charts.

These may be separate follow-up designs after the first vertical slice is proven in game.

## 26. Required generic Core refinements for Park v1

Park exposes several places where Core needs a generic extension, but none require restructuring geometry or membership.

### 26.1 Module-specific anchor eligibility

Core should continue enforcing generic validity + membership, then let the module apply a stricter anchor predicate.

This is reusable by future Industrial HQ and Tourism administration roles.

### 26.2 Multiple-anchor presentation/actions

Core anchor storage already supports multiple Entrances. The UI/action contract needs to expose list/add/remove semantics rather than only one displayed anchor name.

### 26.3 Structured module panel data/actions

Replace/extend the minimal Summary/Tab-only provider with a generic module-panel contract capable of Park's metrics and controls without hard-coding Park into Core.

### 26.4 No Core landscape expansion

Do **not** expand Core membership to trees/props/paths merely for Park.

## 27. Final v1 gameplay loop

The complete intended player loop is:

```text
Draw Park Area
  -> assign one or more plausible Entrance buildings
  -> Park activates

Build using normal CS2 tools
  -> paths
  -> vegetation
  -> props/amenities
  -> leisure facilities
  -> attractions
  -> transit access

Vanilla citizens/tourists choose leisure normally
  -> Park observes real visits
  -> Resident/Tourist statistics update
  -> crowding and Satisfaction emerge

Park evaluates
  -> Attractiveness
  -> Satisfaction
  -> Accessibility
  -> Variety
  -> Maintenance
  -> Comfort
  -> Quality 0–100

Player reacts
  -> improve landscape
  -> diversify facilities
  -> add entrance/transit access
  -> solve crowding
  -> change Park budget
  -> from Lv2, set admission fee

Real visits + strong experience
  -> XP
  -> promotion requirements
  -> Lv1 ... Lv5
  -> Landmark prestige / future Tourism contribution
```

The important rule throughout is that the Park Area manages and interprets the city that the player actually built. It does not replace that city with a second hidden simulation.

## 28. Final design decisions

The Park v1 design is therefore:

- visitor-experience-first, with finance and progression supporting that loop;
- real vanilla leisure visitors, observed rather than simulated separately;
- six-dimensional Park Quality;
- Park-local landscape analysis with density, diversity, diminishing returns and clutter protection;
- real facility attractiveness/leisure/maintenance data where available;
- multiple building-based entrances;
- no physical gate enforcement in v1;
- resident/tourist separation;
- runtime-only short ParkVisitSessions;
- comfortable-capacity-based crowding;
- real city-funded management budget;
- real household-to-city admission transactions from Level 2;
- no v1 ticket-price modification of vanilla destination selection;
- five persistent Levels with no demotion;
- no direct happiness/land-value/demand/city-attractiveness buffs;
- future Tourism integration through exported aggregate contribution;
- authoritative state persisted, heavy caches rebuilt;
- no Core tree/prop membership expansion;
- no Harmony or global vanilla-system replacement as a baseline.
