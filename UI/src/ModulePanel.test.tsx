import React from 'react';
import {render,screen,fireEvent} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import type {AreaPanelItem} from './bindings';
const env=vi.hoisted(()=>({trigger:vi.fn(),slider:true,dict:{'M.Budget':'Budget','M.Quality':'Quality','M.Crowded':'Too crowded','M.Note':'Vanilla upkeep is separate.','M.Free':'Free','M.State':'State','M.Reset':'Reset fee','M.Visitors':'Visitors','M.Tip':'Quality tip'} as Record<string,string>}));
vi.mock('cs2/api',()=>({trigger:env.trigger,useValue:()=>null,bindValue:()=>({})}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:(key:string)=>env.dict[key]??null})}));
vi.mock('cs2/modding',()=>({getModule:(_path:string,name:string)=>{
 if(name==='Slider'){if(!env.slider)throw new Error('moved');
  return ({value,start,end,disabled,onChange,onDragStart,onDragEnd,valueTransformer}:{value:number;start:number;end:number;disabled?:boolean;onChange:(v:number)=>void;onDragStart?:()=>void;onDragEnd?:()=>void;valueTransformer:(s:number,e:number,r:number)=>number})=>
   <input type="range" aria-label="vanilla-slider" min={start} max={end} value={value} disabled={disabled}
    onChange={e=>{onDragStart?.();onChange(valueTransformer(start,end,(Number(e.target.value)-start)/(end-start)));onDragEnd?.();}}/>;}
 return ({left,right,tooltip}:{left?:React.ReactNode;right?:React.ReactNode;tooltip?:React.ReactNode})=><div role="row" data-tooltip={typeof tooltip==='string'?tooltip:undefined}><span>{left}</span><span>{right}</span></div>;
}}));
const {ModulePanel}=await import('./ModulePanel');
const {resetVanillaCache}=await import('./vanilla');
afterEach(()=>{env.trigger.mockClear();env.slider=true;resetVanillaCache();});
const item=(over:Partial<AreaPanelItem>):AreaPanelItem=>({kind:'row',id:'x',labelKey:'M.Visitors',value:null,valueKey:null,numericValue:null,min:null,max:null,step:null,disabled:false,tooltipKey:null,action:null,...over});
const row=(label:string)=>screen.getAllByRole('row').find(r=>r.firstElementChild?.textContent===label)!;
describe('generic module panel items',()=>{
 it('renders rows with raw or localized values and tooltips',()=>{
  render(<ModulePanel stableId="7" items={[item({value:'42'}),item({id:'s',labelKey:'M.State',valueKey:'M.Free',tooltipKey:'M.Tip'})]}/>);
  expect(row('Visitors')).toHaveTextContent('42');
  expect(row('State')).toHaveTextContent('Free');expect(row('State').dataset.tooltip).toBe('Quality tip');
 });
 it('renders bars as accessible progress bars',()=>{
  render(<ModulePanel stableId="7" items={[item({kind:'bar',id:'q',labelKey:'M.Quality',numericValue:64,min:0,max:100})]}/>);
  const bar=screen.getByRole('progressbar',{name:'Quality'});
  expect(bar.getAttribute('aria-valuenow')).toBe('64');expect(screen.getByText('64')).toBeInTheDocument();
 });
 it('renders warnings and notes',()=>{
  render(<ModulePanel stableId="7" items={[item({kind:'warning',id:'w',labelKey:'M.Crowded'}),item({kind:'note',id:'n',labelKey:'M.Note'})]}/>);
  expect(screen.getByRole('alert')).toHaveTextContent('Too crowded');
  expect(screen.getByRole('note')).toHaveTextContent('Vanilla upkeep is separate.');
 });
 it('sends slider changes as a guarded module action with the stable ID',()=>{
  render(<ModulePanel stableId="9007199254740993" items={[item({kind:'slider',id:'b',labelKey:'M.Budget',action:'setBudget',numericValue:100,min:50,max:150,step:5,value:'100%'})]}/>);
  expect(row('Budget')).toHaveTextContent('100%');
  fireEvent.change(screen.getByLabelText('vanilla-slider'),{target:{value:'122'}});
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','9007199254740993','setBudget','120');
 });
 it('falls back to step buttons when the vanilla slider is unavailable, and respects disabled',()=>{
  env.slider=false;
  const view=render(<ModulePanel stableId="7" items={[item({kind:'slider',id:'b',labelKey:'M.Budget',action:'setBudget',numericValue:100,min:50,max:150,step:5})]}/>);
  fireEvent.click(screen.getByRole('button',{name:'+'}));
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','moduleAction','7','setBudget','105');
  fireEvent.click(screen.getByRole('button',{name:'-'}));
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','moduleAction','7','setBudget','100');
  env.trigger.mockClear();
  view.rerender(<ModulePanel stableId="7" items={[item({kind:'slider',id:'b',labelKey:'M.Budget',action:'setBudget',numericValue:150,min:50,max:150,step:5,disabled:true})]}/>);
  fireEvent.click(screen.getByRole('button',{name:'+'}));fireEvent.click(screen.getByRole('button',{name:'-'}));
  expect(env.trigger).not.toHaveBeenCalled();
 });
 it('buttons send their action and value; disabled buttons do nothing',()=>{
  const view=render(<ModulePanel stableId="7" items={[item({kind:'button',id:'r',labelKey:'M.Reset',action:'setFee',value:'0'})]}/>);
  fireEvent.click(screen.getByRole('button',{name:'Reset fee'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','7','setFee','0');
  env.trigger.mockClear();
  view.rerender(<ModulePanel stableId="7" items={[item({kind:'button',id:'r',labelKey:'M.Reset',action:'setFee',value:'0',disabled:true})]}/>);
  fireEvent.click(screen.getByRole('button',{name:'Reset fee'}));
  expect(env.trigger).not.toHaveBeenCalled();
 });
 it('a diagnostic focus button sends the target key as the moduleAction value',()=>{
  env.dict['M.Focus']='Show facility';
  render(<ModulePanel stableId="12" items={[item({kind:'button',id:'focus.LowEfficiency',labelKey:'M.Focus',action:'focusDiagnosticTarget',value:'70.3'})]}/>);
  fireEvent.click(screen.getByRole('button',{name:'Show facility'}));
  expect(env.trigger).toHaveBeenCalledTimes(1);
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','moduleAction','12','focusDiagnosticTarget','70.3');
 });
 it('ignores unknown item kinds from newer modules',()=>{
  expect(()=>render(<ModulePanel stableId="7" items={[item({kind:'chart',id:'c'})]}/>)).not.toThrow();
 });
});
