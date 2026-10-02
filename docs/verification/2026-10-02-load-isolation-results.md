# Load isolation verification — 2026-10-02

Scope: #53, #50, #55, #54 only. Started from GitHub main `8690db37f48daa7eea7e8aec261ee5d9acda0338`; every source blob and the commit/tree hashes were verified against GitHub. No ExtraLib compatibility or changes to runtime/performance issues outside this scope.

## Changes and native API evidence

- **#53:** Register ParkRuntimeSystem in the Park ModuleRegistration unit, once in ModificationEnd before ParkLandscape. Store.Clear and LoadGeneration increment remain owned only by ParkRuntime preload. Supplied 1.6.2f1 GameSystemBase.OnCreate directly subscribes to GameManager.onGamePreload even without UpdateSystem registration; the fix explicitly pins registration/ownership rather than claiming the native callback was absent.
- **#50:** Preload completes admission transfers before deserialization, records already-transferred amounts into old-city lifetime counters, then clears in-flight/queued state and disposes buffers in finally. Runtime recent statistics/shared accounts are not repopulated during preload: subscriber order may already have reset them. No transfer is replayed. Pending jobs also complete before an unready update return. Normal FlushForSave and per-record/listener isolation are retained.
- **#55:** Preload drains read-only observations, drops results/sessions/caches/events, and disposes matches and target maps. An unready update drains and drops pending results. Native buffers are recreated lazily; restored visitor seeding occurs once when adopting the ready generation.
- **#54:** Native SearchSystem runs in Modification5 after GeometrySystem in Modification2B; recovery remains in ModificationEnd. Public LastSystemVersion and completed GetSearchTree writer dependencies provide native-pass evidence. Per-Area content fingerprints distinguish actual geometry changes from unrelated writes to the same chunk. Repeated polling of one native version never expires a timer. Missing/invalid geometry across distinct completed native passes is quarantined per entity, diagnosed once and excluded from consumers. Stable IDs (including quarantined Areas) are reserved before saved-state validation, membership rebuild and activation. Saved components/geometry are never repaired or deleted.
- GameManager.LoadSimulationData invokes preload before CleanupMemory and LoadGameSystem.RunOnce. Only the relevant lifecycle/search/geometry/job-handle APIs were inspected.

## Validation

The environment uses the supplied official Mod.props/Mod.targets and 1.6.2f1 Managed references. `AreaApiCheck=true` output is **not a playable MOD**.

| Command | Result |
|---|---|
| `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -p:AreaApiCheck=true` | API compilation succeeds; VSTest host fails with NullReferenceException at TestEngine.ShouldRunInProcess. Some launches also fail at Process.GetStat due to the environment's PID/proc namespace mismatch. Not a passing test command. |
| `dotnet "$DOTNET_ROOT/sdk/8.0.425/MSBuild.dll" tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -t:Build -p:AreaApiCheck=true -verbosity:minimal` | PASS: real projects compile against supplied DLLs. Existing FinanceRobustnessTests nullable-context warning remains unchanged. |
| `dotnet tests/CS2AreaSystem.Tests/bin/Debug/net8.0/runner.dll --noresult` | PASS: **862/862** NUnit tests (baseline 842). Existing NUnitLite runner used to execute the same test assembly because VSTest cannot start. No tests removed or weakened. |
| `dotnet build src/CS2AreaSystem/CS2AreaSystem.csproj -p:AreaApiCheck=true -c Release` | PASS: 0 warnings, 0 errors. API-only compilation. |
| `cd UI; npm test -- --run` | PASS: **99/99**, 9 test files. |
| `cd UI; npm run build` | PASS: production webpack build. |
| `node scripts/test-offline-contracts.mjs` | PASS. Recovery order guard now requires allReady with the original ordered callbacks. |
| `pwsh -NoProfile -File scripts/verify-source.ps1` | PASS. |
| `pwsh -NoProfile -File scripts/test-source-guards.ps1` | PASS. |
| `pwsh -NoProfile -File scripts/test-msbuild-json.ps1` | PASS. |
| `pwsh -NoProfile -File scripts/test-build-contract.ps1` | Blocked by dotnet Process.GetStat failure. Equivalent BuildGetFullPaths and BuildAreaApiCheckPaths targets executed directly with MSBuild DLL and their real JSON output checked with the repository parser: native NeedBuild=true/nonempty DeployDir; API NeedBuild=false/empty DeployDir. PASS for direct target evaluation. |
| `pwsh -NoProfile -File scripts/verify.ps1 -ApiCheck` | Attempted twice; source guards/parser stages pass, then blocked by the preceding build-contract launch failure. Not claimed as a full PASS. |
| `git diff --check` | PASS. |

Official playable Release/postprocessing/deployment was not run: this Linux workspace has no Windows Unity mod project or executable official postprocessor. This is an environment limitation, not an identified code compilation error. No game-internal PASS is claimed.

Independent read-only whole-branch review found no scoped critical/important/minor issues. Existing unrelated finance/visitor behavior was reviewed only as context and left unchanged.

## Added coverage and remaining game checks

Twenty new tests cover compiled Park registration/native subscription, repeated ParkRuntime preload boundaries, drain-before-readiness/preload ordering, managed pending-state cleanup, load-only financial recording and record isolation, visitor reset/restored seed, completed-search/geometry evidence, same-chunk writes, native version wrap, mixed healthy/missing Areas, diagnostic deduplication, quarantine identity reservation and recovery ordering. Native allocated-container release and real scheduled jobs cannot execute in this standalone .NET runner.

Keep all four issues open until CS2 1.6.2f1 checks complete:

1. **#53:** Save A → save B → new city: one generation increment and Store clear per boundary; all Park consumers reset once; module registration failure still isolates Park only.
2. **#50:** Load/change city immediately after an admission job is scheduled: old household/city transfer and lifetime revenue agree, no double credit, no old result in the new city, no persistent allocation/job safety error. Normal save with an existing batch and queued admissions still records once.
3. **#55:** Load/change city immediately after an observation job: no old visits/active counts/satisfaction or entity lookups survive; no disposed-container/job safety errors; restored visitors receive one fee-satisfied seed and later new visitors are charged normally.
4. **#54:** Load two Areas with one persistently excluded/malformed native polygon: only that Area is quarantined/diagnosed, healthy membership/activation/finance/policy/UI recover, no log spam. Normal load and slowly changing geometry wait for native preparation and recover once.
