import React from 'react';
import {render,screen,fireEvent,within} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import type {AreaSectionData,AreaPanelItem} from './bindings';
// Generated from the real C# payload (ParkPanelFixtureTests); rerun the C# tests with UPDATE_UI_FIXTURES=1 after changes.
import fixture from './__fixtures__/park-panel.json';
const env=vi.hoisted(()=>({trigger:vi.fn(),dict:{} as Record<string,string>}));
env.dict=fixture.enUS as Record<string,string>;
vi.mock('cs2/api',()=>({trigger:env.trigger,useValue:()=>null,bindValue:()=>({})}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:(key:string)=>env.dict[key]??null})}));
vi.mock('cs2/modding',()=>({getModule:(_p:string,name:string)=>{
 if(name==='Slider')throw new Error('use step buttons');
 if(name==='InfoSection')return ({children}:React.PropsWithChildren)=><section>{children}</section>;
 return ({left,right}:{left?:React.ReactNode;right?:React.ReactNode})=><div role="row"><span>{left}</span><span>{right}</span></div>;
}}));
const {AreaInfoSection}=await import('./AreaInfoSection');
const {resetVanillaCache}=await import('./vanilla');
afterEach(()=>{env.trigger.mockClear();resetVanillaCache();});
type Name='level1'|'level2'|'level4'|'unscanned'|'suspended';
const section=(name:Name)=>fixture.sections[name] as unknown as AreaSectionData;
const t=(key:string)=>env.dict[key]??key;
const item=(name:Name,tab:string,id:string)=>section(name).tabs.find(x=>x.id===tab)!.items!.find(x=>x.id===id) as AreaPanelItem;
const row=(label:string)=>screen.getAllByRole('row').find(r=>r.firstElementChild?.textContent===label)!;
const open=(key:string)=>fireEvent.click(screen.getByRole('button',{name:t(key)}));
describe('Park management tabs (real C# payload)',()=>{
 it('offers the five approved tabs and hides the duplicate core level rows',()=>{
  render(<AreaInfoSection {...section('level2')}/>);
  expect(screen.getAllByRole('tab').map(x=>x.textContent)).toEqual(['CS2AS.Tab.Overview','CS2AS.Tab.Visitors','CS2AS.Tab.Finances','CS2AS.Tab.Landscape','CS2AS.Tab.Progress'].map(t));
  expect(screen.queryByText(t('CS2AS.UI.Level'))).toBeNull();
 });
 it('overview shows level name, quality, six metrics and the overlap warning',()=>{
  render(<AreaInfoSection {...section('level2')}/>);
  expect(row(t('CS2AS.Park.Level'))).toHaveTextContent(t('CS2AS.Park.LevelName.2'));
  expect(screen.getByRole('progressbar',{name:t('CS2AS.Park.Quality')}).getAttribute('aria-valuenow')).toBe(String(item('level2','overview','quality').numericValue));
  for(const metric of ['Satisfaction','Attractiveness','Accessibility','Variety','Maintenance','Comfort'])
   expect(screen.getByRole('progressbar',{name:t(`CS2AS.Park.Metric.${metric}`)})).toBeInTheDocument();
  expect(screen.getAllByRole('alert').map(a=>a.textContent)).toContain(t('CS2AS.Park.Warning.NotPrimary'));
 });
 it('shows unavailable metrics as neutral with a low-confidence warning before the first landscape scan',()=>{
  render(<AreaInfoSection {...section('unscanned')}/>);
  expect(row(t('CS2AS.Park.Metric.Attractiveness'))).toHaveTextContent(t('CS2AS.Park.Unavailable'));
  expect(screen.getAllByRole('alert').map(a=>a.textContent)).toContain(t('CS2AS.Park.Warning.LowConfidence'));
 });
 it('lists valid and ineligible entrances',()=>{
  render(<AreaInfoSection {...section('level2')}/>);
  const items=screen.getAllByRole('listitem');
  expect(items[1]).toHaveTextContent(t('CS2AS.UI.AnchorInvalid'));
  fireEvent.click(within(items[1]).getByRole('button',{name:t('CS2AS.UI.RemoveEntrance')}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','removeAnchor','42','31.1');
 });
 it('visitors tab splits residents and tourists',()=>{
  render(<AreaInfoSection {...section('level2')}/>);open('CS2AS.Tab.Visitors');
  expect(row(t('CS2AS.Park.ResidentVisitors'))).toHaveTextContent(item('level2','visitors','residentVisitors').value!);
  expect(row(t('CS2AS.Park.TouristVisitors'))).toHaveTextContent(item('level2','visitors','touristVisitors').value!);
 });
 it('finances at level 1: budget works, fee locked, vanilla upkeep note shown',()=>{
  render(<AreaInfoSection {...section('level1')}/>);open('CS2AS.Tab.Finances');
  expect(row(t('CS2AS.Park.Fee'))).toHaveTextContent(t('CS2AS.Park.Free'));
  const notes=screen.getAllByRole('note').map(n=>n.textContent);
  expect(notes).toContain(t('CS2AS.Park.Fee.Locked'));expect(notes).toContain(t('CS2AS.Park.Note.VanillaSeparate'));
  const [budgetPlus,feePlus]=screen.getAllByRole('button',{name:'+'});
  fireEvent.click(feePlus);
  expect(env.trigger).not.toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','setFee',expect.anything());
  fireEvent.click(budgetPlus);
  const budget=item('level1','finances','budget');
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','setBudget',String(budget.numericValue!+budget.step!));
 });
 it('finances from level 2: the fee slider sends setFee and unpaid admission is shown',()=>{
  render(<AreaInfoSection {...section('level2')}/>);open('CS2AS.Tab.Finances');
  fireEvent.click(screen.getAllByRole('button',{name:'+'})[1]);
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','setFee',String(item('level2','finances','fee').numericValue!+1));
  expect(row(t('CS2AS.Park.RecentUncollected'))).toHaveTextContent(item('level2','finances','recentUncollected').value!);
 });
 it('landscape tab shows the score and contributions',()=>{
  render(<AreaInfoSection {...section('level2')}/>);open('CS2AS.Tab.Landscape');
  expect(screen.getByRole('progressbar',{name:t('CS2AS.Park.Landscape')})).toBeInTheDocument();
  expect(row(t('CS2AS.Park.Vegetation'))).toHaveTextContent(item('level2','landscape','vegetation').value!);
 });
 it('progress tab shows the checklist with met/not met and the Landmark rule',()=>{
  render(<AreaInfoSection {...section('level4')}/>);open('CS2AS.Tab.Progress');
  const quality=item('level4','progress','check.Quality');
  expect(row(t('CS2AS.Park.Check.Quality'))).toHaveTextContent(t(quality.numericValue===1?'CS2AS.UI.Met':'CS2AS.UI.NotMet'));
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain(t('CS2AS.Park.Note.Prestige'));
 });
 it('a suspended Park without quality data explains the missing entrance',()=>{
  render(<AreaInfoSection {...section('suspended')}/>);
  expect(screen.getAllByRole('alert').map(a=>a.textContent)).toContain(t('CS2AS.Park.Warning.NoEntrance'));
  expect(row(t('CS2AS.Park.Quality'))).toHaveTextContent(t('CS2AS.Park.Calculating'));
 });
 it('every key the payload uses is localized',()=>{
  const keys=new Set<string>();
  for(const s of Object.values(fixture.sections)){
   const data=s as unknown as AreaSectionData;keys.add(data.summary);
   for(const tab of data.tabs){keys.add(tab.labelKey);for(const i of tab.items??[]){keys.add(i.labelKey);if(i.valueKey)keys.add(i.valueKey);if(i.tooltipKey)keys.add(i.tooltipKey);}}
  }
  expect([...keys].filter(k=>!(k in env.dict))).toEqual([]);
 });
});
