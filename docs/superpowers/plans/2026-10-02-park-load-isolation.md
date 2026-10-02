# Park load isolation implementation plan

> Execute inline with superpowers:executing-plans, in issue order, with RED → GREEN tests and separate commits.

**Goal:** Fix only #53, #50, #55 and #54 against main `8690db37f48daa7eea7e8aec261ee5d9acda0338`, obtained and blob-verified at work start.

**Architecture:** Keep ParkRuntime as the single generation owner. Drain jobs before deserialization, record already transferred admissions only into old authoritative counters, then release runtime state. Recover healthy Areas after completed native search passes; quarantine persistent unavailable geometry per entity without modifying saved components.

**Tech stack:** C# 9, Unity ECS/public CS2 1.6.2f1 APIs, NUnit, React/Vitest.

**Spec:** User request in this session and GitHub issues #53/#50/#55/#54. Existing approved specs remain unchanged.

## Constraints

- No Harmony, private patches or production private reflection.
- No saved-state deletion, ID reassignment, geometry repair, ExtraLib support or unrelated changes.
- Keep save flushing, per-record isolation and listener registration guarantees.
- Game execution is unavailable; runtime issues remain open.

## Evidence and review focus

- Supplied GameSystemBase.OnCreate subscribes directly to GameManager.onGamePreload, independently of UpdateSystem. Explicit ParkRuntime registration still fixes the ownership/registration contract.
- GameManager.LoadSimulationData invokes preload before CleanupMemory and LoadGameSystem.RunOnce. Old authoritative counters can be recorded there; ephemeral shared finance accounts may already have reset because subscriber order follows creation.
- Native SearchSystem processes Area + Node + Triangle, and records even zero triangle counts. Public LastSystemVersion proves a native update ran; GetSearchTree's writer dependency must be completed before inspecting counts.
- Check callback order independence, no duplicate credits, result disposal before a readiness return, native version wrap/change, and geometry changing between native passes.

### Task 1: #53 registration

Files: `Mod.cs`, `ModSystemOrderTests.cs`, new `ParkLoadLifecycleTests.cs`.

- [ ] Add failing compiled-registration test requiring one ParkRuntime registration in ModificationEnd before ParkLandscape; update the full order expectation only for this addition.
- [ ] Characterize supplied native preload subscription and test ParkRuntime Store clearing/generation advancing on repeated load boundaries.
- [ ] Run targeted tests and observe the missing registration failure.
- [ ] Add ParkRuntime to ModuleRegistration and register it before landscape.
- [ ] Run targeted tests; commit `fix: register Park load-generation owner (#53)`.

### Task 2: #50 admission load boundary

Files: `ParkFinanceSystem.cs`, `ParkAdmissionRecording.cs`, `ParkLoadLifecycleTests.cs`, new `ParkAdmissionLoadTests.cs`.

- [ ] Add failing preload/order/buffer-release tests and load-mode admission tests: transferred money recorded once in lifetime revenue, no ephemeral accounts repopulated, one record failure does not skip later outcomes.
- [ ] Run targeted tests, observe missing preload/load-mode behavior.
- [ ] Add `ParkFinanceSystem.OnGamePreload`: complete the job, call `RecordResults(bool loading)` for old authoritative counters, then dispose/reset local state in finally. Keep generation baseline uninitialized until first ready update.
- [ ] Extend existing `ParkAdmissionRecording.RecordAll` with optional `loadBoundary`; normal recording remains unchanged. Load mode records lifetime only, because recent/account state is being reset.
- [ ] Complete pending work before readiness checks; release unready/old-generation state without applying a transfer again. Do not change FlushForSave.
- [ ] Run new tests plus existing finance/save/listener tests; commit `fix: drain Park admission transfers before load (#50)`.

### Task 3: #55 visitor load boundary

Files: `ParkVisitSystem.cs`, `ParkLoadLifecycleTests.cs`.

- [ ] Add failing preload/drain-order/reset tests. Retain restored visitor seed tests and listener tests.
- [ ] Drain observation jobs before a readiness return. Preload disposes matches and targets, clears sessions/caches/events and leaves generation baseline unset.
- [ ] Recreate native containers lazily on ready updates; initialize restored visitor seed only when adopting the ready generation.
- [ ] Run targeted tests and existing visit tests; commit `fix: isolate Park observation jobs across loads (#55)`.

### Task 4: #54 per-Area native readiness

Files: new `AreaNativeReadiness.cs`, `AreaLoadCoordinatorSystem.cs`, `AreaIdentityLifecycle.cs`, `NativeAreaLocalization.cs`, new `AreaNativeReadinessTests.cs`.

- [ ] Add failing readiness tests using literal evidence: one ready Area/one missing, repeated same native version, changing geometry, persistent missing across distinct completed search passes, reset, normal ready recovery once and per-entity diagnostic deduplication.
- [ ] Track missing Areas by entity and public Node/Triangle buffer change versions. Reset evidence at preload/loading complete.
- [ ] Require a native search update after loading complete and completion of its returned writer dependency. Do not count frames or repeated observations of the same native pass.
- [ ] Wait on first absence or changed geometry; after another completed native update with identical geometry evidence, quarantine only the missing Area as NativeGeometryUnavailable. Valid areas proceed; malformed/missing/empty geometry gets a specific diagnostic.
- [ ] Keep stable ID observation → saved-state validation → membership rebuild → module validation. Quarantine is session-only and all IDs, including quarantined ones, are reserved.
- [ ] Extend existing generic quarantine UI wording to include native geometry failure.
- [ ] Run all readiness/quarantine/order tests; commit `fix: quarantine Areas excluded from native search recovery (#54)`.

### Task 5: validation and handoff

- [ ] Run API compilation, all NUnit tests, UI tests/build, offline contracts, source guards and verification script. Record exact environmental failures and alternate runners; never claim a blocked command passed.
- [ ] Review the whole diff against the four issues; correct scoped important findings with RED → GREEN.
- [ ] Publish commits to the work branch and create a reviewable PR; do not merge without further instruction.
- [ ] Comment on each issue with commit, tests and required game scenarios; keep open.
