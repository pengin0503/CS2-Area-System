---
name: verification
description: Use when checking a CS2-Area-System change before a completion claim or review handoff.
---

# Verification evidence

Require evidence before success claims. **REQUIRED:** Use `superpowers:verification-before-completion`.

1. Enumerate the claims being verified and the commands that prove each.
2. Read `docs/ai-context/INDEX.md` and only the relevant domain maps for applicable commands and boundaries.
3. Run focused checks first. Read their output and exit status.
4. Run applicable repository-level source guards, build, C# tests, UI tests/build and offline contracts. The aggregate entry is `scripts/verify.ps1`; use `-ApiCheck` for configured API compilation.
5. Distinguish code/product failures from environment/tool failures; record which stage actually completed.
6. Never treat an unexecuted or interrupted check as passing. An aggregate check that stops early is incomplete even when earlier stages passed.

## Result format

| Check | Required evidence |
| --- | --- |
| PASS | Exact command, successful exit and executed tests/build result. |
| Product failure | Exact command, failing test/error and reproducible cause. |
| Environment/tool limitation | Exact command/error and checks that could not run. |

Report runtime checks separately from offline/API compilation. Do not infer gameplay success from a source guard or build.
