# Area System built-in assets contract (Phase 3 C2)

Target: Cities: Skylines II **1.6.2f1**  
Spec: `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md` §2.4, §3.3, §4.5, §5, §6 and Phase 3 C2  
Plan: `docs/superpowers/plans/2026-10-02-phase3-park-gate-asset-integration-implementation.md`  
Code: `src/CS2AreaSystem/Core/Semantics/AreaBuiltInAssetContract.cs`

## Identifiers

| Item | Value |
| --- | --- |
| Content package logical ID | `AS_AreaSystemAssets` |
| Semantic provider ID | `CS2AreaSystem.BuiltInContent` |
| Prefab type | `BuildingPrefab` (normal placeable building) |
| Owned identifier prefix | `AS_` (every Area System-owned model/prefab identifier) |

| Prefab name (exact, ordinal) | Semantic (Allow) | Area role |
| --- | --- | --- |
| `AS_ParkHeadquarters` | `ParkHeadquarters` | Park Headquarters |
| `AS_IndustrialHeadquarters` | `IndustrialHeadquarters` | Industrial Headquarters |
| `AS_ParkGate` | `ParkEntrance` | Park Entrance |

Each role has exactly one dedicated prefab and each prefab grants exactly one semantic.

## Authoritative source archives

| Archive | SHA-256 | Supplies |
| --- | --- | --- |
| `CS2_AS_IndustrialHeadquarters.zip` | `30b4138e2ce7b8c57a89ec783a57f5bf3ee4a233f2ce8beff01203c0ca14b1a3` | `AS_IndustrialHeadquarters` (source folder `IndustrialAreaHQ01_5x4`) |
| `CS2_AS_ParkSet_HQ_Gate.zip` | `b4e0d3c249552c3b8922000c93a53b7a17198745e1748bd1ef445e1e4852d175` | `AS_ParkHeadquarters` (source folder `ParkAreaHQ01_4x3`) and `AS_ParkGate` (source folder `ParkGate01_2x1`, textures shared with Park HQ through `settings.json`) |

The standalone `CS2_AS_ParkHeadquarters.zip` is a superseded copy and is not a C2 source. The ParkSet archive is the only
source for both Park assets so the Gate keeps its shared texture/material relationship with the Park Headquarters.

## Identity rules

- A prefab qualifies only when all of these hold:
  - its prefab type is `BuildingPrefab`;
  - its prefab name equals one identifier above with ordinal comparison (no trimming, case folding, substring, prefix or suffix match);
  - its asset is packaged (`SourceMeta.packaged`), is not from the base-game/DLC database, and its package name starts with `AS_`
    and ends with `.cok` (ordinal).
- Display names, localized names, thumbnails, DLC-like names and look-alike buildings are never consulted.
- Runtime prefab `Entity` values are never saved. The provider unregisters its previous registrations and rediscovers the
  current prefab entities every load generation before activation and assignment use them.
- A missing prefab registers nothing for its role. There is no fallback: no vanilla building, no office, no Park Maintenance
  facility and no derived pedestrian-path crossing is substituted.

### Why the package rule is `AS_*.cok`

Verified in the supplied 1.6.2f1 managed assemblies:

- `Game.SceneFlow.GameManager.LoadPrefabs` loads every `PrefabAsset` in `AssetDatabase.global` into `PrefabSystem` at boot.
- `Game.Prefabs.PrefabID` is `(type, name, hash)` where the hash comes from the asset's platform ID or GUID, so a mod asset cannot
  be looked up by a precomputed `PrefabID`; the provider therefore enumerates building prefabs and compares the exact name.
- `Colossal.IO.AssetDatabase.FileSystemDataSource` sets `SourceMeta.packageName = <package display name> + <extension>` for
  packaged assets; `PackageAsset.kExtensions` is `.cok/.cop/.zip`.
- `Game.UI.Editor.PackageHelper.CreatePackage` and `Game.UI.Menu.PdxAssetUploadHandle` name a package after its main asset, so a
  package whose main asset is one of the identifiers above is named `AS_<identifier>.cok`.

## Retired Foundation sources (no fallback after C2)

| Role | Foundation-era source | After C2 |
| --- | --- | --- |
| Industrial Headquarters | ordinary office building (`OfficeProperty`) | rejected; only `AS_IndustrialHeadquarters` |
| Park Headquarters | Park Maintenance facility (`Game.Buildings.ParkMaintenance`) | rejected; only `AS_ParkHeadquarters` |
| Park Entrance | derived dedicated pedestrian-Path boundary crossing (`PathwayData`) | removed; only an assigned `AS_ParkGate` |

Existing saves keep Area StableId, Level/XP, finance, history, policies and Industrial company ownership. Old Headquarters or
Entrance links remain visible as invalid/removable links and are never converted into dedicated anchors; the Area is Suspended
until the matching `AS_` asset is assigned.

## Park Gate pedestrian passage

`AS_ParkGate` carries an editor-authored pedestrian passage (`Game.Prefabs.ObjectSubNets` with a pedestrian Path net prefab)
through its central opening, from the front lot edge (+Z) to the rear lot edge (−Z), with both end nodes at the lot edges so
external pedestrian paths can connect. Area System does not intercept or rewrite citizen pathfinding.

## Model content rule

Models contain only building-fixed elements (doors, windows, vents, piping, wall-mounted equipment). Benches, vegetation,
streetlights and other scenery available as vanilla props are not modeled.

## Source preparation, import and shipping

- `scripts/prepare_area_system_assets.py` produces the Asset Importer project root `AS_AreaSystemAssets/` from the two archives
  (hash-checked, superseded archive refused, files/folders renamed to the exact identifiers, Gate `settings.json` pointed at the
  renamed Park HQ textures, bytes otherwise unchanged). Its `--self-test` pins that behavior.
- Import, Gate pedestrian-passage authoring and packaging are in-editor steps documented in `assets/area-system/README.md`.
- Shipping: the official build target `DeployAreaContent` copies `assets/area-system/package/AS_*.cok` into the mod deploy folder,
  which is also the publish content folder (toolchain `Mod.targets` `DeployWIP`/`AdditionalFilesToDeploy` pattern). The User asset
  database (`EnvPath.kUserDataPath`, package extensions watched) and `ParadoxModsDataSource` index `.cok` files in mod folders;
  `GameManager.LoadPrefabs` then adds their prefabs to `PrefabSystem` at boot. A missing package produces a build warning and,
  at runtime, the missing-content state; nothing is substituted.
