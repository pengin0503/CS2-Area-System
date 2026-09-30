# CS2 Area System — Area Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first production-ready Area Core for Cities: Skylines II 1.6.2f1: three mod-defined management areas selectable from the native construction UI, native AreaToolSystem editing, persistent managed-area identity/lifecycle, incremental membership, headquarters/anchor activation, and a mod-owned vanilla-styled management panel.

**Architecture:** Keep native CS2 area geometry and editing authoritative, and layer mod-owned management state on the resulting area entities. Runtime adapters integrate with PrefabSystem, AreaToolSystem, SearchSystem, SelectedInfoUISystem, and ToolbarUISystem; pure policy/data services isolate registry, ownership, lifecycle, and membership decisions so they can be unit-tested outside the game. Park/Industrial/Tourism remain minimal module stubs in this plan; their full simulation is explicitly deferred.

**Tech Stack:** C# / .NET with the official CS2 1.6.2f1 Modding Toolchain, Unity Entities 1.3.10, Game.dll/Colossal managed APIs, TypeScript + React CS2 UI mod API, NUnit for deterministic C# tests, Vitest for UI tests.

**Spec:** `docs/superpowers/specs/2026-09-30-area-system-core-design.md`

## Global Constraints

- Target Cities: Skylines II **1.6.2f1**.
- Use native `Game.Tools.AreaToolSystem`; do not implement a custom freehand polygon tool.
- Native `Game.Areas.Area`, node/triangle buffers, geometry, and search remain authoritative for spatial state.
- Initial managed types are Park, Industrial, and Tourism.
- Industrial Area requires a headquarters to become Active; Park requires at least one entrance/anchor; Tourism may activate immediately.
- No new 3D modelling is required for Area Core v1.
- No Harmony dependency or global vanilla-system replacement in Area Core v1.
- Do not patch the shipped `index.js` or `index.css`.
- Construction UI must use native toolbar/asset-menu prefab data paths where practical.
- The full membership cache is derived and must not be persisted as authoritative save data.
- Cross-kind overlap is supported; same-kind overlap must not double-count exclusive statistics.
- No all-buildings × all-areas per-frame scan.
- Unknown future area type IDs must preserve state where possible and degrade to module-unavailable behavior rather than deleting data.
- Missing headquarters must suspend only the affected Industrial Area.
- UI failure must not corrupt or disable simulation state.
- Copied/adapted third-party code must be recorded in `THIRD_PARTY.md`; code without an explicit usable license may be studied but not copied.

## Review Focus

1. **Save containing an unknown `TypeId`:** load must retain `ManagedArea` identity/state and mark the module unavailable; Task 6 adds this regression test.
2. **Same-kind overlapping areas both containing one member:** exactly one stable primary owner must be selected across cache rebuilds; Task 5 tests stable-ID tie-breaking.
3. **Industrial headquarters entity is deleted or no longer valid after load:** only that area becomes Suspended, with progression preserved; Task 7 tests loss and reassignment.
4. **Native toolbar/UI registration cannot find an expected vanilla group/icon:** the mod must use a fallback group/icon or log and keep area simulation available; Tasks 3 and 8 test graceful degradation boundaries.
5. **UI bundle fails to initialize:** C# area creation, save/load, lifecycle, and membership must continue without UI-side state mutation; Task 8 adds a binding-level failure test and Task 9 includes the runtime smoke case.

---

## Planned file structure

```text
CS2-Area-System/
  src/CS2AreaSystem/
    CS2AreaSystem.csproj
    Mod.cs
    Core/
      Areas/
        AreaTypeId.cs
        AreaLifecycleState.cs
        ManagedArea.cs
        ManagedAreaProgress.cs
        ManagedAreaSettings.cs
        ManagedAreaStableId.cs
        AreaTypeDescriptor.cs
        AreaTypeRegistry.cs
        AreaStableIdAllocator.cs
        AreaLifecyclePolicy.cs
      Modules/
        IAreaModule.cs
        IAreaMemberClassifier.cs
        IAreaPanelProvider.cs
      Prefabs/
        ManagedAreaPrefabFactory.cs
        AreaToolbarPrefabFactory.cs
        AreaPrefabRegistrationSystem.cs
      Membership/
        AreaMembershipIndex.cs
        AreaMembershipPolicy.cs
        AreaMembershipSystem.cs
      Anchors/
        AreaAnchorRole.cs
        AreaAnchorLink.cs
        AreaAnchorService.cs
      Persistence/
        AreaLoadCoordinatorSystem.cs
      UI/
        AreaManagementUISystem.cs
        AreaManagementViewModel.cs
    Modules/
      Park/ParkAreaModule.cs
      Industrial/IndustrialAreaModule.cs
      Tourism/TourismAreaModule.cs
    Properties/
      PublishConfiguration.xml
  UI/
    package.json
    tsconfig.json
    webpack.config.js
    src/
      index.tsx
      bindings.ts
      AreaManagementPanel.tsx
      AreaManagementPanel.module.scss
      AreaManagementPanel.test.tsx
  tests/CS2AreaSystem.Tests/
    CS2AreaSystem.Tests.csproj
    AreaTypeRegistryTests.cs
    AreaStableIdAllocatorTests.cs
    AreaLifecyclePolicyTests.cs
    AreaMembershipPolicyTests.cs
    AreaAnchorServiceTests.cs
    SerializationContractTests.cs
  scripts/
    verify.ps1
    runtime-smoke-checklist.md
  THIRD_PARTY.md
```

---

### Task 1: Establish the official-toolchain project and verification harness

**Files:**
- Create: `src/CS2AreaSystem/CS2AreaSystem.csproj`
- Create: `src/CS2AreaSystem/Mod.cs`
- Create: `src/CS2AreaSystem/Properties/PublishConfiguration.xml`
- Create: `tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj`
- Create: `scripts/verify.ps1`
- Create: `THIRD_PARTY.md`

**Interfaces:**
- Consumes: user environment variables `CSII_TOOLPATH`, `CSII_MANAGEDPATH`, `CSII_UNITYMODPROJECTPATH`, `CSII_MODPOSTPROCESSORPATH`, `CSII_ENTITIESVERSION=1.3.10`.
- Produces: a buildable `CS2AreaSystem` mod assembly; `Mod : IMod`; a test project referencing the mod project; one verification entry point `scripts/verify.ps1`.

- [ ] **Step 1: Create the failing build/test harness**

Create the project files with the official pattern:

```xml
<CS2ToolPath Condition="'$(CSII_TOOLPATH)' != ''">$(CSII_TOOLPATH)</CS2ToolPath>
<Import Project="$(CS2ToolPath)\Mod.props" />
...
<Import Project="$(CS2ToolPath)\Mod.targets" />
```

Reference at minimum `Game`, `Colossal.Core`, `Colossal.Logging`, `Colossal.UI.Binding`, `Colossal.IO.AssetDatabase`, `Unity.Entities`, `Unity.Collections`, `Unity.Mathematics`, and `UnityEngine.CoreModule` with `Private=false`.

The test project uses NUnit and references `../../src/CS2AreaSystem/CS2AreaSystem.csproj`.

- [ ] **Step 2: Run the initial build and record the expected failure before `Mod.cs` is complete**

Run:

```powershell
dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug
```

Expected initially: FAIL because the mod entry point/metadata is not yet complete.

- [ ] **Step 3: Implement the minimal mod entry point**

Exact interface:

```csharp
public sealed class Mod : IMod
{
    public void OnLoad(UpdateSystem updateSystem);
    public void OnDispose();
}
```

`OnLoad` only registers systems introduced by later tasks; until those types exist, leave registration intentionally minimal and compilable.

- [ ] **Step 4: Add the unified verification script**

`scripts/verify.ps1` must run, in order:

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug
dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug
if (Test-Path UI/package.json) { Push-Location UI; npm test -- --run; npm run build; Pop-Location }
```

Return non-zero on the first failed command.

- [ ] **Step 5: Seed third-party provenance documentation**

`THIRD_PARTY.md` must contain headings for source repository, source file/path, license, copied/adapted portion, local destination, and modification notes. Record `KruemelGames/ParkingLotTool` and `tomkpunkt/ParkManager` as approved research/reuse candidates, but do not claim code has been copied until it actually is.

- [ ] **Step 6: Verify the scaffold**

Run:

```powershell
dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug
```

Expected: both exit 0, even if the test project contains only an initial smoke test.

- [ ] **Step 7: Commit**

```bash
git add src tests scripts THIRD_PARTY.md
git commit -m "build: scaffold Area System mod and tests"
```

---

### Task 2: Implement managed-area identity, registry, lifecycle policy, and stable IDs

**Files:**
- Create: `src/CS2AreaSystem/Core/Areas/AreaTypeId.cs`
- Create: `src/CS2AreaSystem/Core/Areas/AreaLifecycleState.cs`
- Create: `src/CS2AreaSystem/Core/Areas/ManagedArea.cs`
- Create: `src/CS2AreaSystem/Core/Areas/ManagedAreaProgress.cs`
- Create: `src/CS2AreaSystem/Core/Areas/ManagedAreaSettings.cs`
- Create: `src/CS2AreaSystem/Core/Areas/ManagedAreaStableId.cs`
- Create: `src/CS2AreaSystem/Core/Areas/AreaTypeDescriptor.cs`
- Create: `src/CS2AreaSystem/Core/Areas/AreaTypeRegistry.cs`
- Create: `src/CS2AreaSystem/Core/Areas/AreaStableIdAllocator.cs`
- Create: `src/CS2AreaSystem/Core/Areas/AreaLifecyclePolicy.cs`
- Create: `src/CS2AreaSystem/Core/Modules/IAreaModule.cs`
- Create: `src/CS2AreaSystem/Core/Modules/IAreaMemberClassifier.cs`
- Create: `src/CS2AreaSystem/Core/Modules/IAreaPanelProvider.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaTypeRegistryTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaStableIdAllocatorTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaLifecyclePolicyTests.cs`

**Interfaces:**
- Consumes: Unity `Entity` only at the module boundary; registry/policy itself remains deterministic.
- Produces:

```csharp
public enum AreaTypeId : int { Park = 1, Industrial = 2, Tourism = 3 }
public enum AreaLifecycleState : byte { Created, Inactive, Active, Suspended, ModuleUnavailable, Deleted }

public struct ManagedArea : IComponentData, ISerializable
{
    public const ushort CurrentSchemaVersion = 1;
    public ushort SchemaVersion;
    public int TypeId;
    public AreaLifecycleState State;
    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter;
    public void Deserialize<TReader>(TReader reader) where TReader : IReader;
}

public struct ManagedAreaStableId : IComponentData, ISerializable
{
    public ulong Value;
    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter;
    public void Deserialize<TReader>(TReader reader) where TReader : IReader;
}

public sealed class AreaTypeRegistry
{
    public bool Register(AreaTypeDescriptor descriptor);
    public bool TryGet(int typeId, out AreaTypeDescriptor descriptor);
    public IReadOnlyCollection<AreaTypeDescriptor> All { get; }
}

public sealed class AreaStableIdAllocator
{
    public AreaStableIdAllocator(ulong nextValue = 1);
    public ulong Allocate();
    public void ObserveExisting(ulong value);
}
```

- [ ] **Step 1: Write registry and stable-ID failing tests**

Tests must assert:

```csharp
Register(Park=1), Register(Industrial=2), Register(Tourism=3) == true
Register(duplicate TypeId=1) == false
Allocate() from a new allocator == 1 then 2
ObserveExisting(50); Allocate() == 51
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug --filter "AreaTypeRegistryTests|AreaStableIdAllocatorTests"
```

Expected: FAIL because the types do not exist.

- [ ] **Step 3: Implement the registry, IDs, serializable core components, and minimal module contracts**

`AreaTypeDescriptor` must carry stable type ID, localization keys, icon URI, and module reference. Prefab references are assigned by Task 3 after registration and must not be required to construct the descriptor for unit tests.

- [ ] **Step 4: Write lifecycle-policy failing tests**

Exact policy interface:

```csharp
public static class AreaLifecyclePolicy
{
    public static AreaLifecycleState Resolve(
        bool moduleKnown,
        bool activationRequirementsMet,
        bool wasDeleted);
}
```

Assertions:

```text
moduleKnown=false -> ModuleUnavailable
wasDeleted=true -> Deleted
known + requirements=false -> Suspended
known + requirements=true -> Active
```

Created/Inactive are initialization states set by runtime systems before validation; `Resolve` handles validated steady-state.

- [ ] **Step 5: Run lifecycle tests, implement policy, rerun all Task 2 tests**

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2AreaSystem/Core tests/CS2AreaSystem.Tests
git commit -m "feat: add managed area core contracts"
```

---

### Task 3: Register native editable area prefabs and native construction-menu entries

**Files:**
- Create: `src/CS2AreaSystem/Core/Prefabs/ManagedAreaPrefabFactory.cs`
- Create: `src/CS2AreaSystem/Core/Prefabs/AreaToolbarPrefabFactory.cs`
- Create: `src/CS2AreaSystem/Core/Prefabs/AreaPrefabRegistrationSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `THIRD_PARTY.md`
- Test: `tests/CS2AreaSystem.Tests/PrefabDescriptorTests.cs`

**Interfaces:**
- Consumes: `AreaTypeRegistry`, `PrefabSystem`, `AreaPrefab`/`LotPrefab`, `UIToolbarGroupPrefab`, `UIAssetMenuPrefab`, `UIAssetCategoryPrefab`, `UIObject`.
- Produces:

```csharp
public sealed class ManagedAreaPrefabFactory
{
    public LotPrefab CreateAreaPrefab(AreaTypeDescriptor descriptor);
}

public sealed class AreaToolbarPrefabFactory
{
    public UIAssetMenuPrefab CreateAreasMenu(UIToolbarGroupPrefab hostGroup);
    public UIAssetCategoryPrefab CreateAreaTypesCategory(UIAssetMenuPrefab menu);
    public void AttachAreaPrefab(PrefabBase areaPrefab, UIAssetCategoryPrefab category, int priority, string iconUri);
}

public partial class AreaPrefabRegistrationSystem : GameSystemBase
{
    public bool IsReady { get; }
}
```

- [ ] **Step 1: Write tests for descriptor-to-prefab metadata mapping**

Test pure mapping inputs before constructing Unity ScriptableObjects: names must be exactly `CS2AS.ParkArea`, `CS2AS.IndustrialArea`, `CS2AS.TourismArea`; icons use game runtime URIs with a fallback icon URI.

Also test the Review Focus case: an unavailable preferred icon resolves to the configured fallback without changing the area descriptor/type ID.

- [ ] **Step 2: Run tests and verify failure**

Expected: FAIL because the factories do not exist.

- [ ] **Step 3: Implement `ManagedAreaPrefabFactory`**

Create editable `LotPrefab` instances with area colors and a `UIObject`; do not add destructive/clearing geometry flags. Register them through `PrefabSystem.AddPrefab` before use.

Adapt only the minimum proven prefab-lifecycle pattern from GPL-3.0 `KruemelGames/ParkingLotTool` if needed. If any code is copied/adapted, update `THIRD_PARTY.md` in the same commit with exact source path and modifications.

- [ ] **Step 4: Implement native toolbar/menu registration**

Preferred host is the native `Tools Toolbar Group`. Create one native `UIAssetMenuPrefab` named `CS2AS Areas Menu` and one category named `CS2AS Area Types`; attach the three area prefabs using `UIObject.m_Group`, priority, and icon URI.

Fallback rule: if `Tools Toolbar Group` is unavailable, log a warning and register the area prefabs without toolbar attachment; simulation and save/load must remain functional.

- [ ] **Step 5: Gate readiness on initialized `AreaData.m_Archetype`**

`AreaPrefabRegistrationSystem.IsReady` becomes true only after all three prefab entities exist and each `AreaData.m_Archetype` is valid. No area-creation command may be enabled before this state.

- [ ] **Step 6: Register the system in `Mod.OnLoad` and build**

Run:

```powershell
dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug
```

Expected: exit 0.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Core/Prefabs src/CS2AreaSystem/Mod.cs tests THIRD_PARTY.md
git commit -m "feat: register managed area prefabs and toolbar menu"
```

---

### Task 4: Attach managed identity to native area entities and preserve type through create/edit/delete

**Files:**
- Create: `src/CS2AreaSystem/Core/Areas/ManagedAreaLifecycleSystem.cs`
- Create: `src/CS2AreaSystem/Core/Areas/ManagedAreaTypeResolver.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/ManagedAreaTypeResolverTests.cs`

**Interfaces:**
- Consumes: native `PrefabRef`, `Game.Areas.Area`, `Created`, `Updated`, `Deleted`, the three registered area prefabs, `AreaStableIdAllocator`.
- Produces:

```csharp
public sealed class ManagedAreaTypeResolver
{
    public bool TryResolve(string prefabName, out AreaTypeId typeId);
}

public partial class ManagedAreaLifecycleSystem : GameSystemBase
{
    // Adds ManagedArea + ManagedAreaStableId to newly-created matching native area entities.
}
```

- [ ] **Step 1: Write resolver tests**

Assertions map the exact three prefab names to the three IDs and reject unrelated vanilla district/extractor area prefab names.

- [ ] **Step 2: Run tests and verify failure**

Expected: FAIL.

- [ ] **Step 3: Implement resolver and lifecycle system**

On creation of an area whose `PrefabRef` resolves to a CS2AS prefab:

- add `ManagedArea { SchemaVersion=1, TypeId=..., State=Created }` if absent;
- add a new `ManagedAreaStableId` if absent;
- never replace an existing stable ID on edit/recreate;
- on native deletion, let ECS deletion proceed; do not create replacement management entities.

- [ ] **Step 4: Add a runtime-only debug assertion/log for tool activation**

When a CS2AS `LotPrefab` is selected from ToolbarUISystem, verify `ToolSystem.activeTool` resolves to the native area tool. Log an error and disable only creation for that prefab if the game changes this behavior; do not activate a custom freehand tool.

- [ ] **Step 5: Build and run unit tests**

Expected: exit 0.

- [ ] **Step 6: Commit**

```bash
git add src/CS2AreaSystem/Core/Areas src/CS2AreaSystem/Mod.cs tests
git commit -m "feat: track managed native area lifecycle"
```

---

### Task 5: Build the derived incremental membership index and overlap policy

**Files:**
- Create: `src/CS2AreaSystem/Core/Membership/AreaMembershipIndex.cs`
- Create: `src/CS2AreaSystem/Core/Membership/AreaMembershipPolicy.cs`
- Create: `src/CS2AreaSystem/Core/Membership/AreaMembershipSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaMembershipPolicyTests.cs`

**Interfaces:**
- Consumes: `Game.Areas.SearchSystem`, native area triangles/geometry, `ManagedAreaStableId`, candidate building/object positions, Created/Updated/Deleted signals.
- Produces:

```csharp
public readonly struct AreaMembership
{
    public ulong AreaStableId { get; }
    public int TypeId { get; }
    public bool IsPrimaryForType { get; }
}

public sealed class AreaMembershipIndex
{
    public IReadOnlyList<AreaMembership> GetAreas(Entity member);
    public IReadOnlyCollection<Entity> GetMembers(ulong areaStableId);
    public void ReplaceMembership(Entity member, IReadOnlyList<AreaMembership> memberships);
    public void RemoveMember(Entity member);
    public void RemoveArea(ulong areaStableId);
    public void Clear();
}

public static class AreaMembershipPolicy
{
    public static ulong ChoosePrimarySameKind(
        IReadOnlyList<ulong> containingAreaStableIds);
}
```

Primary same-kind ownership rule for v1: choose the **smallest persisted `ManagedAreaStableId`** among containing areas of that type. This is deterministic across cache rebuilds/save-load and does not depend on transient ECS entity indices.

- [ ] **Step 1: Write overlap-policy failing tests**

Assertions:

- Park ID 10 + Tourism ID 20 both contain a member -> both memberships retained and both primary for their own type.
- Park IDs 10 and 30 both contain one member -> both relations retained, only Park ID 10 is primary.
- Input order `[30,10]` and `[10,30]` returns primary `10`.
- Rebuilding the same set produces the same primary owner.

- [ ] **Step 2: Run tests and verify failure**

Expected: FAIL.

- [ ] **Step 3: Implement `AreaMembershipIndex` and pure overlap policy**

Keep bidirectional indexes internally so removal of an area or member is proportional to affected entries, not city size.

- [ ] **Step 4: Implement change-driven `AreaMembershipSystem`**

The runtime system must:

- rebuild from scratch once after load readiness;
- reconsider only spatial candidates affected by an updated area;
- reconsider only nearby managed areas for a created/moved/updated candidate member;
- remove cached entries on member/area deletion;
- query native area geometry/search rather than maintaining a second polygon representation.

Do not schedule a full city×area scan every frame.

- [ ] **Step 5: Add counters for verification, not user-facing diagnostics**

Expose internal counters `FullRebuildCount`, `IncrementalMemberUpdateCount`, `IncrementalAreaUpdateCount` so tests/runtime smoke checks can prove that ordinary updates do not invoke a full rebuild.

- [ ] **Step 6: Run tests and build**

Expected: exit 0.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Core/Membership src/CS2AreaSystem/Mod.cs tests
git commit -m "feat: add incremental area membership index"
```

---

### Task 6: Persist authoritative state and rebuild derived state safely after load

**Files:**
- Create: `src/CS2AreaSystem/Core/Persistence/AreaLoadCoordinatorSystem.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/ManagedArea.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/ManagedAreaProgress.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/ManagedAreaSettings.cs`
- Modify: `src/CS2AreaSystem/Core/Areas/ManagedAreaStableId.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/SerializationContractTests.cs`

**Interfaces:**
- Consumes: `ISerializable`, load/deserialization update phases, `AreaMembershipIndex`, `AreaStableIdAllocator`, `AreaTypeRegistry`.
- Produces:

```csharp
public partial class AreaLoadCoordinatorSystem : GameSystemBase
{
    public bool RebuildPending { get; }
}
```

- [ ] **Step 1: Write serialization contract tests**

Tests must round-trip the fields of `ManagedArea`, `ManagedAreaStableId`, `ManagedAreaProgress`, and `ManagedAreaSettings` through in-memory writer/reader test doubles.

Include the Review Focus case:

```text
ManagedArea.TypeId = 9999
-> deserializes without deletion/exception
-> registry lookup fails
-> lifecycle policy resolves ModuleUnavailable
-> stable ID and serialized state remain present
```

- [ ] **Step 2: Run tests and verify failure**

Expected: FAIL until all serialization methods exist.

- [ ] **Step 3: Implement schema-versioned serialization**

Write schema version first for each persisted component. Version 1 readers must accept exactly the v1 fields; unsupported newer component schema must log and preserve the entity when safely possible rather than silently reinterpret bytes.

- [ ] **Step 4: Implement post-load coordination**

Load order must be:

```text
Deserialize managed components
-> wait for native area/prefab/search readiness
-> observe all existing stable IDs in AreaStableIdAllocator
-> clear/rebuild AreaMembershipIndex once
-> validate modules/anchors
-> resolve lifecycle state
-> enable normal incremental updates
```

- [ ] **Step 5: Run tests and build**

Expected: exit 0.

- [ ] **Step 6: Commit**

```bash
git add src/CS2AreaSystem/Core/Persistence src/CS2AreaSystem/Core/Areas src/CS2AreaSystem/Mod.cs tests
git commit -m "feat: persist and rebuild managed area state"
```

---

### Task 7: Add minimal Park/Industrial/Tourism activation modules and headquarters/anchor assignment

**Files:**
- Create: `src/CS2AreaSystem/Core/Anchors/AreaAnchorRole.cs`
- Create: `src/CS2AreaSystem/Core/Anchors/AreaAnchorLink.cs`
- Create: `src/CS2AreaSystem/Core/Anchors/AreaAnchorService.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Create: `src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaAnchorServiceTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaLifecyclePolicyTests.cs`

**Interfaces:**
- Consumes: `IAreaModule`, `AreaMembershipIndex`, Unity `Entity`, persisted entity references through ECS serialization/remapping.
- Produces:

```csharp
public enum AreaAnchorRole : byte { Entrance = 1, Headquarters = 2 }

public struct AreaAnchorLink : IBufferElementData, ISerializable
{
    public Entity Anchor;
    public AreaAnchorRole Role;
    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter;
    public void Deserialize<TReader>(TReader reader) where TReader : IReader;
}

public sealed class AreaAnchorService
{
    public bool Assign(Entity area, Entity anchor, AreaAnchorRole role);
    public bool Remove(Entity area, Entity anchor, AreaAnchorRole role);
    public bool HasValidAnchor(Entity area, AreaAnchorRole role);
}
```

- [ ] **Step 1: Write anchor and activation failing tests**

Assertions:

- Tourism with no anchors -> Active.
- Industrial with no HQ -> Suspended.
- Assign valid HQ -> Active.
- Delete/invalidate HQ -> Suspended while `ManagedAreaProgress` remains unchanged.
- Reassign a new valid HQ -> Active.
- Park with no Entrance -> Suspended; one valid Entrance -> Active.

- [ ] **Step 2: Run tests and verify failure**

Expected: FAIL.

- [ ] **Step 3: Implement generic anchor storage/service**

For v1, an anchor is valid when it exists, is not Deleted/Destroyed, and satisfies the module's eligibility callback. Industrial permits exactly one active Headquarters link; Park may hold multiple Entrance links later, but activation only needs at least one valid link now.

- [ ] **Step 4: Implement the three minimal modules and register descriptors**

`IndustrialAreaModule` supplies the HQ requirement. `ParkAreaModule` supplies the Entrance requirement. `TourismAreaModule` has no mandatory anchor. No visitor, production, tourism-spending, rating, or progression algorithms belong in this task.

- [ ] **Step 5: Verify serialization/remapping of anchor links**

Add a test double proving entity references are written/read through the game serializer contract rather than stored as external integer IDs.

- [ ] **Step 6: Run tests and build**

Expected: exit 0.

- [ ] **Step 7: Commit**

```bash
git add src/CS2AreaSystem/Core/Anchors src/CS2AreaSystem/Modules src/CS2AreaSystem/Mod.cs tests
git commit -m "feat: add area activation anchors and module stubs"
```

---

### Task 8: Add the mod-owned vanilla-styled management panel and C# bindings

**Files:**
- Create: `src/CS2AreaSystem/Core/UI/AreaManagementViewModel.cs`
- Create: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- Create: `UI/package.json`
- Create: `UI/tsconfig.json`
- Create: `UI/webpack.config.js`
- Create: `UI/src/index.tsx`
- Create: `UI/src/bindings.ts`
- Create: `UI/src/AreaManagementPanel.tsx`
- Create: `UI/src/AreaManagementPanel.module.scss`
- Create: `UI/src/AreaManagementPanel.test.tsx`
- Modify: `src/CS2AreaSystem/CS2AreaSystem.csproj`
- Modify: `src/CS2AreaSystem/Mod.cs`

**Interfaces:**
- Consumes: `SelectedInfoUISystem.selectedEntity`, `ManagedArea`, registry/module panel metadata, `AreaAnchorService`, `AreaPrefabRegistrationSystem`.
- Produces C# bindings under group `cs2AreaSystem`:

```text
selectedArea      : AreaManagementViewModel | null
panelVisible      : bool
```

Triggers:

```text
editArea()
locateArea()
renameArea(string name)
deleteArea()
assignHeadquarters()
```

UI entry point:

```ts
const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("Game", AreaManagementPanelHost);
};
```

Do not patch shipped `index.js`, `index.css`, or private `SectionType` mappings.

- [ ] **Step 1: Write UI failing tests**

Vitest/React tests must assert:

- `selectedArea=null` -> panel not rendered.
- selected managed Industrial Area -> header shows name/type/status and Headquarters action.
- Park/Tourism module tab metadata produces only that module's configured tabs.
- unavailable UI binding/error boundary -> panel hides or shows a local error state without emitting simulation mutations.

- [ ] **Step 2: Run UI tests and verify failure**

```powershell
cd UI
npm test -- --run
```

Expected: FAIL until components exist.

- [ ] **Step 3: Implement `AreaManagementUISystem`**

Read `SelectedInfoUISystem.selectedEntity`. Publish a view model only when that entity has `ManagedArea`; otherwise publish null/hidden. Actions delegate to gameplay services and must validate that the selected entity still matches before mutation.

`AreaManagementViewModel` includes stable ID, name, type localization key, lifecycle state, optional level/progress, module tabs, and available actions.

- [ ] **Step 4: Implement the mod-owned panel host**

Use `moduleRegistry.append("Game", AreaManagementPanelHost)` so the panel is technically isolated from the shipped Selected Info section switch. Style it using the supplied 1.6.2f1 UI bundle as reference for panel background, spacing, typography, dividers, tabs, progress geometry, and button states.

Do not copy shipped CSS wholesale; reproduce only the required local styles or use exposed CS2 UI components/tokens.

- [ ] **Step 5: Wire UI build into the mod build/deploy path**

The C# project should invoke `npm run build` only when UI dependencies are present, and include the built UI output in the WIP deployment. A missing Node dependency must produce a clear build message rather than silently deploying stale UI.

- [ ] **Step 6: Run UI tests, C# tests, and full build**

```powershell
cd UI; npm test -- --run; npm run build; cd ..
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug
dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -c Debug
```

Expected: all exit 0.

- [ ] **Step 7: Commit**

```bash
git add UI src/CS2AreaSystem/Core/UI src/CS2AreaSystem/CS2AreaSystem.csproj src/CS2AreaSystem/Mod.cs
git commit -m "feat: add managed area management panel"
```

---

### Task 9: Add runtime smoke checks, compatibility degradation checks, and final Area Core verification

**Files:**
- Create: `scripts/runtime-smoke-checklist.md`
- Modify: `scripts/verify.ps1`
- Modify: `README.md` if it exists by this point; otherwise create it with build/test/run instructions only.
- Modify: `THIRD_PARTY.md` if any production code was copied/adapted during Tasks 3–8.

**Interfaces:**
- Consumes: all prior tasks.
- Produces: reproducible automated verification plus an explicit in-game checklist for behavior that cannot be proven outside Cities: Skylines II.

- [ ] **Step 1: Add the in-game smoke checklist**

The checklist must require a fresh 1.6.2f1 game session and cover exactly these cases:

```text
1. Areas menu appears in the bottom construction UI without editing shipped UI files.
2. Park, Industrial, Tourism selections activate native AreaToolSystem.
3. Create, vertex-edit, complete, re-edit, and delete each managed area type.
4. Build a Park inside a Tourism Area and verify both memberships exist.
5. Create overlapping Park Areas and verify exclusive membership primary owner is stable.
6. Create Industrial Area: no HQ => Suspended; assign vanilla building => Active; delete HQ => Suspended; reassign => Active.
7. Save and reload; stable IDs/type/lifecycle remain and membership rebuild count increments exactly once for load.
8. Create/move/delete one candidate building and verify incremental counters change without FullRebuildCount increasing.
9. Temporarily break/disable the UI bundle and verify the save still loads and area simulation/state remains intact.
10. Confirm normal roads, paths, vegetation, and vanilla assets remain in their original menus rather than being duplicated.
```

- [ ] **Step 2: Add automated source/policy guards to `verify.ps1`**

Fail verification if production source contains:

```text
0Harmony
HarmonyLib
patch of shipped index.js/index.css
```

Also ensure `THIRD_PARTY.md` exists whenever a source file contains an explicit attribution marker such as `Adapted from`.

- [ ] **Step 3: Run the complete automated verification**

Run:

```powershell
./scripts/verify.ps1
```

Expected: exit 0; NUnit tests pass, mod builds, UI tests pass, UI bundle builds.

- [ ] **Step 4: Perform the in-game smoke checklist**

Record game version, test save name, and pass/fail notes directly in a temporary local test log; do not commit machine-specific paths or save files.

Expected: all ten cases pass before claiming Area Core implementation complete.

- [ ] **Step 5: Review third-party provenance**

For each copied/adapted source file, verify `THIRD_PARTY.md` records repository, exact source path, license, local destination, and modifications. If only concepts/API behavior were studied, state that no code was copied.

- [ ] **Step 6: Commit verification/documentation changes**

```bash
git add scripts README.md THIRD_PARTY.md
git commit -m "test: add Area Core runtime verification"
```

---

## Final self-review checklist for the implementer

Before implementation is considered complete, verify against the approved spec:

- Native AreaToolSystem handles creation/editing; no custom freehand polygon tool exists.
- All three area types are registered and reachable from the native construction UI.
- `ManagedArea` type/lifecycle and stable IDs survive save/load.
- Membership is derived and incremental; full rebuild happens only at controlled load/recovery points.
- Park-in-Tourism overlap is supported.
- Same-kind overlap selects the smallest persisted area stable ID as the exclusive primary owner.
- Industrial headquarters loss suspends only that area and reassignment reactivates it.
- Park activation recognizes at least one entrance anchor; Tourism activates without an anchor.
- No new 3D model is required.
- UI is mod-owned and vanilla-styled, and does not patch shipped `index.js` / `index.css`.
- UI failure cannot mutate or destroy simulation state.
- No Harmony dependency was introduced.
- Third-party copied/adapted code provenance is complete.
- Full Park visitor simulation, industrial production/progression algorithms, and tourism scoring remain out of scope and are not accidentally implemented in Area Core.
