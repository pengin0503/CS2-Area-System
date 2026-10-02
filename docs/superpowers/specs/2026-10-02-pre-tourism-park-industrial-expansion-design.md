# Pre-Tourism Park / Industrial expansion design

Date: 2026-10-02  
Target: Cities: Skylines II 1.6.2f1  
Source of truth at design start: `cacc8caf71abee3bf6088c616e5f2512f85655b2` (`main` after the deferred-feature documentation commit).

## Goal

Implement the previously classified **recommended** and **implementable** Park/Industrial features before beginning Tourism Area gameplay, without weakening existing Area Core isolation, save/load guarantees, primary-owner semantics or low-overhead observation design.

This is a multi-wave program, not one PR.

## Non-goals

The following are explicitly outside this design:

- features listed in `docs/roadmap/2026-10-02-deferred-and-research-features.md`;
- enabling any currently `EffectUnavailable` policy before its runtime proof;
- global economy rebalance;
- direct native Area tax rates;
- mandatory Park entrance pathfinding;
- new `Game.Economy.Resource` enum members;
- Tourism Area gameplay.

## Design principles

1. **Observation before mutation.** Read-only diagnostics and model previews ship before simulation-changing features.
2. **Module ownership.** Park and Industrial own their own derived data; Core receives only genuinely reusable primitives.
3. **No stale Entity actions.** UI focus/assignment commands revalidate the selected Area, stable ID, entity existence and current eligibility before acting.
4. **Primary-owner consistency.** Same-kind overlap continues to use deterministic primary ownership unless an explicit assignment contract says otherwise.
5. **Bounded work.** Spatial/graph analysis is dirty-driven, budgeted and has a low-frequency reconciliation path.
6. **Save data only when it creates player value.** Short trends start runtime-only. Persistence is a later bounded feature.
7. **No hidden double accounting.** Subsidies, fees and event expenses go through Area Finance exactly once.
8. **No policy status inflation.** Finding a plausible DLL field is not enough to mark a policy available.

# Program decomposition

## Wave A — Observability and diagnostics

This is the first implementation wave and should be completed before simulation-mutating expansion.

### A1. Runtime trend foundation

Add a small reusable rolling-series primitive for module-owned runtime metrics.

Park records selected hourly/daily samples such as:

- visitors;
- Quality;
- satisfaction;
- occupancy;
- admission income;
- management expense.

Industrial records selected hourly samples such as:

- Production;
- Logistics;
- Employment;
- Efficiency;
- Performance;
- productive-facility count.

Requirements:

- runtime-only in Wave A;
- fixed-capacity storage;
- no per-frame allocations after warm-up;
- reset at load generation;
- no change to authoritative Park/Industrial saved state;
- expose latest value, previous comparable value and bounded samples for UI.

UI initially shows compact trend/change rows. Full persistent charts are Wave C.

### A2. Safe diagnostic target actions

Introduce a reusable selected-Area diagnostic-target contract.

A diagnostic may carry a target building/entity for:

- Industrial bottlenecks;
- Park landscape hotspots;
- later connectivity problems.

The UI receives an opaque target key, never trusts a raw stale entity blindly, and calls a module action such as `focusDiagnosticTarget`.

The native handler must:

1. validate the currently selected managed Area and expected stable ID;
2. resolve the target key only against the current module snapshot;
3. verify the entity still exists;
4. verify it still belongs to/is relevant to the selected Area;
5. use the existing selection/focus path.

No simulation state is modified.

### A3. Industrial bottleneck focus

Extend Industrial diagnostics so bottlenecks can retain a bounded set of representative facilities.

Examples:

- workforce shortage -> most affected facility;
- low efficiency -> lowest qualifying facility;
- input starvation -> representative producer lacking its weighted input;
- storage saturation -> affected producer/storage facility;
- insufficient cargo -> affected transport-capable facility;
- low active-producer share -> representative inactive producer where meaningful.

Do not convert every bottleneck into a global entity scan. Targets are collected during the existing budgeted observation cycle or from already-read results.

### A4. Industrial resource process display

Expose the real observed process relation:

`input1 [+ input2] -> output`

and aggregate it by Area.

Requirements:

- only companies already eligible for Industrial observation;
- office/commercial exclusions remain unchanged;
- use current primary Industrial owner;
- display unavailable/partial coverage honestly;
- do not invent amounts when the source does not provide a reliable normalized quantity.

The Production/Logistics UI gains a resource/process summary and resource-level flow/status rows.

### A5. Park landscape diagnostics

Extend the existing Park landscape scan with bounded diagnostic output.

The first version should identify:

- underrepresented landscape categories;
- clutter/over-density;
- poorly served zones/cells where enough positional evidence exists;
- path-lighting/amenity shortages where the existing classifier provides that evidence.

Spatial detail must be coarse and bounded (for example a fixed grid/top-N findings), not a retained list of every prop/tree.

The feature is diagnostic only; it does not change Quality scoring beyond the existing model.

### A6. Park pedestrian-connectivity diagnostic

Add a separate budgeted read-only analysis.

Purpose:

- determine whether assigned Entrances and relevant Park facilities appear connected through the vanilla pedestrian network;
- identify disconnected/weakly connected facilities;
- surface warnings and optional focus targets.

Constraints:

- not an activation gate;
- never force visitor routing;
- no citizen × path graph work;
- dirty on Entrance changes, membership changes and relevant net changes where observable;
- bounded traversal with explicit work budget;
- if graph evidence is incomplete, report "unknown/partial" rather than disconnected.

This task should be a separate PR from A1–A5 if implementation complexity grows.

### A7. Park budget/fee what-if preview

Reuse existing pure Park finance/quality models.

The preview is non-authoritative and must never debit/credit money or persist state.

For a candidate budget/fee show the model-estimated effect on values such as:

- management cost;
- effective management coverage / Maintenance contribution;
- fee/value-for-money effect;
- estimated current-condition net using the existing recent visitor basis, clearly labelled as an estimate.

The UI should separate "preview" from the committed value. Only the existing action commits the setting.

## Wave B — Semantics and explicit Industrial ownership

### B1. Mod asset semantic registry

Create a versioned public registration surface so this MOD and compatible asset/mod authors can add classification semantics without hardcoding asset names.

Candidate semantics include:

- Park Entrance eligible;
- Park leisure/attraction relevance;
- Industrial HQ eligible;
- Industrial facility role hint (extractor/producer/storage/cargo);
- future Tourism semantics are reserved but not implemented here.

Design requirements:

- provider/owner ID;
- public registration + unregister/replacement lifecycle;
- stable lookup from runtime prefab/asset identity;
- exception isolation per provider;
- deterministic conflict resolution;
- cached results invalidated on provider change;
- vanilla typed data remains the default classifier; semantic registration supplements or explicitly overrides only documented fields;
- no dependency on ExtraLib/Harmony.

This registry should land before dedicated Entrance/HQ assets or custom factories.

### B2. Manual Industrial company assignment

Do **not** change Core's building-only geometric membership contract.

Implement a module-local explicit company assignment layer.

Recommended semantics:

- one live industrial company may have at most one explicit Industrial Area assignment;
- explicit assignment overrides the geometric primary Industrial owner for that company only;
- the rented building remains geometrically owned as before;
- unassigned companies continue using the current primary-owner rule;
- office/commercial/non-industrial companies cannot be assigned;
- deleting/moving a company clears or invalidates the assignment safely;
- assignment must be serialized through an entity-reference-safe component/buffer and rebuilt after load;
- UI must clearly distinguish "automatic (geometry)" from "manual".

Observation, diagnostics and future scoped company policy effects consume the resolved company owner.

This is intentionally Industrial-local and does not rewrite the shared AreaMembershipIndex.

## Wave C — Bounded feature expansion

### C1. Persistent history

After Wave A proves the runtime trend shape, add bounded persistence.

Recommended initial retention:

- hourly samples;
- a fixed number of in-game days;
- schema-versioned serialized buffers owned by each module;
- downsample or rotate rather than unbounded append.

Migration must tolerate older saves with no history.

### C2. Dedicated Park Entrance and Industrial HQ assets

The code-side requirement is classification/role integration through the semantic registry and existing Anchor system.

Asset/model work remains independent. If custom models are created, only building-fixed details should be authored when vanilla props can supply benches, plants, lamps or similar scenery.

No dedicated asset may become mandatory; existing eligible vanilla/custom buildings continue to work.

### C3. Industrial Area comparison

Add read-only comparison/ranking using existing normalized Area metrics.

Possible columns:

- Production;
- Logistics;
- Employment;
- Efficiency;
- Performance;
- trend.

This compares managed Industrial Areas only and does not change global economy behaviour.

### C4. Industrial subsidies / support payments

Implement fiscal support as explicit Area-originated transactions, not native tax mutation.

Requirements:

- configurable/defined eligibility;
- city treasury debited exactly once;
- receiver semantics explicitly chosen and represented;
- Area Finance records the same settled amount;
- insufficient city money follows existing saturation/failure policy;
- company deletion between scheduling and settlement is isolated;
- save/load cannot replay payment.

Initial implementation should prefer narrow, auditable support rules over a general subsidy scripting engine.

### C5. Park internal events/festivals

Implement as mod-owned Park operations affecting existing Park gameplay only.

A first version may change:

- Area management/event expense;
- comfortable capacity/crowding pressure;
- satisfaction/experience inputs;
- Park-internal XP conditions;
- event history/status.

It must **not** modify vanilla destination choice or attractiveness in this wave. Any vanilla-attraction integration remains in the research-required document.

### C6. Park coverage operating modifier

A bounded Park-only modifier using the verified `ModifiedServiceCoverage` magnitude/capacity path may be prototyped here.

It must:

- operate after/with the relevant vanilla Park calculation rather than accumulate;
- restore neutral behaviour immediately when disabled;
- have save/load tests;
- leave `m_Range` alone because the 1.6.2f1 coverage search does not use the runtime range override as expected.

Runtime game verification is mandatory before release.

## Wave D — Large standalone subsystems

These features are implementable but should each receive their own spec and implementation plan.

### D1. Area-to-Area supply contracts

A mod-owned contract system between Industrial Areas:

- source Area;
- destination Area;
- resource;
- target/limit;
- compliance/status.

Start with observation and advisory state. Do not intercept vanilla pathfinding in the first version.

### D2. Custom factory / existing-resource production content

Use the semantic registry and vanilla existing resources only.

Each custom company/factory must define valid vanilla process data and participate in existing observation without special-case scoring.

Do not introduce new `Resource` enum members.

# Proposed implementation order

1. **A1–A5** — trends, target actions, bottleneck focus, process display, landscape diagnostics.
2. **A6–A7** — pedestrian connectivity and Park what-if preview.
3. **B1** — semantic registry.
4. **B2** — manual Industrial company assignment.
5. **C1 + C3** — persistent history and Industrial comparison.
6. **C4** — subsidy transactions.
7. **C2** — dedicated Entrance/HQ assets once semantics are stable.
8. **C5** — Park internal events.
9. **C6** — Park coverage modifier only with runtime proof.
10. **D1 / D2** — separate specs after the above stabilizes.

# Testing strategy

Every implementation task follows RED -> GREEN tests and keeps existing source guards.

Minimum recurring coverage:

- same-kind overlap / primary owner;
- load generation reset;
- stale UI action rejection;
- deleted/moved target entities;
- Suspended -> Active transitions;
- empty/partial observation;
- bounded work and allocation checks;
- localization coverage;
- generated UI fixture updates;
- net8 API-check build;
- UI tests/build;
- offline contracts/source guards.

Features that alter real money, serialized assignment or vanilla simulation additionally require explicit save/load and idempotence tests.

## Runtime verification dependency

Issues #30 (Park), #43 (Industrial) and #44 (Core) remain important baselines. New features must add focused runtime cases rather than treating static/API tests as proof of in-game behaviour.

# First implementation slice

The first coding slice should be **Wave A1–A5 only**.

Reason:

- it gives immediate player-facing value;
- it is predominantly read-only;
- it creates the diagnostic-target and trend primitives reused by later work;
- it avoids committing to persistence/API compatibility before the UI/data shape is proven;
- it does not require changing currently unavailable policies.

After A1–A5 is implemented and reviewed, A6–A7 should be planned separately if pedestrian graph analysis proves materially more complex than the other diagnostics.
