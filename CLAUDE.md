# CS2 Area System

Cities: Skylines II **1.6.2f1** MOD.

## Start small

1. Check latest `main` at task start; it is the code source of truth. Preserve approved task documents when synchronizing the working branch.
2. First read `docs/ai-context/INDEX.md`.
3. Load the smallest relevant domain map(s), then only targeted spec sections, source files and tests.
4. Expand context when a concrete dependency or failing check justifies it. Avoid broad whole-repository reads unless the task scope requires them.

Current specifications in `docs/superpowers/specs/` remain authoritative where applicable. Navigation maps do not override current specs or code.
Follow the approved task scope and implementation plan. No unrelated refactors or Issue fixes.

## On-demand context

- `UI/package-lock.json` is on-demand for dependency work.
- `UI/src/__fixtures__/` is on-demand for directly relevant UI payload/fixture contracts.
- Historical archived plans are on-demand for provenance, regressions or past decisions; they do not override current specs/code.
- `docs/verification/` verification history is on-demand evidence of past checks, not current requirements.
- Generated outputs and license text are on-demand when the task directly concerns them.

Use guidance rather than hard Read deny rules. Keep existing specifications and historical records intact.

## Verify claims

Use focused checks from the selected map, then applicable broader checks:

```bash
node scripts/test-ai-context-contracts.mjs
node scripts/test-offline-contracts.mjs
pwsh scripts/verify.ps1 -ApiCheck
```

The aggregate script runs source guards, C# tests/build and UI tests/build. API compilation requires the project's configured game references.
Run checks before claiming success. Report exact commands and errors for environment/toolchain limitations; unexecuted or interrupted checks are incomplete.
