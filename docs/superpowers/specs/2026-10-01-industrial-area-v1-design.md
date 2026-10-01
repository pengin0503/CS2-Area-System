# CS2 Area System — Industrial Area v1 Gameplay Design

**Date:** 2026-10-01  
**Target game version:** Cities: Skylines II 1.6.2f1  
**Repository:** `pengin0503/CS2-Area-System`  
**Core baseline:** `main` at `4636dae74e62a078134aa0d74562e3d26da2d532`  
**Status:** User-approved gameplay design; implementation plan and code require the normal Superpowers planning gate

## 1. Purpose

Industrial Area v1 is the second full gameplay module built on Area Core.

The module must not create a parallel industrial economy. The player continues to zone, place specialized industry, storage, cargo infrastructure and supporting workplaces with normal Cities: Skylines II tools. Vanilla systems continue to own production, resources, companies, employment, transport and profitability. Industrial Area observes those real systems, aggregates them inside a management boundary, and adds understandable performance, bottleneck diagnosis and progression.

The intended loop is:

```text
Draw an Industrial Area
  -> establish a headquarters
  -> build or zone real extractors / processors / storage / cargo / workplaces
  -> vanilla companies actually produce, hire, store and transport resources
  -> Industrial Area observes production, logistics, employment and efficiency
  -> the player sees bottlenecks and improves the real district
  -> sustained real activity earns XP and unlocks higher Industrial levels
```

Progression is evidence of a functioning industrial district. Level must not secretly manufacture productivity.

## 2. Relationship to Area Core and Park v1

Area Core remains authoritative for:

- native editable area geometry;
- stable area identity;
- lifecycle;
- building membership;
- deterministic same-kind primary membership;
- persistence framework;
- anchor links;
- selected-info shell;
- localization and toolbar integration.

Industrial v1 remains module-local. It must not move industrial concepts into Core merely because Park and Industrial both need aggregation.

The current Core already supplies an `IndustrialAreaModule` stub whose activation role is `Headquarters`. Industrial v1 extends that module rather than creating a second industrial area type.

Park and Industrial may overlap. Each module keeps its own state and statistics. Same-kind Industrial overlaps use the existing deterministic primary-membership rule so a company, worker capacity or resource flow is not credited to two Industrial Areas.

## 3. Success criteria

Industrial Area v1 is successful when:

1. A completed Industrial Area is Suspended until it has one valid headquarters and becomes Active when that headquarters is valid.
2. Actual buildings and companies inside the Area are classified into extractor, processor/manufacturer, storage, cargo/logistics and supporting-workplace roles using public game state.
3. Unrelated residential, leisure, park and ordinary service buildings do not inflate Industrial performance merely because they are inside the polygon.
4. Production indicators derive from observed vanilla company/resource activity, never from building count alone.
5. Logistics reflects real storage and cargo conditions and identifies resource-flow bottlenecks without replacing transfer or vehicle AI.
6. Employment uses real workplace/employee state and exposes job fill rather than treating building capacity as employment.
7. Efficiency uses vanilla efficiency/profitability signals where available.
8. A 0–100 Industrial Performance score combines Production, Logistics, Employment and Efficiency in a transparent way.
9. Five permanent levels use cumulative XP plus current-performance and milestone gates. Level never demotes.
10. Same-kind overlapping Industrial Areas cannot double-count exclusive production, workers, logistics, XP or milestones.
11. No Industrial v1 mechanic directly modifies company production, resource prices, taxes, demand, pathfinding, vehicle routing, profitability or employment.
12. Save/load preserves only authoritative Industrial state and rebuilds derived facility and metric caches.
13. UI clearly distinguishes exact game values from derived or sampled indicators.
14. Heavy observation is low-frequency, incremental or round-robin and does not add a city-wide per-frame scan.
15. The module can fail independently: Industrial gameplay failure must not disable Area Core, Park, or Tourism stubs.

## 4. Chosen architecture

### 4.1 Alternatives

### A. Independent industrial economy

The Area would own production rates, inventory, workers and exports independently of vanilla companies.

Rejected because it duplicates the simulation, creates contradictory resource values, and makes vanilla industrial problems irrelevant.

### B. Observe vanilla industrial simulation and add management/progression — chosen

Industrial Area reads public ECS state and derives area-level metrics from the real companies and facilities that the base game is already simulating.

The 1.6.2f1 managed assemblies expose relevant public types including:

- `Game.Companies.IndustrialCompany`
- `Game.Companies.ProcessingCompany`
- `Game.Companies.StorageCompany`
- `Game.Companies.CompanyData`
- `Game.Companies.ResourceBuyer`
- `Game.Companies.ResourceSeller`
- `Game.Companies.WorkProvider`
- `Game.Companies.Employee`
- `Game.Companies.Profitability`
- `Game.Buildings.ExtractorFacility`
- `Game.Buildings.StorageProperty`
- `Game.Buildings.CargoTransportStation`
- `Game.Buildings.BuildingEfficiency`
- `Game.Citizens.Worker`
- `Game.Economy.Resources`
- relevant prefab data such as `IndustrialCompanyData`, `ProcessingCompanyData`, `ExtractorCompanyData`, `StorageCompanyData`, `CargoTransportStationData` and `WorkplaceData`.

Implementation must prefer these supported public contracts and verify exact field semantics against 1.6.2f1 before depending on a value.

### C. Patch production, transfer or logistics systems

Harmony-patching or replacing `ProcessingCompanySystem`, `StorageTransferSystem`, `ResourceBuyerSystem`, cargo AI or pathfinding could provide direct control.

Rejected for v1 because it expands compatibility risk and violates the observation-first architecture. Policies that intentionally influence vanilla production may be designed later as a separate feature.

## 5. Lifecycle and headquarters

Industrial uses the shared lifecycle:

```text
Created -> Suspended -> Active
                    \-> Suspended
Active/Suspended -> Deleted
```

A completed Industrial Area without a valid headquarters is Suspended.

### 5.1 Headquarters rules

- exactly one Headquarters role is authoritative for an Industrial Area;
- assigning a new headquarters replaces the previous link through the existing Core anchor flow;
- the headquarters must be a valid building inside the Industrial Area;
- it must be a plausible industrial-administration facility: an industrial/processing/storage/cargo workplace or an office/workplace that can reasonably serve the district;
- ordinary residential buildings, parks, attractions and unrelated civic buildings are rejected;
- exact component/prefab predicates are finalized against public 1.6.2f1 metadata during implementation;
- v1 does not require a custom 3D headquarters asset.

If the headquarters is deleted, leaves the Area, or ceases to satisfy eligibility, the Area becomes Suspended and retains all progression and lifetime state. Reassignment returns it to Active without resetting Level or XP.

Suspended Industrial Areas do not earn XP and do not publish fresh performance as if they were operating. Their last evaluated metrics may be shown as stale/paused until activation returns.

## 6. Industrial facility classification

Core continues to own building membership. Industrial classification is a module-local second stage.

A member entity may expose several relevant components. Industrial v1 records independent capabilities rather than forcing every building into one exclusive enum, while also assigning a primary display role for counts.

### 6.1 Extractors

Evidence includes public extractor building/company components and prefab metadata such as:

- `ExtractorFacility`;
- `ExtractorFacilityData`;
- `ExtractorCompanyData`.

Examples include resource extraction that vanilla already simulates.

### 6.2 Processors and manufacturers

Evidence includes:

- `IndustrialCompany`;
- `ProcessingCompany`;
- `IndustrialCompanyData`;
- `ProcessingCompanyData`.

A processor/manufacturer contributes only when it is a real company/facility, not because its building theme looks industrial.

### 6.3 Storage

Evidence includes:

- `StorageCompany`;
- `StorageProperty`;
- storage-area/prefab metadata;
- public resource inventory/capacity data.

Storage contributes to Logistics, not directly to Production volume.

### 6.4 Cargo and logistics facilities

Evidence includes:

- `CargoTransportStation`;
- `CargoTransportStationData`;
- transport-company or transfer state that is publicly attributable to the member facility.

Cargo facilities improve logistics access only when they are functional. Merely placing a non-operating cargo building must not grant full score.

### 6.5 Supporting workplaces

Industrial or office workplaces that support the district but do not qualify as extractor, processor, storage or cargo remain visible as supporting workplaces. They may contribute to Employment but do not fabricate production throughput.

### 6.6 Unrelated members

Residential, ordinary retail, parks, attractions and unrelated services remain Core members geometrically but contribute zero to Industrial metrics unless a supported industrial role is present.

Unknown modded assets are ignored for specialized scoring unless their public components make their role unambiguous. Name heuristics are not a primary classification mechanism.

## 7. Ownership and overlap

### 7.1 Same-kind overlap

For statistics that must be exclusive, a building/company is attributed only to Core's primary Industrial Area for that member.

Exclusive metrics include:

- production/throughput credit;
- active producer counts;
- storage capacity and pressure;
- cargo/logistics facility counts;
- jobs and employees;
- efficiency/profitability aggregates;
- XP and progression milestones.

This prevents one factory in overlapping Industrial Areas from creating duplicated output and XP.

Non-primary Areas may still show that an overlap exists, but they must not silently claim the same economic activity.

### 7.2 Cross-kind overlap

Park, Industrial and Tourism memberships remain independent by type. A cargo terminal inside an Industrial and Tourism Area may be relevant to both modules under their own rules without one module taking ownership away from the other.

## 8. Observation model and data fidelity

Industrial v1 must distinguish three classes of displayed values.

### 8.1 Exact current values

Use direct public game state when semantics are clear, for example:

- current employees/jobs;
- storage inventory/capacity where exposed;
- current `BuildingEfficiency`;
- current `Profitability`;
- recognized facility/company counts.

### 8.2 Exact accumulated values

Use only when the game exposes a reliable public cumulative or event-like source that can be attributed exactly to an Industrial Area.

### 8.3 Derived sampled values

If 1.6.2f1 exposes no reliable cumulative production counter for a resource, Industrial may derive **observed throughput** from stable public inventory/transfer/company signals sampled over time.

Derived throughput must:

- use bounded rolling windows;
- survive ordinary update-order noise;
- avoid counting a stock transfer as new production where the available signals can distinguish them;
- be labeled as throughput/activity, not falsely presented as an exact lifetime production counter;
- never be used to mutate vanilla inventory.

The implementation plan must pin the exact source and tests for every metric before coding it.

## 9. Production

Production represents real productive activity rather than district size.

### 9.1 Production inputs

The model considers:

- observed output throughput by resource;
- number/share of genuinely active extractors and processors;
- productive utilization where public data permits it;
- resource-chain diversity with diminishing returns.

A building that exists but is idle, abandoned, disabled or otherwise not producing should not earn the same production credit as an operating facility.

### 9.2 Production score

Production is a 0–100 metric. Initial weighting:

- 65% observed productive activity / throughput;
- 25% active-producer ratio and utilization;
- 10% meaningful resource/output diversity.

Absolute scale must be normalized enough that a compact but healthy district can score well. Very large Areas still gain progression faster through greater total activity, but Quality/Performance should not simply equal size.

Resource diversity has diminishing returns. Spamming low-value varieties must not outweigh a district with strong sustained production.

### 9.3 Production statistics

Where data is supportable, UI exposes:

- active extractors;
- active processors/manufacturers;
- top produced/handled resources;
- rolling observed throughput;
- utilization/activity ratio;
- derived production score.

If a resource-specific figure cannot be obtained reliably from public 1.6.2f1 state, it is omitted rather than fabricated.

## 10. Logistics

Logistics measures whether industrial resource flow has enough storage and transport support.

Initial Logistics score, 0–100:

- 35% storage health;
- 25% cargo access;
- 25% flow continuity;
- 15% logistics breadth/resilience.

### 10.1 Storage health

Storage is healthy when capacity exists without being chronically saturated.

Conceptual behavior:

- moderate utilization is healthy;
- persistent near-full output storage indicates a downstream bottleneck;
- persistent lack of needed input stock indicates an upstream bottleneck;
- completely empty unused storage does not earn a large bonus merely for existing.

### 10.2 Cargo access

Functional cargo infrastructure inside the Area is rewarded with diminishing returns.

One working cargo connection is meaningful; many identical stations do not linearly multiply the score.

Where reliable mode data is exposed, rail/ship/air/road breadth may contribute modestly. v1 does not require every mode for a high-performing district.

### 10.3 Flow continuity

Industrial observes symptoms visible in public state, such as:

- input starvation;
- output storage pressure;
- inactive producers despite otherwise valid facilities;
- repeated transfer/resource imbalance where a stable public signal exists.

The module diagnoses these conditions but does not reroute vehicles or inject transfers.

### 10.4 Logistics breadth

A district with extraction/processing, storage and cargo support receives a resilience benefit, but no single building type is mandatory for every industrial play style.

## 11. Employment

Employment uses real workers and workplace capacity.

Industrial tracks where public state permits:

- total jobs/workplaces;
- current employees;
- vacancy count;
- job fill percentage;
- employees by relevant education tier if stable public data is available;
- producer/support-workplace split.

Employment score is 0–100 and is primarily a fill/operational measure, not a raw headcount score.

Initial weighting:

- 75% filled-workplace ratio across relevant workplaces;
- 25% staffing balance, including severe under-staffing and education mismatch where reliably observable.

A district with 5,000 theoretical jobs and 1,000 workers should not score higher than a smaller district simply because its empty capacity is large.

Total employed workers remains an important progression milestone even though it is not the main score basis.

## 12. Efficiency and profitability

Efficiency represents how well the vanilla facilities are actually operating.

Primary signals:

- `Game.Buildings.BuildingEfficiency`;
- `Game.Companies.Profitability`;
- operating/disabled state needed to avoid counting dead facilities as healthy.

Initial Efficiency score:

- 70% weighted building efficiency;
- 30% profitability/viability.

Weight individual entities by a bounded proxy such as workplace or productive relevance so one tiny company does not dominate the Area.

If a metric is temporarily unavailable, the composite must degrade gracefully and mark that submetric unavailable rather than replacing missing data with a misleading hard zero.

Industrial v1 does not change these vanilla values.

## 13. Industrial Performance and bottlenecks

Industrial Performance is a transparent 0–100 composite:

- Production: 35%
- Logistics: 25%
- Employment: 20%
- Efficiency: 20%

The UI must expose all four components; the composite must never be a hidden opaque rating.

The module also derives a short ordered bottleneck list from current conditions. Candidate diagnostics include:

- workforce shortage;
- poor building efficiency;
- low profitability;
- input starvation;
- output/storage saturation;
- insufficient cargo/logistics support;
- low active-producer share.

A bottleneck is diagnostic text, not a new simulation modifier.

## 14. Progression

Industrial has five permanent levels:

1. **Local Industry**
2. **Industrial District**
3. **Production Hub**
4. **Regional Industrial Center**
5. **Industrial Powerhouse**

Level never decreases after promotion.

### 14.1 XP

XP is earned from sustained real operation:

- observed productive activity;
- staffed relevant workplaces;
- successful logistics/flow health;
- maintaining good Industrial Performance.

XP must be rate-limited so rapid sampling cannot multiply rewards.

No XP is earned while Suspended.

Building placement alone grants little or no XP; the placed facility must participate in the real simulation.

### 14.2 Initial promotion gates

These are implementation tuning constants and may be adjusted after runtime testing, but they define the intended scale.

**Level 1 -> 2: Industrial District**

- 1,000 cumulative XP;
- Industrial Performance >= 45;
- at least 3 active productive facilities;
- at least 50 employed workers.

**Level 2 -> 3: Production Hub**

- 5,000 cumulative XP;
- Industrial Performance >= 55;
- at least 8 active productive facilities;
- at least 200 employed workers;
- Logistics >= 45.

**Level 3 -> 4: Regional Industrial Center**

- 15,000 cumulative XP;
- Industrial Performance >= 65;
- at least 20 active productive facilities;
- at least 600 employed workers;
- Production >= 60;
- at least one developed logistics route: functional cargo infrastructure **or** Logistics >= 70 without dedicated cargo infrastructure.

**Level 4 -> 5: Industrial Powerhouse**

- 35,000 cumulative XP;
- Industrial Performance >= 75;
- at least 1,500 employed workers;
- Production >= 70;
- any two prestige objectives:
  - Logistics >= 80;
  - Employment >= 85;
  - Efficiency >= 80;
  - meaningful multi-resource production diversity;
  - a high sustained observed-throughput milestone defined by the implemented throughput source.

The alternative prestige objectives allow specialized, manufacturing-heavy, logistics-heavy and employment-heavy districts to remain viable instead of enforcing one layout.

### 14.3 Level effects

Level is informational/progression state in v1.

It may unlock richer statistics, titles or future policy slots, but it does **not** multiply:

- production;
- resource output;
- cargo capacity;
- employee efficiency;
- profitability;
- taxes;
- vanilla demand.

That separation is a hard v1 rule.

## 15. Finances, policies and direct buffs

Industrial v1 does not introduce:

- a separate Industrial treasury;
- an Industrial budget slider;
- admission/usage fees;
- tax overrides;
- subsidies;
- production bonuses;
- logistics speed bonuses;
- resource-price manipulation;
- policies/ordinances.

These may be designed later as explicit gameplay systems after the observation layer is runtime-validated. Any future policy that modifies vanilla simulation must identify the affected public system and compatibility cost rather than arriving as a hidden Level effect.

## 16. UI

Industrial uses the existing selected-info Area section and module tabs.

Tabs:

```text
Overview | Production | Logistics | Employment | Progress
```

### 16.1 Overview

Shows:

- lifecycle/status;
- headquarters;
- Level and XP;
- Industrial Performance;
- Production / Logistics / Employment / Efficiency scores;
- active productive facilities;
- employees / jobs;
- leading bottlenecks;
- last successful metric update.

### 16.2 Production

Shows:

- extractor count;
- processor/manufacturer count;
- active-producer ratio;
- rolling observed throughput;
- resource/output breakdown where reliable;
- production score;
- a clear label when a throughput figure is derived/sampled.

### 16.3 Logistics

Shows:

- storage facilities;
- current storage pressure/utilization where reliable;
- cargo facilities;
- cargo/logistics access;
- flow-continuity indicators;
- current logistics bottlenecks;
- Logistics score.

### 16.4 Employment

Shows:

- jobs;
- employees;
- vacancies;
- fill rate;
- education/staffing mismatch where reliable;
- Employment score.

### 16.5 Progress

Shows:

- current Level;
- XP and next threshold;
- every promotion gate with current/required value;
- prestige objectives at Level 4;
- a note that Level does not directly buff production.

Industrial v1 has no interactive module action beyond existing generic Area/HQ actions. This keeps the first release observational and avoids shipping unvalidated simulation modifiers.

## 17. Persistence

Persist authoritative Industrial state only.

Conceptually:

```text
IndustrialAreaState
  SchemaVersion
  Level
  Experience
  LifetimeProgressCounters that cannot be reconstructed
  PromotionMilestones
  LifetimePeakEmployees
```

If the chosen production observation source yields a reliable exact cumulative value, its lifetime counter may be persisted. If production is sampled/derived, persist only the minimum accumulator required to make the displayed lifetime/progression statistic semantically correct.

Do not serialize:

- facility classification caches;
- Area membership;
- current performance scores;
- current building efficiency/profitability;
- current jobs/employees;
- rolling bottleneck lists;
- storage snapshots;
- prefab metadata caches.

Those are rebuilt after load from Core and vanilla state.

Unsupported future Industrial component schema versions must be rejected using the existing versioned persistence safety model rather than interpreted optimistically.

## 18. Performance architecture

Industrial observation must not become a second city-wide simulation.

### 18.1 Structural changes

Facility classification responds to Core membership and relevant entity/prefab changes. Prefab classification is cached.

### 18.2 Economic sampling

Economic metrics run at a low cadence suitable for management UI, approximately hourly in simulation time as the starting target. Exact cadence may be tuned.

Large Areas are processed incrementally or round-robin across updates. Heavy work is split rather than forcing all member facilities through one frame.

### 18.3 Job/dependency rules

- use ECS jobs or bounded main-thread work where appropriate;
- avoid steady-state `Complete()` calls on vanilla producer/logistics jobs solely to refresh UI;
- do not enumerate every city company every frame;
- do not add per-worker persistent components merely for area accounting;
- cache prefab/static classification separately from dynamic company values;
- UI binding reads a prepared snapshot and does not trigger expensive scans.

Saving may synchronize when required for authoritative persistence, but normal gameplay sampling must remain asynchronous/low-overhead.

## 19. Failure behavior

Industrial must fail locally and visibly.

- Missing/invalid HQ -> Area Suspended, state retained.
- Unknown facility type -> ignored for specialized scoring, not fatal.
- Missing optional metric -> display unavailable and renormalize or use the documented neutral handling; do not invent a zero.
- Observation exception for one entity -> skip/log with bounded repetition; do not terminate Core.
- Industrial system registration failure -> log and leave Area Core/Park operational.
- Deleted Area -> stop observation and remove runtime caches.
- Load -> rebuild derived caches before publishing fresh scores.

## 20. Compatibility and API boundaries

Industrial v1 must not:

- Harmony-patch or replace vanilla production systems;
- access private queues or internal implementation fields through reflection as a normal data path;
- mutate `Resources` to simulate Area production;
- write `BuildingEfficiency` or `Profitability`;
- create transfer requests to artificially improve Logistics;
- reroute cargo vehicles;
- modify zone demand or taxes;
- require custom assets.

Public 1.6.2f1 ECS components and prefab data are the supported integration surface. Any candidate field whose semantics are uncertain must be proven against the supplied managed DLLs and pinned by a focused test before use.

## 21. Out of scope for Industrial v1

Explicitly deferred:

- industrial policies/ordinances;
- subsidies/tax controls;
- direct production or efficiency buffs;
- custom production chains/resources;
- custom factories;
- custom HQ building/model;
- cargo vehicle/pathfinding control;
- manual company assignment to Areas;
- cross-Area supply contracts;
- Industrial-vs-Industrial competition;
- pollution/noise mitigation mechanics;
- global economy rebalancing;
- mod-asset semantic registration API.

These are potential later modules or v1.x/v2 features, not prerequisites for a useful first Industrial release.

## 22. Verification strategy

Implementation must add automated verification before runtime testing.

### 22.1 Unit/model tests

Cover:

- facility classification;
- unrelated-building rejection;
- same-kind primary ownership;
- Production/Logistics/Employment/Efficiency score boundaries;
- missing optional metrics;
- bottleneck selection;
- progression gates and non-demotion;
- XP rate limiting;
- suspended behavior;
- serialization round trips and schema rejection.

### 22.2 Integration/offline contracts

Pin that Industrial source does not introduce forbidden mutation/system replacement for:

- production systems;
- resource transfers;
- taxes/demand;
- vehicle/pathfinding;
- direct efficiency/profitability writes.

Compile against the supplied 1.6.2f1 game references in the existing API-check profile.

### 22.3 UI fixture tests

Like Park, Industrial's C# panel payload should become the source of truth for a generated UI fixture so React tests do not silently drift from backend bindings.

### 22.4 Runtime smoke checks

A dedicated Industrial Area v1 section must be added to `scripts/runtime-smoke-checklist.md`.

At minimum verify in-game:

1. draw Industrial Area;
2. remains Suspended without HQ;
3. eligible HQ activates it;
4. ineligible HQ is rejected;
5. HQ deletion suspends and reassignment restores;
6. extractor classification;
7. processor/manufacturer classification;
8. storage classification;
9. cargo facility classification;
10. real jobs/employees match visible game behavior;
11. efficiency/profitability values move plausibly with vanilla problems;
12. production/throughput responds to actual operation rather than placement;
13. input shortage produces an appropriate diagnostic;
14. storage/output pressure produces an appropriate diagnostic;
15. same-kind overlap does not double-count;
16. save/load preserves Level/XP/milestones;
17. derived caches rebuild after load;
18. UI tabs update without stale entity references;
19. a large Industrial Area does not cause visible recurring stalls;
20. Park and Tourism/Core remain operational if Industrial observation is unavailable.

Runtime verification is intentionally separate from source/API verification because the current environment cannot prove actual Unity scheduling, live company behavior, cargo state or deployed UI behavior.

## 23. Implementation boundary

The implementation should be decomposed into focused Industrial-local units, conceptually:

```text
IndustrialAreaModule
IndustrialFacilityClassifier
IndustrialObservationSystem
IndustrialProductionModel
IndustrialLogisticsModel
IndustrialEmploymentModel
IndustrialPerformanceModel/System
IndustrialProgressionModel/System
IndustrialPanelBuilder
Industrial persistence/state integration
```

These names are architectural guidance, not permission to create one large all-purpose system. Static classification, dynamic observation, scoring, progression and UI snapshots must remain independently testable.

## 24. Final v1 contract

Industrial Area v1 is a management and diagnosis layer over the real Cities: Skylines II industrial simulation.

The central contract is:

```text
real vanilla companies and facilities
  -> public ECS observation
  -> Industrial-local aggregation
  -> transparent Production / Logistics / Employment / Efficiency
  -> Industrial Performance + bottlenecks
  -> permanent progression
```

No stage in that chain authorizes Industrial Area to replace the underlying economy.

The first implementation plan must preserve this boundary and treat any uncertain production-throughput field as an API-verification task before depending on it.
