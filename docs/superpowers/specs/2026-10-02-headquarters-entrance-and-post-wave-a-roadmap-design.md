# Headquarters / Entrance redesign and post-Wave-A roadmap

Date: 2026-10-02  
Target: Cities: Skylines II 1.6.2f1  
Repository: `pengin0503/CS2-Area-System`  
Source of truth at design start: `bbc22d74cbc570e74e0d4741869832b87ad71d60` (Wave A1-A5 merged to `main`).

For Headquarters/Entrance semantics, C2 asset scope, and the implementation order after A1-A5, this document supersedes the corresponding parts of `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md`.

## Goal

Replace the current single-anchor activation model with a safer multi-requirement model that matches the intended gameplay:

- Foundation initially uses an ordinary office building as Industrial Headquarters, Park Maintenance as Park Headquarters, and typed path/gate evidence for Park Entrance so the core mechanics can be implemented without custom content.
- Phase 3 C2 intentionally replaces those temporary sources with three required Area System-owned assets:
  - `AS_IndustrialHeadquarters` for Industrial Headquarters;
  - `AS_ParkHeadquarters` for Park Headquarters;
  - `AS_ParkGate` for Park Entrance.
- The three prefabs come from two authoritative source archives: `CS2_AS_IndustrialHeadquarters.zip` supplies Industrial HQ, while `CS2_AS_ParkSet_HQ_Gate.zip` supplies both Park HQ and Park Gate. The standalone `CS2_AS_ParkHeadquarters.zip` is not used for C2.
- All Area System-owned model/prefab identifiers introduced by C2 start with `AS_` to avoid future base-game/DLC naming collisions.
- After C2, the Foundation-era office, Park Maintenance and path-derived Entrance sources do not remain activation fallbacks.
- A1-A5 are complete. The previously agreed Phase 1-3 program is now part of the formal implementation roadmap.

This design does not require visitors to physically traverse an entrance. Entrance evidence is an activation/access-management concept, not a pathfinding interception.

## Verified 1.6.2f1 facts

The supplied managed assemblies expose relevant typed data including:

- `Game.Buildings.OfficeProperty`
- `Game.Companies.OfficeCompany`
- `Game.Buildings.ParkMaintenance`
- `Game.Vehicles.ParkMaintenanceVehicle`
- `Game.Net.PedestrianLane`
- `Game.Prefabs.PathwayData`
- `Game.Prefabs.PathwayPrefab`
- `Game.Prefabs.PedestrianLaneData`
- `Game.Prefabs.GateData`

`Game.Prefabs.GateData` existing by itself is not proof that a safe, placeable Park gate building exists. The implementation must verify an actual eligible vanilla building/prefab and its stable typed semantics before enabling explicit gate assignment.

## 1. Core activation model

The current `IAreaAnchorPolicy.RequiredAnchorRole` model supports only one required role. That is insufficient for Park, which now needs Headquarters **and** Entrance evidence.

Introduce a generic activation contract that can express multiple requirements without hard-coding Park into Core.

Conceptually:

```
Industrial Area:
  Headquarters >= 1

Park Area:
  Headquarters >= 1
  AND
  EntranceEvidence >= 1
```

Core remains responsible for:

- stable Area identity;
- lifecycle;
- building membership;
- anchor persistence;
- stale-action rejection;
- generic building validity;
- UI composition.

Modules remain responsible for role-specific eligibility and activation rules.

The existing `AreaAnchorService` remains the source of persisted building anchors. It already supports one Headquarters link and multiple Entrance links. Runtime-derived path entrances are **not** serialized as `AreaAnchorLink`.

The management UI must no longer assume one displayed anchor role per Area. It must be able to present at least:

- Headquarters;
- explicit Entrance building links, when supported;
- runtime detected pedestrian-path entrance count/status.

## 2. Industrial Headquarters

### 2.1 Eligibility

Industrial Headquarters becomes an ordinary office building inside the Industrial Area.

Initial assignment should prefer stable building/prefab evidence such as `OfficeProperty`. A live `OfficeCompany` may be supplementary evidence, but tenant presence must not be the only persistent qualification because tenants can vacate.

The following no longer qualify merely because of their industrial role:

- extractor;
- factory/processor;
- warehouse/storage;
- cargo facility.

An office building that also exposes unrelated Industrial metrics must not gain production/logistics credit simply because it is Headquarters. Existing Industrial observation exclusions for office companies remain in force.

### 2.2 Lifecycle

Exactly one Industrial Headquarters is authoritative.

After a valid office is assigned:

- temporary vacancy;
- office tenant replacement;
- poor profitability;
- zero employees;
- disabled operation

do not suspend the Area while the valid building remains inside the Area.

Deletion/destruction, movement outside the Area, anchor removal, or replacement by a non-building invalidates the Headquarters.

### 2.3 Existing saves

Existing Industrial Headquarters links that point to the old extractor/factory/storage/cargo classes are retained as saved links for visibility but no longer satisfy the Foundation activation rule.

The Area becomes Suspended until the player assigns a valid Foundation office Headquarters.

Level, XP, finance and historical state are preserved.

Do not silently convert an arbitrary old industrial facility into an office Headquarters.

### 2.4 Phase 3 C2 dedicated Headquarters cutover

At C2, `AS_IndustrialHeadquarters` becomes the sole valid Industrial Headquarters prefab.

Ordinary office buildings are no longer Headquarters-eligible after this cutover. Existing office Headquarters links are not auto-converted; the Area becomes Suspended until the player assigns the dedicated AS-prefixed Headquarters. StableId, Level/XP, finance, history, policies and company-ownership state are preserved.

## 3. Park Headquarters

### 3.1 Eligibility

Park Headquarters is a Park Maintenance facility.

For the vanilla 1.6.2f1 implementation, `Game.Buildings.ParkMaintenance` is the primary typed instance evidence. Equivalent modded assets may be supported later through the semantic registry in Phase 1.

Exactly one Park Headquarters is authoritative.

### 3.2 Lifecycle

Before C2, Park is not Active unless its Park Maintenance Headquarters is valid.

Deleting or moving the Headquarters outside the Park suspends the Park even if entrance evidence still exists.

The Headquarters' temporary operating health does not need to invalidate the link unless runtime evidence proves the building itself is no longer a valid Park Maintenance facility.

### 3.3 Phase 3 C2 dedicated Headquarters cutover

At C2, `AS_ParkHeadquarters` becomes the sole valid Park Headquarters prefab.

Park Maintenance buildings no longer satisfy the Headquarters role after this cutover. Existing Park Maintenance Headquarters links are not auto-converted; the Park remains Suspended until the dedicated AS-prefixed Headquarters is assigned.

## 4. Park Entrance

Park requires at least one entrance in addition to Headquarters.

### 4.1 Explicit gate entrance

Before implementing fallback-only behavior, inspect the vanilla 1.6.2f1 prefab set for an actual placeable building that can safely represent a Park entrance.

A candidate is accepted only when:

- it is a valid building;
- it lies inside / on the Park membership boundary under the current Core anchor contract;
- stable typed prefab/component evidence distinguishes it as a suitable gate or equivalent entrance;
- eligibility does not depend on display-name matching.

The existence of generic `GateData` is insufficient.

If such a candidate exists, the initial 1.6.2f1 implementation uses explicit gate assignment as its primary Entrance mode and persists that Entrance through the existing `AreaAnchorLink` mechanism.

### 4.2 Dedicated pedestrian-path fallback

If no safe vanilla Park gate candidate is verified during implementation research, the 1.6.2f1 baseline switches to dedicated-pedestrian-Path fallback mode: Park entrance evidence is derived from a dedicated pedestrian Path crossing the Park boundary.

Only dedicated pedestrian Path networks count.

**Do not count:**

- ordinary road sidewalks;
- pedestrian lanes that exist only as part of a normal road;
- arbitrary road crossings;
- vehicle roads merely touching/crossing the polygon.

The implementation must distinguish a real Pathway using stable typed net/prefab evidence such as `PathwayData` / `PathwayPrefab`, with pedestrian-lane capability where required.

An automatic entrance exists when one eligible dedicated pedestrian Path continuously crosses from outside the Park polygon to inside it.

### 4.3 Runtime-only path evidence

Automatic path entrances are derived runtime state.

Do not serialize the net Entity as authoritative Area state because network edits can recreate entities.

Persist only the Park's normal authoritative state. Rebuild path entrance evidence after load.

The runtime snapshot should retain bounded, deterministic evidence sufficient for:

- entrance count;
- activation;
- Accessibility inputs;
- optional future diagnostic focus/location.

Nearby duplicate crossing points from the same physical connection must be collapsed.

### 4.4 Work budget

Do not scan the whole city network every frame.

Entrance detection must be:

- dirty-driven after Park geometry edits;
- dirty/reconciled after relevant network changes where observable;
- rebuilt after load;
- spatially bounded around the Park AABB plus a small margin;
- processed under an explicit work budget;
- periodically reconciled at low frequency to recover from missed dirty signals.

If a scan is incomplete, activation must not fabricate an entrance. Keep the previous known-good snapshot or an explicit pending/unknown state according to the implementation plan.

### 4.5 Phase 3 C2 dedicated Gate cutover

The Foundation dedicated pedestrian-Path crossing behavior is temporary and exists so Park activation can be implemented before custom content is available.

At C2, `AS_ParkGate` becomes the sole valid Park Entrance prefab. Path-derived Entrance evidence no longer satisfies activation and does not remain as a fallback.

The Gate must contain an editor-authored pedestrian passage/subnetwork through its opening with both ends configured to connect/snap to external pedestrian paths using the verified 1.6.2f1 asset workflow. This is asset-authored network connectivity, not a citizen/pathfinding interception.

Existing Parks that used path-derived Entrance evidence are not auto-converted. Their authoritative Area state is preserved, but they remain Suspended until the dedicated AS-prefixed Gate is assigned.

## 5. Park activation

Before C2, the Foundation activation model remains:

```
Valid Park Maintenance Headquarters
AND
At least one valid Foundation Entrance evidence
```

At Phase 3 C2 the authoritative activation model changes to:

```
Valid AS_ParkHeadquarters Headquarters
AND
Valid AS_ParkGate Entrance
```

Park Maintenance and path-derived Entrance evidence no longer satisfy activation after C2.

The dedicated Gate's authored pedestrian path may be used naturally by citizens once connected to the pedestrian network, but Area System does not force visitor routing through the gate and does not patch vanilla pathfinding.

## 6. UI behavior

Industrial Area:

- before C2, Assign/Change Headquarters follows the Foundation office-HQ behavior;
- after C2, assignment accepts only `AS_IndustrialHeadquarters`;
- an old office Headquarters may remain visible as invalid/removable state but does not activate the Area.

Park Area:

- show Headquarters separately from Entrance;
- before C2, Foundation Park Maintenance/path behavior remains available;
- after C2, Headquarters assignment accepts only `AS_ParkHeadquarters` and Entrance assignment accepts only `AS_ParkGate`;
- path-derived entrance status is retired from activation/UI once C2 removes the fallback;
- clearly explain which required dedicated asset is missing when the Area is Suspended.

The UI must preserve stable-ID/live-selection guards for all assignments.

## 7. Compatibility and persistence

This redesign must preserve:

- Area StableId;
- Level / XP;
- Park and Industrial authoritative module state;
- Area finance history;
- current A1-A5 runtime features;
- same-kind primary ownership;
- module failure isolation.

No save migration may fabricate a new Headquarters.

Before C2, path entrances are rebuildable and therefore runtime-only.

At C2, path-derived Entrance evidence is retired. Old office/Park Maintenance/legacy Entrance links may remain visible as invalid/removable links where the existing anchor lifecycle naturally exposes them, but they do not satisfy activation and are not auto-converted. The cutover preserves Area StableId, Level/XP, finance, history, policies and Industrial company ownership.

## 8. Testing and runtime proof

Minimum tests:

- Industrial office Headquarters accepted;
- old factory/extractor/storage/cargo HQ assignment rejected;
- existing accepted office HQ survives tenant vacancy;
- Park Maintenance accepted as Park Headquarters;
- unrelated service/park/leisure building rejected as Park Headquarters;
- Park remains Suspended with HQ only;
- Park remains Suspended with entrance only;
- Park activates only with both;
- dedicated pedestrian Path crossing creates entrance evidence;
- road-attached sidewalk does not;
- outside-only / inside-only Path does not count;
- duplicate crossings are deterministically collapsed;
- geometry edit invalidates/rebuilds path evidence;
- relevant network change invalidates/rebuilds path evidence;
- load rebuilds path evidence without saved Net Entity dependence;
- old invalid anchors do not erase XP/Level/state;
- no global per-frame net scan;
- explicit gate assignment, if enabled, uses typed evidence rather than display names;
- C2 exact `AS_` prefab identifiers are unique and collision-resistant against future DLC names;
- after C2, office Industrial HQ, Park Maintenance HQ and path-only Park Entrance are rejected;
- after C2, `AS_IndustrialHeadquarters`, `AS_ParkHeadquarters` and `AS_ParkGate` are accepted for their exact roles;
- old saves preserve Area state while becoming Suspended until dedicated anchors are reassigned;
- the dedicated Gate's authored pedestrian passage connects to external pedestrian paths without a runtime pathfinding hook.

In-game verification remains required for actual net classification and lifecycle behavior.

## 9. Formal implementation roadmap

Wave A1-A5 is complete and merged.

The implementation program from this point is:

### Foundation — Headquarters / Entrance redesign

Implement this specification first because later semantic registration, persistence and custom assets depend on the final role/activation model.

Scope:

- generic multi-requirement activation contract;
- multi-role management UI support;
- Industrial office Headquarters;
- Park Maintenance Headquarters;
- explicit gate feasibility check;
- dedicated-pedestrian-path entrance fallback;
- migration/runtime verification.

This Foundation is a formal implementation phase, not an optional side task.

### Phase 1 — diagnostics, semantics and history foundation

Previously agreed scope, now formally scheduled:

- A6 Park pedestrian-connectivity diagnostic;
- A7 Park budget/fee what-if preview;
- B1 mod asset semantic registry;
- C1 persistent bounded history;
- C3 Industrial Area comparison.

Relative implementation size: approximately **170 points** under the earlier sizing model.

The semantic registry must understand the revised roles from Foundation, including Park Headquarters and Park Entrance semantics.

### Phase 2 — explicit ownership and simulation-changing features

Previously agreed scope, now formally scheduled:

- B2 manual Industrial company assignment;
- C4 Industrial subsidies/support payments;
- C5 Park internal events/festivals;
- C6 Park coverage operating modifier.

Relative implementation size: approximately **160 points**.

Simulation-mutating work retains strict save/load, idempotence and runtime-proof requirements.

### Phase 3 — content and large standalone systems

Previously agreed scope, now formally scheduled:

- C2 dedicated Area System Headquarters/Gate asset/content integration;
- D1 Area-to-Area supply contracts;
- D2 custom factory / existing-resource production content.

Relative implementation size: approximately **125 points plus 3D/content work**.

C2 is revised by this design:

- use `CS2_AS_IndustrialHeadquarters.zip` as the authoritative source archive for `AS_IndustrialHeadquarters`;
- use `CS2_AS_ParkSet_HQ_Gate.zip` as the authoritative source archive for both `AS_ParkHeadquarters` and `AS_ParkGate`, preserving its shared texture/material relationship;
- do not use the standalone `CS2_AS_ParkHeadquarters.zip` as a C2 implementation source;
- import the completed Industrial HQ as `AS_IndustrialHeadquarters` and register it as the sole `IndustrialHeadquarters` source after C2;
- import the completed Park HQ as `AS_ParkHeadquarters` and register it as the sole `ParkHeadquarters` source after C2;
- import the completed Park Gate as `AS_ParkGate` and register it as the sole `ParkEntrance` source after C2;
- author a pedestrian passage/subnetwork through the Gate with endpoints that connect/snap to external pedestrian paths using the verified 1.6.2f1 asset workflow;
- retire the Foundation office/Park Maintenance/path-Entrance activation fallbacks at this cutover;
- preserve authoritative Area state when old anchors become invalid; do not auto-convert old anchors;
- package/load the three required assets through the verified CS2 content pipeline and rediscover runtime prefab entities each load;
- all Area System-owned model/prefab IDs begin with `AS_`;
- custom models follow the project rule that vanilla-prop-replaceable scenery is not authored; only building-fixed elements are modeled.

## 10. Order

Formal order:

1. Wave A1-A5 — **complete**
2. Foundation — Headquarters / Entrance redesign
3. Phase 1
4. Phase 2
5. Phase 3
6. Tourism Area gameplay

Foundation and each Phase should be implemented in reviewable PR slices rather than one monolithic PR.

## Non-goals

This redesign does not:

- force citizens to physically traverse a Park entrance;
- patch vanilla pathfinding;
- make normal road sidewalks valid entrances;
- enable currently `EffectUnavailable` policies;
- change Tourism gameplay;
- create new economy Resource enum members;
- globally rebalance the economy;
- keep Foundation office/Park Maintenance/path-Entrance fallbacks after the Phase 3 C2 dedicated-asset cutover.
