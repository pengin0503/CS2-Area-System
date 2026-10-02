# Area District-like UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Execution target for this project:** Native mode on `main`, tasks in order.

**Goal:** Placed managed Areas get a name, a district-style map label, label-click selection and the policy row above the Area content, like vanilla districts.

**Architecture:** Pure helpers (auto-name, label mesh data, label hit test, click resolution) carry the logic and the tests. Two thin ECS systems wire them to the game: `AreaPlacementSystem` (ModificationEnd, after `ManagedAreaLifecycleSystem`) names and selects new Areas; `AreaLabelSystem` (ToolUpdate, after `DefaultToolSystem`) lays out labels with the vanilla TextMeshPro, resolves hover and clicks, and draws one mesh with the district name material from a `beginContextRendering` callback.

**Tech Stack:** C# 9, Unity ECS / Cities: Skylines II 1.6.2f1 managed APIs, TextMeshPro, Unity Input System, NUnit 4.

**Spec:** `docs/superpowers/specs/2026-10-02-area-district-like-ux-design.md`

## Global Constraints

- Target game version is exactly Cities: Skylines II **1.6.2f1**; check members against the supplied managed DLLs, never guess.
- No Harmony, no reflection on game internals (`scripts/verify-source.ps1` rejects Harmony).
- Do not change vanilla prefabs or vanilla `AreaTypePrefab` settings.
- A label or placement failure must not disable Area Core: catch, log through `Mod.Log`, and keep going (existing failure-isolation pattern).
- Keep `Mod.cs` and `AreaManagementUISystem.cs` thin; new logic lives in `src/CS2AreaSystem/Core/Labels/`.
- Full verification after each integration-sized task: `pwsh -NoProfile -File scripts/verify.ps1 -ApiCheck`.
- Work on `main`.

## Review Focus

- A building at the Area centre under the label: the label must win when it is nearer on the ray than the vanilla hit (Task 3 test `LabelNearerThanVanillaHitOverridesSelection`).
- Two Areas with nearby labels: the nearest label on the ray wins; equal distance picks the lower entity index, so the result is stable (Task 3 test `NearestLabelWinsAndTiesAreStable`).
- A name that wraps to two lines: both lines are clickable (Task 3 test `EveryLabelLineIsClickable`).
- Clicking over a UI panel or with another tool active never changes the selection (Task 3 test `NoSelectionWithoutWorldClickInDefaultTool`).
- Rename to an empty string (vanilla removes `CustomName`) and deletion of a hovered Area: the label set changes and the mesh rebuilds; a deleted entity is never selected (Task 2 test `SnapshotDetectsRenameMoveHighlightAndRemoval`, Task 4 guard).

---

### Task 1: Auto-name and auto-select new Areas

**Files:**
- Create: `src/CS2AreaSystem/Core/Labels/AreaAutoName.cs`
- Create: `src/CS2AreaSystem/Core/Labels/AreaPlacementSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/ManagedAreaLifecycleSystem.cs` (record newly identified Areas)
- Modify: `src/CS2AreaSystem/Mod.cs` (register after the lifecycle system)
- Test: `tests/CS2AreaSystem.Tests/AreaAutoNameTests.cs`, `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- Produces: `public static string AreaAutoName.Next(string typeName, IEnumerable<string> takenNames)`; `public static Entity AreaAutoName.SelectionAfterPlacement(bool areaToolActive, IReadOnlyList<Entity> placed)`; `public IReadOnlyList<Entity> ManagedAreaLifecycleSystem.Identified` (Areas that received a new identity in the latest update, cleared at the start of each update).

- [ ] **Step 1: Write the failing tests** — `NextUsesTheSmallestFreeNumber` (`Next("公園エリア", {"公園エリア 1","公園エリア 3"}) == "公園エリア 2"`; empty set → `"公園エリア 1"`; names of other types and user names such as `"Central Park"` do not block); `NextFallsBackForMissingTypeName` (`Next(null, {}) == "Area 1"`, whitespace too); `NextIgnoresNearMisses` (`"公園エリア 01"`, `"公園エリア 1 "` do not take 1); `SelectionOnlyWhileAreaToolIsActive` (inactive → `Entity.Null`; active with `{a,b}` → `b`; active with `{}` → `Entity.Null`); schedule test `PlacementRunsAfterLifecycleInModificationEnd`.
- [ ] **Step 2: Run** `dotnet test tests/CS2AreaSystem.Tests -p:AreaApiCheck=true --filter "AreaAutoName|PlacementRunsAfter"` — expect compile failure.
- [ ] **Step 3: Implement.** `Next` compares ordinal strings `"{type} {n}"`. Lifecycle adds each entity it gives `ManagedArea` to (the `!hasManaged` branch) to `Identified`. `AreaPlacementSystem : GameSystemBase` reads `Identified`; for each Area without `NameSystem.TryGetCustomName`, takes the localized `descriptor.DisplayNameKey` from `GameManager.instance.localizationManager.activeDictionary` (key itself not used; null → fallback), collects `GetRenderedLabelName` of all live managed Areas and calls `SetCustomName`. Then `SetSelection(SelectionAfterPlacement(tools.activeTool is AreaToolSystem, identified))` when non-null. Whole update in try/catch → `Mod.Log.Error`. Register `updateSystem.UpdateAfter<AreaPlacementSystem, ManagedAreaLifecycleSystem>(SystemUpdatePhase.ModificationEnd)` inside a try/catch like the UI registrations.
- [ ] **Step 4: Run** the filtered tests — PASS.
- [ ] **Step 5: Commit** `feat: name new Areas and select them after placement`.

### Task 2: Label snapshot and mesh data

**Files:**
- Create: `src/CS2AreaSystem/Core/Labels/AreaLabelMesh.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaLabelMeshTests.cs`

**Interfaces:**
- Produces:
  - `[StructLayout(Sequential)] public struct AreaLabelVertex { Vector3 Position; Color32 Color; Vector2 UV0; Vector2 UV1; Vector3 UV2; }` (same layout as vanilla `LabelVertexData`) and `public static VertexAttributeDescriptor[] AreaLabelVertex.Layout()`.
  - `public struct AreaLabelGlyphVertex { Vector3 Position; Color32 Color; Vector2 UV0; Vector2 UV1; int Material; }`; `public sealed class AreaLabelLayout { IReadOnlyList<AreaLabelGlyphVertex> Vertices; IReadOnlyList<Bounds2> Lines; }`.
  - `public readonly struct AreaLabelEntry { Entity Area; string Name; float3 Position; bool Highlighted; }` and `public static bool AreaLabelEntry.Equivalent(IReadOnlyList<AreaLabelEntry> a, IReadOnlyList<AreaLabelEntry> b)` (same order, same fields).
  - `public sealed class AreaLabelMeshData { AreaLabelVertex[] Vertices; uint[] Indices; SubMeshDescriptor[] SubMeshes; }` and `public static AreaLabelMeshData AreaLabelMesh.Build(IReadOnlyList<(float3 position, Color32 tint, AreaLabelLayout layout)> labels, int materialCount)`.

- [ ] **Step 1: Write the failing tests** — `QuadsBecomeTwoTrianglesPerMaterialSubMesh` (two glyph quads on material 1 and one on material 0 → submesh 0 has 4 vertices/6 indices first, submesh 1 has 8/12, indices of a quad are `v, v+1, v+2, v+2, v+3, v`); `VertexCarriesLabelPositionAndTintedColor` (UV2 == label position; colour `(255,128,64,200)` × tint `(128,128,128,128)` → `(127,64,32,100)` with `(c*t)>>8`); `VertexLayoutMatchesVanillaLabelVertexData` (size 44 bytes, five attributes in order Position F32×3, Color UNorm8×4, TexCoord0 F32×2, TexCoord1 F32×2, TexCoord2 F32×3); `EmptyInputBuildsEmptySubMeshes`; `SnapshotDetectsRenameMoveHighlightAndRemoval`.
- [ ] **Step 2: Run** `--filter AreaLabelMesh` — compile failure.
- [ ] **Step 3: Implement** `Build` as a two-pass count-then-fill like `AreaBufferSystem.FillNameDataJob` (glyph vertices come in fours).
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `feat: build district-style Area label mesh data`.

### Task 3: Label hit test and click resolution

**Files:**
- Create: `src/CS2AreaSystem/Core/Labels/AreaLabelPicking.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaLabelPickingTests.cs`

**Interfaces:**
- Consumes: `AreaLabelLayout.Lines` (Task 2).
- Produces:
  - `public static bool AreaLabelPicking.TryHit(Line3.Segment ray, float3 cameraRight, float3 labelPosition, IReadOnlyList<Bounds2> lines, out float distance)` — vanilla `RaycastLabelsJob` math.
  - `public static bool AreaLabelPicking.TryPick(Line3.Segment ray, float3 cameraRight, IReadOnlyList<(Entity area, float3 position, IReadOnlyList<Bounds2> lines)> labels, out Entity area, out float distance)` — smallest distance, tie → lower `Entity.Index`.
  - `public static Entity AreaLabelPicking.ResolveClick(bool pressed, bool overWorld, bool defaultToolActive, bool labelHit, Entity label, float labelDistance, bool vanillaHit, float vanillaDistance)` — returns the Area to select or `Entity.Null` (leave vanilla's selection).

- [ ] **Step 1: Write the failing tests** — `RayThroughLabelCentreHits` (camera at (0,1000,0) looking down, label at origin, line bounds (−100,−20)…(100,20)); `RayBesideLabelMisses`; `EveryLabelLineIsClickable` (second line only); `LabelScaleGrowsWithCameraDistance` (a ray that misses at 1000 m hits at 4000 m for the same screen offset ratio); `NearestLabelWinsAndTiesAreStable`; `NoSelectionWithoutWorldClickInDefaultTool` (any of pressed/overWorld/defaultToolActive false → Null); `LabelNearerThanVanillaHitOverridesSelection`; `VanillaHitNearerKeepsVanillaSelection`; `NoVanillaHitSelectsLabel`.
- [ ] **Step 2: Run** `--filter AreaLabelPicking` — compile failure.
- [ ] **Step 3: Implement** with `AreaUtils.CalculateLabelRotation`, `CalculateLabelMatrix`, `CalculateLabelScale` and `MathUtils.Intersect(Quad3, Line3.Segment, out t)`; distance `t − size(bounds.y) × scale / max(1, length(ray))`.
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `feat: pick Area labels like district labels`.

### Task 4: AreaLabelSystem (layout, hover, click, render)

**Files:**
- Create: `src/CS2AreaSystem/Core/Labels/AreaLabelSystem.cs`
- Modify: `src/CS2AreaSystem/CS2AreaSystem.csproj` (add `Unity.TextMeshPro;Unity.InputSystem` to the game references)
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs` (comment: the list is no longer the only route)
- Test: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- Consumes: Tasks 2 and 3.
- Produces: `public Entity AreaLabelSystem.Hovered { get; }` (diagnostics only).

- [ ] **Step 1: Write the failing test** `LabelsRunAfterTheDefaultToolInToolUpdate` (mod registers `UpdateAfter<AreaLabelSystem, DefaultToolSystem>(ToolUpdate)`; vanilla registers `DefaultToolSystem` `UpdateAt` `ToolUpdate`).
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Implement.**
  - Query: `ManagedArea`, `Geometry`, none of `Temp`, `Hidden`, `Deleted`. Run only in game mode with `AreaManagementUISystem.IsReady`.
  - Each update: build entries (name from `GetRenderedLabelName`, position `Geometry.m_CenterPosition`, highlighted = selected or hovered); layouts cached per name string (TextMeshPro from `OverlayRenderSystem.GetTextMesh()`, set `sizeDelta (250,100)`, `fontSize 200`, `Center` before each `GetTextInfo`); materials created per font atlas texture from the district `AreaTypePrefab.m_NameMaterial` + `CopyFontAtlasParameters`, found through `PrefabSystem` and an `AreaTypeData` query; colours from the district prefab's `AreaNameData` or `GetDefaults()`.
  - Picking: camera from `CameraUpdateSystem.TryGetViewer`; ray `ToolRaycastSystem.CalculateRaycastLine(viewer.camera)`, camera right `viewer.right`; hover when the default tool is active and `InputManager.instance.controlOverWorld`; click via `ResolveClick` with `Mouse.current?.leftButton.wasPressedThisFrame`, vanilla result from `ToolRaycastSystem.GetRaycastResult` (ignored when its owner is `Deleted`); select through `SelectedInfoUISystem.SetSelection`.
  - Mesh: rebuild only when `Equivalent` is false or a layout/material changed; bounds = union of `Geometry.m_Bounds` expanded by 2000 m. Draw in `RenderPipelineManager.beginContextRendering` for Game/SceneView cameras unless `RenderingSystem.hideOverlay`; destroy mesh and materials and unsubscribe in `OnDestroy`; clear on `OnGamePreload`.
  - Registration: `updateSystem.UpdateAfter<AreaLabelSystem, Game.Tools.DefaultToolSystem>(SystemUpdatePhase.ToolUpdate)` in try/catch.
- [ ] **Step 4: Run** the full verify — PASS.
- [ ] **Step 5: Commit** `feat: draw and click district-style Area labels`.

### Task 5: Policy row above the Area section

**Files:**
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs` (create `AreaPolicyInfoSection` before `AreaInfoSection`)
- Modify: `src/CS2AreaSystem/Core/UI/AreaPolicyInfoSection.cs` (comment)
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyViewModelTests.cs` (or a new `AreaSectionOrderTests.cs`)

- [ ] **Step 1: Write the failing test** `PolicySectionIsRegisteredBeforeAreaSection`: `IlCallScanner.Find` over `AreaManagementUISystem.OnCreate` for `GetOrCreateSystemManaged<T>` calls; the `AreaPolicyInfoSection` call offset is lower than the `AreaInfoSection` one.
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Swap the two calls**; update both comments.
- [ ] **Step 4: Run** full verify — PASS.
- [ ] **Step 5: Commit** `feat: show Area policies above the Area section like districts`, then push `main`.

### In-game checklist (user)

Label look and scale versus a district label; click on a label over a building at the Area centre; title rename and the label update; auto-select after drawing (and whether the panel shows while the Area tool stays active); policy row position; labels hidden in photo mode.
