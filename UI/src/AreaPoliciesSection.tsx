import React,{useState} from 'react';
import {trigger} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {Button} from 'cs2/ui';
import type {AreaPolicyData,AreaPolicySectionData} from './bindings';
import {localize} from './localization';
import {PanelBoundary} from './boundary';
import {vanilla,vanillaPolicies,type VanillaPolicyData,type VanillaRequirement} from './vanilla';

// Must equal the C# section type's full name: the vanilla panel picks components by __Type.
export const AREA_POLICY_SECTION_TYPE='CS2AreaSystem.Core.UI.AreaPolicyInfoSection';
const LOCKED_REASON='CS2AS.Policy.Reason.Locked';
const UNAVAILABLE_REASON='CS2AS.Policy.Reason.EffectUnavailable';
// Localized as CS2AS.Policy.Requirement.Level1 … Level5.
const levelRequirement=(policy:AreaPolicyData)=>`CS2AS.Policy.Requirement.Level${policy.minLevel}`;
// The vanilla Policy component localizes Policy.TITLE[id] / Policy.DESCRIPTION[id]; C# publishes the full TITLE key.
export function vanillaPolicyId(policy:AreaPolicyData):string{
 const match=/^Policy\.TITLE\[(.+)\]$/.exec(policy.nameKey);
 return match?match[1]:`CS2AS.Policy.${policy.policyId}`;
}
// Presentation mapping of the C# flags onto the vanilla toggle: a policy that is not configured cannot be switched on
// while it is Level-locked or its effect is unavailable, but a configured policy can always be switched off.
export function blocked(policy:AreaPolicyData):boolean{return !policy.desiredEnabled&&(policy.locked||!policy.effectAvailable);}
export function toVanillaPolicy(policy:AreaPolicyData):VanillaPolicyData{
 const requireAll:VanillaRequirement[]=[];
 if(blocked(policy)){
  if(policy.locked)requireAll.push({__Type:'prefabs.PrefabUnlockedRequirement',locked:true,labelId:levelRequirement(policy)});
  if(!policy.effectAvailable)requireAll.push({__Type:'prefabs.PrefabUnlockedRequirement',locked:true,
   labelId:policy.reasonKey&&policy.reasonKey!==LOCKED_REASON?policy.reasonKey:UNAVAILABLE_REASON});
 }
 return {id:vanillaPolicyId(policy),icon:policy.icon,entity:{index:policy.policyId,version:0},active:policy.desiredEnabled,locked:blocked(policy),uiTag:'',
  requirements:{requireAll,requireAny:[]},data:null};
}
function statusKey(policy:AreaPolicyData):string{
 if(policy.effective)return 'CS2AS.Policy.Status.Active';
 if(policy.reasonKey===LOCKED_REASON)return levelRequirement(policy);
 return policy.reasonKey??(policy.desiredEnabled?'CS2AS.Policy.Status.On':'CS2AS.Policy.Status.Off');
}
const money=(value:number)=>'¢'+Math.round(value).toLocaleString('en-US');
// The vanilla toggle keeps its own useState(active); remounting on every authoritative change (for example the old
// policy of a conflict swap) keeps it in step with C#.
const remountKey=(policy:AreaPolicyData)=>`${policy.policyId}:${policy.desiredEnabled?1:0}:${blocked(policy)?1:0}`;

function AreaPoliciesContent({stableId,policies}:AreaPolicySectionData){
 const {translate}=useLocalization();
 const text=(key:string)=>localize(translate,key);
 const {InfoSection,InfoRow}=vanilla();
 const {Policy,CompactPolicy,InfoSectionFoldout,AutoNavigationScope,policyClasses,sectionClasses,foldoutClasses}=vanillaPolicies();
 const [expanded,setExpanded]=useState(policies.length<3);
 const [hovered,setHovered]=useState(false);
 if(policies.length===0)return null;
 const adjust=(policy:AreaPolicyData)=>(_entity:unknown,active:boolean)=>
  trigger('cs2AreaSystem','setPolicyEnabled',stableId,String(policy.policyId),active?'1':'0');
 const detail=(policy:AreaPolicyData)=><InfoRow subRow={true} left={text(statusKey(policy))} right={money(policy.estimatedDailyUpkeep)+text('CS2AS.Policy.PerDay')}/>;
 const title=<InfoRow uppercase={true} disableFocus={true} className={sectionClasses.label} left={text('SelectedInfoPanel.POLICIES')}/>;
 if(!Policy||!InfoSectionFoldout){
  // Fallback when the 1.6.2f1 policy modules are not found: same data and trigger as plain vanilla rows.
  return <InfoSection disableFocus={true}>{title}{policies.map(policy=><React.Fragment key={policy.policyId}>
   <InfoRow left={text(policy.nameKey)} right={<Button variant="flat" disabled={blocked(policy)} selected={policy.desiredEnabled}
    onSelect={()=>adjust(policy)(null,!policy.desiredEnabled)}>{text(policy.desiredEnabled?'CS2AS.Policy.On':'CS2AS.Policy.Off')}</Button>}/>
   {detail(policy)}</React.Fragment>)}</InfoSection>;
 }
 const Scope=AutoNavigationScope??React.Fragment;
 // Same composition as the vanilla District PoliciesSection, with this mod's guarded trigger instead of policies.setPolicy.
 const header=expanded||!CompactPolicy?title:<div className={sectionClasses.compactPolicies}>{policies.map(policy=>
  <CompactPolicy key={remountKey(policy)} policy={toVanillaPolicy(policy)} onAdjust={adjust(policy)} onMouseOver={()=>setHovered(true)} onMouseLeave={()=>setHovered(false)}/>)}</div>;
 return <InfoSectionFoldout disableFocus={true} initialExpanded={policies.length<3} expandFromContent={true} onToggleExpanded={setExpanded}
  className={hovered?foldoutClasses.disableActiveState:undefined} header={header}>
  <Scope>{policies.map((policy,index)=><React.Fragment key={policy.policyId}>
   <Policy key={remountKey(policy)} policy={toVanillaPolicy(policy)} onAdjust={adjust(policy)} className={index===0?policyClasses.padFirst:undefined}/>
   {detail(policy)}</React.Fragment>)}</Scope>
 </InfoSectionFoldout>;
}
// A failure inside the policy section never takes down the vanilla selected-info panel or the area section.
export function AreaPoliciesSection(props:AreaPolicySectionData){return <PanelBoundary><AreaPoliciesContent {...props}/></PanelBoundary>;}
