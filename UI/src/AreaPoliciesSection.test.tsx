import React,{useState} from 'react';
import {render,screen,fireEvent} from '@testing-library/react';
import {describe,it,expect,vi,afterEach} from 'vitest';
import type {AreaPolicyData} from './bindings';
type VanillaPolicy={id:string;icon:string;entity:unknown;active:boolean;locked:boolean;uiTag:string;requirements:{requireAll:{__Type:string;locked:boolean;labelId:string}[];requireAny:unknown[]};data:null};
const env=vi.hoisted(()=>({trigger:vi.fn(),missing:new Set<string>(),throwPolicy:false,mounts:0,dictionary:{
 'SelectedInfoPanel.POLICIES':'Policies','CS2AS.Policy.Status.Active':'In effect','CS2AS.Policy.Status.Off':'Off','CS2AS.Policy.Reason.Suspended':'Paused while the area is suspended',
 'CS2AS.Policy.Reason.EffectUnavailable':'Effect unavailable','CS2AS.Policy.Requirement.Level3':'Requires Area Level 3','CS2AS.Policy.PerDay':'/day',
 'Policy.TITLE[CS2AS.Policy.EnhancedMaintenance]':'Enhanced Maintenance','Policy.TITLE[CS2AS.Policy.NightEvents]':'Night Events',
 'Policy.TITLE[CS2AS.Policy.LandscapeProtection]':'Landscape Protection','CS2AS.Policy.On':'On','CS2AS.Policy.Off':'Off'} as Record<string,string>}));
vi.mock('cs2/api',()=>({trigger:env.trigger,useValue:()=>null,bindValue:()=>({})}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:(key:string)=>env.dictionary[key]??null})}));
vi.mock('cs2/modding',()=>({getModule:(path:string,name:string)=>{
 if(env.missing.has(name))throw new Error('module moved: '+path);
 const policyView=(compact:boolean)=>({policy,onAdjust,className}:{policy:VanillaPolicy;onAdjust:(entity:unknown,active:boolean,value:number)=>void;className?:string})=>{
  if(env.throwPolicy)throw new Error('vanilla policy broke');
  // Like the 1.6.2f1 Policy/CompactPolicy: local state initialised once from props.active.
  const [active,setActive]=useState(policy.active);
  React.useEffect(()=>{env.mounts++;},[]);
  return <button data-compact={compact} data-id={policy.id} data-icon={policy.icon} data-class={className} aria-checked={active} role="checkbox" disabled={policy.locked}
   data-requirements={JSON.stringify(policy.requirements)} onClick={()=>{onAdjust(policy.entity,!active,0);setActive(!active);}}>{policy.id}</button>;
 };
 switch(name){
  case 'Policy':return policyView(false);
  case 'CompactPolicy':return policyView(true);
  case 'InfoSectionFoldout':return ({header,children,initialExpanded,onToggleExpanded}:React.PropsWithChildren<{header:React.ReactNode;initialExpanded:boolean;onToggleExpanded:(v:boolean)=>void}>)=>
   <section data-vanilla="InfoSectionFoldout" data-initial-expanded={String(initialExpanded)}><header>{header}</header><button onClick={()=>onToggleExpanded(true)}>expand</button><button onClick={()=>onToggleExpanded(false)}>collapse</button>{children}</section>;
  case 'AutoNavigationScope':return ({children}:React.PropsWithChildren)=><div data-vanilla="AutoNavigationScope">{children}</div>;
  case 'InfoSection':return ({children}:React.PropsWithChildren)=><section data-vanilla="InfoSection">{children}</section>;
  case 'InfoRow':return ({left,right,subRow,uppercase}:{left?:React.ReactNode;right?:React.ReactNode;subRow?:boolean;uppercase?:boolean})=><div role="row" data-sub={String(!!subRow)} data-upper={String(!!uppercase)}><span>{left}</span><span>{right}</span></div>;
  case 'classes':return path.endsWith('policy.module.scss')?{padFirst:'pad-first_WSR'}:path.endsWith('policies-section.module.scss')?{compactPolicies:'compact-policies_qEr',label:'label_wgk'}:{disableActiveState:'disable-active-state_UBt'};
 }
 return undefined;
}}));
const {AreaPoliciesSection,AREA_POLICY_SECTION_TYPE}=await import('./AreaPoliciesSection');
const {resetVanillaCache}=await import('./vanilla');
const policy=(id:number,name:string,patch:Partial<AreaPolicyData>={}):AreaPolicyData=>({policyId:id,nameKey:`Policy.TITLE[CS2AS.Policy.${name}]`,descriptionKey:`Policy.DESCRIPTION[CS2AS.Policy.${name}]`,
 icon:'Media/Game/Icons/Landscaping.svg',minLevel:1,desiredEnabled:false,effective:false,locked:false,suspended:false,effectAvailable:true,reasonKey:null,estimatedDailyUpkeep:300,...patch});
const maintenance=policy(2001,'EnhancedMaintenance');
const night=policy(2002,'NightEvents',{minLevel:2});
const landscape=policy(2004,'LandscapeProtection',{minLevel:3,locked:true,reasonKey:'CS2AS.Policy.Reason.Locked'});
const checkbox=(id:string,compact=false)=>screen.getAllByRole('checkbox').find(b=>b.dataset.id===id&&b.dataset.compact===String(compact))!;
const requirements=(element:HTMLElement)=>JSON.parse(element.dataset.requirements!) as VanillaPolicy['requirements'];
afterEach(()=>{env.trigger.mockClear();env.missing.clear();env.throwPolicy=false;env.mounts=0;resetVanillaCache();});
describe('area policies selected-info section',()=>{
 it('matches the C# section type name',()=>expect(AREA_POLICY_SECTION_TYPE).toBe('CS2AreaSystem.Core.UI.AreaPolicyInfoSection'));
 it('renders the vanilla Policy component for every policy inside the vanilla foldout section',()=>{
  const view=render(<AreaPoliciesSection stableId="77" policies={[maintenance,night]}/>);
  expect(view.container.querySelector('[data-vanilla="InfoSectionFoldout"]')).not.toBeNull();
  expect(checkbox('CS2AS.Policy.EnhancedMaintenance')).toHaveAttribute('data-icon','Media/Game/Icons/Landscaping.svg');
  expect(checkbox('CS2AS.Policy.EnhancedMaintenance').dataset.class).toBe('pad-first_WSR');
  expect(checkbox('CS2AS.Policy.NightEvents').dataset.class).toBeUndefined();
  expect(screen.getAllByRole('row')[0]).toHaveTextContent('Policies');
 });
 it('collapses to compact policy icons like the district panel when there are three or more',()=>{
  const view=render(<AreaPoliciesSection stableId="77" policies={[maintenance,night,landscape]}/>);
  expect(view.container.querySelector('[data-vanilla="InfoSectionFoldout"]')!.getAttribute('data-initial-expanded')).toBe('false');
  expect(checkbox('CS2AS.Policy.NightEvents',true)).toBeDefined();
  fireEvent.click(screen.getByText('expand'));
  expect(screen.getAllByRole('checkbox').some(b=>b.dataset.compact==='true')).toBe(false);
  expect(screen.getAllByRole('row')[0]).toHaveTextContent('Policies');
 });
 it('sends only the guarded trigger with the stable ID, policy ID and 1/0',()=>{
  render(<AreaPoliciesSection stableId="9007199254740993" policies={[maintenance,policy(2003,'QuietOperations',{desiredEnabled:true,effective:true})]}/>);
  fireEvent.click(checkbox('CS2AS.Policy.EnhancedMaintenance'));
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','setPolicyEnabled','9007199254740993','2001','1');
  fireEvent.click(checkbox('CS2AS.Policy.QuietOperations'));
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','setPolicyEnabled','9007199254740993','2003','0');
  expect(env.trigger).toHaveBeenCalledTimes(2);
 });
 it('keeps Level-locked policies visible but locked with the Level requirement',()=>{
  render(<AreaPoliciesSection stableId="77" policies={[maintenance,landscape]}/>);
  const locked=checkbox('CS2AS.Policy.LandscapeProtection');
  expect(locked).toBeDisabled();
  expect(requirements(locked).requireAll).toEqual([{__Type:'prefabs.PrefabUnlockedRequirement',locked:true,labelId:'CS2AS.Policy.Requirement.Level3'}]);
  expect(screen.getAllByRole('row').some(r=>r.textContent==='Requires Area Level 3¢300/day')).toBe(true);
 });
 it('shows a suspended configured policy as ON with a suspended status',()=>{
  render(<AreaPoliciesSection stableId="77" policies={[policy(2001,'EnhancedMaintenance',{desiredEnabled:true,suspended:true,reasonKey:'CS2AS.Policy.Reason.Suspended'})]}/>);
  expect(checkbox('CS2AS.Policy.EnhancedMaintenance')).toHaveAttribute('aria-checked','true');
  expect(checkbox('CS2AS.Policy.EnhancedMaintenance')).not.toBeDisabled();
  expect(screen.getAllByRole('row').find(r=>r.dataset.sub==='true')).toHaveTextContent('Paused while the area is suspended');
 });
 it('cannot enable an effect-unavailable policy but can always turn a configured one off',()=>{
  const unavailable={effectAvailable:false,reasonKey:'CS2AS.Policy.Reason.EffectUnavailable'};
  render(<AreaPoliciesSection stableId="77" policies={[policy(2002,'NightEvents',unavailable),policy(2001,'EnhancedMaintenance',{...unavailable,desiredEnabled:true})]}/>);
  const off=checkbox('CS2AS.Policy.NightEvents');
  expect(off).toBeDisabled();
  expect(requirements(off).requireAll).toEqual([{__Type:'prefabs.PrefabUnlockedRequirement',locked:true,labelId:'CS2AS.Policy.Reason.EffectUnavailable'}]);
  const on=checkbox('CS2AS.Policy.EnhancedMaintenance');
  expect(on).not.toBeDisabled();
  fireEvent.click(on);
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','setPolicyEnabled','77','2001','0');
 });
 it('shows the C# estimated upkeep and effective status per policy',()=>{
  render(<AreaPoliciesSection stableId="77" policies={[policy(2001,'EnhancedMaintenance',{desiredEnabled:true,effective:true,estimatedDailyUpkeep:12345})]}/>);
  const sub=screen.getAllByRole('row').find(r=>r.dataset.sub==='true')!;
  expect(sub).toHaveTextContent('In effect');
  expect(sub).toHaveTextContent('¢12,345/day');
 });
 it('remounts the vanilla toggle when C# publishes a conflict swap so its local state follows',()=>{
  const view=render(<AreaPoliciesSection stableId="77" policies={[policy(2002,'NightEvents',{desiredEnabled:true,effective:true}),policy(2003,'QuietOperations')]}/>);
  fireEvent.click(checkbox('CS2AS.Policy.QuietOperations'));
  view.rerender(<AreaPoliciesSection stableId="77" policies={[policy(2002,'NightEvents'),policy(2003,'QuietOperations',{desiredEnabled:true,effective:true})]}/>);
  expect(checkbox('CS2AS.Policy.NightEvents')).toHaveAttribute('aria-checked','false');
  expect(checkbox('CS2AS.Policy.QuietOperations')).toHaveAttribute('aria-checked','true');
 });
 it('falls back to vanilla rows with toggle buttons when the policy modules move',()=>{
  env.missing.add('Policy');
  render(<AreaPoliciesSection stableId="77" policies={[maintenance,landscape]}/>);
  expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
  const toggle=screen.getAllByRole('button').find(b=>b.closest('[role="row"]')?.textContent?.startsWith('Enhanced Maintenance'))!;
  fireEvent.click(toggle);
  expect(env.trigger).toHaveBeenLastCalledWith('cs2AreaSystem','setPolicyEnabled','77','2001','1');
  expect(screen.getAllByRole('button').find(b=>b.closest('[role="row"]')?.textContent?.startsWith('Landscape Protection'))).toBeDisabled();
 });
 it('falls back when the foldout module moves even if Policy exists',()=>{
  env.missing.add('InfoSectionFoldout');
  render(<AreaPoliciesSection stableId="77" policies={[maintenance]}/>);
  expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
  expect(screen.getAllByRole('row')[0]).toHaveTextContent('Policies');
 });
 it('contains a vanilla policy render failure inside the section',()=>{
  env.throwPolicy=true;
  const spy=vi.spyOn(console,'error').mockImplementation(()=>{});
  expect(()=>render(<AreaPoliciesSection stableId="77" policies={[maintenance]}/>)).not.toThrow();
  spy.mockRestore();
 });
 it('renders nothing without policies (Tourism)',()=>{
  const view=render(<AreaPoliciesSection stableId="77" policies={[]}/>);
  expect(view.container).toBeEmptyDOMElement();
 });
});
