import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const failures = [];
const check = (condition, message) => { if (!condition) failures.push(message); };
const exists = p => fs.existsSync(path.join(root, p));
const read = p => exists(p) ? fs.readFileSync(path.join(root, p), 'utf8') : '';
const lines = text => text.trimEnd().split(/\r?\n/).length;
const maps = {
  core: ['docs/superpowers/specs/2026-09-30-area-system-core-design.md', 'src/CS2AreaSystem/Core/', 'tests/CS2AreaSystem.Tests/AreaPanelModelTests.cs', 'scripts/runtime-smoke-checklist.md'],
  park: ['docs/superpowers/specs/2026-09-30-park-area-v1-design.md', 'src/CS2AreaSystem/Modules/Park/', 'tests/CS2AreaSystem.Tests/ParkPanelBuilderTests.cs', 'UI/src/ParkPanel.test.tsx'],
  industrial: ['docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md', 'src/CS2AreaSystem/Modules/Industrial/', 'tests/CS2AreaSystem.Tests/IndustrialPanelBuilderTests.cs', 'UI/src/IndustrialPanel.test.tsx'],
  tourism: ['src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs', 'tests/CS2AreaSystem.Tests/TourismAreaModuleTests.cs', 'tests/CS2AreaSystem.Tests/TourismBoundaryContractTests.cs'],
  ui: ['src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs', 'UI/src/', 'UI/src/__fixtures__/', 'UI/package-lock.json']
};
const guidancePaths = ['CLAUDE.md', 'docs/ai-context/INDEX.md', ...Object.keys(maps).map(name => `docs/ai-context/${name}.md`)];
for (const p of guidancePaths) check(exists(p), `${p}: required file is missing`);
const claude = read('CLAUDE.md');
const index = read('docs/ai-context/INDEX.md');
check(lines(claude) <= 150, 'CLAUDE.md: must stay at or below 150 lines');
check(claude.includes('docs/ai-context/INDEX.md'), 'CLAUDE.md: must route through the context index');
for (const [name, representatives] of Object.entries(maps)) {
  const p = `docs/ai-context/${name}.md`;
  const text = read(p);
  check(index.includes(`${name}.md`), `INDEX.md: must route to ${name}.md`);
  check(/navigation guidance/i.test(text) && /does not override current specs\/code/i.test(text), `${p}: must state navigation-only authority`);
  for (const representative of representatives) {
    check(text.includes(representative), `${p}: must expose ${representative}`);
    check(exists(representative), `${p}: target does not exist: ${representative}`);
  }
}
check(/boundary-only.*unless a newer approved spec/i.test(read('docs/ai-context/tourism.md')), 'tourism.md: must preserve the current boundary-only scope');
check(/fixtures\/lockfile.*only.*directly relevant UI contract\/dependency/i.test(read('docs/ai-context/ui.md')), 'ui.md: fixtures/lockfile must be on demand');
for (const item of ['UI/package-lock.json', 'UI/src/__fixtures__/', 'archived plans', 'verification history'])
  check(claude.split(/\r?\n/).some(line => line.includes(item) && /on-demand/i.test(line)), `CLAUDE.md: ${item} must be on-demand context`);

// Validate every exposed repository path, not just the representative subset.
for (const p of guidancePaths) {
  for (const match of read(p).matchAll(/`((?:docs|src|tests|scripts|UI|\.claude)\/[^`\s]+|CLAUDE\.md)`/g))
    check(exists(match[1]), `${p}: broken repository path: ${match[1]}`);
}

if (failures.length) {
  for (const failure of failures) console.error(`FAIL: ${failure}`);
  console.error(`AI context contracts failed: ${failures.length}`);
  process.exitCode = 1;
} else console.log('AI context contract checks passed.');
