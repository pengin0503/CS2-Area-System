# Industrial context map

This is navigation guidance and does not override current specs/code.

Scope: Industrial observations, scoring, operational controls and module panel. Read requirements from the applicable current spec.

## Authority and implementation

- v1: `docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md`.
- Later expansion: `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md`.
- Headquarters and subsequent phases: `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md`.
- Module: `src/CS2AreaSystem/Modules/Industrial/`; select only the affected subsystem.

## Focused checks

- `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- `tests/CS2AreaSystem.Tests/IndustrialApiContractTests.cs`
- `UI/src/IndustrialPanel.test.tsx`

```bash
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj --filter 'FullyQualifiedName~IndustrialPanelBuilderTests|FullyQualifiedName~IndustrialApiContractTests'
cd UI && npm test -- --run src/IndustrialPanel.test.tsx
```

## Cross-domain and on-demand

Load `docs/ai-context/core.md` for shared finance, membership, history or Headquarters contracts; `docs/ai-context/ui.md` for rendering/bindings.
Calculation evidence in `docs/verification/industrial-score-examples.md`, archived plans and `UI/src/__fixtures__/` are on-demand for directly relevant calculation, regression or payload work.
