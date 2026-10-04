---
name: implementation
description: Use when executing an approved feature or maintenance plan in CS2-Area-System.
---

# Planned implementation

Use the approved spec/implementation plan as the task brief; keep requirements in their authoritative documents.

1. Check latest `main` and synchronize the requested branch while retaining approved documents.
2. Read the approved spec/implementation plan and its global constraints.
3. Read `docs/ai-context/INDEX.md`; load targeted domain maps before broader exploration.
4. Expand only into referenced spec sections, source and tests needed for the current task. State the boundary that justifies another map.
5. Preserve existing contracts and save compatibility. If the approved design changes them, apply only its explicit changes.
6. Execute scoped plan tasks in order with observed failing tests before implementation, GREEN verification and frequent task commits.
7. Use `superpowers:executing-plans` for inline execution when requested; use `superpowers:test-driven-development` and `superpowers:verification-before-completion` for implementation and claims.

## Scope and handoff

Whole-repository reading requires a concrete need in the plan. Archive history, verification history, generated fixtures and lockfiles stay on-demand.
Do not add requirements or unrelated refactors. Report task commits, exact verification results and environment-limited checks as incomplete.

| Situation | Next action |
| --- | --- |
| Context is nearly full | Resume from the current task brief and ledger. |
| Adjacent feature looks useful | Stay within the approved task. |
