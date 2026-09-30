import React from 'react';
import {render,screen,fireEvent} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import {AreaManagementPanel,AreaManagementPanelHost} from './AreaManagementPanel';
import type {AreaView} from './bindings';
const binding=vi.hoisted(()=>({selected:null as unknown,visible:true,fail:false,trigger:vi.fn()}));
const translations:Record<string,string>={
 'CS2AS.Type.Park':'Park Area','CS2AS.Type.Industrial':'Industrial Area','CS2AS.Type.Tourism':'Tourism Area',
 'CS2AS.Tab.Overview':'Overview','CS2AS.Summary.Active':'Active','CS2AS.Summary.AnchorRequired':'Assign a required facility to activate this area.',
 'CS2AS.State.Suspended':'Suspended','CS2AS.UI.AreaManagement':'Area management','CS2AS.UI.Level':'Level','CS2AS.UI.Experience':'Experience',
 'CS2AS.UI.EditArea':'Edit area','CS2AS.UI.Locate':'Locate','CS2AS.UI.AreaName':'Area name','CS2AS.UI.Rename':'Rename',
 'CS2AS.UI.AssignHeadquarters':'Assign headquarters','CS2AS.UI.AssignEntrance':'Assign entrance','CS2AS.UI.SelectAnchor':'Select an existing building inside this area.',
 'CS2AS.UI.CancelAssignment':'Cancel assignment','CS2AS.UI.DeleteArea':'Delete area','CS2AS.UI.DeleteConfirm':'Delete this management boundary?',
 'CS2AS.UI.ConfirmDelete':'Confirm delete','CS2AS.UI.Cancel':'Cancel'
};
vi.mock('cs2/api',()=>({bindValue:(_group:string,name:string)=>({name}),useValue:(value:{name:string})=>{if(binding.fail)throw new Error('binding unavailable');return value.name==='panelVisible'?binding.visible:binding.selected;},trigger:binding.trigger}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:(key:string)=>translations[key]??key})}));
const industrial:AreaView={stableId:'9007199254740993',name:'Harbor Industry',typeKey:'CS2AS.Type.Industrial',state:'CS2AS.State.Suspended',level:null,experience:null,tabs:[{id:'overview',labelKey:'CS2AS.Tab.Overview'}],summary:'CS2AS.Summary.AnchorRequired',anchorRole:'Headquarters',pendingAnchor:false};
afterEach(()=>{binding.fail=false;binding.selected=null;binding.trigger.mockClear();});
describe('managed area panel',()=>{
 it('host reads the selected area binding',()=>{binding.selected=industrial;render(<AreaManagementPanelHost/>);expect(screen.getByText('Harbor Industry')).toBeInTheDocument();});
 it('renders nothing without a managed selection',()=>{render(<AreaManagementPanelHost/>);expect(screen.queryByRole('complementary')).toBeNull();});
 it('uses localization for the Industrial header, lifecycle state and Headquarters action',()=>{render(<AreaManagementPanel area={industrial}/>);expect(screen.getByText('Harbor Industry')).toBeInTheDocument();expect(screen.getByText('Industrial Area')).toBeInTheDocument();expect(screen.getByText('Suspended')).toBeInTheDocument();expect(screen.getByRole('button',{name:'Assign headquarters'})).toBeInTheDocument();});
 it.each(['Park','Tourism'])('renders only the %s module configured tabs',kind=>{const keys=[`CS2AS.${kind}.Overview`,`CS2AS.${kind}.Details`];render(<AreaManagementPanel area={{...industrial,typeKey:`CS2AS.Type.${kind}`,tabs:[{id:'overview',labelKey:keys[0]},{id:'custom',labelKey:keys[1]}],anchorRole:null}}/>);expect(screen.getAllByRole('tab').map(t=>t.textContent)).toEqual(keys);expect(screen.queryByRole('button',{name:'Assign headquarters'})).toBeNull();});
 it('binding failure stays local without simulation mutations',()=>{const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});binding.fail=true;render(<AreaManagementPanelHost/>);expect(screen.queryByRole('complementary')).toBeNull();expect(binding.trigger).not.toHaveBeenCalled();consoleError.mockRestore();});
 it('sends the persisted ID unchanged to protect against stale selections',()=>{render(<AreaManagementPanel area={industrial}/>);fireEvent.click(screen.getByRole('button',{name:'Assign headquarters'}));expect(binding.trigger).toHaveBeenCalledWith('cs2AreaSystem','assignHeadquarters','9007199254740993');});
 it('resyncs the rename input when the backend name changes',()=>{const view=render(<AreaManagementPanel area={industrial}/>);const input=screen.getByRole('textbox',{name:'Area name'}) as HTMLInputElement;expect(input.value).toBe('Harbor Industry');view.rerender(<AreaManagementPanel area={{...industrial,name:'Renamed Industry'}}/>);expect(input.value).toBe('Renamed Industry');});
 it('keeps typed name and selected tab when the backend republishes identical content',()=>{const tabs=[{id:'overview',labelKey:'CS2AS.Tab.Overview'},{id:'custom',labelKey:'Custom'}];const view=render(<AreaManagementPanel area={{...industrial,tabs}}/>);const input=screen.getByRole('textbox',{name:'Area name'}) as HTMLInputElement;fireEvent.change(input,{target:{value:'Draft name'}});fireEvent.click(screen.getByRole('tab',{name:'Custom'}));view.rerender(<AreaManagementPanel area={{...industrial,tabs:tabs.map(t=>({...t}))}}/>);expect(input.value).toBe('Draft name');expect(screen.getByRole('tab',{name:'Custom'})).toHaveAttribute('aria-selected','true');});
});
