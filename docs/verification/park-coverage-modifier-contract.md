# Park coverage modifier — 1.6.2f1 native contract (Phase 2 C6, Task 1)

Status: **API contract proven; runtime release gate PASS — user-confirmed 2026-10-04.** The user reports the C601–C606 in-game checks completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat.

Sources: the supplied `CS2-All-Managed-1.6.2f1-fix` zip. `Game.dll` was decompiled whole-project with ilspycmd 9.1. Single types were decompiled from `Unity.Entities.dll` (Entities 1.3.10) and `Colossal.Core.dll`. Line numbers refer to that decompile.

## 1. Component

`Game.Buildings.ModifiedServiceCoverage : IComponentData, IQueryTypeParameter, ISerializable`

- **Fields:** `float m_Range`, `float m_Capacity`, `float m_Magnitude`.
- **Constructor:** `ModifiedServiceCoverage(CoverageData)` copies those three fields from the prefab `CoverageData`.
- **`ReplaceData(ref CoverageData)`:** overwrites the three fields of a prefab coverage copy.
- **Serialization:** `Serialize`/`Deserialize` write and read capacity, range and magnitude, in that order.

The component is added only by the `Game.Prefabs.Park` prefab archetype (`Park.GetArchetypeComponents`), and only to buildings that are not service upgrades. It sits next to `Game.Buildings.Park`, `Renter`, `MaintenanceConsumer`, `UpdateFrame` and `CurrentDistrict`.

## 2. Every writer

These are all `Game.dll` references to the type. `CoveragePreviewSystem` and `ServiceCoverageSystem` take read-only lookups.

| Writer | Phase / cadence | What it writes |
| --- | --- | --- |
| `Game.Simulation.ParkAISystem` (`ParkTickJob`, `IJobChunk.ScheduleParallel`, RW type handle) | `GameSimulation`, interval `262144 / kUpdatesPerDay` = 1024 frames (`kUpdatesPerDay = 256`), offset assigned by `UpdateSystem` | For every entity in `Park + ModifiedServiceCoverage + Renter`, excluding `Temp`, `Destroyed` and `Deleted`, whose prefab has `ParkData` and `CoverageData`, it writes `GetModifiedServiceCoverage(park, parkData, coverageData, cityModifiers)` |
| `Game.Buildings.ParkInitializeSystem` (`InitializeParksJob`) | `Modification5`, entities with `Created` | The same function, with maintenance reset to the pool |
| Save/load (`ComponentSerializerLibrary`) | `Deserialize` | The saved value, because the type is `ISerializable` |
| Structural paths (archetype rebuilds in `InitializeSystem`/`ReplacePrefabSystem`/`GenerateObjectsSystem`, Temp → original application) | Modification phases | Copied or default values; never a value derived by the mod |

`GetModifiedServiceCoverage` always starts from `new ModifiedServiceCoverage(prefabCoverageData)`. It then:

- scales `m_Magnitude` and `m_Range` by the maintenance tier;
- applies the city `ParkEntertainment` modifier to `m_Magnitude`;
- leaves `m_Capacity` at the prefab value.

**The native value is therefore never derived from the previous component value.** Every native write is a fresh baseline from prefab data. This is the property that makes a mod factor non-compounding across native passes. Any value left behind by a mod is overwritten by the next `ParkAISystem` pass, at most 1024 frames later.

## 3. Consumers and ordering

The consumers are:

- **`Game.Simulation.ServiceCoverageSystem`** (`GameSimulation`, every frame). Its building job reads `ModifiedServiceCoverage` through a read-only `ComponentLookup` and calls `ReplaceData` on the prefab `CoverageData`. `m_Magnitude` scales each edge's coverage (`(1 − cost²) × m_Magnitude × efficiency`). `m_Capacity` becomes the building's total and remaining capacity.
- **`Game.Tools.CoveragePreviewSystem`** (`ModificationEnd`). It reads the same component for placement previews; a `Temp` entity uses `m_Original`.

`m_Range` is copied into the coverage data. The spec still requires the mod to leave it alone.

Vanilla order (`SystemOrder.Initialize`):

- `ServiceCoverageSystem` is registered with `UpdateAt(GameSimulation)` before `ParkAISystem`'s `UpdateAt(GameSimulation)`.
- `UpdateSystem.Refresh` orders a phase by registration index.
- So in any simulation frame, the coverage consumer runs **before** the native writer.

`UpdateSystem` mechanics (`Game.UpdateSystem`):

- `Update(phase, updateIndex, iteration)` runs a system only when `(updateIndex & (interval − 1)) == offset`.
- `SimulationSystem` passes the simulation `frameIndex` as `updateIndex`.
- `UpdateAfter<Mod, ParkAISystem>(GameSimulation)` places the mod system directly after `ParkAISystem` in the phase list (`AddSystemUpdate`).
- When the mod system returns the **same interval** with offset −1, it **inherits ParkAISystem's offset** (`flag = interval equal && offset < 0`). It then runs in exactly the same update index, immediately after it.

The mod pass can therefore run only after `ParkAISystem` in the same frame. The next consumer read is in a later frame (`ServiceCoverageSystem`) or in a later phase (`CoveragePreviewSystem`, `ModificationEnd`). Another mod that also registers `UpdateAfter<X, ParkAISystem>` could be ordered between them; that is an external conflict and is covered by the freshness check below.

Job dependencies (`Unity.Entities` 1.3.10):

- `EntityManager.GetComponentData<T>` calls `DependencyManager.CompleteWriteDependency(T)`, which completes the `ParkTickJob` that `ParkAISystem` registered as a writer.
- `EntityManager.SetComponentData<T>` calls `CompleteReadAndWriteDependency(T)`, which also completes outstanding coverage reader jobs before the mod writes.
- `EntityManager.CompleteDependencyBeforeRO<T>()` is the same write-completion, without an entity.

## 4. Fresh-baseline evidence

Value equality is not used as freshness evidence. The signal is the chunk change version, checked against the native writer's system version:

1. `SystemBase.Update` runs `BeforeUpdateVersioning` → `OnUpdate` → `AfterUpdateVersioning`.
   - `BeforeUpdateVersioning` increments `GlobalSystemVersion`, giving version V.
   - `AfterUpdateVersioning` stores `m_LastSystemVersion = GlobalSystemVersion`, which is still V, and then increments it again.
   - `ParkAISystem.OnUpdate` only fetches handles, schedules one job and registers it with `EndFrameBarrier`; nothing in it increments the version.
   - So `parkAI.LastSystemVersion == V`.
2. `ParkAISystem` fetches its RW `ComponentTypeHandle<ModifiedServiceCoverage>` through `InternalCompilerInterface.GetComponentTypeHandle(ref handle, ref CheckedStateRef)`. This calls `handle.Update(ref state)` and stamps `GlobalSystemVersion = V`.
3. `ArchetypeChunk.GetNativeArray(ref RW handle)` calls `GetOptionalComponentDataWithTypeRW(..., typeHandle.GlobalSystemVersion, ...)`. This sets the chunk's change version for that component to V for every chunk the job processes.
4. `ArchetypeChunk.GetChangeVersion(ref handle)` and `EntityManager.GetChunk(entity)` are public.

So, after `CompleteDependencyBeforeRO<ModifiedServiceCoverage>()`, an entity is a **fresh native baseline** only if all of these hold:

- `GetChunk(e).GetChangeVersion(ref handle) == parkAI.LastSystemVersion`;
- that version differs from the one the mod recorded on its previous pass;
- the entity's prefab has `ParkData` and `CoverageData`, so the job wrote it rather than only touching its chunk.

Only `ParkAISystem`'s update uses version V, and any later write or structural move would stamp a higher version. Equality therefore proves both that `ParkAISystem` wrote this value and that nothing has written it since.

Every other case is treated as **not fresh**, and the mod does not apply its factor. These include:

- `OnStopRunning`, which advances `LastSystemVersion` without a job;
- a structural move;
- a `ParkInitializeSystem` write;
- a load;
- another mod's write.

A missed signal leaves vanilla behaviour in place. It never compounds.

## 5. Save restore

`Game.Serialization.SerializerSystem` writes entities in the `Serialize` phase (`EntitySerializer.Serialize` over its query). `ComponentSerializerLibrary.Initialize` includes every `ISerializable` component, so **a modified value would be saved** unless it is restored first.

A mod system registered with `UpdateBefore<ModSave, SerializerSystem>(SystemUpdatePhase.Serialize)` runs before that write; the existing `AreaFinanceSaveSystem` uses the same hook. It restores every tracked entity whose current value still equals the mod's last write back to the stored native baseline.

A value that differs has been rewritten natively or externally, and is left alone. After the save, the factor is reapplied only on the next fresh native pass (§4), never from the cached pre-save baseline.

On load, the saved values are native, and the runtime tracker starts empty. Even if a modified value was saved (for example after a crash mid-restore), it is overwritten by the next `ParkAISystem` pass (§2). The mod treats it as not fresh (§4), so it cannot be compounded.

## 5a. Restore timing

The writer runs only in `ParkAISystem`'s update slot (every 1024 frames). These cases therefore end at the next native pass, at most 1024 frames later, not at once:

- the owning Park is suspended or deleted;
- the building leaves the Park;
- the budget changes.

At that pass the native writer has already replaced the value with a fresh baseline, so the mod only forgets the entry. A tracked building that gets no fresh pass (destroyed, or its prefab lost its data) is restored to its stored baseline while the value is still the mod's write.

Immediate restores happen on these paths:

- save (`ParkCoverageModifierSaveSystem`);
- writer failure (the `catch` in `OnUpdate`);
- `Shutdown()`.

Setting `Enabled = false` from outside restores through `OnStopRunning` at the writer's next update slot. This meets the plan's "same or next valid writer pass" rule. After every save, including autosave, buildings keep vanilla coverage until the next native pass.

## 6. Conclusion and remaining gate

All five properties are proven from the supplied sources:

| Property | Evidence |
| --- | --- |
| Exact fields | §1 |
| Every native writer | §2 |
| Consumer ordering and job completion | §3 |
| Fresh-baseline signal | §4 |
| Non-accumulating native rewrite | §2 and §4 |
| Serialization path and restore hook | §5 |

C6 is therefore **not BLOCKED** at the API level.

The runtime release gate has passed. `ParkCoverageModifierGate.RuntimeVerified` is `true` on the readiness branch. The production UI exposes **Options → CS2 Area System → Enable Park coverage modifier**; turning it off restores tracked native values and suppresses subsequent modifier writes while leaving Park/Core running.

### Release gate

The in-game checks in `scripts/runtime-smoke-checklist.md` (C6 section) were reported PASS on 2026-10-04:

- enable;
- change the budget;
- disable;
- save and load;
- delete;
- ownership overlap.

All six cases are **PASS (user-confirmed 2026-10-04)**. The C# contract tests still provide separate shape/order evidence and do not themselves execute ECS jobs.
