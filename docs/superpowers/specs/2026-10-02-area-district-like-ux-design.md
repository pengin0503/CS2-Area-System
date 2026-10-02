# Area District-like UX Design

Date: 2026-10-02. Game target: Cities: Skylines II 1.6.2f1. Status: approved direction (option A, "keep Space areas").

## 1. Problem

A placed managed Area (Park / Industrial / Tourism, a `SpacePrefab` instance) behaves like a painted ground texture:

- it cannot be selected from the map: `DefaultToolSystem.InitializeRaycast` raycasts only `AreaTypeMask.Lots`, and districts are picked through their name labels (`TypeMask.Labels`), whose raycast query is `District`-only (`RaycastSystem.cs:758`);
- it has no map label: `AreaBufferSystem` builds label meshes only for area types whose `AreaTypePrefab.m_NameMaterial` is set (districts), and the Space fill is drawn only while a tool requires Spaces;
- it gets no name of its own: without a `CustomName`, `NameSystem.GetRenderedLabelName` returns the prefab title, so every Park Area is "Park Area".

The selected-info panel content already exists (`AreaInfoSection`, `AreaPolicyInfoSection`), and the vanilla `TitleSection` renames any selected entity (`TitleSection.OnRename` → `NameSystem.SetCustomName`). The only route to select an Area today is the mod's Area list.

## 2. Goal

A placed Area works like a vanilla district:

1. it gets a name when it is completed, and the player can rename it from the panel title;
2. its name is drawn on the map at its centre, in the district label style;
3. clicking that label in the default tool selects the Area and opens the selected-info panel with the Area and policy sections;
4. the policy section sits where the district panel shows policies (above the statistics).

## 3. Decisions

- **Keep the Space type.** Districts change `CurrentDistrict` of buildings and cannot overlap; managed Areas must not. Vanilla Space rendering settings are not changed (that would draw every building sub-space).
- **No Harmony.** Everything uses public systems: `NameSystem`, `SelectedInfoUISystem.SetSelection`, `ToolSystem`, `ToolRaycastSystem.GetRaycastResult`, `ToolRaycastSystem.CalculateRaycastLine`, `OverlayRenderSystem.GetTextMesh` / `CopyFontAtlasParameters`, `RenderPipelineManager.beginContextRendering`.
- **Auto-name.** When an Area receives its identity (new placement), and it has no custom name, it gets `"<type display name> <n>"` (for example `公園エリア 1`), with the smallest `n ≥ 1` whose name no managed Area currently renders. The name is a vanilla `CustomName`, so it is saved by `NameSystem` and renamed through the vanilla title.
- **Auto-select.** When the new Area was placed while the Area tool is the active tool, it becomes the selection (`SelectedInfoUISystem.SetSelection`). With several in one frame, the last one wins. Saved Areas without a custom name (for example from saves made before this feature) are named once per load, in stable-ID order, and never selected.
- **Label look.** Labels reuse the district `AreaTypePrefab.m_NameMaterial`, text layout (`TextMeshPro` from `OverlayRenderSystem.GetTextMesh`, size 250×100, font size 200, centred), vertex layout (`AreaBufferSystem.LabelVertexData`: position f32×3, colour unorm8×4, uv0 f32×2, uv1 f32×2, uv2 f32×3 = label world position), quad indices (0,1,2,2,3,0) and colours (`AreaNameData` of the district prefab, defaults when missing; glyph colour × tint `>> 8`). The highlighted colour (`m_SelectedColor`) is used for the selected and the hovered Area. Labels are hidden when `RenderingSystem.hideOverlay` is set, and for `Temp`, `Hidden` and `Deleted` Areas.
- **Click = label only, like districts.** Clicking the ground inside an Area does not select it; this keeps "click empty ground to deselect" working inside large Areas. A label hit follows vanilla `RaycastLabelsJob` math (`AreaUtils.CalculateLabelMatrix`, `CalculateLabelRotation`, per-line `LabelExtents`, normalized distance `t − lineHeight × scale / max(1, rayLength)`).
- **Priority.** On the frame the default tool's apply is pressed over the world, after `DefaultToolSystem` has run: if a label is hit and it is nearer than the vanilla raycast result (or vanilla hit nothing), the Area becomes the selection. Otherwise vanilla's selection stands. The press is the left mouse button (`Mouse.current.leftButton.wasPressedThisFrame`, the default binding of the tool's mouse apply) gated by `InputManager.controlOverWorld`; the tool's own `applyAction` is not public. Gamepad selection of Area labels is out of scope.
- **Policies first.** `AreaPolicyInfoSection` is registered before `AreaInfoSection`, so the policy row appears above the Area content, as `PoliciesSection` does for districts.

## 4. Out of scope

- The vanilla bottom-bar delete button (`ActionsSection.deletable` is hard-coded to District / ServiceUpgrade / TransportLine); the Area section keeps its own Edit and Delete.
- Interior click selection, boundary outline rendering, label colour per Area type.
- Fixing the ExtraLib toolbar conflict (needs the user's ExtraLib.dll).

## 5. Verification

- Offline: unit tests for naming, mesh building, label hit test and click resolution; schedule tests for the new systems; `scripts/verify.ps1 -ApiCheck`.
- In game (user): label look and scale versus a district label, click priority over a building at the Area centre, rename from the title, auto-select after drawing, policy row position, no label in photo mode.
