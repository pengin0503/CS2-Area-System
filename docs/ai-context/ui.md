# UI context map

This is navigation guidance and does not override current specs/code.

Scope: React presentation, bindings, localization and C# panel payload compatibility.

## Authority and implementation

- Shared UI requirements: `docs/superpowers/specs/2026-09-30-area-system-core-design.md`.
- Later UX: `docs/superpowers/specs/2026-10-02-area-district-like-ux-design.md`.
- Backend bindings: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`.
- Frontend: `UI/src/`; read the affected component/test only.
- Shared payload types: `src/CS2AreaSystem/Core/Modules/AreaPanelModels.cs`.

## Focused checks

- `UI/src/ModulePanel.test.tsx`
- `UI/src/ParkPanel.test.tsx`
- `UI/src/IndustrialPanel.test.tsx`

```bash
cd UI && npm test -- --run src/ModulePanel.test.tsx
# From UI, run broader checks when the change affects shared rendering:
npm test -- --run
npm run build
```

## Cross-domain and on-demand

Load `docs/ai-context/core.md` for shared payload/binding changes and the relevant module map for its own panel.
The fixtures/lockfile are opened only for directly relevant UI contract/dependency work: `UI/src/__fixtures__/` and `UI/package-lock.json`.
Fixtures come from existing C# fixture-generation tests; use that workflow when authorized. Archived plans and `docs/verification/` remain on-demand historical evidence.
