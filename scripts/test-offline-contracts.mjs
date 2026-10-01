import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const root = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(scriptDir, '..');
const filePath = p => path.join(root, p);
const read = p => fs.readFileSync(filePath(p), 'utf8');
const failures = [];
const check = (condition, message) => { if (!condition) failures.push(message); };
const gitBlobSha = p => {
  const bytes = fs.readFileSync(filePath(p));
  return crypto.createHash('sha1').update(Buffer.from(`blob ${bytes.length}\0`)).update(bytes).digest('hex');
};
const walk = directory => {
  const absolute = filePath(directory);
  const result = [];
  for (const entry of fs.readdirSync(absolute, {withFileTypes:true})) {
    const relative = path.join(directory, entry.name);
    if (entry.isDirectory()) result.push(...walk(relative));
    else result.push(relative);
  }
  return result;
};

const specPath='docs/superpowers/specs/2026-09-30-area-system-core-design.md';
const planPath='docs/superpowers/plans/2026-09-30-area-system-core-implementation.md';
const spec = read(specPath);
const plan = read(planPath);
check(gitBlobSha(specPath)==='b43f8b99932b058744284902df9700a9e0aae0d9', 'approved design spec must remain byte-identical to its approved UTF-8 blob');
check(gitBlobSha(planPath)==='a0176200ec6cb5de73959a44062563fd66e39c03', 'approved implementation plan must remain byte-identical to its approved UTF-8 blob');
check(spec.startsWith('# CS2 Area System — Area Core Design\n') && spec.includes('\n## 1. Purpose\n'), 'design spec must remain readable Markdown');
check(plan.startsWith('# CS2 Area System — Area Core Implementation Plan\n') && plan.includes('### Task 9:'), 'implementation plan must retain all planned tasks');

// Park Area v1: approved spec/plan are immutable; Park observes vanilla simulation and stays within its v1 scope.
const parkSpecPath='docs/superpowers/specs/2026-09-30-park-area-v1-design.md';
const parkPlanPath='docs/superpowers/plans/2026-09-30-park-area-v1-implementation.md';
check(gitBlobSha(parkSpecPath)==='3f8a76d7ff9e03c5a4feffca09d5afc5fb936f3a', 'approved Park v1 design spec must remain byte-identical');
check(gitBlobSha(parkPlanPath)==='2d6cca5ab9dbc6c73044fb271764dfbf42c5642d', 'approved Park v1 implementation plan must remain byte-identical');
for (const source of walk('src').filter(p=>p.endsWith('.cs') && !/[\\/](bin|obj)[\\/]/.test(p))) {
  const text = read(source);
  if (/\bLeisureEvent\b/.test(text)) failures.push(`${source}: must not access LeisureSystem's private LeisureEvent queue`);
  if (/<\s*(Game\.Simulation\.)?LeisureSystem\s*>/.test(text)) failures.push(`${source}: must not replace, disable or reorder vanilla LeisureSystem`);
}
for (const source of walk('src/CS2AreaSystem/Modules/Park').filter(p=>p.endsWith('.cs'))) {
  const text = read(source);
  if (/\bGame\.(Policies|Events)\b/.test(text)) failures.push(`${source}: Park Policies/Events are out of v1 scope`);
  if (/\b(LandValue|CitizenHappiness|Happiness|ResidentialDemand|DemandSystem|TourismSystem|AttractivenessParameterData)\b/.test(text))
    failures.push(`${source}: Park v1 must not modify happiness, land value, demand or vanilla tourism`);
}
const candidatePolicy = read('src/CS2AreaSystem/Core/Membership/AreaMembershipCandidatePolicy.cs');
check(/isBuilding\s*&&/.test(candidatePolicy), 'Core membership must stay building-only (no tree/prop/path index)');
const facade = read('src/CS2AreaSystem/Modules/Park/ParkGameplayFacade.cs');
const facadeActions = [...facade.matchAll(/case\s+([^:]+?)\s*:/g)].map(m=>m[1].trim()).sort().join(',');
check(facadeActions==='SetBudget,SetFee', `Park UI actions must be limited to budget and admission fee (found ${facadeActions})`);
const parkPanelTest = read('UI/src/ParkPanel.test.tsx');
check(parkPanelTest.includes("./__fixtures__/park-panel.json"), 'Park UI tests must use the fixture generated from the C# payload');

// Industrial Area v1: approved spec/plan are immutable; Industrial observes the vanilla economy and never mutates it.
const industrialSpecPath='docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md';
const industrialPlanPath='docs/superpowers/plans/2026-10-01-industrial-area-v1-implementation.md';
check(gitBlobSha(industrialSpecPath)==='0e912259279d904c27f7a03ba928c4f06c7a02e3', 'approved Industrial v1 design spec must remain byte-identical');
check(gitBlobSha(industrialPlanPath)==='aa703b244d011aaeea4e4d68fb250ffc072ddd3a', 'approved Industrial v1 implementation plan must remain byte-identical');
for (const doc of ['docs/verification/industrial-observation-contracts.md','docs/verification/industrial-score-examples.json','docs/verification/industrial-score-examples.md','docs/verification/industrial-area-v1-execution-record.md'])
  check(fs.existsSync(filePath(doc)), `${doc} must exist`);
check(JSON.parse(read('docs/verification/industrial-score-examples.json')).synthetic===true, 'calculation examples must stay labeled synthetic');
const industrialDir='src/CS2AreaSystem/Modules/Industrial';
const industrialSources=walk(industrialDir).filter(p=>p.endsWith('.cs'));
// Forbidden: replacing/patching vanilla production, transfer, tax/demand, cargo or pathfinding systems, and direct
// writes to Resources, efficiency or profitability. Industrial reads public state only.
const forbiddenIndustrial=[
  [/\b(ProcessingCompanySystem|ExtractorCompanySystem|StorageCompanySystem|StorageTransferSystem|ResourceBuyerSystem|BuyingCompanySystem|ResourceExporterSystem|CompanyProfitabilitySystem|IndustrialAISystem|ExtractorAISystem)\b/, 'vanilla production/transfer systems'],
  [/\b(TaxSystem|IndustrialDemandSystem|ResidentialDemandSystem|CommercialDemandSystem|SetTaxRate)\b/, 'taxes/demand'],
  [/\b(Game\.Pathfind|PathfindSetupSystem|SetupQueue|StorageTransferRequest|ResourceExporter\b|TransportCompany|CargoTransportVehicle)/, 'pathfinding/cargo routing or transfer requests'],
  [/\b(AddResources|SetResources|SetEfficiencyFactors?|AddComponent<\s*Resources|SetBuffer<\s*Resources)/, 'direct Resources/efficiency mutation'],
  [/SetComponentData\s*(<\s*(Game\.Companies\.)?Profitability|\([^)]*new\s+(Game\.Companies\.)?Profitability)/, 'Profitability writes'],
  [/SetComponentData\s*<\s*(Game\.Buildings\.)?(BuildingEfficiency|Building)\b/, 'building/efficiency writes'],
  [/BindingFlags\.NonPublic|Harmony/, 'private reflection or patching'],
  [/\.Enabled\s*=\s*false/, 'disabling vanilla systems']];
for (const source of industrialSources) {
  // Code only: comments may cite the vanilla systems whose behavior is mirrored.
  const text = read(source).replace(/\/\/.*$/gm, '');
  for (const [pattern, what] of forbiddenIndustrial) if (pattern.test(text)) failures.push(`${source}: Industrial v1 must not touch ${what}`);
}
const mod = read('src/CS2AreaSystem/Mod.cs');
const industrialRegistration = mod.slice(mod.indexOf('void RegisterIndustrialSystems'));
check(mod.includes('RegisterIndustrialSystems(updateSystem);'), 'Mod.OnLoad must register Industrial systems');
check(/UpdateAfter<CS2AreaSystem\.Modules\.Industrial\.IndustrialRuntimeSystem>\(SystemUpdatePhase\.ModificationEnd\)/.test(industrialRegistration), 'IndustrialRuntimeSystem must run in ModificationEnd');
check(/UpdateAt<CS2AreaSystem\.Modules\.Industrial\.IndustrialObservationSystem>\(SystemUpdatePhase\.GameSimulation\)/.test(industrialRegistration), 'IndustrialObservationSystem must run in GameSimulation');
check(/UpdateAfter<CS2AreaSystem\.Modules\.Industrial\.IndustrialProgressionSystem,\s*CS2AreaSystem\.Modules\.Industrial\.IndustrialObservationSystem>\(SystemUpdatePhase\.GameSimulation\)/.test(industrialRegistration), 'IndustrialProgressionSystem must run after observation');
check(/catch\s*\(System\.Exception error\)\s*\{\s*Log\.Error\(error,\s*"Industrial/.test(industrialRegistration), 'Industrial registration failure must be isolated and logged');
const industrialFacade = read(`${industrialDir}/IndustrialGameplayFacade.cs`);
check(!/SetComponentData|AddComponent|RemoveComponent|public static bool HandleAction/.test(industrialFacade), 'Industrial facade must stay read-only (no setters/actions)');
const industrialModule = read(`${industrialDir}/IndustrialAreaModule.cs`);
check(/HandleAction\([^)]*\)\s*=>\s*false;/.test(industrialModule), 'Industrial module must not handle Industrial-specific UI actions');
const panelBuilder = read(`${industrialDir}/IndustrialPanelBuilder.cs`);
check(!/Action\s*=|AreaPanelItemKind\.(Slider|Button)/.test(panelBuilder), 'Industrial panel must not emit actions, sliders or buttons');
const classifier = read(`${industrialDir}/IndustrialFacilityClassifier.cs`);
const classify = classifier.slice(classifier.indexOf('public static IndustrialFacilityRole Classify'), classifier.indexOf('public static bool IsHeadquartersEligible'));
check(classify.length > 0 && !/HasOffice/.test(classify), 'ordinary office properties/companies must never create an Industrial role');
const logisticsModel = read(`${industrialDir}/IndustrialLogisticsModel.cs`);
check(!/\b(CargoFacilities|StorageFacilities|CargoOperational|StorageCapacity|StoredAmount)\b/.test(logisticsModel), 'Logistics scoring must have no warehouse/cargo presence path');
check(/DailyXpCap\s*=\s*240/.test(read(`${industrialDir}/IndustrialProgressionModel.cs`)), 'Industrial daily XP cap must be 240');
check(read('UI/src/IndustrialPanel.test.tsx').includes('./__fixtures__/industrial-panel.json'), 'Industrial UI tests must use the fixture generated from the C# payload');
check(fs.existsSync(filePath('UI/src/__fixtures__/industrial-panel.json')), 'Industrial UI fixture must be committed');
check(read('tests/CS2AreaSystem.Tests/IndustrialPanelFixtureTests.cs').includes('UPDATE_UI_FIXTURES'), 'Industrial UI fixture must be generated from C#');
const membershipSystem = read('src/CS2AreaSystem/Core/Membership/AreaMembershipSystem.cs');
check(!/IndustrialCompany|CompanyData|Citizens/.test(membershipSystem), 'Core membership must not index companies, workers or resources');

const uiSystem = read('src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs');
check(uiSystem.includes('State = "CS2AS.State." + area.State'), 'management view must publish lifecycle localization keys');

const lifecycle = read('src/CS2AreaSystem/Core/Areas/ManagedAreaLifecycleSystem.cs');
check(!lifecycle.includes('registration.IsReady'), 'lifecycle must not be globally blocked by unrelated prefab readiness');
check(lifecycle.includes('registration.IsTypeReady(typeId)'), 'creation must use per-type prefab readiness');

const load = read('src/CS2AreaSystem/Core/Persistence/AreaLoadCoordinatorSystem.cs');
check(!load.includes('registration.IsReady'), 'existing-save recovery must not wait for every area prefab to be ready');
for (const source of walk('src').filter(p=>p.endsWith('.cs'))) {
  if (read(source).includes('registration.IsReady')) failures.push(`${source}: global AreaPrefabRegistrationSystem readiness must not gate unrelated runtime work`);
}

const registration = read('src/CS2AreaSystem/Core/Prefabs/AreaPrefabRegistrationSystem.cs');
check(!registration.includes('private bool registered;'), 'prefab registration must not be one-shot after a failed AddPrefab');
check(registration.includes('IsTypeReady'), 'registration system must expose per-type readiness');
check(registration.includes('EnsurePrefabRegistered'), 'registration system must retry missing/unregistered area prefabs');
check(registration.includes('pendingPrefabs'), 'failed prefab registration must retain the candidate for retry');
check(registration.includes('toolbarTypes'), 'toolbar attachment must track ready area types incrementally');

const factory = read('src/CS2AreaSystem/Core/Prefabs/ManagedAreaPrefabFactory.cs');
check(!factory.includes('uri => !string.IsNullOrEmpty(uri)'), 'production icon availability must not equate non-empty URI with resource existence');
check(factory.includes('ResolveIcon'), 'production icon selection must use the target-version verified icon resolver');
for (const icon of ['LargeParks.svg','ZoneIndustrial.svg','Attraction.svg','SurfaceAreaTool.svg'])
  check(factory.includes(icon), `verified target-version icon set must include ${icon}`);

const anchorService = read('src/CS2AreaSystem/Core/Anchors/AreaAnchorService.cs');
const activation = read('src/CS2AreaSystem/Core/Anchors/AreaActivationSystem.cs');
check(anchorService.includes('ForgetArea'), 'anchor service must explicitly prune reverse entries for deleted areas');
check(activation.includes('ForgetArea'), 'area deletion flow must invoke reverse-anchor pruning');

const section = read('UI/src/AreaInfoSection.tsx');
const uiIndex = read('UI/src/index.tsx');
const sectionSystem = read('src/CS2AreaSystem/Core/UI/AreaInfoSection.cs');
const webpack = read('UI/webpack.config.js');
const dts = read('UI/types/cs2.d.ts');
check(section.includes("from 'cs2/l10n'") || section.includes('from "cs2/l10n"'), 'area section must use CS2 localization API');
check(!section.includes('const labels:'), 'area section must not hardcode its translation table');
// Area details are a section of the vanilla selected-info panel (public extension points only).
check(sectionSystem.includes('AddMiddleSection(this)') && sectionSystem.includes('InfoSectionBase'), 'C# area section must register through SelectedInfoUISystem.AddMiddleSection');
check(section.includes("AREA_INFO_SECTION_TYPE='CS2AreaSystem.Core.UI.AreaInfoSection'"), 'UI section key must equal the C# section type name');
check(uiIndex.includes("'selectedInfoSectionComponents'") && uiIndex.includes('registry.extend('), 'UI must add the section through the public module registry');
check(section.includes('PanelBoundary'), 'area section failures must be contained inside the section');
for (const external of ['cs2/l10n','cs2/ui','cs2/modding'])
  check(webpack.includes(`'${external}'`), `webpack must externalize ${external}`);
check(dts.includes("declare module 'cs2/l10n'") && dts.includes("declare module 'cs2/ui'"), 'local UI typings must declare the game UI modules used');

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
