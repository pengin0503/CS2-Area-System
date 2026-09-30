import React from 'react';
import {render,screen,fireEvent,act} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import {AreaSystemHost} from './AreaSystemHost';
import {PANEL_RETRY_BASE_MS,PANEL_RETRY_MAX_MS} from './boundary';
const binding=vi.hoisted(()=>({fail:false,reads:0,listAvailable:false,listOpen:false,list:[] as unknown[],panelSide:'right',pending:null as unknown,trigger:vi.fn()}));
const translations:Record<string,string>={'CS2AS.UI.AreaList':'Managed areas','CS2AS.UI.NoAreas':'No managed areas yet.','CS2AS.Type.Industrial':'Industrial Area','CS2AS.Type.Park':'Park Area',
 'CS2AS.State.Suspended':'Suspended','CS2AS.State.Active':'Active','CS2AS.UI.SelectHeadquarters':'Select a building inside this area as headquarters.','CS2AS.UI.CancelAssignment':'Cancel assignment'};
vi.mock('cs2/api',()=>({bindValue:(_group:string,name:string)=>({name}),useValue:(value:{name:string})=>{binding.reads++;if(binding.fail)throw new Error('binding unavailable');
 return ({areaListAvailable:binding.listAvailable,areaListOpen:binding.listOpen,areaList:binding.list,panelSide:binding.panelSide,pendingAssignment:binding.pending} as Record<string,unknown>)[value.name];},trigger:binding.trigger}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:(key:string)=>translations[key]??null})}));
afterEach(()=>{Object.assign(binding,{fail:false,reads:0,listAvailable:false,listOpen:false,list:[],panelSide:'right',pending:null});binding.trigger.mockClear();vi.useRealTimers();});
describe('area system floating host',()=>{
 it('renders nothing until managed-area data is ready',()=>{render(<AreaSystemHost/>);expect(screen.queryByRole('button')).toBeNull();});
 it('toggles the mod-owned area list through the backend',()=>{binding.listAvailable=true;render(<AreaSystemHost/>);fireEvent.click(screen.getByRole('button',{name:'Managed areas'}));expect(binding.trigger).toHaveBeenCalledWith('cs2AreaSystem','setAreaListOpen',true);});
 it('lists completed areas and selects one by persisted stable ID',()=>{
  binding.listAvailable=true;binding.listOpen=true;binding.list=[{stableId:'3',name:'Harbor Industry',typeKey:'CS2AS.Type.Industrial',state:'CS2AS.State.Suspended'},{stableId:'9007199254740993',name:'Central Park',typeKey:'CS2AS.Type.Park',state:'CS2AS.State.Active'}];
  render(<AreaSystemHost/>);
  expect(screen.getByRole('button',{name:'Managed areas'})).toHaveAttribute('aria-expanded','true');
  expect(screen.getByText('Industrial Area · Suspended')).toBeInTheDocument();
  fireEvent.click(screen.getByText('Central Park'));
  expect(binding.trigger).toHaveBeenCalledWith('cs2AreaSystem','selectArea','9007199254740993');
 });
 it('explains an empty area list',()=>{binding.listAvailable=true;binding.listOpen=true;render(<AreaSystemHost/>);expect(screen.getByRole('status')).toHaveTextContent('No managed areas yet.');});
 it('guides an anchor assignment while a building is selected and can cancel it',()=>{
  binding.pending={stableId:'7',name:'Harbor Industry',role:'Headquarters'};render(<AreaSystemHost/>);
  expect(screen.getByRole('status')).toHaveTextContent('Harbor Industry');
  expect(screen.getByRole('status')).toHaveTextContent('Select a building inside this area as headquarters.');
  fireEvent.click(screen.getByRole('button',{name:'Cancel assignment'}));
  expect(binding.trigger).toHaveBeenCalledWith('cs2AreaSystem','cancelAnchorAssignment','7');
 });
 it('places the host on the side chosen in the options',()=>{binding.listAvailable=true;const view=render(<AreaSystemHost/>);expect(view.container.firstElementChild!.className).not.toMatch(/left/);binding.panelSide='left';view.rerender(<AreaSystemHost/>);expect(view.container.firstElementChild!.className).toMatch(/left/);});
 it('binding failure stays local without simulation mutations',()=>{const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});binding.fail=true;render(<AreaSystemHost/>);expect(screen.queryByRole('button')).toBeNull();expect(binding.trigger).not.toHaveBeenCalled();consoleError.mockRestore();});
 it('recovers in the same session after a transient binding error without sending actions',()=>{
  vi.useFakeTimers();const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});
  binding.fail=true;binding.listAvailable=true;render(<AreaSystemHost/>);
  expect(screen.queryByRole('button')).toBeNull();
  binding.fail=false;act(()=>{vi.advanceTimersByTime(PANEL_RETRY_BASE_MS);});
  expect(screen.getByRole('button',{name:'Managed areas'})).toBeInTheDocument();
  expect(binding.trigger).not.toHaveBeenCalled();consoleError.mockRestore();
 });
 it('retries a persistent failure with capped backoff instead of a render loop',()=>{
  vi.useFakeTimers();const consoleError=vi.spyOn(console,'error').mockImplementation(()=>{});
  binding.fail=true;render(<AreaSystemHost/>);
  const readsPerAttempt=binding.reads;
  for(let second=0;second<10*60;second++)act(()=>{vi.advanceTimersByTime(1000);});
  // 1s,2s,4s,8s,16s then every 30s: about 5+18 retries in ten minutes, never one per frame.
  const retries=(binding.reads-readsPerAttempt)/readsPerAttempt;
  expect(retries).toBeGreaterThanOrEqual(5);expect(retries).toBeLessThanOrEqual(25);expect(PANEL_RETRY_MAX_MS).toBe(30000);
  expect(binding.trigger).not.toHaveBeenCalled();consoleError.mockRestore();
 });
});
