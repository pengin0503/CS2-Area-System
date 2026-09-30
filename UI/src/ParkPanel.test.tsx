import React from 'react';
import {render,screen,fireEvent,within} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import type {AreaSectionData,AreaPanelItem} from './bindings';
const env=vi.hoisted(()=>({trigger:vi.fn(),dict:{
 'CS2AS.Tab.Overview':'Overview','CS2AS.Tab.Visitors':'Visitors','CS2AS.Tab.Finances':'Finances','CS2AS.Tab.Landscape':'Landscape','CS2AS.Tab.Progress':'Progress',
 'CS2AS.Park.Level':'Park level','CS2AS.Park.LevelName.1':'Local Park','CS2AS.Park.LevelName.2':'Neighborhood Park','CS2AS.Park.Quality':'Park Quality',
 'CS2AS.Park.Metric.Satisfaction':'Visitor satisfaction','CS2AS.Park.Metric.Attractiveness':'Attractiveness','CS2AS.Park.Unavailable':'Unavailable (counted as neutral)',
 'CS2AS.Park.Warning.NotPrimary':'Overlaps another Park','CS2AS.Park.Warning.LowConfidence':'Some quality data is not available yet',
 'CS2AS.Park.ActiveVisitors':'Visitors now','CS2AS.Park.TouristVisitors':'Tourist visits','CS2AS.Park.ResidentVisitors':'Resident visits',
 'CS2AS.Park.Budget':'Park budget','CS2AS.Park.Fee':'Admission fee','CS2AS.Park.Fee.Locked':'Admission unlocks at Neighborhood Park (level 2).','CS2AS.Park.Free':'Free',
 'CS2AS.Park.Note.VanillaSeparate':'Normal upkeep and income of the buildings inside are not included here.','CS2AS.Park.RecentNet':'Net Area cash effect (last day)',
 'CS2AS.Park.Landscape':'Landscape score','CS2AS.Park.Vegetation':'Vegetation','CS2AS.Park.Check.Quality':'Park Quality','CS2AS.Park.Check.Visitors':'Total visits',
 'CS2AS.UI.Met':'Met','CS2AS.UI.NotMet':'Not met','CS2AS.UI.Type':'Type','CS2AS.UI.Status':'Status','CS2AS.UI.Entrances':'Entrances',
 'CS2AS.Park.Note.Prestige':'Landmark Park needs any two of the three Landmark objectives.'
} as Record<string,string>}));
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
const i=(over:Partial<AreaPanelItem>):AreaPanelItem=>({kind:'row',id:'x',labelKey:'',value:null,valueKey:null,numericValue:null,min:null,max:null,step:null,disabled:false,tooltipKey:null,action:null,...over});
// Shape mirrors ParkPanelBuilder output (C# ParkPanelBuilderTests pin the same ids/keys).
const park=(level:number):AreaSectionData=>({stableId:'42',typeKey:'CS2AS.Type.Park',state:'CS2AS.State.Active',level,experience:'2500',summary:'CS2AS.Park.Summary.Active',
 anchorRole:'Entrance',anchorName:'North Gate',anchors:[{key:'30.1',name:'North Gate'}],pendingAnchor:false,confirmDelete:true,tabs:[
 {id:'overview',labelKey:'CS2AS.Tab.Overview',items:[
  i({id:'level',labelKey:'CS2AS.Park.Level',valueKey:`CS2AS.Park.LevelName.${level}`}),
  i({kind:'bar',id:'quality',labelKey:'CS2AS.Park.Quality',numericValue:61.2,min:0,max:100,value:'61'}),
  i({kind:'bar',id:'satisfaction',labelKey:'CS2AS.Park.Metric.Satisfaction',numericValue:70,min:0,max:100,value:'70'}),
  i({id:'attractiveness',labelKey:'CS2AS.Park.Metric.Attractiveness',valueKey:'CS2AS.Park.Unavailable'}),
  i({id:'activeVisitors',labelKey:'CS2AS.Park.ActiveVisitors',value:'14'}),
  i({kind:'warning',id:'w1',labelKey:'CS2AS.Park.Warning.NotPrimary'}),
  i({kind:'warning',id:'w2',labelKey:'CS2AS.Park.Warning.LowConfidence'})]},
 {id:'visitors',labelKey:'CS2AS.Tab.Visitors',items:[i({id:'residentVisitors',labelKey:'CS2AS.Park.ResidentVisitors',value:'800'}),i({id:'touristVisitors',labelKey:'CS2AS.Park.TouristVisitors',value:'200'})]},
 {id:'finances',labelKey:'CS2AS.Tab.Finances',items:[
  i({kind:'slider',id:'budget',labelKey:'CS2AS.Park.Budget',action:'setBudget',numericValue:110,min:50,max:150,step:5,value:'110%'}),
  i({kind:'slider',id:'fee',labelKey:'CS2AS.Park.Fee',action:'setFee',numericValue:level>=2?20:0,min:0,max:60,step:1,value:level>=2?'¢20':null,valueKey:level>=2?null:'CS2AS.Park.Free',disabled:level<2,tooltipKey:level>=2?null:'CS2AS.Park.Fee.Locked'}),
  ...(level<2?[i({kind:'note',id:'feeLocked',labelKey:'CS2AS.Park.Fee.Locked'})]:[]),
  i({id:'recentNet',labelKey:'CS2AS.Park.RecentNet',value:'¢500'}),
  i({kind:'note',id:'vanillaSeparate',labelKey:'CS2AS.Park.Note.VanillaSeparate'})]},
 {id:'landscape',labelKey:'CS2AS.Tab.Landscape',items:[i({kind:'bar',id:'landscape',labelKey:'CS2AS.Park.Landscape',numericValue:55,min:0,max:100,value:'55'}),i({id:'vegetation',labelKey:'CS2AS.Park.Vegetation',value:'21.4 / 30'})]},
 {id:'progress',labelKey:'CS2AS.Tab.Progress',items:[
  i({kind:'check',id:'check.Quality',labelKey:'CS2AS.Park.Check.Quality',value:'61 / 55',numericValue:1}),
  i({kind:'check',id:'check.Visitors',labelKey:'CS2AS.Park.Check.Visitors',value:'1,000 / 1,500',numericValue:0}),
  i({kind:'note',id:'prestige',labelKey:'CS2AS.Park.Note.Prestige'})]}]});
const row=(label:string)=>screen.getAllByRole('row').find(r=>r.firstElementChild?.textContent===label)!;
const open=(tab:string)=>fireEvent.click(screen.getByRole('button',{name:tab}));
describe('Park management tabs',()=>{
 it('offers the five approved tabs',()=>{
  render(<AreaInfoSection {...park(2)}/>);
  expect(screen.getAllByRole('tab').map(t=>t.textContent)).toEqual(['Overview','Visitors','Finances','Landscape','Progress']);
 });
 it('overview shows level, quality, metrics, visitors and warnings including overlap and low confidence',()=>{
  render(<AreaInfoSection {...park(2)}/>);
  expect(row('Park level')).toHaveTextContent('Neighborhood Park');
  expect(screen.getByRole('progressbar',{name:'Park Quality'}).getAttribute('aria-valuenow')).toBe('61.2');
  expect(screen.getByRole('progressbar',{name:'Visitor satisfaction'})).toBeInTheDocument();
  expect(row('Attractiveness')).toHaveTextContent('Unavailable (counted as neutral)');
  expect(row('Visitors now')).toHaveTextContent('14');
  const alerts=screen.getAllByRole('alert').map(a=>a.textContent);
  expect(alerts).toContain('Overlaps another Park');expect(alerts).toContain('Some quality data is not available yet');
 });
 it('visitors tab splits residents and tourists',()=>{
  render(<AreaInfoSection {...park(2)}/>);open('Visitors');
  expect(row('Resident visits')).toHaveTextContent('800');expect(row('Tourist visits')).toHaveTextContent('200');
 });
 it('finances: budget works at level 1 while the fee stays locked, with the vanilla upkeep note',()=>{
  render(<AreaInfoSection {...park(1)}/>);open('Finances');
  expect(row('Admission fee')).toHaveTextContent('Free');
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain('Admission unlocks at Neighborhood Park (level 2).');
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain('Normal upkeep and income of the buildings inside are not included here.');
  const plus=screen.getAllByRole('button',{name:'+'});
  fireEvent.click(plus[1]);
  expect(env.trigger).not.toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','setFee',expect.anything());
  fireEvent.click(plus[0]);
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','setBudget','115');
  expect(row('Net Area cash effect (last day)')).toHaveTextContent('¢500');
 });
 it('finances: from level 2 the fee slider sends setFee',()=>{
  render(<AreaInfoSection {...park(2)}/>);open('Finances');
  fireEvent.click(screen.getAllByRole('button',{name:'+'})[1]);
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','42','setFee','21');
 });
 it('landscape tab shows the score and category contributions',()=>{
  render(<AreaInfoSection {...park(2)}/>);open('Landscape');
  expect(screen.getByRole('progressbar',{name:'Landscape score'})).toBeInTheDocument();
  expect(row('Vegetation')).toHaveTextContent('21.4 / 30');
 });
 it('progress tab shows the promotion checklist and landmark rule',()=>{
  render(<AreaInfoSection {...park(4)}/>);open('Progress');
  expect(row('Park Quality')).toHaveTextContent('61 / 55');expect(row('Park Quality')).toHaveTextContent('Met');
  expect(row('Total visits')).toHaveTextContent('Not met');
  expect(screen.getAllByRole('note').map(n=>n.textContent)).toContain('Landmark Park needs any two of the three Landmark objectives.');
 });
});
