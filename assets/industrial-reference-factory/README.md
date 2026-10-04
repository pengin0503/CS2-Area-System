# Reference factory content (Phase 3 D2)

Contract: `docs/verification/custom-factory-process-contract.md`

Status: **BLOCKED.** No source model for the factory was supplied, and asset import and packaging are in-editor steps that
cannot run in this repository. Nothing here ships until a model that follows the project model rule exists and the steps
below have been completed and checked in game.

The code side is in place. When a packaged `AS_FurnitureFactory` prefab matching the contract is loaded, the built-in
content provider registers it as `IndustrialProducer`. When it is absent, nothing changes.

## Model requirements

- Exact asset/prefab identifier `AS_FurnitureFactory` (files and folder named the same, like the C2 assets).
- Building-fixed elements only (walls, doors, windows, vents, piping, loading docks, wall-mounted equipment). No benches,
  vegetation, streetlights or other scenery available as vanilla props.
- LOD0–LOD2, collision and a road-side front (+Z), as for the C2 assets.

## Import and authoring in the 1.6.2f1 editor (NOT RUN)

1. Main menu → Editor → **Asset Importer**. Use a project root whose folder is `AS_FurnitureFactory`.
2. Prefab Preset: **Signature Building**. The preset creates a `BuildingPrefab` with `SignatureBuilding` (default zone
   `Office High`) and `BuildingProperties` (default manufactured `Software`).
3. In `SignatureBuilding`, set **Zone Type** to a vanilla Industrial manufacturing zone. It must not be an office zone: the
   zone's `ZoneData` must be `AreaType.Industrial` without `ZoneFlags.Office`.
4. In `BuildingProperties`:
   - **Allowed Manufactured** = `Furniture` only;
   - **Allowed Input**, **Allowed Sold** and **Allowed Stored** empty;
   - **Residential Properties** 0.
5. Do not add any company, process or resource data: the game places a vanilla Furniture company.

## Package and ship (NOT RUN)

Package with `AS_FurnitureFactory` as the main asset, so the package is `AS_FurnitureFactory.cok`. Alternatively, include
it in the C2 `AS_*.cok` package. Copy it to `assets/area-system/package/`, where the existing `DeployAreaContent` target
ships every `AS_*.cok`.

## Checks before release

Run D201–D206 in `scripts/runtime-smoke-checklist.md`. The log line `Area System content facts AS_FurnitureFactory ...`
shows the values the contract check read. A rejected prefab is reported in the content warning and gets no role.
