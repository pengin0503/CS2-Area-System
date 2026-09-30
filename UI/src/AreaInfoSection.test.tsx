import React,{createContext,useContext} from 'react';
import {render,screen,fireEvent,within} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import type {AreaSectionData} from './bindings';
const env=vi.hoisted(()=>({trigger:vi.fn(),registryFails:false,dictionaries:{
 'en-US':{'CS2AS.UI.Type':'Type','CS2AS.UI.Status':'Status','CS2AS.UI.Headquarters':'Headquarters','CS2AS.UI.Entrance':'Entrance','CS2AS.UI.NotAssigned':'Not assigned',
  'CS2AS.Type.Industrial':'Industrial Area','CS2AS.Type.Park':'Park Area','CS2AS.State.Suspended':'Suspended','CS2AS.State.Active':'Active',
  'CS2AS.Summary.AnchorRequired':'Assign a required facility to activate this area.','CS2AS.UI.AssignHeadquarters':'Assign headquarters','CS2AS.UI.ChangeHeadquarters':'Change headquarters',
  'CS2AS.UI.AssignEntrance':'Assign entrance','CS2AS.UI.SelectAnchor':'Select an existing building inside this area.','CS2AS.UI.CancelAssignment':'Cancel assignment',
  'CS2AS.UI.EditArea':'Edit area','CS2AS.UI.DeleteArea':'Delete area','CS2AS.UI.DeleteConfirm':'Delete this management boundary?','CS2AS.UI.ConfirmDelete':'Confirm delete','CS2AS.UI.Cancel':'Cancel',
  'CS2AS.UI.Level':'Level','CS2AS.UI.Entrances':'Entrances','CS2AS.UI.AddEntrance':'Add entrance','CS2AS.UI.RemoveEntrance':'Remove','CS2AS.UI.AnchorInvalid':'Not eligible','CS2AS.UI.AnchorRemoved':'Building removed','M.Visitors':'Visitors','M.Budget':'Budget','Module.Tab.Finances':'Finances','CS2AS.UI.Experience':'Experience','Module.Tab.Visitors':'Visitors'},
 'ja-JP':{'CS2AS.UI.Type':'種類','CS2AS.UI.Status':'状態','CS2AS.UI.Entrance':'入口','CS2AS.UI.NotAssigned':'未割り当て','CS2AS.Type.Park':'公園エリア','CS2AS.State.Suspended':'停止中',
  'CS2AS.UI.AssignEntrance':'入口を割り当て','Module.Tab.Visitors':'来園者'}
} as Record<string,Record<string,string>>}));
const Locale=createContext('en-US');
vi.mock('cs2/api',()=>({trigger:env.trigger,useValue:()=>null,bindValue:()=>({})}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>{const locale=useContext(Locale);return {translate:(key:string)=>env.dictionaries[locale]?.[key]??null};}}));
vi.mock('cs2/modding',()=>({getModule:(_path:string,name:string)=>{
 if(env.registryFails)throw new Error('module moved');
 return name==='InfoSection'?({children}:React.PropsWithChildren)=><section data-vanilla="InfoSection">{children}</section>
  :({left,right,tooltip}:{left?:React.ReactNode;right?:React.ReactNode;tooltip?:React.ReactNode})=><div role="row" data-tooltip={typeof tooltip==='string'?tooltip:undefined}><span>{left}</span><span>{right}</span></div>;
}}));
const {AreaInfoSection,AREA_INFO_SECTION_TYPE}=await import('./AreaInfoSection');
const {resetVanillaCache}=await import('./vanilla');
const industrial:AreaSectionData={stableId:'9007199254740993',typeKey:'CS2AS.Type.Industrial',state:'CS2AS.State.Suspended',level:null,experience:null,
 summary:'CS2AS.Summary.AnchorRequired',anchorRole:'Headquarters',anchorName:null,pendingAnchor:false,confirmDelete:true,tabs:[{id:'overview',labelKey:'CS2AS.Tab.Overview'}]};
const row=(label:string)=>screen.getAllByRole('row').find(r=>r.firstElementChild?.textContent===label)!;
afterEach(()=>{env.trigger.mockClear();env.registryFails=false;resetVanillaCache();});
describe('managed area selected-info section',()=>{
 it('matches the C# section type name used as __Type',()=>expect(AREA_INFO_SECTION_TYPE).toBe('CS2AreaSystem.Core.UI.AreaInfoSection'));
 it('renders district-style rows inside the vanilla InfoSection',()=>{
  const view=render(<AreaInfoSection {...industrial}/>);
  expect(view.container.querySelector('[data-vanilla="InfoSection"]')).not.toBeNull();
  expect(row('Type')).toHaveTextContent('Industrial Area');
  expect(row('Status')).toHaveTextContent('Suspended');
  expect(row('Status').dataset.tooltip).toBe('Assign a required facility to activate this area.');
  expect(row('Headquarters')).toHaveTextContent('Not assigned');
  expect(screen.queryByRole('tablist')).toBeNull();
 });
 it('shows the assigned anchor and offers to change it',()=>{
  render(<AreaInfoSection {...industrial} anchorName="Harbor Offices" state="CS2AS.State.Active"/>);
  expect(row('Headquarters')).toHaveTextContent('Harbor Offices');
  fireEvent.click(screen.getByRole('button',{name:'Change headquarters'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','assignHeadquarters','9007199254740993');
 });
 it('shows progress rows only when the module supports progress',()=>{
  render(<AreaInfoSection {...industrial} level={3} experience="1200"/>);
  expect(row('Level')).toHaveTextContent('3');expect(row('Experience')).toHaveTextContent('1200');
 });
 it('guides and cancels a pending anchor assignment',()=>{
  render(<AreaInfoSection {...industrial} pendingAnchor={true}/>);
  expect(screen.getByRole('status')).toHaveTextContent('Select an existing building inside this area.');
  expect(screen.queryByRole('button',{name:'Assign headquarters'})).toBeNull();
  fireEvent.click(screen.getByRole('button',{name:'Cancel assignment'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','cancelAnchorAssignment','9007199254740993');
 });
 it('edits and deletes with confirmation by default',()=>{
  render(<AreaInfoSection {...industrial}/>);
  fireEvent.click(screen.getByRole('button',{name:'Edit area'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','editArea','9007199254740993');
  fireEvent.click(screen.getByRole('button',{name:'Delete area'}));
  expect(env.trigger).not.toHaveBeenCalledWith('cs2AreaSystem','deleteArea','9007199254740993');
  fireEvent.click(screen.getByRole('button',{name:'Confirm delete'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','deleteArea','9007199254740993');
 });
 it('deletes immediately when the confirmation option is off',()=>{
  render(<AreaInfoSection {...industrial} confirmDelete={false}/>);
  fireEvent.click(screen.getByRole('button',{name:'Delete area'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','deleteArea','9007199254740993');
 });
 it('shows module tabs only when there is more than one, with readable fallbacks',()=>{
  render(<AreaInfoSection {...industrial} tabs={[{id:'visitors',labelKey:'Module.Tab.Visitors'},{id:'queues',labelKey:'Module.Tab.RideQueues'}]}/>);
  expect(screen.getAllByRole('tab').map(t=>t.textContent)).toEqual(['Visitors','Ride queues']);
 });
 it('reflects a runtime locale change',()=>{
  const park={...industrial,typeKey:'CS2AS.Type.Park',anchorRole:'Entrance' as const};
  const view=render(<Locale.Provider value="en-US"><AreaInfoSection {...park}/></Locale.Provider>);
  expect(row('Type')).toHaveTextContent('Park Area');
  view.rerender(<Locale.Provider value="ja-JP"><AreaInfoSection {...park}/></Locale.Provider>);
  expect(row('種類')).toHaveTextContent('公園エリア');expect(row('入口')).toHaveTextContent('未割り当て');
  expect(screen.getByRole('button',{name:'入口を割り当て'})).toBeInTheDocument();
 });
 it('falls back to plain rows if the vanilla modules cannot be found',()=>{
  env.registryFails=true;
  const view=render(<AreaInfoSection {...industrial}/>);
  expect(view.container.querySelector('[data-vanilla]')).toBeNull();
  expect(screen.getByText('Industrial Area')).toBeInTheDocument();
 });
 it('contains its own failures so the vanilla panel keeps working',()=>{
  const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});
  const broken={...industrial,tabs:undefined as unknown as AreaSectionData['tabs']};
  expect(()=>render(<AreaInfoSection {...broken}/>)).not.toThrow();
  expect(env.trigger).not.toHaveBeenCalled();consoleError.mockRestore();
 });
 it('lists every entrance with remove actions keyed by the opaque link key',()=>{
  const park={...industrial,typeKey:'CS2AS.Type.Park',state:'CS2AS.State.Active',anchorRole:'Entrance' as const,anchorName:'North Gate',
   anchors:[{key:'30.1',name:'North Gate',valid:true},{key:'31.4',name:'South Gate',valid:true}]};
  render(<AreaInfoSection {...park}/>);
  expect(row('Entrances')).toHaveTextContent('2');
  expect(screen.getByText('North Gate')).toBeInTheDocument();expect(screen.getByText('South Gate')).toBeInTheDocument();
  fireEvent.click(screen.getAllByRole('button',{name:'Remove'})[1]);
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','removeAnchor','9007199254740993','31.4');
  fireEvent.click(screen.getByRole('button',{name:'Add entrance'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','assignEntrance','9007199254740993');
 });
 it('renders structured items of the selected module tab',()=>{
  const item=(over:object)=>({kind:'row',id:'x',labelKey:'M.Visitors',value:'12',valueKey:null,numericValue:null,min:null,max:null,step:null,disabled:false,tooltipKey:null,action:null,...over});
  const tabs=[{id:'overview',labelKey:'Module.Tab.Visitors',items:[item({})]},{id:'finances',labelKey:'Module.Tab.Finances',items:[item({id:'b',labelKey:'M.Budget',value:'100%'})]}];
  render(<AreaInfoSection {...industrial} tabs={tabs}/>);
  expect(row('Visitors')).toHaveTextContent('12');expect(screen.queryByText('Budget')).toBeNull();
  fireEvent.click(screen.getByRole('button',{name:'Finances'}));
  expect(row('Budget')).toHaveTextContent('100%');expect(screen.queryByText('12')).toBeNull();
 });
 it('keeps core rows and actions when a module tab fails to render',()=>{
  const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});
  const tabs=[{id:'overview',labelKey:'Module.Tab.Visitors',items:[null as unknown as never]}];
  render(<AreaInfoSection {...industrial} tabs={tabs}/>);
  expect(row('Type')).toHaveTextContent('Industrial Area');
  expect(screen.getByRole('button',{name:'Edit area'})).toBeInTheDocument();
  consoleError.mockRestore();
 });
 it('marks ineligible or removed entrances and still lets the player remove them',()=>{
  const park={...industrial,typeKey:'CS2AS.Type.Park',anchorRole:'Entrance' as const,anchorName:null,
   anchors:[{key:'30.1',name:'Old House',valid:false},{key:'31.4',name:null,valid:false},{key:'32.1',name:'East Gate',valid:true}]};
  render(<AreaInfoSection {...park}/>);
  expect(row('Entrances')).toHaveTextContent('1');
  const items=screen.getAllByRole('listitem');
  expect(items[0]).toHaveTextContent('Old House');expect(items[0]).toHaveTextContent('Not eligible');
  expect(items[1]).toHaveTextContent('Building removed');
  expect(items[2]).not.toHaveTextContent('Not eligible');
  fireEvent.click(within(items[0]).getByRole('button',{name:'Remove'}));
  expect(env.trigger).toHaveBeenCalledWith('cs2AreaSystem','removeAnchor','9007199254740993','30.1');
 });
 it('omits the core level rows when the module presents progress itself',()=>{
  render(<AreaInfoSection {...industrial} level={2} experience="2500" moduleShowsProgress={true}/>);
  expect(screen.queryByText('Level')).toBeNull();expect(screen.queryByText('Experience')).toBeNull();
 });
});
