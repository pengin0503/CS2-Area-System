# Park context map

This is navigation guidance and does not override current specs/code.

Scope: Park module observations, management controls and module panel. Read feature requirements from current specs.

## Authority and implementation

- v1: `docs/superpowers/specs/2026-09-30-park-area-v1-design.md`.
- Later expansion: `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md`.
- Headquarters and subsequent phases: `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md`.
- Module: `src/CS2AreaSystem/Modules/Park/`; select the relevant subsystem.
- Payload boundary: `src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs`.

## Focused checks

- `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- `tests/CS2AreaSystem.Tests/ParkAreaFinanceIntegrationTests.cs`
- `UI/src/ParkPanel.test.tsx`

```bash
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj --filter 'FullyQualifiedName~ParkPanelBuilderTests|FullyQualifiedName~ParkAreaFinanceIntegrationTests'
cd UI && npm test -- --run src/ParkPanel.test.tsx
```

## Cross-domain and on-demand

Load `docs/ai-context/core.md` for shared membership, finance or Headquarters contracts; `docs/ai-context/ui.md` for panel rendering/bindings.
Open only the directly relevant sections of the later specs. Archived plans, `docs/verification/` and `UI/src/__fixtures__/` are on-demand for regressions or payload work.
