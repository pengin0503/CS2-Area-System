# Area Core requirements audit — 2026-09-30

Final automated result after the single review fix pass: source guard tests and official/API build-target evaluation contracts passed; NUnit **56/56**; UI **7/7**; fresh UI bundle and net8/net48 API compilations passed (C# 0 warnings/errors). `scripts/verify.ps1 -ApiCheck` passed. These are not game runtime results.

| Requirement | Implementation / automatic evidence | Game check |
|---|---|---|
| Native creation/edit/delete; no polygon tool | SpacePrefab/AreaToolSystem, native ActionsSection edit API; native gameplay gate regression and prefab/type/readiness tests | 1–3 pending |
| Three native menu assets, no duplicate vanilla assets | native menu/category/groups, native locale names; descriptor/menu tests | 1,2,10 pending |
| Native geometry/search authoritative | native area/object quadtrees, AreaUtils.GetTriangle2; point/edge/outside tests | 3–5,8 pending |
| Derived incremental membership, bidirectional cleanup | updater/index tests prohibit city enumeration during ordinary changes; counters; cache has no serializer | 4,5,7,8 pending |
| Cross-kind overlap, one primary same-kind owner | Public native validators recheck only updated owned previews; allowed managed-pair overlap becomes Warning; invalid shapes/city limits/vanilla conflicts/module veto stay blocking. Ownership tests choose smallest stable ID independent of input order | 4,5 pending, including ECB/icon ordering |
| Save authoritative fields, Entity remapping | actual ISerializable/IReader/IWriter contract tests; schema first; anchor Entity overload/remapping test | 7 pending |
| Unknown TypeId retained | TypeId=9999 serialization + ModuleUnavailable policy regression | 7 pending |
| Recovery order / one rebuild | readiness-gated observe→rebuild→activation sequence tests; native search job completion/entry checks | 7 pending |
| Industrial HQ loss scoped, progress preserved | anchor loss/reassignment tests, reverse anchor invalidation; progress is never modified by activation | 6,7 pending |
| Park entrance / Tourism immediate activation | module anchor tests; eligible existing vanilla Building, no new models | 3,6 pending |
| Modules isolated, classifier exception localized | module contract + classifier error tests; runtime activation exception handler | 6,9 pending |
| Mod-owned vanilla-styled panel / no private patch | append Game host; binding failure + tab/action tests; source guards | 9 pending |
| Stale UI actions rejected | persisted ID string + Entity/version/selection guard tests | 3,6 pending |
| UI build/deploy current output | fresh webpack .js/.css build; missing dependencies refuse deployment | runtime deployment pending |
| No Harmony / shipped files untouched | source guard negative fixtures and real source checks | 1,9 pending |
| Third-party provenance | GPL area-color and UI packaging/type adaptations recorded in THIRD_PARTY.md; license text included | no game prerequisite |
| No invented visitor/production/tourism algorithms | module stubs expose activation and Overview only | source review |

Official postprocess was attempted and refused with `Modding toolchain is incomplete`. The supplied processor PathSet requires Windows User Unity-version variables, Windows registry Unity installation, Unity ILPP runner, complete Unity Mod Project packages and codegen assemblies. Those runtime resources are not supplied by the DLL/toolchain-reference ZIPs. No synthetic runner, game assembly or bypass was used. A Windows official-toolchain build and all ten in-game checks remain mandatory before calling this operational.

The fresh-context review covered `bd732ae..5217c74`. Its two Critical findings were the gameplay Lot creation gate and an accidental override of the official BuildGetFullPaths target. Both were reproduced RED and addressed in one fix pass, followed by the complete GREEN suite and net48 API compile. The API-only target now has a separate name. The verified native SpaceData path replaces editor-only LotData; no global tool/error bypass or Harmony was added. The reviewer's two Minor findings are recorded as deferred in the execution record.

The approved spec/plan are unchanged. Main is unchanged; all implementation commits are on feat/area-core. No push, merge or remote branch writes. The permanent ledger and review disposition are in [area-core-execution-record.md](area-core-execution-record.md).
