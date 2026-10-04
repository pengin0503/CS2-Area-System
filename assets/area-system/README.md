# Area System content (Phase 3 C2)

Contract: `docs/content/area-system-built-in-assets-contract.md`

This folder documents how the three required Area System assets are produced and shipped. Source meshes/textures and the
editor-built `.cok` package are binary editor inputs/outputs and are not tracked in git (`.gitignore`):

```
assets/area-system/
  README.md              (this file, tracked)
  import/                prepared Asset Importer project root (not tracked)
  package/AS_*.cok       editor-built content package(s) shipped with the mod (not tracked)
```

## 1. Prepare the importer sources (verified offline)

```bash
python3 scripts/prepare_area_system_assets.py \
  --industrial /path/to/CS2_AS_IndustrialHeadquarters.zip \
  --park /path/to/CS2_AS_ParkSet_HQ_Gate.zip \
  --out assets/area-system/import
python3 scripts/prepare_area_system_assets.py --self-test
```

The script refuses the superseded `CS2_AS_ParkHeadquarters.zip`, checks both archives against the SHA-256 values in the
contract, renames every file/folder to its exact `AS_` identifier, rewrites the Park Gate `settings.json` to share the renamed
`AS_ParkHeadquarters` textures (no duplicated atlas), copies meshes/textures byte-for-byte and writes `manifest.json`.

Result (`AS_AreaSystemAssets` is the Asset Importer project root):

```
AS_AreaSystemAssets/
  AS_IndustrialHeadquarters/AS_IndustrialHeadquarters*.fbx|png
  AS_ParkSet/AS_ParkHeadquarters/AS_ParkHeadquarters*.fbx|png
  AS_ParkSet/AS_ParkGate/AS_ParkGate*.fbx + settings.json
  manifest.json
```

Pre-packaging blockers (not changed by the script). Do not package or ship until both are resolved in the authoring source:

- FBX-internal node names and embedded texture paths still carry the authoring names (`ParkAreaHQ01_4x3`, `ParkGate01_2x1`,
  `IndustrialAreaHQ01_5x4`, `/home/claude/cs2hq/...`). Binary FBX strings are length-prefixed, so they are not patched here.
  The importer resolves assets and textures by file/folder name; confirm in the editor that no imported prefab/mesh
  identifier uses the old names, otherwise re-export from the authoring source with the `AS_` names.
- The source README states that the Headquarters meshes include streetlights in their emissive set and that the Gate
  includes four path bollards. Under the project model rule these replaceable props should be removed from the meshes and
  placed as vanilla props instead. The supplied archives cannot satisfy this rule as they are.

## 2. Import in the 1.6.2f1 editor (in game; NOT RUN in this repository)

1. Main menu → Editor → **Asset Importer**.
2. Project Root: `AS_AreaSystemAssets`. Assets Folder: the project root (one batch, so the Park Gate shares the Park HQ
   textures as required by the ParkSet source).
3. Prefab Preset: **Building** for all three. Confirm the prefab names are exactly `AS_ParkHeadquarters`,
   `AS_IndustrialHeadquarters`, `AS_ParkGate`.
4. Check each prefab: LOD0–LOD2, materials/textures, collision, lot size (Park HQ 4×3, Industrial HQ 5×4, Gate 2×1), front (+Z)
   on the road side, and road access.
5. Do not add benches, plants, streetlights or other props to the prefabs.

## 3. Author the Park Gate pedestrian passage (in game; NOT RUN)

On `AS_ParkGate` add the `Object Sub Nets` component (`Game.Prefabs.ObjectSubNets`) with one entry:

- Net prefab: the vanilla pedestrian Path (a `PathwayPrefab`, which carries `PathwayData` and `LocalConnectData`).
- Bezier curve: a straight line through the central opening from the front lot edge to the rear lot edge,
  `(0, 0, +4)` → `(0, 0, −4)` in prefab space (the source README places the 4.0 m paved passage on Z = +4 … −4).
- Leave node indices at `-1` so both ends are free nodes on the lot edges that external pedestrian paths can snap to.

Then verify in the editor and in a city: an external pedestrian Path snaps to both ends, the route is continuous through the
gate, and nothing collides with the gate posts. Area System adds no pathfinding hook.

## 4. Package and ship

1. In the editor, create the package from the imported prefabs with `AS_ParkHeadquarters` as the main asset and the other two
   included, so the package is named `AS_ParkHeadquarters.cok` (editor packages are named after their main asset). Any
   `AS_*.cok` name satisfies the ownership rule; non-`AS_` packages and unpackaged local prefabs are ignored.
2. Copy the package to `assets/area-system/package/`.
3. The official build target `DeployAreaContent` copies `assets/area-system/package/AS_*.cok` into the mod deploy folder, which
   is also the publish content folder. It warns when no package is present. The User (`Mods` under the user data folder) and
   ParadoxMods asset databases index `.cok` files in mod folders, and `GameManager.LoadPrefabs` loads their prefabs at boot.
4. At load, Area System logs which required prefabs it registered. Missing content is reported in the Area panel and nothing is
   substituted.

## Release gate

C2 has no fallback, so a mod deployed without `AS_*.cok` suspends every Park and Industrial Area. A deploying build
(`DeployAreaContent`) therefore fails when `assets/area-system/package/AS_*.cok` is missing. Only the missing-content
runtime case C207 may deploy without it, with `-p:AllowMissingAreaContent=true` (a warning instead of the error).
