# Core context map

This is navigation guidance and does not override current specs/code.

Scope: shared area lifecycle, membership, module interfaces, finance, serialization and UI payload boundaries.

## Authority and implementation

- Core spec: `docs/superpowers/specs/2026-09-30-area-system-core-design.md`.
- For policy tasks: `docs/superpowers/specs/2026-10-01-area-policy-system-design.md`.
- For later Headquarters/history boundaries: `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md`.
- Shared implementation: `src/CS2AreaSystem/Core/`; choose the affected file before reading a directory.
- UI payload boundary: `src/CS2AreaSystem/Core/Modules/AreaPanelModels.cs` and `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`.

## Focused checks

- `tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs`
- `tests/CS2AreaSystem.Tests/SerializationContractTests.cs`
- `tests/CS2AreaSystem.Tests/AreaMembershipPolicyTests.cs`

```bash
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj --filter 'FullyQualifiedName~AreaPanelModelTests|FullyQualifiedName~SerializationContractTests|FullyQualifiedName~AreaMembershipPolicyTests'
node scripts/test-offline-contracts.mjs
```

## Cross-domain and on-demand

Load the affected Park/Industrial/Tourism map only when a module consumes a changed Core contract. Load `docs/ai-context/ui.md` when bindings or payloads cross into React.
Runtime-only checks: `scripts/runtime-smoke-checklist.md`.
Archived plans, `docs/verification/`, game reference ZIPs and generated UI fixtures are on-demand for evidence that the current task needs.
