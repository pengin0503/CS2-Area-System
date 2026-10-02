# Headquarters / Entrance redesign and post-Wave-A roadmap

Date: 2026-10-02  
Target: Cities: Skylines II 1.6.2f1  
Repository: `pengin0503/CS2-Area-System`  
Source of truth at design start: `bbc22d74cbc570e74e0d4741869832b87ad71d60` (Wave A1-A5 merged to `main`).

## Goal

Replace the current single-anchor activation model with a safer multi-requirement model that matches the intended gameplay:

- Industrial Area uses an ordinary office building as its Headquarters.
- Park Area requires both:
  - one Park Maintenance headquarters; and
  - at least one valid entrance.
- Park entrance prefers a verified gate / gate-like building when a safe typed vanilla candidate exists.
- If no safe vanilla park-gate candidate exists for 1.6.2f1, a dedicated pedestrian Path crossing the Park boundary becomes the entrance evidence.
- Ordinary road sidewalks do not count as automatic Park entrances.
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

Existing Industrial Headquarters links that point to the old extractor/factory/storage/cargo classes are retained as saved links for visibility but no longer satisfy the new activation rule.

The Area becomes Suspended until the player assigns a valid office Headquarters.

Level, XP, finance and historical state are preserved.

Do not silently convert an arbitrary old industrial facility into an office Headquarters.

## 3. Park Headquarters

### 3.1 Eligibility

Park Headquarters is a Park Maintenance facility.

For the vanilla 1.6.2f1 implementation, `Game.Buildings.ParkMaintenance` is the primary typed instance evidence. Equivalent modded assets may be supported later through the semantic registry in Phase 1.

Exactly one Park Headquarters is authoritative.

### 3.2 Lifecycle

Park is not Active unless its Park Maintenance Headquarters is valid.

Deleting or moving the Headquarters outside the Park suspends the Park even if entrance evidence still exists.

The Headquarters' temporary operating health does not need to invalidate the link unless runtime evidence proves the building itself is no longer a valid Park Maintenance facility.

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

If such a candidate exists, it may be assigned as an explicit Entrance and persisted through the existing `AreaAnchorLink` mechanism.

### 4.2 Dedicated pedestrian-path fallback

If no safe vanilla Park gate candidate is verified, Park entrance evidence is derived from a dedicated pedestrian Path crossing the Park boundary.

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

### 4.5 Future explicit gate assets

If the current 1.6.2f1 base game has no safe gate candidate, dedicated pedestrian-path crossing becomes the baseline compatible behavior.

A future custom gate asset added in Phase 3 is an additional explicit entrance option and must **not** retroactively invalidate old Parks that rely on path-crossing entrance evidence.

## 5. Park activation

Park activation requires both conditions:

```
Valid Park Maintenance Headquarters
AND
At least one valid Entrance evidence
```

Entrance evidence may be:

- an explicit supported gate building; or
- a detected dedicated pedestrian Path boundary crossing.

No visitor pathfinding is forced through the entrance.

This means the existing deferred item "mandatory physical Entrance gate traversal" remains deferred. The new rule validates an access point; it does not intercept or rewrite citizen routing.

## 6. UI behavior

Industrial Area:

- show one Headquarters row;
- Assign/Change Headquarters selects an eligible office;
- old invalid Headquarters remains visible/removable until replaced.

Park Area:

- show Headquarters separately from Entrance;
- Headquarters assignment selects Park Maintenance;
- if explicit gate assignment is supported, show gate Entrance links and add/remove controls;
- show detected dedicated-path entrances separately, e.g. "Detected path entrances: N";
- clearly explain which missing requirement causes Suspended state.

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

Path entrances are rebuildable and therefore remain runtime-only.

Old Park leisure/attraction Entrance links that no longer satisfy the explicit gate rule may remain visible as invalid links, but activation is determined by the new Headquarters + EntranceEvidence contract.

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
- explicit gate assignment, if enabled, uses typed evidence rather than display names.

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

- C2 optional dedicated Area asset/content integration;
- D1 Area-to-Area supply contracts;
- D2 custom factory / existing-resource production content.

Relative implementation size: approximately **125 points plus 3D/content work**.

C2 is revised by this design:

- a dedicated Industrial HQ asset is **not required** because ordinary office buildings are the intended Headquarters;
- Park Maintenance Depot is the baseline Park Headquarters;
- an optional dedicated Park gate/entrance asset may be added and registered through B1;
- any custom model follows the project rule that vanilla-prop-replaceable scenery is not authored; only building-fixed elements are modeled.

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
- make a dedicated custom HQ/gate asset mandatory.
