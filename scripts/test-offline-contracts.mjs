import fs from 'node:fs';
import path from 'node:path';

const root = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(import.meta.dirname, '..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const failures = [];
const check = (condition, message) => { if (!condition) failures.push(message); };

const spec = read('docs/superpowers/specs/2026-09-30-area-system-core-design.md');
check(spec.startsWith('# CS2 Area System — Area Core Design\n'), 'approved design spec must be restored as UTF-8 Markdown');
check(spec.includes('\n## 1. Purpose\n'), 'design spec must preserve Markdown section structure');

const plan = read('docs/superpowers/plans/2026-09-30-area-system-core-implementation.md');
check(plan.startsWith('# CS2 Area System — Area Core Implementation Plan\n'), 'approved implementation plan must be restored as UTF-8 Markdown');
check(plan.includes('### Task 9:'), 'implementation plan must retain all planned tasks');

const uiSystem = read('src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs');
const recreate = uiSystem.indexOf('recreate = area');
const setPrefab = uiSystem.indexOf('prefab = prefab', recreate + 1);
const editMode = uiSystem.indexOf('mode = AreaToolSystem.Mode.Edit', setPrefab + 1);
const activate = uiSystem.indexOf('tools.activeTool =', editMode + 1);
check(recreate >= 0 && setPrefab > recreate && editMode > setPrefab && activate > editMode,
  'Edit area must target the selected native area via recreate -> prefab -> Edit -> activeTool');

const lifecycle = read('src/CS2AreaSystem/Core/Areas/ManagedAreaLifecycleSystem.cs');
check(!lifecycle.includes('registration.IsReady'), 'lifecycle must not be globally blocked by unrelated prefab readiness');
check(lifecycle.includes('registration.IsTypeReady(typeId)'), 'creation must use per-type prefab readiness');

const load = read('src/CS2AreaSystem/Core/Persistence/AreaLoadCoordinatorSystem.cs');
check(!load.includes('registration.IsReady'), 'existing-save recovery must not wait for every area prefab to be ready');

const registration = read('src/CS2AreaSystem/Core/Prefabs/AreaPrefabRegistrationSystem.cs');
check(!registration.includes('private bool registered;'), 'prefab registration must not be one-shot after a failed AddPrefab');
check(registration.includes('IsTypeReady'), 'registration system must expose per-type readiness');
check(registration.includes('EnsurePrefabRegistered'), 'registration system must retry missing/unregistered area prefabs');
check(registration.includes('toolbarTypes'), 'toolbar attachment must track ready area types incrementally');

const factory = read('src/CS2AreaSystem/Core/Prefabs/ManagedAreaPrefabFactory.cs');
check(!factory.includes('uri => !string.IsNullOrEmpty(uri)'), 'production icon availability must not equate non-empty URI with resource existence');
check(factory.includes('ResolveIcon'), 'production icon selection must use the target-version verified icon resolver');

const anchorService = read('src/CS2AreaSystem/Core/Anchors/AreaAnchorService.cs');
const activation = read('src/CS2AreaSystem/Core/Anchors/AreaActivationSystem.cs');
check(anchorService.includes('ForgetArea'), 'anchor service must explicitly prune reverse entries for deleted areas');
check(activation.includes('ForgetArea'), 'area deletion flow must invoke reverse-anchor pruning');

const panel = read('UI/src/AreaManagementPanel.tsx');
const webpack = read('UI/webpack.config.js');
const dts = read('UI/types/cs2.d.ts');
check(panel.includes("from 'cs2/l10n'") || panel.includes('from "cs2/l10n"'), 'management panel must use CS2 localization API');
check(!panel.includes('const labels:'), 'management panel must not hardcode its translation table');
check(panel.includes('useEffect'), 'management panel must resync editable name state when the backend name changes');
check(webpack.includes("'cs2/l10n'") || webpack.includes('"cs2/l10n"'), 'webpack must externalize cs2/l10n');
check(dts.includes("declare module 'cs2/l10n'"), 'local UI typings must declare cs2/l10n');

const localization = read('src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs');
check(localization.includes('CS2AS.UI.EditArea'), 'C# localization source must include management panel actions');
check(localization.includes('CS2AS.State.Suspended'), 'C# localization source must include lifecycle states');

const requirements = read('docs/verification/area-core-requirements.md');
check(!requirements.includes('Main is unchanged; all implementation commits are on feat/area-core'), 'verification docs must describe the current merged repository state');

if (failures.length) {
  console.error(`Offline contract checks failed (${failures.length}):`);
  for (const failure of failures) console.error(` - ${failure}`);
  process.exit(1);
}
console.log('Offline contract checks passed.');
