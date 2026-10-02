# Park / Industrial deferred and research-required features

Date: 2026-10-02  
Target: Cities: Skylines II 1.6.2f1  
Repository: `pengin0503/CS2-Area-System`

## Purpose

This document records features that were reviewed before the Tourism Area work and are intentionally **not** part of the next implementation wave.

Two classes are tracked:

- **Implementable but not recommended**: technically achievable, but the maintenance cost, global side effects, accounting risk or scope mismatch is not justified for CS2-Area-System.
- **Unavailable or requires further research**: no sufficiently safe Area-scoped path has been verified yet, or runtime validation is required before the feature can leave prototype status.

These classifications are not permanent bans. They are guardrails for the current 1.6.2f1 implementation.

## Implementable but not recommended

### Park Area

#### City-wide happiness bonus

A city-wide modifier can be made to influence wellbeing/happiness, but it no longer represents an effect confined to a Park Area. It would change residents outside the managed area and makes overlap/ownership semantics ambiguous.

**Decision:** do not implement as a Park Area feature.

#### Move vanilla building upkeep into Park accounting

Vanilla service upkeep is already accounted through the game's own service/economy systems. Rebooking ordinary building upkeep into Area Finance would require intercepting or compensating the vanilla entry to avoid double charging.

**Decision:** keep vanilla upkeep separate. Park Area Finance owns only Area-originated costs/revenue.

#### Per-citizen long-term Park preference/history

A persistent favourite-Park or long-term citizen history system is possible with mod-owned state, but it creates potentially large per-citizen persistence, migration and cleanup cost.

**Decision:** prefer Area-level rolling history and aggregate resident/tourist metrics.

### Industrial Area

#### Global economy rebalance

Global economy/demand parameter singletons can be changed, but the result affects companies outside the Industrial Area and turns an Area module into an economy overhaul.

**Decision:** out of scope.

#### Area competition that mutates global prices/demand

Area-to-Area comparison is safe; using the comparison to rewrite global market prices, demand or other economy parameters is not Area-local.

**Decision:** comparison/ranking may be implemented, market mutation should not.

#### Pseudo Area tax implemented through native tax-rate setters

The native industrial/commercial/office tax setters are resource/city-wide, not company- or Area-scoped.

**Decision:** use explicit subsidy/rebate transactions if fiscal support is desired; do not alter native tax rates to simulate Area taxation.

## Unavailable or requires further research

### Park Area

#### Mandatory physical Entrance gate

The pedestrian graph can be inspected, but no safe Area-scoped hook has been established that requires every Park visitor to traverse a selected Entrance without invasive pathfinding changes.

**Status:** research required.

#### Exact visitor-to-commercial-spend attribution

The current Park visitor implementation intentionally observes vanilla leisure without consuming the private LeisureSystem event queue. Public data is sufficient for Area/store aggregates, but not yet for an exact "this visitor spent this amount at this Park shop" ledger.

**Status:** exact attribution unavailable; aggregate commercial observation remains allowed.

#### Direct land-value bonus

No Park-local `LocalModifierType.LandValue` equivalent has been verified.

**Status:** unavailable with the current safe public path.

#### Direct residential/commercial demand bonus

Demand systems are predominantly city/global. No verified Area-local demand mutation path is available.

**Status:** unavailable / further research required.

#### Local Wellbeing / Health provider

`LocalEffectSystem` and `LocalModifierType.Wellbeing/Health` provide a real simulation path, but a mod-owned provider requires a lifecycle-safe provider prefab/entity, bounded area, save/load behaviour and cleanup tests.

**Status:** prototype only until lifecycle and runtime effects are verified.

#### Vanilla attractiveness override

A post-`AttractionSystem` override of `AttractivenessProvider` appears technically plausible and would feed real attraction/tourism consumers. Runtime ordering, save residue, destination-choice effect and interaction with the future Tourism Area remain unresolved.

**Status:** prototype/runtime research required.

#### Night Events (2002)

Candidate channels: attractiveness pressure plus increased noise.

**Status:** keep `EffectUnavailable` until the attractiveness/noise composition and save/load restoration are proven in game.

#### Quiet Operations (2003)

Candidate channels: reduced noise plus reduced attraction pressure.

**Status:** keep `EffectUnavailable` until the same runtime contracts are proven.

#### Major Event Attraction (2006)

Candidate channel: temporary attractiveness increase, potentially combined with mod-owned event/crowding logic.

**Status:** keep `EffectUnavailable` until runtime verification and Tourism ownership are settled.

### Industrial Area

#### Enhanced Safety Audits (3001)

`DestructibleObjectData.m_FireHazard` is a real fire-hazard input, but it is prefab-side. A building-prefab clone / PrefabRef replacement would affect a very broad set of systems and has no proven safe precedent comparable to company-prefab scoping.

**Status:** keep `EffectUnavailable`; safer per-instance mechanism required.

#### Environmental Regulation (3002)

A per-building `PollutionEmitModifier` path exists. A production/efficiency trade-off is also plausible through scoped company-prefab data. Cross-policy composition, save boundary restoration and actual in-game balance are not yet proven.

**Status:** prototype/runtime verification required.

#### Production Priority (3003)

Scoped company-prefab data can affect real industrial behaviour, but output/input amounts, worker demand, profit and UI must remain internally consistent.

**Status:** prototype/runtime verification required.

#### Logistics Priority (3004)

`TransportCompanyData.m_MaxTransports` affects actual transport capacity. This is a strong logistics-support mechanism, but it is not literally an Area-aware pathfinding priority. Storage interval applicability is also not universal.

**Status:** implement only after validating that the policy description matches the measured effect.

#### 24-Hour Operations (3005)

`WorkplaceData` shift probabilities affect new workers' Day/Evening/Night assignment and `WorkerSystem` work timing. Existing workers retain their shift unless explicitly changed.

**Status:** runtime verification and reversible existing-worker strategy required.

#### Restricted Operating Hours (3006)

The shift path can bias work toward Day shifts, but immediate enforcement on existing workers requires touching `Worker.m_Shift`, with restore/worker-transfer edge cases.

**Status:** runtime verification required.

#### Advanced Automation (3007)

`IndustrialProcessData.m_MaxWorkersPerCell` can reduce vanilla-computed labour demand and `WorkplaceData.m_WorkConditions` affects employee-happiness efficiency.

**Status:** strong prototype candidate, but scoped company-prefab lifecycle/save boundary must be proven first.

#### Direct freight vehicle/path control

Vanilla resource pathfinding has no managed-Area contract. Making routes Area-aware requires pathfinding or transfer-request intervention.

**Status:** high-risk research project; not part of the current implementation wave.

#### Native Area-level tax rate

The game exposes city/resource tax setters, not company/Area-level tax rates.

**Status:** unavailable as a native Area tax. Use mod-owned subsidy/rebate transactions instead.

#### New Resource enum member / fully new economic resource

The economy uses a fixed `Resource : ulong` bit enum, dense resource indices, `Resource.Last`, fixed-size arrays and resource switches throughout the simulation.

**Status:** not supported as a normal CS2-Area-System extension. Treat as a separate economy-overhaul project.

## Rules for revisiting this document

A feature may move into the active roadmap only when the relevant requirements are met:

1. the effect is scoped to the intended Area or its explicitly owned entities;
2. enable/disable is reversible;
3. save/load does not persist a temporary override accidentally;
4. Area deletion, building/company deletion and renter replacement leave no orphan state;
5. overlapping same-type Areas follow deterministic primary-owner semantics;
6. other mods/vanilla writers are not silently overwritten;
7. the observed game behaviour matches the player-facing description;
8. runtime cost is bounded and measured.

Until then, existing unavailable policies remain visible but non-effective/non-billable.
