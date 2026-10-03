# AI Context Optimization Design

Date: 2026-10-03  
Repository: `pengin0503/CS2-Area-System`  
Base: latest `main` at design start (`cb1745b6aecf3d52e74dd8aacfc6bc955263da5c`)

## Problem

The repository has grown enough that AI-assisted work can accumulate a large working context from source, tests, historical implementation plans, verification records, generated fixtures, and lockfiles. The goal is to reduce unnecessary prompt/cache churn without deleting implementation history or weakening the project's existing source-of-truth documents.

This design changes only how AI agents discover and load project context. It does not change gameplay behavior, save schema, runtime code, UI behavior, build outputs, tests, or current product requirements.

## Goals

1. Make the default AI entry point small and stable.
2. Route agents to only the domain context needed for the current task.
3. Keep current specifications authoritative while separating completed implementation history from active guidance.
4. Replace repeated long-form task instructions with reusable Claude Skills.
5. Prevent routine tasks from loading large generated or low-value files unless the task requires them.
6. Preserve repository history and maintain valid internal document references after any moves.
7. Keep the approach useful to other coding agents even when Claude-specific features are not available.

## Non-goals

- Do not alter `src/`, gameplay behavior, save compatibility, or runtime architecture.
- Do not rewrite or shorten existing specifications merely to save tokens.
- Do not delete historical plans or verification records.
- Do not add hard `Read` deny rules in `.claude/settings.json` in this change.
- Do not require every future task to use Claude.
- Do not archive a plan when its completion status is ambiguous.

## Architecture

### 1. Root `CLAUDE.md`

Add a concise repository-level `CLAUDE.md`, targeting roughly 100–150 lines or less.

It will contain only stable, high-value instructions:

- project identity and target game version;
- latest `main` is the source of truth at task start;
- read `docs/ai-context/INDEX.md` before broad exploration;
- select the smallest relevant domain map;
- current specs remain authoritative;
- completed plans and verification history are read only when needed for regression/history analysis;
- avoid reading large fixtures and lockfiles unless directly relevant;
- normal verification commands and scope discipline;
- no unrelated refactors.

The root file must not duplicate domain-specific requirements already held in specs or context maps.

### 2. `docs/ai-context/` progressive-disclosure maps

Add:

- `INDEX.md`
- `core.md`
- `park.md`
- `industrial.md`
- `tourism.md`
- `ui.md`

`INDEX.md` is the routing layer. It tells an agent which map to load for a task and when more than one map is justified.

Each domain map is deliberately small. It contains:

- current responsibilities and boundaries;
- authoritative spec paths;
- primary source directories/files;
- primary focused test files or test groups;
- known shared interfaces that commonly require cross-domain checks;
- large files that should be opened only when necessary;
- verification commands relevant to that domain.

The maps contain references, not duplicated specifications.

### 3. Claude Skills

Add repository-local skills under:

- `.claude/skills/issue-fix/SKILL.md`
- `.claude/skills/implementation/SKILL.md`
- `.claude/skills/verification/SKILL.md`

The skills encode repeated workflow instructions that are currently sent in prompts.

#### `issue-fix`

Use for a scoped bug/Issue:

1. resolve latest `main`;
2. read the AI context index and only the relevant domain map;
3. read the issue and the minimum authoritative spec/source/tests needed;
4. use systematic debugging;
5. apply TDD for the fix;
6. keep unrelated Issues and performance work out of scope;
7. verify focused tests, then applicable broader checks;
8. record any environment-limited verification accurately.

#### `implementation`

Use for planned feature implementation:

1. start from the approved spec/plan;
2. load only affected domain maps;
3. expand into referenced source/spec sections on demand;
4. preserve existing contracts and save compatibility unless the approved design changes them;
5. implement in scoped steps with tests;
6. avoid whole-repository reading unless the plan explicitly requires it.

#### `verification`

Use after a change:

1. identify the claims being verified;
2. run focused checks first;
3. run repository-level build/test/UI checks that apply;
4. distinguish product failures from host/tooling failures;
5. never claim success for a check that did not complete.

Skills should remain short and link to repository context rather than embedding large requirements.

### 4. Historical plan archive

Create `docs/superpowers/plans/archive/`.

Move only implementation plans that are clearly completed and no longer active execution guidance. Before moving each file:

1. establish completion from repository/PR history or the plan's own execution record;
2. search repository references to that path;
3. update references in the same change;
4. preserve the file contents unchanged except where a path reference must be corrected.

Ambiguous, future, deferred, or still-active plans stay in `docs/superpowers/plans/`.

Specifications remain in `docs/superpowers/specs/` and are not archived merely because an implementation phase completed.

The archive is historical context, not default context. `CLAUDE.md` and `docs/ai-context/INDEX.md` will explicitly say to read it only for provenance, regressions, or historical decisions.

### 5. Large-file access guidance

The following classes are excluded from routine reading by guidance, not hard permissions:

- `UI/package-lock.json`;
- `UI/src/__fixtures__/*.json`;
- large generated fixture/output files;
- historical execution records;
- archived implementation plans;
- license text.

An agent may read them when the current task directly concerns dependencies, fixture generation/contracts, licensing, regressions, or historical decisions.

This preserves flexibility while reducing accidental context inflation.

## Context loading flow

Default AI workflow:

```text
CLAUDE.md
  -> docs/ai-context/INDEX.md
      -> one relevant domain map
          -> targeted spec section(s)
          -> targeted source file(s)
          -> targeted test file(s)
```

Cross-domain tasks may load multiple maps, but the reason should be explicit. Whole-repository scans are reserved for tasks whose scope genuinely requires them.

## Source-of-truth rules

1. Latest `main` at task start remains the code source of truth.
2. Current files in `docs/superpowers/specs/` remain requirements/design sources of truth where applicable.
3. Context maps are navigation aids and must not override specs or code.
4. Archived plans are historical records and must not override current specs or current code.
5. Verification records describe what was checked at a point in time; they are not current implementation requirements.

## Archive selection criteria

A plan may be moved to `archive/` only when all are true:

- its scoped implementation has landed or was explicitly superseded;
- it is not an active roadmap or future-phase instruction;
- no open task currently depends on executing it as written;
- path references can be updated safely.

If any condition is uncertain, leave the plan in place.

Initial implementation should prefer a conservative archive set rather than maximizing the number of moves.

## Compatibility and failure handling

- No source/runtime files should change as part of the context optimization itself.
- A broken Markdown reference is considered a regression.
- A context-map reference to a nonexistent file is considered a regression.
- A Skill referring to nonexistent commands/files is considered a regression.
- If a historical plan's status cannot be proven, it must not be moved.
- If path updates would create uncertain semantic changes, keep the original location.

## Verification

The implementation must provide evidence for:

1. **Scope:** diff contains only AI guidance/docs/Skill/archive-reference changes; no gameplay/runtime source changes.
2. **Reference integrity:** every repository path named in `CLAUDE.md`, `docs/ai-context/*.md`, and the new Skills exists.
3. **Archive integrity:** moved plan contents are preserved and all known repository references are updated.
4. **Skill structure:** each `SKILL.md` has valid metadata/instructions and does not embed large duplicated project specifications.
5. **Repository checks:** run existing verification scripts/build/tests that are feasible even though behavior is unchanged; report environment limitations rather than masking them.
6. **Diff hygiene:** no unintended fixture/lockfile rewrites or unrelated formatting churn.

## Expected outcome

After this change, an AI fixing an Industrial issue should normally load the root instructions, the context index, the Industrial map, and only the specific Industrial spec/source/tests it needs. It should not need to load Park design history, completed Core plans, large UI fixtures, or dependency lockfiles unless the task directly crosses those boundaries.

The intended result is lower context/cache creation pressure and more stable long-running AI-assisted development without sacrificing existing documentation or historical traceability.
