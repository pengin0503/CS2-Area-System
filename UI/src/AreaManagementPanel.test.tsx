import React from 'react';
import {render,screen,fireEvent} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import {AreaManagementPanel,AreaManagementPanelHost} from './AreaManagementPanel';
import type {AreaView} from './bindings';
const binding=vi.hoisted(()=>({selected:null as unknown,visible:true,fail:false,trigger:vi.fn()}));
vi.mock('cs2/api',()=>({bindValue:(_group:string,name:string)=>({name}),useValue:(value:{name:string})=>{if(binding.fail)throw new Error('binding unavailable');return value.name==='panelVisible'?binding.visible:binding.selected;},trigger:binding.trigger}));
const industrial:AreaView={stableId:'9007199254740993',name:'Harbor Industry',typeKey:'CS2AS.Type.Industrial',state:'Suspended',level:null,experience:null,tabs:[{id:'overview',labelKey:'Overview'}],summary:'Anchor required',anchorRole:'Headquarters',pendingAnchor:false};
afterEach(()=>{binding.fail=false;binding.selected=null;binding.trigger.mockClear();});
describe('managed area panel',()=>{
 it('host reads the selected area binding',()=>{binding.selected=industrial;render(<AreaManagementPanelHost/>);expect(screen.getByText('Harbor Industry')).toBeInTheDocument();});
 it('renders nothing without a managed selection',()=>{render(<AreaManagementPanelHost/>);expect(screen.queryByRole('complementary')).toBeNull();});
 it('shows the Industrial header and Headquarters action',()=>{render(<AreaManagementPanel area={industrial}/>);expect(screen.getByText('Harbor Industry')).toBeInTheDocument();expect(screen.getByText('Industrial Area')).toBeInTheDocument();expect(screen.getByText('Suspended')).toBeInTheDocument();expect(screen.getByRole('button',{name:'Assign headquarters'})).toBeInTheDocument();});
 it.each(['Park','Tourism'])('renders only the %s module configured tabs',kind=>{render(<AreaManagementPanel area={{...industrial,typeKey:`CS2AS.Type.${kind}`,tabs:[{id:'overview',labelKey:`${kind} overview`},{id:'custom',labelKey:`${kind} details`}],anchorRole:null}}/>);expect(screen.getAllByRole('tab').map(t=>t.textContent)).toEqual([`${kind} overview`,`${kind} details`]);expect(screen.queryByRole('button',{name:'Assign headquarters'})).toBeNull();});
 it('binding failure stays local without simulation mutations',()=>{const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});binding.fail=true;render(<AreaManagementPanelHost/>);expect(screen.queryByRole('complementary')).toBeNull();expect(binding.trigger).not.toHaveBeenCalled();consoleError.mockRestore();});
 it('sends the persisted ID unchanged to protect against stale selections',()=>{render(<AreaManagementPanel area={industrial}/>);fireEvent.click(screen.getByRole('button',{name:'Assign headquarters'}));expect(binding.trigger).toHaveBeenCalledWith('cs2AreaSystem','assignHeadquarters','9007199254740993');});
});
