# CS2 Area System — Area Core Design

**Date:** 2026-09-30  
**Target game version:** Cities: Skylines II 1.6.2f1  
**Repository:** `pengin0503/CS2-Area-System`  
**Status:** Design specification for review; no production implementation yet

## 1. Purpose

CS2 Area System is a generic management-area framework for Cities: Skylines II. The first supported area kinds are:

- Park Area
- Industrial Area
- Tourism Area

The framework is intentionally not a park-only mod. The Area Core owns geometry, editing, identity, lifecycle, membership discovery, persistence, selection, and shared UI integration. Area-specific simulation lives in separate modules.

The player should continue to build the city using normal Cities: Skylines II tools. An area defines a management boundary; it does not procedurally generate the finished place.

Examples:

- A Park Area is drawn, then the player lays pedestrian paths, places props and facilities, and real citizens/tourists use the resulting park.
- An Industrial Area is drawn, then roads, extractors, processors, storage, cargo infrastructure, and a headquarters are placed. Production remains the real game simulation; the area aggregates and adds management/progression around it.
- A Tourism Area is drawn around hotels, attractions, parks, entertainment, commercial activity, and transport. It aggregates actual tourism activity rather than inventing a second parallel economy.

## 2. Success criteria

Area Core is successful when all of the following are true:

1. The player can create, edit, and delete mod-defined management areas using the base game's area interaction model rather than a custom freehand polygon tool.
2. Park, Industrial, and Tourism areas share one geometry/membership/persistence/UI foundation while keeping their simulation state isolated.
3. A placed building/object can be classified as a member of zero, one, or multiple compatible management areas without scanning every entity against every area every frame.
4. Save/load preserves authoritative area state and rebuilds derived membership caches safely.
5. The bottom construction UI looks and behaves like native CS2 UI and uses native toolbar/asset-menu data wherever practical.
6. No new 3D modelling is required for the first implementation.
7. Harmony is not a baseline dependency. Native prefab, ECS, tool, and UI-binding mechanisms are preferred.
8. Failure in one area module must not corrupt or disable unrelated areas.

## 3. Scope of this specification

This specification covers **Area Core and its UI shell**.

It defines the contracts that Park, Industrial, and Tourism modules will consume, but it does not fully specify their later simulation algorithms. Detailed park visitor simulation, industrial production/progression rules, tourism scoring, and specialized facilities will receive separate specifications/plans after Area Core is stable.

### Explicitly out of scope for Area Core v1

- New custom 3D building models
- Custom ride/zoo simulation
- Replacing vanilla pathfinding
- Replacing `LeisureSystem`, `TourismSystem`, or production systems
- Rewriting `AreaToolSystem`
- Patching the shipped `index.js` / `index.css`
- Deep monkey-patching of the native Selected Info Panel
- Persisting a complete area-to-member cache

## 4. Core architectural principle

The system is split into two layers:

```text
Vanilla spatial/tool layer
    Game.Tools.AreaToolSystem
    Game.Areas.Area
    Node / Triangle buffers
    Area geometry/search
            |
            v
CS2 Area System management layer
    ManagedArea
    Area type registry
    Lifecycle state
    Membership cache
    Module adapters
    Management UI
```

The native layer remains authoritative for area geometry and editing. The mod layer remains authoritative for management semantics.

This separation is required so that future game updates or future official area-management DLC can be adapted through integration code instead of forcing a rewrite of every simulation module.

## 5. Native area creation and editing

### 5.1 Use the base `AreaToolSystem`

The primary tool is `Game.Tools.AreaToolSystem`.

The 1.6.2f1 API exposes the normal area operations required by the design: creation, node insertion, node movement, merge/delete node actions, undo, completion, deletion, recreate/edit state, snapping, and prefab selection.

The mod must not reproduce ParkManager's custom polygon-input implementation for normal area creation.

Expected user flow:

```text
Open Areas construction menu
  -> choose Park / Industrial / Tourism Area
  -> native AreaToolSystem becomes active with the chosen AreaPrefab
  -> draw area using normal CS2 controls
  -> complete area
  -> configure/use the area through its management panel
```

### 5.2 Runtime area prefabs

Each management-area kind receives a mod-defined `AreaPrefab`/`LotPrefab`-based prefab registered early enough for the game's prefab initialization lifecycle.

Initial prefab set:

- `CS2AS.ParkArea`
- `CS2AS.IndustrialArea`
- `CS2AS.TourismArea`

The implementation must follow the prefab-initialization timing lessons demonstrated by GPL-3.0 `KruemelGames/ParkingLotTool`: register the prefab before the relevant initialization pass and do not instantiate from it until `AreaData.m_Archetype` is valid.

Area prefabs must remain editable and selectable. They must not use broad object-clearing/override geometry flags merely to create the management boundary.

### 5.3 Area overlap policy

Cross-kind overlap is a supported feature. Example:

```text
Tourism Area
  contains
    Park Area
```

This is needed so a park can contribute to tourism while retaining independent park management.

Because native area overlap validation is based on the game's area geometry types and cannot express every custom module rule cleanly, Area Core v1 treats overlap as a **management-layer rule** rather than rewriting the native area tool.

Rules:

- Cross-kind overlap is allowed by default.
- Module descriptors may reject specific cross-kind combinations later.
- Same-kind overlap is tolerated geometrically but must not double-count a member. For a member contained by multiple areas of the same kind, one deterministic primary area is selected for exclusive statistics; the others may still expose the geometric relation.
- The first implementation should prefer deterministic behavior and a warning over invasive native-tool patches.

## 6. Core data model

Authoritative area state is stored on the native area entity through mod components.

Conceptual components:

```csharp
ManagedArea
{
    ushort SchemaVersion;
    int TypeId;
    AreaLifecycleState State;
}

ManagedAreaProgress
{
    int Level;
    long Experience;
}

ManagedAreaSettings
{
    float BudgetMultiplier;
}
```

Not every module is required to use `ManagedAreaProgress` or `BudgetMultiplier`; these are optional shared concepts and should not become a dumping ground for module-specific data.

Module state stays separate:

```text
ManagedArea
  + ParkAreaState        (Park module only)
  + IndustrialAreaState  (Industrial module only)
  + TourismAreaState     (Tourism module only)
```

### 6.1 Stable area type registry

Area Core owns an `AreaTypeRegistry` keyed by stable integer IDs.

Initial IDs are reserved for:

- Park
- Industrial
- Tourism

A registered descriptor provides:

- stable type ID
- display/localization keys
- icon URI
- area prefab
- activation rule
- overlap policy
- membership classifiers
- management panel provider
- module lifecycle hooks

This avoids hard-coding Park/Industrial/Tourism conditionals throughout Area Core and leaves room for later Campus, Nature Reserve, Entertainment, Harbor, or other modules.

## 7. Area lifecycle

Shared lifecycle states:

```text
Created -> Inactive / Active / Suspended -> Deleted
```

The module decides the activation requirement.

Initial expectations:

- Park Area: requires at least one valid entrance before fully operating.
- Industrial Area: requires a valid headquarters.
- Tourism Area: can become active immediately after a valid area is completed.

Deleting or invalidating a required facility should suspend the module, not delete the area or erase progression.

For example:

```text
Industrial Area Active
  -> headquarters removed
  -> Industrial Area Suspended
  -> player assigns/rebuilds headquarters
  -> Active
```

## 8. Headquarters and no-model requirement

Industrial Areas use a headquarters because it provides a clear CS1 Industries-style activation and management anchor.

**New 3D modelling is not a prerequisite.**

Area Core v1 must support assigning an eligible existing vanilla building as the headquarters through the area's management UI. This is the guaranteed baseline because it requires no cloned visual asset and does not modify every instance of a vanilla prefab.

The data relationship is conceptually:

```csharp
AreaHeadquarters
{
    Entity Area;
}

AreaHeadquartersLink
{
    Entity Headquarters;
}
```

Entity references must use the game's serialization/remapping path rather than unmanaged external IDs.

A later module may add a dedicated placeable headquarters prefab that reuses a vanilla visual/prefab pattern, but Area Core correctness cannot depend on that enhancement.

The same role-assignment mechanism may later support Park Administration or Tourism Administration without forcing those buildings to be mandatory.

## 9. Membership system

### 9.1 Derived, not authoritative

The complete list of members inside an area is a runtime-derived cache and is **not persisted as authoritative save data**.

Area Core maintains indexes sufficient to answer:

- Which management areas contain this entity?
- Which entities currently belong to this area?
- Which area is the primary same-kind owner for exclusive statistics?

### 9.2 Spatial source

Use the game's existing area geometry/search infrastructure, including `Game.Areas.SearchSystem` and native area triangle data, instead of implementing a second polygon database.

### 9.3 Incremental invalidation

Forbidden design:

```text
every frame:
  every building x every area
```

Preferred invalidation inputs:

- area created/updated/deleted
- building/object created/updated/deleted
- building moved/replaced
- module classifier definition changes
- load/rebuild event

When an area changes, only potentially affected spatial entities are reconsidered. When a building changes, only nearby candidate areas are tested.

### 9.4 Membership point

For v1, building-like entities use their canonical world position/anchor as the membership point unless a module supplies a more appropriate rule.

Large-footprint and linear entities may receive specialized membership policies later. Area Core should expose the strategy boundary rather than hard-code footprint intersection into v1.

## 10. Module contracts

Area Core exposes services; modules provide policy.

Conceptual interfaces:

```text
IAreaModule
  DescribeType()
  ValidateActivation(area)
  OnAreaCreated(area)
  OnAreaActivated(area)
  OnAreaSuspended(area)
  OnAreaDeleted(area)

IAreaMemberClassifier
  Classify(entity, area) -> membership roles

IAreaPanelProvider
  BuildSummary(area)
  HandleAction(area, action)
```

The precise C# signatures are deferred to the implementation plan, but the dependency direction is fixed:

```text
Area Core does not depend on Park implementation details.
Park/Industrial/Tourism depend on Area Core contracts.
```

## 11. UI findings from the 1.6.2f1 bundle

The supplied `CS2-UI-reference-1.6.2f1.zip` contains the shipped `index.js`, `index.css`, `index.html`, `Media/Tools`, `Media/Glyphs`, and `Media/Game/Icons` needed for UI reference.

The bundle confirms that the construction UI is already data-driven around the `toolbar` binding group.

### 11.1 Native toolbar binding model

The shipped UI binds:

- `toolbarGroups`
- `assetCategories`
- `assets`
- `selectedAssetMenu`
- `selectedAssetCategory`
- `selectedAsset`
- theme/asset-pack filters

`ToolbarItemType` has two item classes:

```text
asset
menu
```

The toolbar button behavior is important:

- selecting a `menu` calls the asset-menu selection path;
- selecting an `asset` calls the asset selection path;
- selected asset/menu state drives the native visual selection state.

On the C# side, `Game.UI.InGame.ToolbarUISystem` owns corresponding bindings and activates the selected prefab through `ToolSystem.ActivatePrefabTool(prefab)`.

**Design decision:** Area System should enter the bottom construction UI through native UI prefabs and `ToolbarUISystem`, not by inserting custom DOM into the shipped toolbar.

### 11.2 Native prefab hierarchy for construction UI

Relevant game-side prefab types confirmed in 1.6.2f1:

- `UIToolbarGroupPrefab`
- `UIAssetMenuPrefab`
- `UIGroupPrefab`
- `UIGroupElement`
- `UIObject`

`UIGroupPrefab.AddElement()` adds a child entity to the UI group. `UIObject` carries the target group, priority, and icon and adds itself to that group during prefab initialization.

This provides the intended insertion point for an Area System construction hierarchy without patching the base React bundle.

### 11.3 Area System construction hierarchy

Initial native construction hierarchy:

```text
Areas
  -> Area Types
       -> Park Area
       -> Industrial Area
       -> Tourism Area
```

The top-level `Areas` entry is a native toolbar/menu entry. Its asset menu contains the management-area prefabs.

Selecting one of the three area prefabs should flow through `ToolbarUISystem -> ToolSystem.ActivatePrefabTool(prefab)` and land on the native area tool because the selected prefab is an area prefab.

Do **not** duplicate normal roads, pedestrian paths, vegetation, benches, props, or vanilla buildings in this menu. The player uses the game's existing construction categories for those.

Module-specific placeable assets may be added later only when they are real distinct assets.

### 11.4 Icon strategy

Do not copy the game's media files into the repository merely to imitate them. Prefer runtime references to existing game media URIs where appropriate.

Useful shipped icon candidates visible in the supplied reference include:

- `Media/Game/Icons/SurfaceAreaTool.svg`
- `Media/Game/Icons/LargeParks.svg`
- `Media/Game/Icons/PropsPark.svg`
- `Media/Game/Icons/ZoneIndustrial.svg`
- `Media/Game/Icons/ZoneExtractors.svg`
- `Media/Game/Icons/Attraction.svg`
- `Media/Game/Icons/Administration.svg`
- `Media/Tools/Area Tool/Edit.svg`
- `Media/Tools/Area Tool/Generate.svg`

A custom icon should only be bundled when no suitable runtime icon exists.

## 12. Asset Menu behavior

The shipped Asset Menu reads the selected asset menu, gets categories through `assetCategories`, then gets assets for the selected category. The category tab bar handles icon, selected, locked, highlight, tooltip, and close behavior using native bindings.

Area System should therefore model its construction UI as native menu/category/asset data, preserving:

- keyboard/gamepad navigation
- lock/highlight behavior
- tooltips/localization
- normal asset selection state
- native close/back behavior
- future compatibility with theme/asset-pack filters where relevant

No custom recreation of the asset grid is required for Area Core v1.

## 13. Selected area management UI

The shipped `selectedInfo` binding is section-driven. It provides title, top, middle, and bottom sections and contains native section identities for concepts including:

- Districts
- Ticket Price
- Attractiveness
- Efficiency
- Employees
- Upkeep
- Level
- Park
- Storage
- Cargo

The shipped Selected Info Panel is also aware of the native `AREA_TOOL` state.

These are valuable visual and interaction references, but Area Core v1 must **not patch the minified shipped bundle or depend on private React symbols**.

### 13.1 v1 management-panel decision

Area Core uses a mod-owned management panel styled to match the vanilla Selected Info Panel and opened when a managed area is selected.

The panel must reuse vanilla design language observed in the supplied CSS:

- panel gradient/background variables
- native spacing scale
- native typography variables
- native section/divider treatment
- native progress-bar geometry
- native button/tab states
- native percentage slider behavior where applicable

It should sit in the same general selected-entity workflow, but remain technically isolated from the game's private `SectionType` switch until a stable supported extension point is demonstrated.

This gives the desired vanilla appearance without binding correctness to obfuscated/minified component identifiers.

### 13.2 Common panel shell

Common header:

```text
Area name
Area type
Lifecycle status
Level/progress when the module supports it
```

Common actions:

- Edit area
- Locate/focus area
- Rename
- Delete area
- Assign/change required anchor facility when applicable

Module tabs/content are provided by the module.

Examples:

```text
Park:       Overview | Visitors | Finances | Maintenance
Industrial: Overview | Production | Logistics | Employment
Tourism:    Overview | Visitors | Spending | Attractions
```

The tab sets are module-owned; Area Core supplies the shell.

## 14. Progress and budget UI

The shipped UI exposes reusable visual patterns for progress bars and service budget sliders.

The service budget control uses a percentage slider pattern and the game's service UI uses the familiar 50–150% budget range. Area modules may use that range when it matches the simulation semantics, but Area Core must not force it on every area type.

Progress/level displays should use the same visual proportions as the shipped progress bar rather than inventing a visually unrelated component.

## 15. Park module boundary

The Park module will later consume Area Core to implement:

- entrance registration
- real visitor tracking
- resident/tourist split
- leisure/attractiveness integration
- park rating
- progression
- maintenance
- optional admission pricing
- revenue/expense reporting

The intended gameplay remains:

```text
Draw Park Area
  -> build ordinary paths/props/facilities using normal game tools
  -> establish an entrance
  -> real citizens/tourists navigate to and use the park
  -> Park module measures and manages that activity
```

Area Core does not fake visitor counts.

## 16. Industrial module boundary

Industrial Area requires a headquarters to become Active.

The Industrial module later classifies and aggregates actual game entities such as:

- extractors
- processors
- warehouses/storage
- factories
- cargo/logistics infrastructure
- employees/workplaces

Existing CS2 specialized industry already uses area-based extraction (`LotPrefab` + `ExtractorArea` -> `Game.Areas.Extractor`), so the Industrial module should aggregate and extend that simulation rather than replace it.

Progression should ultimately derive from real production, employment, processing, export/logistics, and efficiency metrics rather than building count alone.

## 17. Tourism module boundary

Tourism Area can activate without a mandatory headquarters.

The Tourism module later aggregates real tourism-related entities and activity such as:

- hotels/lodging
- attractions
- parks
- entertainment
- restaurants/commercial activity
- transport access
- tourist presence/spending

A Park Area inside a Tourism Area should retain Park simulation while contributing to Tourism statistics.

## 18. Persistence

Persist only authoritative state:

- managed area identity/type
- module lifecycle/progression state
- settings
- headquarters/required-anchor links when applicable
- lifetime statistics that cannot be reconstructed

Do not persist the full membership cache.

Load flow:

```text
Deserialize authoritative area/module state
  -> native Area SearchSystem rebuild/ready
  -> rebuild Area Core membership indexes
  -> validate required facilities
  -> set Active/Suspended state
  -> start module simulation
```

If a referenced headquarters no longer exists, the Industrial Area becomes Suspended rather than failing load.

## 19. Performance constraints

Area System is city-wide infrastructure and must be designed around low steady-state overhead.

Requirements:

- no all-buildings x all-areas per-frame scan
- use change-driven/incremental membership updates
- throttle non-critical statistics
- keep expensive module aggregation off the rendering/UI frame where possible
- cache classification results that depend only on prefab/component identity
- separate UI refresh rate from simulation update rate

A performance diagnostic counter set should eventually report membership rebuild counts and update cost, but diagnostic UI is not required for the first Area Core deliverable.

## 20. Error handling and compatibility

Area Core should fail by feature, not globally.

Examples:

- Missing/changed optional icon -> use fallback icon.
- Missing headquarters building -> suspend only that Industrial Area.
- Unknown future `TypeId` in a save -> preserve entity/state where possible and mark module unavailable instead of deleting data.
- Module exception during optional statistics -> log and keep geometry/editing available.
- UI integration failure -> area simulation remains loadable and recoverable.

The mod should avoid modifying global vanilla systems unless a later specification demonstrates that the benefit outweighs compatibility risk.

## 21. Third-party source policy

Code reuse is encouraged when the repository has a compatible explicit license and the reused implementation materially reduces risk.

Approved research/reuse candidates currently include:

- `KruemelGames/ParkingLotTool` — GPL-3.0; especially runtime area prefab lifecycle, area prefab initialization timing, and related area handling.
- `tomkpunkt/ParkManager` — GPL-3.0; especially serialization/cleanup/logical ownership patterns. Its custom polygon input is not the intended UX for Area Core.
- Apache/MIT-licensed CS2 UI/tool mods may be reused where directly relevant.

Repositories without an explicit usable license may be studied for behavior but their code must not be copied.

When production code is copied or adapted, create/update `THIRD_PARTY.md` with:

- source repository
- source path/file
- license
- copied/adapted portion
- local destination
- modification notes

The repository is currently private, but code provenance must still be tracked so future distribution decisions remain possible.

## 22. Testing strategy for Area Core

The implementation plan must cover at least these behaviors:

1. Prefab registration is complete before an area instance uses its archetype.
2. Park/Industrial/Tourism area selection activates the native area tool.
3. Create/edit/delete flows preserve the correct managed type.
4. Save/load retains type and lifecycle state.
5. Membership cache rebuilds after load.
6. Moving/creating/deleting a candidate member updates affected area indexes without a full-city scan.
7. Cross-kind overlap supports Park inside Tourism.
8. Same-kind overlap does not double-count exclusive module statistics.
9. Industrial Area becomes Suspended when headquarters is missing and returns Active after reassignment.
10. Toolbar/Asset Menu registration does not require modifying the game's shipped `index.js`.
11. UI failure does not corrupt simulation state.

Game-runtime tests will still be necessary for native tool behavior that cannot be proven outside Cities: Skylines II.

## 23. Planned repository decomposition

Area Core should be implementation-isolated roughly as follows; exact filenames are finalized in the implementation plan:

```text
src/
  Core/
    Areas/          geometry identity, lifecycle, registry
    Membership/     spatial membership cache and invalidation
    Persistence/    serialization/rebuild coordination
    Prefabs/        runtime area/UI prefab registration
    UI/             bindings + management panel shell
  Modules/
    Park/
    Industrial/
    Tourism/
```

The first implementation plan must target **Area Core only**, with minimal module stubs sufficient to prove three area types and activation rules. Full Park, Industrial, and Tourism simulation work follows in separate specs/plans.

## 24. Final design decisions

The approved direction is therefore:

- Use native `AreaToolSystem`, not a custom freehand tool.
- Register custom editable area prefabs.
- Treat an area as a management boundary, not a generator.
- Keep module simulation separate from Area Core.
- Build membership as a derived incremental cache.
- Permit useful cross-kind nesting/overlap.
- Require headquarters for Industrial Area, with vanilla-building assignment as the no-modelling baseline.
- Integrate the bottom construction menu through native `UIToolbarGroupPrefab` / `UIAssetMenuPrefab` / `UIObject` / `ToolbarUISystem` data paths.
- Use the supplied 1.6.2f1 UI bundle as a visual/behavioral reference, not as a file to patch.
- Use a mod-owned vanilla-styled management panel until a stable native Selected Info Panel extension point is proven.
- Avoid Harmony and global-system replacement in Area Core v1.
- Track all third-party copied/adapted code in `THIRD_PARTY.md`.
