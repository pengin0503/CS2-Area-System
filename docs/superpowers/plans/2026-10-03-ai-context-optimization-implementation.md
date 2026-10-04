# AI Context Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce unnecessary AI context/cache churn by adding progressive-disclosure project guidance, reusable Claude Skills, and a conservative archive for completed implementation plans without changing game/runtime behavior.

**Architecture:** Add a small root entry point that routes agents through domain-specific context maps, then into only the specs/source/tests needed for the task. Add repository-local Claude Skills for repeated workflows, archive only plans with explicit completion evidence, and add an offline contract script so the navigation layer cannot silently rot.

**Tech Stack:** Markdown, Node.js 22 ESM, PowerShell, GitHub Actions, existing C#/NUnit and React/Vitest verification.

**Spec:** `docs/superpowers/specs/2026-10-03-ai-context-optimization-design.md`

## Global Constraints

- Do not alter gameplay behavior, save schema, runtime architecture, UI behavior, build outputs, tests' product semantics, or current product requirements.
- Do not rewrite or shorten existing specifications merely to save tokens.
- Do not delete historical plans or verification records.
- Do not add hard `Read` deny rules in `.claude/settings.json`.
- Latest `main` at execution start remains the code source of truth; preserve this approved spec and plan when synchronizing the implementation branch.
- Current files under `docs/superpowers/specs/` remain authoritative where applicable.
- Context maps are navigation aids only and must never override current code/specs.
- Archived plans are historical records and must not override current specs/current code.
- Large fixtures, lockfiles, historical execution records, archived plans, and license text are read only when the current task requires them.
- Archive conservatively: if completion or path-reference safety is uncertain, leave a plan in place.
- No unrelated refactors.

## Review Focus

1. **Stale path after archival:** every moved plan remains reachable through updated verification/script references; test by asserting old paths are absent, archive paths exist, and pinned blob hashes remain unchanged.
2. **Navigation drift:** every file path intentionally exposed by the AI entry layer exists; test required paths and representative links in the context-contract script.
3. **Instruction bloat:** `CLAUDE.md` remains at or below 150 lines and each repository-local Skill remains at or below 120 lines.
4. **Workflow invisibility:** the new context-contract test is invoked both by `scripts/verify.ps1` and GitHub Actions, so future path drift fails normal verification.
5. **Accidental product change:** final diff contains no modifications under `src/`, no UI product source changes, and no fixture/lockfile rewrites.

---

### Task 1: Add the progressive-disclosure context entry layer

**Files:**
- Create: `scripts/test-ai-context-contracts.mjs`
- Create: `CLAUDE.md`
- Create: `docs/ai-context/INDEX.md`
- Create: `docs/ai-context/core.md`
- Create: `docs/ai-context/park.md`
- Create: `docs/ai-context/industrial.md`
- Create: `docs/ai-context/tourism.md`
- Create: `docs/ai-context/ui.md`

**Interfaces:**
- Consumes: existing repository paths/specs/source/tests; no product interface changes.
- Produces: `CLAUDE.md` as the root AI entry point and `docs/ai-context/INDEX.md` as the routing layer for all later Skills/tasks.

- [ ] **Step 1: Create the failing context-contract test**

Create `scripts/test-ai-context-contracts.mjs` using the same zero-dependency Node ESM style as `scripts/test-offline-contracts.mjs`.

It must initially assert all of the following, before the target docs exist:

- required files:
  - `CLAUDE.md`
  - `docs/ai-context/INDEX.md`
  - `docs/ai-context/core.md`
  - `docs/ai-context/park.md`
  - `docs/ai-context/industrial.md`
  - `docs/ai-context/tourism.md`
  - `docs/ai-context/ui.md`
- `CLAUDE.md` line count is `<= 150`;
- `CLAUDE.md` points to `docs/ai-context/INDEX.md`;
- `INDEX.md` names all five domain maps;
- every domain map explicitly states that it is navigation guidance and does not override current specs/code;
- all domain maps contain their required representative paths below;
- routine-reading guidance names `UI/package-lock.json`, `UI/src/__fixtures__/`, archived plans, and verification history as on-demand context rather than default context.

Required representative paths per map:

`core.md`
- `docs/superpowers/specs/2026-09-30-area-system-core-design.md`
- `src/CS2AreaSystem/Core/`
- `tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs`
- `scripts/runtime-smoke-checklist.md`

`park.md`
- `docs/superpowers/specs/2026-09-30-park-area-v1-design.md`
- `src/CS2AreaSystem/Modules/Park/`
- `tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs`
- `UI/src/ParkPanel.test.tsx`

`industrial.md`
- `docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md`
- `src/CS2AreaSystem/Modules/Industrial/`
- `tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs`
- `UI/src/IndustrialPanel.test.tsx`

`tourism.md`
- `src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs`
- `tests/CS2AreaSystem.Tests/TourismAreaModuleTests.cs`
- `tests/CS2AreaSystem.Tests/TourismBoundaryContractTests.cs`
- explicit statement that current Tourism gameplay is boundary-only unless a newer approved spec changes it.

`ui.md`
- `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- `UI/src/`
- `UI/src/__fixtures__/`
- `UI/package-lock.json`
- statement that fixtures/lockfile are opened only for directly relevant UI contract/dependency work.

The test must print one failure per violated contract and exit non-zero when any contract fails.

- [ ] **Step 2: Run the new test and verify RED**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: FAIL because `CLAUDE.md` and `docs/ai-context/*.md` do not exist yet.

- [ ] **Step 3: Create the root `CLAUDE.md`**

Keep it `<= 150` lines and include only stable repository-wide guidance:

- target Cities: Skylines II 1.6.2f1;
- latest `main` at task start is source of truth;
- first read `docs/ai-context/INDEX.md`;
- load the smallest relevant domain map(s);
- current specs override navigation docs;
- archived plans and verification history are on-demand only;
- large fixtures/lockfiles are on-demand only;
- avoid broad whole-repository reads unless scope requires them;
- use existing verification commands;
- no unrelated refactors.

Do not duplicate Park/Industrial/Tourism feature requirements.

- [ ] **Step 4: Create `docs/ai-context/INDEX.md` and the five domain maps**

Keep each file concise and reference, rather than reproduce, the current specifications.

`INDEX.md` must define the normal loading flow:

```text
CLAUDE.md
  -> docs/ai-context/INDEX.md
      -> smallest relevant domain map(s)
          -> targeted spec section(s)
          -> targeted source file(s)
          -> targeted test file(s)
```

For each domain map include:

- scope/responsibility;
- authoritative spec(s), when one exists;
- primary source path(s);
- primary focused tests;
- shared boundary files that justify loading another map;
- files that are intentionally on-demand;
- focused verification commands or test groups.

- [ ] **Step 5: Run the context-contract test and verify GREEN**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: PASS, zero failures.

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md docs/ai-context scripts/test-ai-context-contracts.mjs
git commit -m "docs: add progressive AI context maps"
```

---

### Task 2: Add reusable repository-local Claude Skills

> **Required sub-skill for this task:** Use `superpowers:writing-skills` before creating or editing the repository-local Skills.

**Files:**
- Modify: `scripts/test-ai-context-contracts.mjs`
- Create: `.claude/skills/issue-fix/SKILL.md`
- Create: `.claude/skills/implementation/SKILL.md`
- Create: `.claude/skills/verification/SKILL.md`

**Interfaces:**
- Consumes: `docs/ai-context/INDEX.md` and the domain maps from Task 1.
- Produces: three short reusable workflow Skills that later prompts can invoke without embedding project requirements.

- [ ] **Step 1: Extend the test for Skill contracts**

Add checks for all three `SKILL.md` files:

- file exists;
- starts with YAML frontmatter delimited by `---`;
- frontmatter `name` exactly matches its directory:
  - `issue-fix`
  - `implementation`
  - `verification`
- non-empty `description`;
- each file has at most 120 lines;
- each points agents to `docs/ai-context/INDEX.md`;
- `issue-fix` requires systematic debugging, TDD, scope isolation, and verification;
- `implementation` requires the approved spec/plan and targeted domain maps before broader exploration;
- `verification` requires evidence before success claims and distinguishes product failures from environment/tool failures.

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: FAIL because the three Skills do not exist.

- [ ] **Step 3: Create `issue-fix/SKILL.md`**

Workflow:

1. resolve/sync latest main;
2. read context index and smallest relevant map;
3. read target Issue plus minimum spec/source/tests;
4. systematic debugging;
5. TDD;
6. do not touch unrelated Issues/performance work;
7. focused verification, then applicable broader checks;
8. report environment-limited checks as incomplete, never as passing.

Keep project requirements linked, not duplicated.

- [ ] **Step 4: Create `implementation/SKILL.md`**

Workflow:

1. start from approved spec/implementation plan;
2. load only affected domain map(s);
3. expand to referenced spec/source/tests on demand;
4. preserve existing contracts/save compatibility unless approved design changes them;
5. execute scoped plan tasks with tests and frequent commits;
6. avoid whole-repository reading unless required by the plan.

- [ ] **Step 5: Create `verification/SKILL.md`**

Workflow:

1. enumerate claims being verified;
2. run focused checks first;
3. run applicable repository-level checks;
4. distinguish code/product failures from host/tooling failures;
5. never treat an unexecuted or interrupted check as passing;
6. report exact commands and results.

- [ ] **Step 6: Run the context-contract test and verify GREEN**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add .claude/skills scripts/test-ai-context-contracts.mjs
git commit -m "docs: add reusable Claude workflows"
```

---

### Task 3: Archive only clearly completed implementation plans

**Files:**
- Modify: `scripts/test-ai-context-contracts.mjs`
- Modify: `scripts/test-offline-contracts.mjs`
- Move: `docs/superpowers/plans/2026-09-30-area-system-core-implementation.md` -> `docs/superpowers/plans/archive/2026-09-30-area-system-core-implementation.md`
- Move: `docs/superpowers/plans/2026-09-30-park-area-v1-implementation.md` -> `docs/superpowers/plans/archive/2026-09-30-park-area-v1-implementation.md`
- Move: `docs/superpowers/plans/2026-10-01-industrial-area-v1-implementation.md` -> `docs/superpowers/plans/archive/2026-10-01-industrial-area-v1-implementation.md`
- Move: `docs/superpowers/plans/2026-10-02-foundation-phase1-implementation.md` -> `docs/superpowers/plans/archive/2026-10-02-foundation-phase1-implementation.md`
- Modify: `docs/verification/area-core-execution-record.md`
- Modify: `docs/verification/park-area-v1-execution-record.md`
- Modify: `docs/verification/industrial-area-v1-execution-record.md`
- Modify: `docs/verification/foundation-phase1-execution-record.md`

**Interfaces:**
- Consumes: explicit completion evidence in the four execution records.
- Produces: `docs/superpowers/plans/archive/` as historical-only storage while preserving immutable plan bytes.

Initial archive set is intentionally limited to these four plans. Do not move Area Policy, review-fix, Phase 2, Phase 3, pre-Tourism, load-isolation, or scoped-regression plans in this task.

Pinned Git blob SHAs that must remain unchanged after the moves:

- Area Core: `a0176200ec6cb5de73959a44062563fd66e39c03`
- Park v1: `2d6cca5ab9dbc6c73044fb271764dfbf42c5642d`
- Industrial v1: `aa703b244d011aaeea4e4d68fb250ffc072ddd3a`
- Foundation Phase 1: `df7cdda4ed9d5003da2dd3bae3070f899c4e0511`

- [ ] **Step 1: Extend the context-contract test for archival**

Add checks that:

- all four archive paths exist;
- all four old active-plan paths no longer exist;
- each archived file's Git blob SHA equals the pinned SHA above;
- the four execution records point to the archive path;
- `scripts/test-offline-contracts.mjs` references the archive paths for Core/Park/Industrial.

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: FAIL because the plans are still in the active directory.

- [ ] **Step 3: Move the four plans without changing their contents**

Use `git mv` for each file. Do not edit the moved plan bodies.

- [ ] **Step 4: Update known repository references**

Update only the path strings:

- `docs/verification/area-core-execution-record.md`
- `docs/verification/park-area-v1-execution-record.md`
- `docs/verification/industrial-area-v1-execution-record.md`
- `docs/verification/foundation-phase1-execution-record.md`
- `scripts/test-offline-contracts.mjs` Core/Park/Industrial plan constants

Do not alter the pinned hashes or substantive verification statements.

- [ ] **Step 5: Verify both archive integrity and existing offline contracts**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
node scripts/test-offline-contracts.mjs
```

Expected: both PASS.

- [ ] **Step 6: Search for stale old paths**

Search the repository for each of the four old `docs/superpowers/plans/<filename>` paths.

Expected: zero references outside Git history.

- [ ] **Step 7: Commit**

```bash
git add docs/superpowers/plans docs/verification scripts/test-ai-context-contracts.mjs scripts/test-offline-contracts.mjs
git commit -m "docs: archive completed implementation plans"
```

---

### Task 4: Make AI context contracts part of normal verification

**Files:**
- Modify: `scripts/test-ai-context-contracts.mjs`
- Modify: `scripts/verify.ps1`
- Modify: `.github/workflows/verify.yml`

**Interfaces:**
- Consumes: `scripts/test-ai-context-contracts.mjs`.
- Produces: local and CI enforcement so future path drift is detected automatically.

- [ ] **Step 1: Add failing self-enforcement checks**

Extend `scripts/test-ai-context-contracts.mjs` to assert:

- `scripts/verify.ps1` invokes `test-ai-context-contracts.mjs`;
- `.github/workflows/verify.yml` invokes `node scripts/test-ai-context-contracts.mjs`.

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: FAIL because neither verification entry point invokes it yet.

- [ ] **Step 3: Wire the test into local verification**

In `scripts/verify.ps1`, run:

```powershell
& node "$PSScriptRoot/test-ai-context-contracts.mjs"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Place it with the existing offline/source contract checks before build/test work.

- [ ] **Step 4: Wire the test into GitHub Actions**

In `.github/workflows/verify.yml`, add a step:

```yaml
- name: AI context contracts
  run: node scripts/test-ai-context-contracts.mjs
```

Place it next to the existing Offline contracts step.

- [ ] **Step 5: Verify GREEN**

Run:

```bash
node scripts/test-ai-context-contracts.mjs
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/test-ai-context-contracts.mjs scripts/verify.ps1 .github/workflows/verify.yml
git commit -m "ci: verify AI context contracts"
```

---

### Task 5: Final verification and scope audit

**Files:**
- No planned product-code changes.
- Modify only if verification exposes an implementation defect in Tasks 1–4; do not expand scope.

**Interfaces:**
- Consumes: completed Tasks 1–4.
- Produces: evidence that the AI-context restructuring is internally consistent and does not change the MOD.

- [ ] **Step 1: Run the dedicated contract checks**

```bash
node scripts/test-ai-context-contracts.mjs
node scripts/test-offline-contracts.mjs
```

Expected: PASS / PASS.

- [ ] **Step 2: Run existing source/contract regression checks**

```powershell
pwsh scripts/verify-source.ps1
pwsh scripts/test-source-guards.ps1
pwsh scripts/test-msbuild-json.ps1
```

Expected: all complete successfully.

- [ ] **Step 3: Run repository verification**

Run:

```powershell
pwsh scripts/verify.ps1 -ApiCheck
```

Expected: full verification completes successfully in a correctly provisioned environment.

If a known host/toolchain limitation prevents completion, record the exact failing command/error and continue only with claims proven by the checks that did run. Do not label the aggregate verification as passing.

- [ ] **Step 4: Audit the diff for scope**

Run:

```bash
git diff --check
git diff --name-status <implementation-base>...HEAD
```

Verify:

- no file under `src/` changed;
- no `UI/src/*.tsx`, `UI/src/*.ts`, `UI/src/*.scss` product file changed;
- no `UI/src/__fixtures__/*.json` changed;
- `UI/package-lock.json` did not change;
- moved plans retain their pinned hashes;
- only approved AI guidance/docs/Skills/archive/reference/verification wiring changed.

- [ ] **Step 5: Final whole-branch review**

Review the branch against:

- `docs/superpowers/specs/2026-10-03-ai-context-optimization-design.md`
- this implementation plan.

Critical/Important findings must be fixed and reverified before handoff. Minor unrelated polish is deferred.

- [ ] **Step 6: Commit any verification-driven fix, if one was required**

If no fix was required, do not create an empty commit.

