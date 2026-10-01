# CS2 Area System — Industrial Area v1 Gameplay Design

**Date:** 2026-10-01  
**Target game version:** Cities: Skylines II 1.6.2f1  
**Repository:** `pengin0503/CS2-Area-System`  
**Core baseline:** `main` at `4636dae74e62a078134aa0d74562e3d26da2d532`  
**Status:** Revised from the user's eight approved decisions on 2026-10-01; implementation is delegated to Opus 5.5 Native. This document and the matching implementation plan are the source of truth.

**Revision scope:** daily XP cap 240; one-missing-input XP; flow-based logistics; explicit calculations and examples; observed daily-average streak; industrial-role employment only; per-Area restart; HQ presence independent of operating health.

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

- Exactly one Headquarters link is authoritative. Reassignment replaces the previous link through Core.
- Assignment requires an existing, valid building inside the Area with supported industrial, extractor, storage or cargo capability. Industrial building/prefab capability may establish eligibility even during temporary vacancy.
- Generic offices, residential buildings, parks, attractions and unrelated civic buildings cannot be assigned. Name heuristics are not eligibility evidence.
- Modded buildings exposing equivalent supported capabilities are eligible; no custom HQ asset is required.
- After assignment, an existing valid HQ building inside the boundary permits the Area to remain Active. Bankruptcy, tenant replacement, vacancy, disabled production, poor efficiency, low profitability or lack of employees must not invalidate an existing HQ link or suspend the Area.
- Actual HQ deletion/destruction, movement outside the boundary, removal of the link, or replacement by a non-building invalidates the link and suspends the Area. Reassignment preserves Level/XP/lifetime state.
- Assignment eligibility and continued-link validity are separate policies. Do not continuously reapply the assignment classifier to an already accepted HQ.
- HQ health may be observed independently, but v1 applies no HQ-health debuff. Future debuffs require a separate approved design.

Active means management is enabled, not that companies are healthy. Inactive production can yield poor metrics and zero XP while the Area remains Active. Suspended Areas retain progression, earn no XP and show any previous snapshot only as paused/stale.

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

Cargo capability classifies the facility even when disabled; operating status is observed separately. Cargo placement/function/mode is diagnostic context and never grants direct Logistics points.

### 6.5 Supporting workplaces

Employment counts workplaces directly attributable to eligible extractor, processor/manufacturer, storage or cargo facilities and their supported industrial companies. A SupportingWorkplace flag is descriptive within one of those eligible facilities; it cannot make an otherwise unrelated building eligible.

Ordinary office-only buildings are excluded, even when inside the boundary. There is no v1 heuristic for whether an office "supports" industry. Producers and industrial logistics/support jobs are displayed separately; a multi-role facility's jobs/employees are counted once.

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
- never treat raw inventory delta alone as production, because transfers and consumption can change inventory without production;
- combine only signals whose direction/meaning can be validated against 1.6.2f1 public state;
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
- 25% confirmed active-producer share;
- 10% meaningful resource/output diversity.

Absolute scale must be normalized enough that a compact but healthy district can score well. The same normalized operating quality earns the same hourly XP regardless of Area size in v1. Scale is represented by existing facility/employee promotion gates, not an additional XP multiplier. Quality/Performance must not simply equal size.

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

Logistics rates observed resource-flow health rather than the placement of dedicated infrastructure.

- Input continuity: 50%.
- Output/dispatch continuity: 50%.

A healthy district can score 100 with neither a dedicated warehouse nor a cargo terminal inside its boundary. Drawing a larger boundary to include an unused warehouse/terminal must not increase Logistics. Actual attributable flow problems may change the score.

### 10.1 Flow symptoms and window

Use a rolling six-game-hour window. At least four game-hours of valid observation are required per channel; earlier data is WarmingUp. Input continuity observes validated input-starvation symptoms at productive facilities. Output continuity observes validated dispatch/output blockage or sustained output saturation. Idle production alone does not prove either cause.

For each applicable producer, compute the fraction of valid observed time affected by the channel's confirmed symptom. Aggregate using the producer weights in §12.1, then:
`continuity = 100 * (1 - affectedWeightedTime / validWeightedTime)`.
A healthy, observable applicable channel yields 100; 50% affected time yields 50; continuously affected yields 0.

No observation is not evidence of health. Unsupported channels are Unavailable. A source that establishes a process needs no imported input makes its input channel NotApplicable, not Unavailable. Exclude NotApplicable channels and renormalize applicable weights. One unavailable channel can be excluded with a visible partial-data label if the other channel is available. Missing channel coverage caused by warm-up is excluded the same way, with its WarmingUp reason preserved; a channel still requires its own four valid observed hours before it is scored. With no available applicable channel, Logistics is unavailable.

### 10.2 Storage and cargo context

Show reliable storage amount/capacity/utilization, storage roles, cargo roles, known operating status and modes as diagnostic context. Counts or infrastructure breadth grant no direct points. Empty unused storage is not healthy-flow evidence; full stock alone is not dispatch failure without validated demand/process context and persistence in the observation window.

An external cargo terminal may serve the district. v1 need not discover all routes or prove terminal usage: evaluate attributable flow inside the Area and label external-route information unknown when unsupported. Never claim a connection based only on distance or terminal placement.

### 10.3 Bottleneck evidence

Bottlenecks report observed symptoms, their affected facility count, valid observation period, and a short relevant improvement suggestion. Do not assert an unproven root cause. "Insufficient cargo" is permitted only with attributable supporting evidence; absence of an in-boundary terminal alone is insufficient.

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
- 25% staffing balance: the arithmetic mean of per-facility job-fill percentages across eligible facilities with positive job capacity. Education mismatch is a separate diagnostic where reliably observable; it does not introduce an undefined additional score.

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

### 12.1 Units, normalization and availability

The adapter must document each metric's exact public source, units, entity attribution, denominator, conversion and availability reason in `docs/verification/industrial-observation-contracts.md`. Inspect the supplied DLL consumer/system and relevant vanilla UI calculation; public member existence or a compiling test alone does not prove meaning.

- Productive activity: 100 times validated actual productive activity divided by a validated attainable capacity/reference over the same period, clamped to 0–100. If the source or denominator is unproven, it is unavailable. Inventory delta is never a substitute.
- Active-producer share: 100 times confirmed operating producer facilities / known producer facilities; zero known producers makes Production NotApplicable. Building placement is not proof of operating state.
- Production retains 65% validated activity, 25% active share, 10% meaningful output diversity. Validated productive activity is required to publish a Production score; optional active-share/diversity weights are renormalized if unavailable. Diversity is 50/75/100 for 1/2/3+ meaningful outputs; zero verified activity has diversity 0, unknown outputs are unavailable. Meaningful output has observed activity at least 20% of the dominant output.
- Job fill: 100 times total actual employees / total valid jobs. Staffing balance is defined in §11. Zero valid jobs makes Employment NotApplicable. Negative capacities are invalid/unavailable; zero jobs are excluded, not silently credited as fully staffed.
- Building efficiency: normalize the proven vanilla efficiency multiplier with multiplier 1.0 = 100. Keep raw values (including values above 100%) visible; only scoring is clamped.
- Profitability: map the proven vanilla profitability percentage/index to its documented 0–100 viability scale. If its units or endpoints cannot be established, mark this subsource unavailable instead of guessing.
- Per-company efficiency/profitability weighting: `w = clamp(validJobs, 1, 100)`; missing/invalid jobs use weight 1 and a partial-coverage label. Per-facility flow weighting uses the same rule with facility jobs counted once.
- Scores are clamped to 0–100 after source normalization. NaN/infinity and invalid denominators become unavailable, never zero.

Every DTO/panel metric carries a reason: Available, Partial, Unavailable, NotApplicable, WarmingUp or Stale, plus observed/eligible coverage. Score computation accepts Available/Partial only. Exclude other reasons from weighted averages; all excluded inputs yields unavailable. Efficiency renormalizes either of its two available sources; Performance requires at least three of four components. XP has its own three-of-four rule (§14.1). Never fill an unsupported field with zero or 100.

### 12.2 Executable calculation examples

Create a small committed `docs/verification/industrial-score-examples.json` fixture and a readable table in `docs/verification/industrial-score-examples.md`. Tests execute the production models on the JSON inputs and assert the documented expected outputs; the implementation must not merely copy expected numbers into UI. These are synthetic semantic-input examples, not evidence that live ECS fields have been validated.

| Example | Normalized inputs | Expected |
|---|---|---|
| Healthy specialized small district | Activity 100; active share 100; one meaningful output (diversity 50); input/output continuity 100; fill/balance 100; efficiency/profitability 100 | Production 95; Logistics 100; Employment 100; Efficiency 100; Performance 98.25; hourly XP 10 |
| Equally healthy large district | Same proportions and one output, more real jobs/facilities | Same component scores, Performance 98.25 and hourly XP 10; scale gates differ |
| No dedicated warehouse/cargo terminal | Same validated flow inputs as healthy small district | Logistics 100; no infrastructure-count deduction |
| Partial flow problem | Input affected-time share 0.5; output affected-time share 0.25 | Logistics 62.5 |
| Stopped district with confirmed symptoms | Activity 0; active share 0; verified diversity 0; both channels affected 100%; fill/balance 0; efficiency/profitability 0; active producer count 0 | All scores 0; XP 0 |
| One unavailable component | Production unavailable; Logistics 80; Employment 60; Efficiency 100 | Performance 80; XP operating score 72.307692…; hourly XP 7 |
| Two unavailable components | Production and Logistics unavailable | Performance unavailable; fewer than three XP inputs, XP 0 with explanation |

Keep additional tests for no applicable input channel, one missing Efficiency source, zero jobs, partial observation coverage, invalid values and multi-tenant deduplication.

If one of Efficiency's two source signals is unavailable, reweight the available signal to 100% for that subscore and mark the missing signal unavailable in UI. If both are unavailable, Efficiency itself is unavailable rather than zero.

Industrial v1 does not change these vanilla values.

## 13. Industrial Performance and bottlenecks

Industrial Performance is a transparent 0–100 composite:

- Production: 35%
- Logistics: 25%
- Employment: 20%
- Efficiency: 20%

The UI must expose all four components; the composite must never be a hidden opaque rating.

If one component score is unavailable, Industrial Performance renormalizes the remaining three weights to 100% and explicitly marks the missing component. If two or more component scores are unavailable, Industrial Performance is shown as unavailable rather than inferring a rating from too little evidence.

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

XP is earned only while Active, from a fresh completed hourly observation with at least one confirmed operating productive facility. It uses Production 35%, Employment 25%, Logistics 20%, Performance 20%.

Require at least three of these four XP inputs. If exactly one is unavailable, exclude it and renormalize the other original weights. If two or more are unavailable, grant zero and show the reason. A missing Production score does not prohibit XP when operating producers are independently confirmed and the other three XP inputs exist; never fabricate throughput to achieve this.

`operatingScore = sum(available score * original weight) / sum(available original weights)`
`hourlyXp = clamp(roundAwayFromZero(operatingScore / 10), 0, 10)`
`award = min(hourlyXp, 240 - XpGrantedToday)`

The daily cap is **240 XP per game calendar day**, never a wall-clock day. There is no unconditional minimum XP and no Area-size XP multiplier. All zero scores yield zero XP.

A given simulation-hour bucket is settled at most once per Area. Persist the last settled hour and daily grant counter so pause, observation retries, HQ reassignment and save/reload in the same hour cannot duplicate awards. Missing hours are not backfilled with the latest score; no XP accrues while Suspended or while observations are stale. Persisted XP addition saturates at long.MaxValue.

At the maximum rate, cumulative thresholds 1,000 / 5,000 / 15,000 / 35,000 require approximately 5 / 21 / 63 / 146 full-day-equivalent earning periods, subject to other gates. The first partial calendar day may hit less than the cap.

### 14.1a Daily productive-activity streak

The prestige objective is **seven consecutive completed game days with time-weighted mean validated productive activity >=80 and at least 18 valid observed game-hours per day**.

- Accumulate intervals bounded by fresh, valid activity observations while Active, no more than one game-hour apart. Longer gaps, unavailable data, Suspended intervals and observations from an invalidated generation add no coverage.
- Integrate `activity * elapsedGameHours`; split intervals at calendar-day boundaries. Coverage is elapsed simulation time, not sample count. Never extrapolate a snapshot over an unobserved gap.
- For covered intervals use the earlier validated normalized sample; publish the average as sampled/derived. The first sample establishes an endpoint but adds no prior coverage.
- Complete each day once on transition to the next game day. A completed day qualifies only if observed hours >=18 and weightedSum/observedHours >=80.
- Insufficient coverage, a low average or a skipped day breaks the streak; UI explains which. A brief spike cannot qualify a day. Repeated evaluations of one day never increment twice.
- Persist the current-day weighted sum, valid observed hours, day identity and last credited interval/hour, plus the completed-day streak and last evaluated day. Runtime raw samples/rolling windows are not serialized.
- After load or a relevant Area change, discard the interpolation baseline. Resume with new endpoints without bridging the gap; previously accumulated covered time remains authoritative. Never reset an earned Level.

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
  - at least 3 distinct output resource classes, each with rolling activity at least 20% of the dominant output resource;
  - seven consecutive completed game days each have >=18 valid observed hours and time-weighted average productive activity >=80 (§14.1a).

The alternative prestige objectives allow specialized, manufacturing-heavy, logistics-heavy and employment-heavy districts to remain viable instead of enforcing one layout.

### 14.3 Level effects

Level is informational/progression state in v1.

v1 displays the level title and milestone checklist; all diagnostic statistics remain accessible at every Level. Future policy slots are deferred. Level does **not** multiply:

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
- a note that Level does not directly buff production;
- hourly XP, current-day XP / 240 and any blocked-XP reason;
- completed-day activity average, valid observed hours / required 18, and streak failure reason.

Industrial v1 has no interactive module action beyond existing generic Area/HQ actions. This keeps the first release observational and avoids shipping unvalidated simulation modifiers.

## 17. Persistence

Shared `ManagedAreaProgress` remains authoritative for Level and Experience; do not duplicate them in Industrial state.

Persist `IndustrialAreaState` schema v1 in this exact conceptual order:
SchemaVersion; LifetimePeakEmployees; XpDay; XpGrantedToday; LastSettledXpHour; ActivityDay; ActivityWeightedSum; ActivityObservedHours; LastCreditedActivityHour; HighThroughputStreakDays; HighThroughputLastDay.

Initial last-hour fields are -1; the last completed streak day is uint.MaxValue so game day zero is valid. Initialize current day identities from game simulation time when creating the component. Daily accumulators are the minimal authoritative history for §14.1a, not serialized current score snapshots. Unsupported future schemas are rejected safely.

Do not serialize facility/classification caches, membership, current scores/efficiency/profitability/jobs/employees, rolling flow windows, raw samples, stale flags or prefab metadata. Clear runtime snapshots and interpolation baselines after load; observe again before publishing current values. Saving partial observation work grants no XP or coverage.

## 18. Performance architecture

Industrial observation must not become a second city-wide simulation.

### 18.1 Structural changes

Facility classification responds to Core membership and relevant entity/prefab changes. Prefab classification is cached.

### 18.2 Economic sampling

Economic metrics run at a low cadence suitable for management UI, approximately hourly in simulation time as the starting target. Exact cadence may be tuned.

Large Areas are processed incrementally/round-robin. Initial total dynamic-read budget is 128 member buildings per simulation update across all Industrial Areas, with fair scheduling. Cache prefab traits, cap per-update tenant work, and measure wall time/allocations in runtime tests rather than claiming this count guarantees a frame-time budget. A cycle spanning more than one game-hour cannot supply a current score/XP/coverage; show delayed/stale state and record it for tuning.

Each Area cycle captures its own membership/ownership revision and module-local structure generation. Restart only that Area when its boundary, relevant membership, primary Industrial ownership, member destruction or relevant tenant/role structure changes. Ordinary economic values do not restart structural work; they are sampled. Park/Tourism/other-Area-only changes do not invalidate this Area. Primary ownership changes invalidate both old and new affected Industrial Areas.

Use a module-neutral per-Area Core membership token plus an Industrial-local structure token; no global membership revision as the restart predicate. Token comparisons use equality, not ordering. Load clears all cycles; deletion removes the token/cache. Publish only a completed coherent generation; never mix partial generations.

### 18.3 Job/dependency rules

- use ECS jobs or bounded main-thread work where appropriate;
- avoid steady-state `Complete()` calls on vanilla producer/logistics jobs solely to refresh UI;
- do not enumerate every city company every frame;
- do not add per-worker persistent components merely for area accounting;
- cache prefab/static classification separately from dynamic company values;
- UI binding reads a prepared snapshot and does not trigger expensive scans.

Saving may synchronize when required for authoritative persistence, but normal gameplay sampling must remain asynchronous/low-overhead. Game-day boundaries use the validated game calendar, not a hard-coded frames-per-day constant.

## 19. Failure behavior

Industrial must fail locally and visibly.

- HQ physically absent/invalid or outside the boundary -> Suspended, state retained. Poor HQ/company operating health alone does not suspend the Area.
- Unknown facility type -> ignored for specialized scoring, not fatal.
- Missing optional metric -> display its reason/coverage and apply the explicit renormalization rules; no invented neutral score.
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
- XP daily cap 240, one-input renormalization and same-hour save/load deduplication;
- executable score examples and healthy small/large/infrastructure-free districts;
- time-weighted daily streak coverage, missing days and brief spikes;
- per-Area restart isolation and primary ownership transfers;
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
4. ineligible HQ assignment is rejected; an accepted HQ remains valid through tenant vacancy/poor operating health;
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
20. Park and Tourism/Core remain operational if Industrial observation is unavailable;
21. healthy industry using external cargo/no dedicated warehouse receives no infrastructure-presence penalty;
22. ordinary office-only employment does not inflate Industrial totals;
23. one missing XP input permits reweighted XP, two missing inputs explain zero XP;
24. day cap 240 and same-hour save/reload do not duplicate XP;
25. <18 observed hours and a brief daily spike do not satisfy the seven-day streak;
26. other-Area/Park edits do not restart this Area, while ownership transfer invalidates both affected Areas;
27. synthetic calculation examples match models; actual company/flow field semantics and coverage are validated separately.

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
