# Custom factory process contract (Phase 3 D2)

Target: Cities: Skylines II **1.6.2f1**  
Spec: `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §D2  
Plan: `docs/superpowers/plans/2026-10-02-phase3-custom-factory-content-implementation.md`  
Code: `src/CS2AreaSystem/Modules/Industrial/IndustrialReferenceFactoryContract.cs`  
Tests: `tests/CS2AreaSystem.Tests/CustomFactoryProcessContractTests.cs`

## Decision

The reference factory **does not author an `IndustrialProcessData` recipe**. It is an Industrial signature building whose
only manufactured resource is vanilla `Resource.Furniture`. Only a vanilla Furniture processing company can rent it, and
that company's own `IndustrialProcessData` is the recipe that runs and that Area System observes.

| Item | Value |
| --- | --- |
| Prefab name (exact, ordinal) | `AS_FurnitureFactory` (`BuildingPrefab` with `SignatureBuilding`) |
| Output (manufactured) | `Resource.Furniture` = `512` (`ResourceInEditor.Furniture`) |
| Allowed input | empty list (defaults to all resources), so the vanilla company recipe decides the inputs |
| Vanilla input relationship | `Resource.Timber` = `128`, the only material Furniture is made from |
| Sold / stored / residential | none / none / 0 |
| Zone | a vanilla `ZonePrefab` whose `ZoneData.m_AreaType` is `Industrial` and whose `m_ZoneFlags` lacks `Office` |
| Process input/output amounts | not authored. They come from the vanilla Furniture company prefab and are not present in the supplied managed assemblies, so they are not pinned here |

No new `Resource` member, dense resource index or economy array is added. No score or observation code refers to this
asset or to Furniture.

## Evidence in the supplied 1.6.2f1 managed assemblies

Read in the decompiled `CS2-All-Managed-1.6.2f1-fix.zip` (`Game.dll`):

- **The vanilla chain.** `Game.Economy.EconomyUtils.IsProducedFrom(product, material)` lists every vanilla production relationship.
  `Timber` produces only `Furniture` (`Resource.Timber => (product & Resource.Furniture) != 0`). No other material's mask contains
  `Furniture`. So Furniture is a manufactured good made from Timber alone. Wood → Timber is the only other relationship that
  involves either resource.
- **How a building gets a company.** Two vanilla steps:
  - `Game.Simulation.PropertyProcessingSystem` (`PropertyProcessingJob`), for a building with `Signature`, walks the industrial
    company prefabs (`ArchetypeData` + `IndustrialCompanyData` + `IndustrialProcessData`, excluding `StorageCompanyData`) and
    creates one free company of the first prefab whose `m_Output.m_Resource == BuildingPropertyData.m_AllowedManufactured` and
    (`m_AllowedInput == EconomyUtils.GetAllResources()` or `m_AllowedInput == m_Input1 | m_Input2`). It does not move that
    company in; it only puts the building on the market (`PropertyOnMarket`).
  - The tenant is chosen by the industrial find-property flow (`Game.Buildings.PropertyUtils.CompanyFindPropertyJob`), which
    adds `5000` to the score of any `Signature` property. `IndustrialSpawnSystem` also spawns demand companies more readily
    while empty signature buildings exist. So any Furniture company may win the building, not necessarily the one created above.
  - What every possible tenant shares: `PropertyUtils.PropertyAllowsResource` admits a company only when its output is in
    `m_AllowedManufactured` and its inputs are all in `m_AllowedInput`. With manufactured = Furniture only and the empty
    (= all) input list, the tenant is always a vanilla Furniture processing company running its own recipe.
- **Defaults.** `Game.Prefabs.BuildingProperties.GetPropertyData` maps `m_AllowedInput` to
  `EconomyUtils.GetResources(list, GetAllResources())`, so an empty list means any input. The manufactured, sold and stored
  lists map to `Resource.NoResource` when empty. The `IndustrialProperty` archetype (and no `OfficeProperty`/`ExtractorProperty`)
  follows from a non-office, non-extractor `m_AllowedManufactured`.
- **What the company carries.** `Game.Prefabs.ProcessingCompany.Initialize` writes `IndustrialProcessData` from its
  `IndustrialProcess` (`m_Input1`, `m_Input2`, `m_Output`, `m_MaxWorkersPerCell`) through `EconomyUtils.GetResource`.
  `ResourceInEditor.Furniture` maps to `Resource.Furniture`.
- **Signature buildings.** `Game.Prefabs.SignatureBuilding` adds `SignatureBuildingData`, `SpawnableBuildingData` (level 5,
  `m_ZonePrefab` = its `m_ZoneType`) and the `Signature` archetype component, and the building is `PlacementFlags.Unique`.
- **Authoring path.** `Game.UI.Editor.AssetImportPanel` has a `SignatureBuilding` preset. It creates a `BuildingPrefab` with
  `SignatureBuilding` (default zone `Office High`) and `BuildingProperties` (default manufactured `Software`). The author changes
  the zone to an Industrial, non-office zone and the manufactured list to `Furniture`.

## Runtime content check

Every load, the built-in content provider registers `AS_FurnitureFactory` as `IndustrialProducer`, under the same provider
`CS2AreaSystem.BuiltInContent` and the same packaged `AS_*.cok` source rule as the C2 assets. It registers it only when the
loaded prefab entity satisfies this contract:

- `BuildingPropertyData.m_AllowedManufactured == Furniture`;
- `BuildingPropertyData.m_AllowedInput == EconomyUtils.GetAllResources()` (the empty list). A narrowed list that leaves out
  Timber would leave the building empty for ever, and an explicit list may not match the company recipe, so both are rejected;
- no sold or stored resources and no residential properties;
- `SignatureBuildingData` is present;
- the `SpawnableBuildingData.m_ZonePrefab` zone is Industrial and not Office.

A mismatched prefab (for example an import that changed the manufactured resource) is rejected and logged. It gets no role.
The factory is optional content: its absence never suspends an Area and never warns as missing.

## Not verified here

The following are **NOT RUN**:
- that a vanilla Furniture company prefab exists in the loaded game data, and its amounts;
- that such a company actually moves into the factory;
- the asset import and packaging themselves.

These are in-game checks (D201–D206 in `scripts/runtime-smoke-checklist.md`).
