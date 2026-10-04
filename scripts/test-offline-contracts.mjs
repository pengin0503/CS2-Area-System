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
const planPath='docs/superpowers/plans/archive/2026-09-30-area-system-core-implementation.md';
const spec = read(specPath);
const plan = read(planPath);
check(gitBlobSha(specPath)==='b43f8b99932b058744284902df9700a9e0aae0d9', 'approved design spec must remain byte-identical to its approved UTF-8 blob');
check(gitBlobSha(planPath)==='a0176200ec6cb5de73959a44062563fd66e39c03', 'approved implementation plan must remain byte-identical to its approved UTF-8 blob');
check(spec.startsWith('# CS2 Area System — Area Core Design\n') && spec.includes('\n## 1. Purpose\n'), 'design spec must remain readable Markdown');
check(plan.startsWith('# CS2 Area System — Area Core Implementation Plan\n') && plan.includes('### Task 9:'), 'implementation plan must retain all planned tasks');

// Park Area v1: approved spec/plan are immutable; Park observes vanilla simulation and stays within its v1 scope.
const parkSpecPath='docs/superpowers/specs/2026-09-30-park-area-v1-design.md';
const parkPlanPath='docs/superpowers/plans/archive/2026-09-30-park-area-v1-implementation.md';
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
// Wave A5: the only other Park action is the read-only landscape diagnostic focus, routed before any setter runs.
const parkActionConsts = [...facade.matchAll(/(\w+)\s*=\s*"[^"]*"/g)].map(m=>m[1]).sort().join(',');
check(parkActionConsts==='FocusDiagnosticTarget,PreviewBudget,PreviewFee,SetBudget,SetFee,StartEvent', `Park facade actions must be budget, fee, read-only preview, diagnostic focus and the C5 event start (found ${parkActionConsts})`);
// Phase 2 C5: the event start is routed before the setters, only to ParkEventSystem, and never writes event state itself.
check(/if\s*\(action\s*==\s*StartEvent\)\s*return\s+HandleStartEvent\(/.test(facade), 'Park event start must be routed before the setters');
const parkStart = facade.slice(facade.indexOf('private static bool HandleStartEvent'), facade.indexOf('private static bool HandleFocus'));
check(parkStart.length > 0 && /events\.RequestStart\(area, eventId\) == ParkEventRequestOutcome\.Request/.test(parkStart) && /IsQuarantined\(area\)/.test(parkStart) &&
  !/SetComponentData|AddComponent|RemoveComponent|SetBuffer|GetBuffer|EnqueueExpense/.test(parkStart), 'Park event start must only ask ParkEventSystem (quarantine-checked) and never write state or money itself');
const preview = read('src/CS2AreaSystem/Modules/Park/ParkWhatIfPreview.cs');
check(!/SetComponentData|AddComponent|RemoveComponent|EntityCommandBuffer|AreaFinanceHouseholdTransfer|ISerializable/.test(preview), 'Park what-if must be pure and never persist or transfer money');
check(/if\s*\(action\s*==\s*FocusDiagnosticTarget\)\s*return\s+HandleFocus\(/.test(facade), 'Park diagnostic focus must be routed before the setters');
const parkFocus = facade.slice(facade.indexOf('private static bool HandleFocus'), facade.indexOf('public static ParkPanelModel Read'));
check(parkFocus.length > 0 && !/SetComponentData|AddComponent|RemoveComponent|SetBuffer|DestroyEntity/.test(parkFocus), 'Park diagnostic focus must not change any entity');
const parkPanelTest = read('UI/src/ParkPanel.test.tsx');
check(parkPanelTest.includes("./__fixtures__/park-panel.json"), 'Park UI tests must use the fixture generated from the C# payload');

// Industrial Area v1: approved spec/plan are immutable; Industrial observes the vanilla economy and never mutates it.
const industrialSpecPath='docs/superpowers/specs/2026-10-01-industrial-area-v1-design.md';
const industrialPlanPath='docs/superpowers/plans/archive/2026-10-01-industrial-area-v1-implementation.md';
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
// Phase 2 C4: the dedicated support settlement file is the only Industrial source allowed to write company money, and only
// through the verified EconomyUtils.AddResources(Resource.Money, ...) path; every other Resources/efficiency write stays forbidden.
const supportSettlement = `${industrialDir}/IndustrialSupportSettlement.cs`;
for (const source of industrialSources) {
  // Code only: comments may cite the vanilla systems whose behavior is mirrored.
  let text = read(source).replace(/\/\/.*$/gm, '');
  if (source === supportSettlement) {
    const writes = text.match(/(?:\b\w+\.)?AddResources\s*\([^,]*/g) || [];
    check(writes.length === 1 && writes.every(w => /EconomyUtils\.AddResources\s*\(\s*Resource\.Money$/.test(w)), `${source}: support may only add Resource.Money once through EconomyUtils.AddResources`);
    text = text.replace(/EconomyUtils\.AddResources\s*\(\s*Resource\.Money,/g, '');
  }
  for (const [pattern, what] of forbiddenIndustrial) if (pattern.test(text)) failures.push(`${source}: Industrial v1 must not touch ${what}`);
}
const mod = read('src/CS2AreaSystem/Mod.cs');
const industrialRegistration = mod.slice(mod.indexOf('RegisterIndustrialSystems(UpdateSystem updateSystem)'), mod.indexOf('void RegisterIndustrialSupport('));
check(mod.includes('RegisterIndustrialSystems(updateSystem);'), 'Mod.OnLoad must register Industrial systems');
const supportRegistration = mod.slice(mod.indexOf('void RegisterIndustrialSupport('));
check(/ModuleRegistration\.Requires\(finance, "Industrial support"/.test(supportRegistration) && /ModuleRegistration\.Requires\(industrial, "Industrial support"/.test(supportRegistration), 'Industrial support must require complete finance and Industrial modules');
check(/catch\s*\(System\.Exception error\)\s*\{\s*support\.Fail\(error,\s*type => Disable\(updateSystem,\s*type\),\s*ReportRegistrationFailure\);/.test(supportRegistration), 'Industrial support registration failure must disable the whole support module and be logged');
check(/UpdateAfter<CS2AreaSystem\.Modules\.Industrial\.IndustrialRuntimeSystem>\(SystemUpdatePhase\.ModificationEnd\)/.test(industrialRegistration), 'IndustrialRuntimeSystem must run in ModificationEnd');
check(/UpdateAt<CS2AreaSystem\.Modules\.Industrial\.IndustrialObservationSystem>\(SystemUpdatePhase\.GameSimulation\)/.test(industrialRegistration), 'IndustrialObservationSystem must run in GameSimulation');
check(/UpdateAfter<CS2AreaSystem\.Modules\.Industrial\.IndustrialProgressionSystem,\s*CS2AreaSystem\.Modules\.Industrial\.IndustrialObservationSystem>\(SystemUpdatePhase\.GameSimulation\)/.test(industrialRegistration), 'IndustrialProgressionSystem must run after observation');
check(/catch\s*\(System\.Exception error\)\s*\{\s*industrial\.Fail\(error,\s*type => Disable\(updateSystem,\s*type\),\s*ReportRegistrationFailure\);/.test(industrialRegistration), 'Industrial registration failure must be isolated, disable the whole module and be logged');
const industrialFacade = read(`${industrialDir}/IndustrialGameplayFacade.cs`);
// B2: simulation remains read-only; assignment writes are confined to module-owned Area buffers.
check(!/SetComponentData|AddComponent|RemoveComponent|SetBuffer|AddBuffer|EntityCommandBuffer|DestroyEntity/.test(industrialFacade), 'Industrial facade must stay read-only (no setters)');
const industrialFacadeActions = [...industrialFacade.matchAll(/public const string (\w+)\s*=\s*"([^"]*)"/g)].map(m => m[2]);
check(JSON.stringify(industrialFacadeActions) === JSON.stringify(['focusDiagnosticTarget', 'assignCompanyHere', 'clearCompanyAssignment', 'setSupportPerCompany']), 'Industrial facade must whitelist exactly focus, company assignment/removal and the C4 support setting');
check(/bool contract = IndustrialSupplyContractActions\.IsContractAction\(action\);\s*if \(action != FocusDiagnosticTarget && action != AssignCompanyHere && action != ClearCompanyAssignment && action != SetSupportPerCompany && !contract\) return false;/.test(industrialFacade), 'Industrial facade must reject actions outside the whitelist first');
// Phase 3 D1: contract actions are a closed set, routed to the enabled contract system (the only contract writer).
const contractActionSource = read(`${industrialDir}/IndustrialSupplyContractActions.cs`);
const contractActionNames = [...contractActionSource.matchAll(/(\w+) = "(contract[\w.]*)"/g)].map(m => m[2]);
check(JSON.stringify(contractActionNames) === JSON.stringify(['contractDraftDestination', 'contractDraftResource', 'contractDraftTarget', 'contractCreate', 'contractTarget.', 'contractToggle', 'contractRemove']), 'Industrial contract actions must be exactly the draft, create, target, toggle and remove actions');
check(/return system\.TryApplyAction\(area, areaId, action, value, live\.Contains, outputs\.Contains\);/.test(industrialFacade), 'Industrial contract actions must be applied by the contract system');
// C4: the support setting is written only by the enabled support system, through the setting-only action helper.
check(/if \(action == SetSupportPerCompany\) return HandleSupport\(area, value, context\);/.test(industrialFacade) && /return support\.TrySetPerCompany\(area, value\);/.test(industrialFacade), 'Industrial support setting must route through IndustrialSupportSystem');
const supportSystem = read(`${industrialDir}/IndustrialSupportSystem.cs`);
const supportSetter = supportSystem.slice(supportSystem.indexOf('internal bool TrySetPerCompany'), supportSystem.indexOf('// Live B2 resolution'));
check(/if \(!Enabled \|\|/.test(supportSetter) && /IndustrialSupportActions\.TryApply\(true, true, value, ref state\)/.test(supportSetter), 'Industrial support setting must require the enabled module and change only the setting');
const industrialModule = read(`${industrialDir}/IndustrialAreaModule.cs`);
check(/HandleAction\([^)]*\)\s*\{\s*bool applied = IndustrialGameplayFacade\.HandleAction\(area, action, value, context\);\s*if \(applied\) panels\.Invalidate\(\);\s*return applied;\s*\}/.test(industrialModule), 'Industrial module must route actions only to the read-only facade');
const panelBuilder = read(`${industrialDir}/IndustrialPanelBuilder.cs`);
check((panelBuilder.match(/AreaPanelItemKind\.Slider/g) || []).length === 2 && /Kind = AreaPanelItemKind\.Slider, Id = "support\.perCompany"[^\n]*Action = IndustrialGameplayFacade\.SetSupportPerCompany/.test(panelBuilder) &&
  /Kind = AreaPanelItemKind\.Slider, Id = id, LabelKey = P \+ "Contracts\.Target", Action = action/.test(panelBuilder), 'Industrial panel may emit only the C4 support slider and the D1 contract target sliders');
const panelActions = [...panelBuilder.matchAll(/Action\s*=\s*([^,}]+)/g)].map(m => m[1].trim());
check(panelActions.length === 9 && panelActions.every(action => ['IndustrialGameplayFacade.FocusDiagnosticTarget', 'IndustrialGameplayFacade.AssignCompanyHere', 'IndustrialGameplayFacade.ClearCompanyAssignment', 'IndustrialGameplayFacade.SetSupportPerCompany',
  'IndustrialSupplyContractActions.Create', 'IndustrialSupplyContractActions.Toggle', 'IndustrialSupplyContractActions.Remove', 'action'].includes(action)), 'Industrial panel may emit only the whitelisted actions');
// The two `Action = action` helpers (Select, TargetSlider) receive only contract action constants.
const helperActions = [...panelBuilder.matchAll(/(?:Select|TargetSlider)\((?:"[^"]*"|id \+ "target")(?:, P \+ "[^"]+")?, (IndustrialSupplyContractActions\.\w+(?: \+ contractId)?|[^,)]+)/g)].map(m => m[1]);
check(helperActions.length === 4 && helperActions.every(action => /^IndustrialSupplyContractActions\.(DraftDestination|DraftResource|DraftTarget|TargetPrefix \+ contractId)$/.test(action)), 'Industrial contract selects/sliders may send only contract actions');
check((panelBuilder.match(/AreaPanelItemKind\.Button/g) || []).length === 6, 'Industrial panel must emit only focus, assign, remove and the contract create/toggle/remove buttons');
check(/GetBuffer<IndustrialCompanyAssignment>/.test(industrialFacade), 'assignment persistence must use only the module-owned buffer');
check(/candidates.AssignmentRevision != assignments.Revision/.test(read(`${industrialDir}/IndustrialCompanyAssignmentActions.cs`)), 'assignment actions must reject stale revisions');
check(/live.Building == candidate.Building/.test(read(`${industrialDir}/IndustrialCompanyAssignmentActions.cs`)), 'assignment actions must revalidate the exact rented building');
check(!/EntityQuery|ToEntityArray/.test(read(`${industrialDir}/IndustrialCompanyOwnership.cs`)), 'ownership resolution must not add a whole-world scan');
check(/assignments.GetAreaRevision\(areaId\)/.test(read(`${industrialDir}/IndustrialObservationSystem.cs`)), 'assignment observation generations must be scoped per Area');
check(/Store.InvalidateOwnership\(Assignments.ChangedAreas\)/.test(read(`${industrialDir}/IndustrialRuntimeSystem.cs`)), 'assignment changes must invalidate only affected owner snapshots');
const industrialObservation = read(`${industrialDir}/IndustrialObservationSystem.cs`);
check(/if \(hasPending\) Consume\(bucket\);/.test(industrialObservation), 'pending observation reads must carry current consumption hour to ownership UI');
check(/runtime.ObserveCompanyCandidate\(read, company, currentHourBucket\)/.test(industrialObservation), 'ownership candidates must publish at current consumption hour, not cycle start hour');
check(!/ObserveCompanyCandidate\([^;]*cycle.HourBucket/.test(industrialObservation), 'delayed cycles must not publish candidates with an expired hour key');
const classifier = read(`${industrialDir}/IndustrialFacilityClassifier.cs`);
const classify = classifier.slice(classifier.indexOf('public static IndustrialFacilityRole Classify'), classifier.indexOf('public static bool IsHeadquartersEligible'));
const positiveClassify = classify.replace(/if\s*\([^;]*HasOffice[^;]*\)\s*return\s+IndustrialFacilityRole\.None;/, '');
check(classify.length > 0 && !/HasOffice/.test(positiveClassify), 'ordinary office properties/companies must never create an Industrial role (an exclusion guard is allowed)');
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
// Identity lifecycle (#62–#65): one observation point per load generation, live IDs only, quarantine instead of renumbering.
const coordinatorSource = read('src/CS2AreaSystem/Core/Persistence/AreaLoadCoordinatorSystem.cs');
check(/services\.Identity\.BeginLoad\(\)/.test(coordinatorSource.slice(coordinatorSource.indexOf('OnGamePreload'))), 'every load generation must reset the stable-ID allocator and quarantine');
check(/ids = GetEntityQuery\(new EntityQueryDesc \{\s*All = new\[\] \{ComponentType\.ReadOnly<ManagedAreaStableId>\(\)\},\s*None = new\[\] \{ComponentType\.ReadOnly<Deleted>\(\),ComponentType\.ReadOnly<Temp>\(\)\}/.test(coordinatorSource),
  'saved stable IDs must be observed from live (non-Deleted, non-Temp) entities only');
for (const source of walk('src').filter(p=>p.endsWith('.cs'))) {
  const code = read(source).replace(/\/\/.*$/gm, '');
  if (/ObserveExisting\(/.test(code) && !/AreaIdentityLifecycle\.cs$|AreaStableIdAllocator\.cs$/.test(source))
    failures.push(`${source}: saved stable IDs must be observed only through AreaIdentityLifecycle.Observe`);
}
check(!lifecycle.includes('ObserveExisting') && !lifecycle.includes('savedIds'), 'ManagedAreaLifecycleSystem must not observe saved IDs a second time');
const observeStep = coordinatorSource.slice(coordinatorSource.indexOf('private void ObserveIdentities'), coordinatorSource.indexOf('private void ValidateSavedState'));
check(/services\.Identity\.Observe\(records\)[\s\S]*ValidateSavedState\(managed\)/.test(observeStep),
  'identity and saved-state quarantine must both be decided in the first recovery step, before membership is rebuilt');
check(/sequence\.TryRecover\(allReady, ObserveIdentities, membership\.RebuildNow, ValidateModules\)/.test(coordinatorSource), 'per-Area readiness gates recovery in order: identities, membership, activation');
for (const [file, what] of [
  ['src/CS2AreaSystem/Core/Membership/AreaMembershipSystem.cs', 'membership'],
  ['src/CS2AreaSystem/Core/Anchors/AreaActivationSystem.cs', 'activation'],
  ['src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs', 'anchor eligibility'],
  ['src/CS2AreaSystem/Core/Policies/AreaPolicySystem.cs', 'policy state and upkeep'],
  ['src/CS2AreaSystem/Modules/Park/ParkRuntimeSystem.cs', 'Park runtime'],
  ['src/CS2AreaSystem/Modules/Industrial/IndustrialRuntimeSystem.cs', 'Industrial runtime'],
  ['src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs', 'management UI']])
  check(/IsQuarantined/.test(read(file)), `${what} must skip quarantined areas`);
const toolGuard = read('src/CS2AreaSystem/Core/Areas/ManagedAreaToolGuardSystem.cs');
check(toolGuard.includes('registration.IsTypeReady((int)kind)'), 'creation must use per-type prefab readiness');
check(!toolGuard.includes('registration.IsReady'), 'tool guard must not be globally blocked by unrelated prefab readiness');

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

// 1.6.2f1 native toolbar graph: UIObject.LateInitialize consumes m_Group and adds the
// child to the parent UIGroupPrefab. Area prefabs therefore must have their category
// assigned before PrefabSystem.AddPrefab; patching UIObjectData/UIGroupElement later is
// not an equivalent prefab-initialization path.
const toolbarFactory = read('src/CS2AreaSystem/Core/Prefabs/AreaToolbarPrefabFactory.cs');
const toolbarUnlock = read('src/CS2AreaSystem/Core/Prefabs/AreaToolbarUnlockSystem.cs');
const areaPrefabFactory = read('src/CS2AreaSystem/Core/Prefabs/ManagedAreaPrefabFactory.cs');
check(toolbarFactory.includes('ui.m_Group = hostGroup;'), 'Areas Menu must belong to the native Tools Toolbar Group before registration');
check(toolbarFactory.includes('category.m_Menu = menu;'), 'Area Types category must reference the Areas Menu before registration');
check(toolbarFactory.includes('ui.m_Group = category;'), 'Area prefab UIObject must target the Area Types category');
for (const areaName of ['CS2AS.ParkArea','CS2AS.IndustrialArea','CS2AS.TourismArea'])
  check(areaPrefabFactory.includes(areaName), `native toolbar graph must retain the independent ${areaName} prefab`);
check(/foreach\s*\(var type in services\.Registry\.All\)[\s\S]*if \(decision\.RegisterPrefabs\) EnsurePrefabRegistered\(type,\s*decision\.AttachToolbar\)/.test(registration),
  'all registered Area types must pass through the same pre-initialization toolbar path');
check(/toolbar\.Update\(TryEnsureToolbarGroups\)/.test(registration),
  'a toolbar graph failure must be bounded by AreaToolbarExposureState instead of blocking Area prefab registration');
const ensureAreaStart = registration.indexOf('private void EnsurePrefabRegistered');
const ensureAreaEnd = registration.indexOf('private string ResolveIcon', ensureAreaStart);
const ensureAreaRegistration = registration.slice(ensureAreaStart, ensureAreaEnd);
check(/if \(attachToolbar\) PrepareAreaPrefabForToolbar\(candidate,\s*type\);[\s\S]*prefabs\.AddPrefab\(candidate\)/.test(ensureAreaRegistration),
  'each Area prefab must receive its Area Types UIObject group before PrefabSystem.AddPrefab');
check(!/EntityManager\.SetComponentData\(\s*areaPrefabEntity,\s*new UIObjectData/.test(registration),
  'Area toolbar registration must not patch UIObjectData after prefab initialization');
check(!/category\.AddElement\(EntityManager,\s*areaPrefabEntity\)/.test(registration),
  'Area toolbar registration must not duplicate UIObject.LateInitialize with a manual category.AddElement');
check(/GetBuffer<UIGroupElement>\(category,[^)]*\)\.Length == 0\) return false;/.test(toolbarUnlock),
  'an empty Area Types category must not be treated as an initialized toolbar graph');
check(/UpdateBefore<AreaToolbarUnlockSystem,\s*Game\.UI\.InGame\.ToolbarUISystem>\(SystemUpdatePhase\.UIUpdate\)/.test(mod),
  'AreaToolbarUnlockSystem must remain immediately before ToolbarUISystem in UIUpdate');

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

// Area Policy System (docs/superpowers/specs/2026-10-01-area-policy-system-design.md): finance, policy and budget guards.
const code = source => read(source).replace(/\/\/.*$/gm, '');
const parkSources = walk('src/CS2AreaSystem/Modules/Park').filter(p=>p.endsWith('.cs'));
for (const source of parkSources) {
  const text = code(source);
  // Park management is an Area expense applied in full even when city money is negative (design §13–15).
  if (/\bPayManagement\b|\bStoredCityMoney\b|\bCityMoneyLimit\b|\bkMaxMoney\b|\bApply(Expense|Income)\s*\(/.test(text))
    failures.push(`${source}: Park must not clamp or apply city money itself (no available-balance management clamp)`);
}
// Phase 2 C5: Park events change only mod-owned Park inputs; never vanilla attraction, leisure providers, destination choice
// or any vanilla component, and their expense is the ParkEvent Area Management kind.
const parkEventSources = parkSources.filter(p => /\/ParkEvent[A-Za-z]*\.cs$/.test(p));
check(parkEventSources.length >= 6, `Park event sources must exist (found ${parkEventSources.length})`);
for (const source of parkEventSources) {
  const text = code(source);
  if (/Attraction|LeisureProvider|Pathfind|Citizen|(Set|Add)ComponentData<\s*Game\.|GetComponentLookup<\s*Game\.[^>]*>\s*\(\s*false/.test(text))
    failures.push(`${source}: Park events must not touch vanilla attraction, leisure, citizens, pathfinding or vanilla components`);
}
check(/EnqueueExpense\(\s*parkId,\s*AreaFinanceKind\.ParkEvent,/.test(code('src/CS2AreaSystem/Modules/Park/ParkEventSystem.cs')), 'Park event cost must be queued as a ParkEvent Area Management expense');
// Phase 2 C6: only the coverage modifier writes ModifiedServiceCoverage, never m_Range, never prefab CoverageData or the
// coverage network; the writer is created disabled behind a closed runtime gate.
const allModSources = walk('src/CS2AreaSystem').filter(p => p.endsWith('.cs'));
const coverageWriters = ['src/CS2AreaSystem/Modules/Park/ParkCoverageModifierSystem.cs', 'src/CS2AreaSystem/Modules/Park/ParkCoverageModifierModel.cs', 'src/CS2AreaSystem/Modules/Park/ParkCoverageModifierSaveSystem.cs'];
for (const source of allModSources) {
  const text = code(source);
  const normalized = source.split(path.sep).join('/');
  if (/m_Range\s*(\*|\+|-)?=(?!=)/.test(text) && !(normalized.endsWith('ParkCoverageModifierModel.cs') && /m_Range = baseline\.m_Range[,\s]/.test(text) && (text.match(/m_Range\s*=(?!=)/g) || []).length === 1))
    failures.push(`${source}: coverage range (m_Range) must never be written`);
  if (/ModifiedServiceCoverage/.test(text) && !coverageWriters.includes(normalized))
    failures.push(`${source}: only the Park coverage modifier may use ModifiedServiceCoverage`);
  if (/(Set|Add)ComponentData<\s*(Game\.Prefabs\.)?CoverageData\s*>|ServiceCoverage>\s*\(\s*false|GetBuffer<\s*(Game\.Net\.)?ServiceCoverage\s*>/.test(text))
    failures.push(`${source}: prefab coverage data and the coverage network must not be modified`);
}
const coverageSystem = code('src/CS2AreaSystem/Modules/Park/ParkCoverageModifierSystem.cs');
check(/public const bool RuntimeVerified = true;/.test(coverageSystem), 'Park coverage modifier gate must be open after the in-game release gate passes');
check(/Enabled = ParkCoverageModifierGate\.RuntimeVerified;/.test(coverageSystem), 'Park coverage writer must be created disabled behind its runtime gate');
check(/ParkCoverageModifierModel\.IsFresh\(/.test(coverageSystem) && /GetChangeVersion\(ref coverageHandle\)/.test(coverageSystem), 'Park coverage writer must write only from a verified fresh native baseline');
check(/writer\.RestoreTracked\(\);/.test(code('src/CS2AreaSystem/Modules/Park/ParkCoverageModifierSaveSystem.cs')), 'Park coverage values must be restored before the native serializer');
const parkFinance = code('src/CS2AreaSystem/Modules/Park/ParkFinanceSystem.cs');
check(/EnqueueExpense\(\s*park\.StableId,\s*AreaFinanceKind\.ParkManagement,/.test(parkFinance), 'Park management must be queued as an Area Management expense');
check(/AreaFinanceHouseholdTransfer\.Transfer\(/.test(parkFinance) && /RecordSettledIncome\(/.test(code('src/CS2AreaSystem/Modules/Park/ParkTransactions.cs') + parkFinance),
  'Park admission must be a household-to-city transfer recorded once as Area Service Fees');
// Admission is its own Area row: never booked under a fixed vanilla income/expense source or player resource.
for (const source of [...parkSources, ...walk('src/CS2AreaSystem/Core/Finance').filter(p=>p.endsWith('.cs'))]) {
  const text = code(source);
  if (/\bIncomeSource\b|\bPlayerResource\b|\bStatisticType\.Income\b|\bFee(Parking|PublicTransport)\b/.test(text))
    failures.push(`${source}: Area income must not be mapped to a vanilla IncomeSource, PlayerResource or income statistic`);
}
const budgetUi = read('UI/src/AreaBudgetDetails.tsx').replace(/\/\/.*$/gm, '');
check(!/Fee(Parking|PublicTransport|Healthcare|Electricity|Education|Garbage|Water)|IncomeSource/.test(budgetUi), 'budget rows must not name or reuse a fixed vanilla fee source');
check(!/\btrigger\s*\(|from 'cs2\/api';[\s\S]*\btrigger\b/.test(budgetUi), 'budget rows must not send any game action');
check(budgetUi.includes("AREA_MANAGEMENT_SOURCE='CS2AS.AreaManagement'") && budgetUi.includes("AREA_SERVICE_FEES_SOURCE='CS2AS.AreaServiceFees'"), 'budget rows must use the localized Area sub-item ids');
check(uiIndex.includes('registerAreaBudget(registry)'), 'UI must register the budget rows through the module registry');
const budgetBinding = code('src/CS2AreaSystem/Core/Finance/AreaBudgetBindingSystem.cs');
check(!/PlayerMoney|Enqueue(Expense|Income)|RecordSettledIncome|ComponentLookup/.test(budgetBinding), 'the budget binding must only read settled amounts');
// Industrial policy effects reach scores only through vanilla state that observation reads (design §12).
for (const source of [...walk(`${industrialDir}/Policies`).filter(p=>p.endsWith('.cs')), `${industrialDir}/IndustrialPolicyEffectSystem.cs`, `${industrialDir}/IndustrialPolicyCatalog.cs`]) {
  const text = code(source);
  if (/\b(IndustrialAreaState|IndustrialRuntimeSnapshot|IndustrialPerformance\w*|IndustrialProduction\w*|IndustrialLogistics\w*|IndustrialEmployment\w*|IndustrialProgression\w*|IndustrialObservationAggregator)\b|\.\s*(Score|Performance|Production|Logistics|Employment|Efficiency)\s*=(?!=)/.test(text))
    failures.push(`${source}: Industrial policy code must not read or write Industrial scores`);
}
// Every policy has positive base upkeep (design §13); the constructor enforces it at runtime as well.
const upkeep = code('src/CS2AreaSystem/Core/Policies/AreaPolicyUpkeep.cs');
check(/!\(BaseDailyUpkeep > 0\)/.test(upkeep), 'AreaPolicyUpkeep must reject non-positive base upkeep');
const commonPolicies = code('src/CS2AreaSystem/Core/Policies/CommonAreaPolicies.cs');
const commonUpkeep = [...commonPolicies.matchAll(/new AreaPolicyUpkeep\(\s*([-\d.]+)/g)].map(m=>Number(m[1]));
check(commonUpkeep.length===1 && commonUpkeep.every(v=>v>0), `common policies must have positive base upkeep (found ${commonUpkeep})`);
for (const [catalog, count] of [['src/CS2AreaSystem/Modules/Park/ParkPolicyCatalog.cs', 6], [`${industrialDir}/IndustrialPolicyCatalog.cs`, 7]]) {
  const rows = [...code(catalog).matchAll(/Define\(AreaPolicyIds\.\w+,\s*"\w+",\s*"\w+",\s*\d+,\s*([-\d.]+)/g)].map(m=>Number(m[1]));
  check(rows.length===count && rows.every(v=>v>0), `${catalog}: all ${count} policies must have positive base upkeep (found ${rows})`);
}
// Tourism stays policy-free in this release.
for (const source of walk('src/CS2AreaSystem/Modules/Tourism').filter(p=>p.endsWith('.cs')))
  if (/AreaPolicy|IAreaPolicyProvider|PolicyDefinitions/.test(code(source))) failures.push(`${source}: Tourism must not register policies`);
for (const source of walk('src').filter(p=>p.endsWith('.cs') && !/[\\/](bin|obj)[\\/]/.test(p)))
  if (/new AreaPolicyDefinition\([^;]*AreaTypeId\.Tourism/s.test(code(source))) failures.push(`${source}: no policy may support the Tourism Area type`);
// Required policy/finance registrations and order (each isolated in try/catch).
for (const [pattern, what] of [
  [/UpdateAfter<CS2AreaSystem\.Core\.Policies\.AreaPolicySystem,\s*CS2AreaSystem\.Core\.Anchors\.AreaActivationSystem>\(SystemUpdatePhase\.ModificationEnd\)/, 'AreaPolicySystem after AreaActivationSystem (ModificationEnd)'],
  [/UpdateAfter<CS2AreaSystem\.Core\.Finance\.AreaFinanceSystem>\(SystemUpdatePhase\.GameSimulation\)/, 'AreaFinanceSystem after GameSimulation producers'],
  [/UpdateBefore<CS2AreaSystem\.Core\.Finance\.AreaFinanceSaveSystem,\s*Game\.Serialization\.SerializerSystem>\(SystemUpdatePhase\.Serialize\)/, 'Area finance flush before the serializer'],
  [/UpdateAt<CS2AreaSystem\.Core\.Policies\.AreaPolicyUpkeepSystem>\(SystemUpdatePhase\.GameSimulation\)/, 'policy upkeep as a GameSimulation producer'],
  [/UpdateBefore<CS2AreaSystem\.Modules\.Park\.ParkFinanceSaveSystem,\s*CS2AreaSystem\.Core\.Finance\.AreaFinanceSaveSystem>\(SystemUpdatePhase\.Serialize\)/, 'Park finance save before Area finance flush'],
  [/UpdateBefore<IndustrialPolicyEffectSystem,\s*IndustrialObservationSystem>\(SystemUpdatePhase\.GameSimulation\)|UpdateBefore<CS2AreaSystem\.Modules\.Industrial\.IndustrialPolicyEffectSystem,\s*CS2AreaSystem\.Modules\.Industrial\.IndustrialObservationSystem>\(SystemUpdatePhase\.GameSimulation\)/, 'Industrial policy effects before observation'],
  [/try\s*\{\s*updateSystem\.UpdateAfter<CS2AreaSystem\.Core\.Finance\.AreaBudgetBindingSystem>\(SystemUpdatePhase\.UIUpdate\);\s*\}\s*catch/, 'isolated Area budget binding (UIUpdate)']])
  check(pattern.test(mod), `Mod.cs must register ${what}`);
const tryBlocks = mod.match(/try\s*\{[^}]*?(AreaPolicySystem|AreaFinanceSystem|AreaPolicyUpkeepSystem)[^}]*\}\s*catch/g) ?? [];
check(tryBlocks.length >= 3, 'policy and finance registrations must each be isolated in try/catch');

const requirements = read('docs/verification/area-core-requirements.md');
check(!requirements.includes('Main is unchanged; all implementation commits are on feat/area-core'), 'verification docs must describe the current merged repository state');

const entranceSystem = code('src/CS2AreaSystem/Modules/Park/ParkEntranceSystem.cs');
const entranceEvidence = code('src/CS2AreaSystem/Modules/Park/ParkEntranceEvidence.cs');
check(entranceSystem.includes('HasComponent<PathwayData>'), 'automatic Park entrance classification must use PathwayData');
check(!/ISerializable|IBufferElementData|IComponentData/.test(entranceEvidence), 'path entrance evidence must remain runtime-only');
check(!/Pathfind|Citizen|GetEntityQuery\([^;]*ReadOnly<Game.Net.Edge>/s.test(entranceSystem.split('private ParkNetPage ReadPage')[1]??''), 'entrance query must be spatial, with no citizen/pathfinding changes');
check(/UpdateBefore<CS2AreaSystem.Modules.Park.ParkEntranceSystem,\s*CS2AreaSystem.Core.Anchors.AreaActivationSystem>/.test(mod), 'Park entrances must update before activation');

// Foundation + Phase 1: derived network diagnostics cannot become authoritative save data.
for (const module of ['Park', 'Industrial']) {
  const history = code(`src/CS2AreaSystem/Modules/${module}/${module}HistoryEntry.cs`);
  check(!/\bEntity\b|\bGame\.Net\b/.test(history), `${module} hourly history must not serialize runtime entity identities`);
}
for (const source of ['ParkPathEntranceDetector.cs', 'ParkEntranceEvidence.cs', 'ParkConnectivityModel.cs']) {
  check(!/ISerializable|\bSerialize\s*\(/.test(code(`src/CS2AreaSystem/Modules/Park/${source}`)), `${source}: derived path evidence must remain runtime-only`);
}
const connectivity = code('src/CS2AreaSystem/Modules/Park/ParkConnectivitySystem.cs');
check(!/SetComponentData|SetBuffer|AddComponent|RemoveComponent|DestroyEntity|EntityCommandBuffer/.test(connectivity), 'Park connectivity must only read the vanilla network');

if (failures.length) {
  console.error(`Offline contract checks failed (${failures.length}):`);
  for (const failure of failures) console.error(` - ${failure}`);
  process.exit(1);
}
console.log('Offline contract checks passed.');
