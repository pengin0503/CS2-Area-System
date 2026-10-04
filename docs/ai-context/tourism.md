# Tourism context map

This is navigation guidance and does not override current specs/code.

Scope: Tourism module registration and shared boundaries. Current Tourism gameplay is boundary-only unless a newer approved spec changes it. Do not infer Tourism gameplay requirements from Park/Industrial plans.

## Authority and implementation

- Shared module authority: `docs/superpowers/specs/2026-09-30-area-system-core-design.md`.
- Implementation: `src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs`.
- No standalone approved Tourism gameplay spec is identified here; resolve a newer approved spec only when a task supplies one.

## Focused checks

- `tests/CS2AreaSystem.Tests/TourismAreaModuleTests.cs`
- `tests/CS2AreaSystem.Tests/TourismBoundaryContractTests.cs`

```bash
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj --filter 'FullyQualifiedName~TourismAreaModuleTests|FullyQualifiedName~TourismBoundaryContractTests'
node scripts/test-offline-contracts.mjs
```

## Cross-domain and on-demand

Shared module contracts: `src/CS2AreaSystem/Core/Modules/`; load `docs/ai-context/core.md` if those contracts change.
Load `docs/ai-context/ui.md` only for shared panel/binding effects. Future proposals, archived plans and verification history are on-demand rather than evidence of implemented Tourism gameplay.
