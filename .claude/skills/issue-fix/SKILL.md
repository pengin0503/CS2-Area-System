---
name: issue-fix
description: Use when fixing a scoped bug or GitHub Issue in CS2-Area-System.
---

# Scoped Issue fix

Resolve the target bug from reproducible evidence while keeping context small.

1. Check latest `main`; synchronize the working branch while preserving approved task documents.
2. Read `docs/ai-context/INDEX.md` and the smallest relevant domain map.
3. Read the target Issue and only its required current spec sections, source and tests.
4. **REQUIRED:** Use `superpowers:systematic-debugging` to reproduce the failure and establish its cause.
5. **REQUIRED:** Use `superpowers:test-driven-development`: observe a failing regression test, make the scoped fix, then observe GREEN.
6. Maintain scope isolation: leave unrelated Issues, performance work and refactors untouched.
7. Run focused checks, then applicable broader verification from the selected map.
8. **REQUIRED:** Use `superpowers:verification-before-completion` before success claims or handoff. Commit the scoped result when authorized.

## Evidence at handoff

Report the changed behavior, tests and exact commands/results. Environment-limited, unexecuted or interrupted checks are incomplete, never passing.

| Temptation | Required action |
| --- | --- |
| Obvious quick fix | Reproduce it and observe RED first. |
| Nearby easy Issue | Keep the target scope isolated. |
| Missing toolchain | Record its command/error and incomplete check. |
