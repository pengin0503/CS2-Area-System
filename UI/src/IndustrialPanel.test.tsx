import React from 'react';
import {render,screen,fireEvent} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import type {AreaSectionData,AreaPanelItem} from './bindings';
// Generated from the real C# payload (IndustrialPanelFixtureTests); rerun the C# tests with UPDATE_UI_FIXTURES=1 after changes.
import fixture from './__fixtures__/industrial-panel.json';
const env=vi.hoisted(()=>({trigger:vi.fn(),dict:{} as Record<string,string>}));
env.dict=fixture.enUS as Record<string,string>;
vi.mock('cs2/api',()=>({trigger:env.trigger,useValue:()=>null,bindValue:()=>({})}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:(key:string)=>env.dict[key]??null})}));
vi.mock('cs2/modding',()=>({getModule:(_p:string,name:string)=>{
 if(name==='Slider')throw new Error('no sliders in the Industrial panel');
 if(name==='InfoSection')return ({children}:React.PropsWithChildren)=><section>{children}</section>;
 return ({left,right}:{left?:React.ReactNode;right?:React.ReactNode})=><div role="row"><span>{left}</span><span>{right}</span></div>;
}}));
const {AreaInfoSection}=await import('./AreaInfoSection');
const {resetVanillaCache}=await import('./vanilla');
afterEach(()=>{env.trigger.mockClear();resetVanillaCache();});
type Name='active'|'suspended'|'paused'|'unavailable'|'overlapping'|'maxLevel';
const section=(name:Name)=>fixture.sections[name] as unknown as AreaSectionData;
const t=(key:string)=>env.dict[key]??key;
const item=(name:Name,tab:string,id:string)=>section(name).tabs.find(x=>x.id===tab)!.items!.find(x=>x.id===id) as AreaPanelItem;
const row=(label:string)=>screen.getAllByRole('row').find(r=>r.firstElementChild?.textContent===label)!;
const open=(key:string)=>fireEvent.click(screen.getByRole('button',{name:t(key)}));
describe('Industrial management tabs (real C# payload)',()=>{
 it('assigns and removes ownership with the rendered stable ID and opaque company key',()=>{
  render(<AreaInfoSection {...section('active')}/>);open('CS2AS.Tab.Companies');
  const companies=section('active').tabs.find(tab=>tab.id==='companies')!.items!;
  const assign=companies.find(item=>item.action==='assignCompanyHere'&&!item.disabled)!;
  fireEvent.click(screen.getAllByRole('button',{name:t('CS2AS.Industrial.Company.Assign')})[0]);
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','moduleAction','42','assignCompanyHere',assign.value);
  const clear=companies.find(item=>item.action==='clearCompanyAssignment')!;
  fireEvent.click(screen.getByRole('button',{name:t('CS2AS.Industrial.Company.Clear')}));
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','moduleAction','42','clearCompanyAssignment',clear.value);
  expect(screen.getByText(t('CS2AS.Industrial.Company.Automatic'))).toBeInTheDocument();
  expect(screen.getByText(t('CS2AS.Industrial.Company.Manual'))).toBeInTheDocument();
 });
 it('offers exactly Overview | Production | Logistics | Employment | Progress | Comparison and hides the duplicate core level rows',()=>{
  render(<AreaInfoSection {...section('active')}/>);
  expect(screen.getAllByRole('tab').map(x=>x.textContent)).toEqual(['CS2AS.Tab.Overview','CS2AS.Tab.Production','CS2AS.Tab.Logistics','CS2AS.Tab.Employment','CS2AS.Tab.Progress','CS2AS.Tab.Comparison','CS2AS.Tab.Companies'].map(t));
  expect(screen.queryByText(t('CS2AS.UI.Level'))).toBeNull();
  expect(row(t('CS2AS.UI.Headquarters'))).toHaveTextContent('Riverside Steelworks');
 });
 it('overview shows level, performance, the four component bars, facilities, jobs and last update',()=>{
  render(<AreaInfoSection {...section('active')}/>);
  expect(row(t('CS2AS.Industrial.Level'))).toHaveTextContent(t('CS2AS.Industrial.LevelName.2'));
  expect(screen.getByRole('progressbar',{name:t('CS2AS.Industrial.Metric.Performance')}).getAttribute('aria-valuenow')).toBe(String(item('active','overview','performance').numericValue));
  for(const metric of ['Production','Logistics','Employment','Efficiency'])
   expect(screen.getByRole('progressbar',{name:t(`CS2AS.Industrial.Metric.${metric}`)})).toBeInTheDocument();
  expect(row(t('CS2AS.Industrial.ActiveFacilities'))).toHaveTextContent(item('active','overview','activeFacilities').value!);
  expect(row(t('CS2AS.Industrial.EmployeesJobs'))).toHaveTextContent(item('active','overview','employees').value!);
  expect(row(t('CS2AS.Industrial.LastUpdate'))).toHaveTextContent(item('active','overview','lastUpdate').value!);
 });
 it('unavailable and warming-up metrics are labeled, never drawn as zero bars',()=>{
  render(<AreaInfoSection {...section('unavailable')}/>);
  expect(row(t('CS2AS.Industrial.Metric.Production'))).toHaveTextContent(t('CS2AS.Industrial.Status.Unavailable'));
  expect(row(t('CS2AS.Industrial.Metric.Logistics'))).toHaveTextContent(t('CS2AS.Industrial.Status.WarmingUp'));
  expect(screen.queryByRole('progressbar',{name:t('CS2AS.Industrial.Metric.Production')})).toBeNull();
  expect(screen.queryByRole('progressbar',{name:t('CS2AS.Industrial.Metric.Logistics')})).toBeNull();
 });
 it('bottlenecks show evidence and a suggestion, and overlap is reported',()=>{
  render(<AreaInfoSection {...section('overlapping')}/>);
  const alerts=screen.getAllByRole('alert').map(a=>a.textContent);
  const first=section('overlapping').tabs[0].items!.find(i=>i.kind==='warning'&&i.id.startsWith('bottleneck.'))!;
  expect(alerts).toContain(`${t(first.labelKey)} ${first.value}`);
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain(t('CS2AS.Industrial.Suggestion.'+first.id.substring('bottleneck.'.length)));
  expect(row(t('CS2AS.Industrial.OverlapMembers'))).toHaveTextContent('3');
 });
 it('a suspended Area without headquarters explains why it is waiting',()=>{
  render(<AreaInfoSection {...section('suspended')}/>);
  expect(screen.getAllByRole('alert').map(a=>a.textContent)).toContain(t('CS2AS.Industrial.Warning.HeadquartersRequired'));
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain(t('CS2AS.Industrial.Summary.HeadquartersRequired'));
  expect(row(t('CS2AS.Industrial.Metric.Performance'))).toHaveTextContent(t('CS2AS.Industrial.Calculating'));
 });
 it('a paused snapshot is marked stale and draws no current score bars',()=>{
  render(<AreaInfoSection {...section('paused')}/>);
  expect(screen.getAllByRole('alert').map(a=>a.textContent)).toContain(t('CS2AS.Industrial.Warning.Paused'));
  const bars=screen.queryAllByRole('progressbar').map(b=>b.getAttribute('aria-label'));
  expect(bars).toEqual([t('CS2AS.Industrial.Experience')]);
 });
 it('production tab labels sampled throughput and shows resource rows with vanilla titles',()=>{
  render(<AreaInfoSection {...section('active')}/>);open('CS2AS.Tab.Production');
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain(t('CS2AS.Industrial.Note.Sampled'));
  expect(row(t('CS2AS.Industrial.Processors'))).toHaveTextContent(item('active','production','processors').value!);
  // Resource titles are game localization (Resources.TITLE[...]); the test dictionary only holds mod keys.
  expect(item('active','production','resource.Furniture').labelKey).toBe('Resources.TITLE[Furniture]');
 });
 it('logistics tab shows storage, cargo, flow continuity and the no-placement-bonus note',()=>{
  render(<AreaInfoSection {...section('active')}/>);open('CS2AS.Tab.Logistics');
  expect(row(t('CS2AS.Industrial.StorageUtilization'))).toHaveTextContent(item('active','logistics','storageUtilization').value!);
  expect(screen.getByRole('progressbar',{name:t('CS2AS.Industrial.Metric.InputContinuity')})).toBeInTheDocument();
  expect(screen.getByRole('progressbar',{name:t('CS2AS.Industrial.Metric.OutputContinuity')})).toBeInTheDocument();
  const notes=screen.getAllByRole('note').map(n=>n.textContent);
  expect(notes).toContain(t('CS2AS.Industrial.Note.NoPlacementBonus'));expect(notes).toContain(t('CS2AS.Industrial.Note.ExternalCargo'));
 });
 it('employment tab shows jobs, vacancies, fill and education-level vacancies',()=>{
  render(<AreaInfoSection {...section('active')}/>);open('CS2AS.Tab.Employment');
  expect(row(t('CS2AS.Industrial.Vacancies'))).toHaveTextContent(item('active','employment','vacancies').value!);
  expect(screen.getByRole('progressbar',{name:t('CS2AS.Industrial.Metric.JobFill')})).toBeInTheDocument();
  expect(row(t('CS2AS.Industrial.Vacancy.0'))).toHaveTextContent(item('active','employment','vacancy.0').value!);
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain(t('CS2AS.Industrial.Note.OfficesExcluded'));
 });
 it('progress tab shows gate checks, XP cap, streak coverage and the no-buff note',()=>{
  render(<AreaInfoSection {...section('active')}/>);open('CS2AS.Tab.Progress');
  const performance=item('active','progress','check.Performance');
  expect(row(t('CS2AS.Industrial.Check.Performance'))).toHaveTextContent(t(performance.numericValue===1?'CS2AS.UI.Met':'CS2AS.UI.NotMet'));
  expect(row(t('CS2AS.Industrial.DailyXp'))).toHaveTextContent('120 / 240');
  expect(row(t('CS2AS.Industrial.DayHours'))).toHaveTextContent('/ 18.0 h');
  expect(row(t('CS2AS.Industrial.LastDay'))).toHaveTextContent(t('CS2AS.Industrial.Day.InsufficientCoverage'));
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain(t('CS2AS.Industrial.Note.NoBuff'));
 });
 it('the daily cap reason is visible when XP is blocked',()=>{
  render(<AreaInfoSection {...section('overlapping')}/>);open('CS2AS.Tab.Progress');
  expect(screen.getAllByRole('alert').map(a=>a.textContent)).toContain(t('CS2AS.Industrial.XpBlocked.DailyCap'));
 });
 it('max level shows no further gates',()=>{
  render(<AreaInfoSection {...section('maxLevel')}/>);open('CS2AS.Tab.Progress');
  expect(row(t('CS2AS.Industrial.MaxLevel'))).toHaveTextContent(t('CS2AS.Industrial.LevelName.5'));
 });
 it('offers no Industrial mutation: the only module action is the read-only bottleneck focus button',()=>{
  for(const name of ['active','paused','overlapping'] as Name[]){
   const {unmount}=render(<AreaInfoSection {...section(name)}/>);
   for(const tab of ['CS2AS.Tab.Production','CS2AS.Tab.Logistics','CS2AS.Tab.Employment','CS2AS.Tab.Progress']){
    open(tab);
    expect(screen.queryAllByRole('button',{name:'+'})).toEqual([]);
   }
   unmount();
  }
  expect(env.trigger).not.toHaveBeenCalledWith('cs2AreaSystem','moduleAction',expect.anything(),expect.anything(),expect.anything());
  const items=Object.values(fixture.sections).flatMap(s=>(s as unknown as AreaSectionData).tabs.flatMap(tab=>tab.items??[]));
  const actions=items.filter(i=>i.action!==null||i.kind==='slider'||i.kind==='button');
  expect(actions.length).toBeGreaterThan(0);
  expect(actions.every(i=>i.kind==='button'&&['focusDiagnosticTarget','assignCompanyHere','clearCompanyAssignment'].includes(i.action!)&&!!i.value)).toBe(true);
 });
 it('production tab lists each observed recipe as input and output rows with its measured rate',()=>{
  render(<AreaInfoSection {...section('active')}/>);open('CS2AS.Tab.Production');
  // Resource titles are game localization (Resources.TITLE[...]), absent from the test dictionary; rows are found by role text.
  const roles=(key:string)=>screen.getAllByRole('row').filter(r=>r.lastElementChild?.textContent===t(key)).length;
  expect(roles('CS2AS.Industrial.Process.Input')).toBe(5);
  expect(roles('CS2AS.Industrial.Process.Output')).toBe(3);
  expect(item('active','production','process.1.input.1').labelKey).toBe('Resources.TITLE[Ore]');
  const rates=screen.getAllByRole('row').filter(r=>r.firstElementChild?.textContent===t('CS2AS.Industrial.Process.ActualPerDay')).map(r=>r.lastElementChild?.textContent);
  expect(rates).toEqual([item('active','production','process.0.actual').value,item('active','production','process.1.actual').value,t('CS2AS.Industrial.Status.Unavailable')]);
 });
 it('a bottleneck focus button sends its opaque target key through the guarded module action',()=>{
  render(<AreaInfoSection {...section('overlapping')}/>);
  const button=section('overlapping').tabs[0].items!.find(i=>i.kind==='button')!;
  fireEvent.click(screen.getAllByRole('button',{name:t(button.labelKey)})[0]);
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','focusDiagnosticTarget',button.value);
 });
 it('every mod key the payload uses is localized',()=>{
  const keys=new Set<string>();
  for(const s of Object.values(fixture.sections)){
   const data=s as unknown as AreaSectionData;keys.add(data.summary);keys.add(data.typeKey);keys.add(data.state);
   for(const tab of data.tabs){keys.add(tab.labelKey);for(const i of tab.items??[]){keys.add(i.labelKey);if(i.valueKey)keys.add(i.valueKey);if(i.tooltipKey)keys.add(i.tooltipKey);}}
  }
  expect([...keys].filter(k=>!k.startsWith('Resources.TITLE[')&&!(k in env.dict))).toEqual([]);
 });
});
