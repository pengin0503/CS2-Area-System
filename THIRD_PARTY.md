# Third-party provenance

The area display-color construction pattern below is adapted under GPL-3.0. Additional UI packaging/type declaration adaptations are listed below. Remaining code is original. The license text is included at `licenses/GPL-3.0.txt`.
Game APIs and the supplied UI bundle are reference material only; game DLLs and media are not redistributed.

| Source repository | Source file/path | License | Copied/adapted portion | Local destination | Modification notes |
|---|---|---|---|---|---|
| [KruemelGames/ParkingLotTool](https://github.com/KruemelGames/ParkingLotTool) | Tools/ParkingLotLotArea.cs, TryResolveLotOwnerPrefab (main inspected 2026-09-30) | GPL-3.0, repository LICENSE verified | Original adaptation: minimal LotPrefab setup and display colors; current code retains the display-color pattern; registration/readiness concepts researched | src/CS2AreaSystem/Core/Prefabs/ManagedAreaPrefabFactory.cs | Three type descriptors and independent colors; switched to native SpacePrefab because Lot is editor-only; overlap is handled by original scoped ECS validation; no parking/economy/clearing components or custom tool |
| [tomkpunkt/ParkManager](https://github.com/tomkpunkt/ParkManager) | UI/webpack.config.js (5815652aa2d096cf7743891e8dda44b82f2fed22); UI/types/api.d.ts; UI/types/modding.d.ts (main inspected 2026-09-30) | GPL-3.0, repository LICENSE verified | CS2 UI module metadata, external modules, module output/CSS packaging; subset of public binding/registrar declarations | UI/webpack.config.js; UI/types/cs2.d.ts | SCSS, original panel, explicit .js filename, no private registry APIs; only bindValue/useValue/trigger/append declarations retained |

ParkManager serialization/lifecycle implementations were researched; no source from those files was copied. The shipped game UI CSS was inspected for tokens and proportions, not copied. No game DLLs/media are redistributed.
