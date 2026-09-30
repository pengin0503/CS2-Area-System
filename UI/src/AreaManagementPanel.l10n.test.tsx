import React,{createContext,useContext} from 'react';
import {render,screen} from '@testing-library/react';
import {describe,it,expect,vi} from 'vitest';
import type {AreaView} from './bindings';
const l10n=vi.hoisted(()=>({dictionaries:{
 'en-US':{'CS2AS.Type.Park':'Park Area','CS2AS.State.Suspended':'Suspended','CS2AS.UI.AssignEntrance':'Assign entrance','CS2AS.Summary.AnchorRequired':'Assign a required facility to activate this area.','Module.Tab.Visitors':'Visitors'},
 'ja-JP':{'CS2AS.Type.Park':'公園エリア','CS2AS.State.Suspended':'停止中','CS2AS.UI.AssignEntrance':'入口を割り当て','CS2AS.Summary.AnchorRequired':'このエリアを有効化するには必要な施設を割り当ててください。','Module.Tab.Visitors':'来園者'}
} as Record<string,Record<string,string>>}));
const Locale=createContext('en-US');
vi.mock('cs2/l10n',()=>({useLocalization:()=>{const locale=useContext(Locale);return {translate:(key:string)=>l10n.dictionaries[locale]?.[key]??null};}}));
vi.mock('cs2/api',()=>({bindValue:()=>({}),useValue:()=>null,trigger:vi.fn()}));
const {AreaManagementPanel}=await import('./AreaManagementPanel');
const park:AreaView={stableId:'5',name:'Central Park',typeKey:'CS2AS.Type.Park',state:'CS2AS.State.Suspended',level:null,experience:null,
 tabs:[{id:'visitors',labelKey:'Module.Tab.Visitors'},{id:'unknown',labelKey:'Module.Tab.RideQueues'}],summary:'CS2AS.Summary.AnchorRequired',anchorRole:'Entrance',pendingAnchor:false};
describe('panel localization',()=>{
 it('reflects a runtime locale change, including module-supplied tab keys',()=>{
  const view=render(<Locale.Provider value="en-US"><AreaManagementPanel area={park}/></Locale.Provider>);
  expect(screen.getByText('Park Area')).toBeInTheDocument();
  expect(screen.getByRole('tab',{name:'Visitors'})).toBeInTheDocument();
  view.rerender(<Locale.Provider value="ja-JP"><AreaManagementPanel area={park}/></Locale.Provider>);
  expect(screen.getByText('公園エリア')).toBeInTheDocument();
  expect(screen.getByText('停止中')).toBeInTheDocument();
  expect(screen.getByRole('button',{name:'入口を割り当て'})).toBeInTheDocument();
  expect(screen.getByRole('tab',{name:'来園者'})).toBeInTheDocument();
  expect(screen.getByText('このエリアを有効化するには必要な施設を割り当ててください。')).toBeInTheDocument();
 });
 it('shows a readable fallback instead of a raw key for entries missing in the active locale',()=>{
  render(<Locale.Provider value="ja-JP"><AreaManagementPanel area={park}/></Locale.Provider>);
  expect(screen.getByRole('tab',{name:'Ride queues'})).toBeInTheDocument();
  expect(screen.getByRole('button',{name:'Delete area'})).toBeInTheDocument();
  expect(screen.queryByText(/CS2AS\./)).toBeNull();
 });
});
