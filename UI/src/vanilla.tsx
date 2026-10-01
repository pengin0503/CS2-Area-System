import React from 'react';
import {getModule} from 'cs2/modding';

// Vanilla selected-info building blocks, looked up through the public module registry so the section looks
// like the district panel. If a path changes in a future game version a plain fallback keeps the section usable.
type InfoSectionProps=React.PropsWithChildren<{focusKey?:unknown;tooltip?:React.ReactNode;disableFocus?:boolean;className?:string}>;
type InfoRowProps={icon?:string;left?:React.ReactNode;right?:React.ReactNode;tooltip?:React.ReactNode;uppercase?:boolean;subRow?:boolean;disableFocus?:boolean;className?:string};
const SECTION='game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx';
const SLIDER='game-ui/common/input/slider/slider.tsx';
const ROW='game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx';
function lookup<T>(path:string,name:string,fallback:T):T{try{return (getModule(path,name) as T)??fallback;}catch{return fallback;}}
const FallbackSection=({children,className}:InfoSectionProps)=><div className={className}>{children}</div>;
const FallbackRow=({left,right}:InfoRowProps)=><div style={{display:'flex',justifyContent:'space-between'}}><span>{left}</span><span>{right}</span></div>;
// Vanilla slider (not part of cs2/ui). Missing in a future version -> ModulePanel uses step buttons instead.
export type SliderProps={value:number;start:number;end:number;gamepadStep?:number;disabled?:boolean;valueTransformer?:(start:number,end:number,ratio:number)=>number;
 onChange?:(value:number)=>void;onDragStart?:()=>void;onDragEnd?:()=>void;className?:string};
let cache:{InfoSection:React.ComponentType<InfoSectionProps>;InfoRow:React.ComponentType<InfoRowProps>;Slider:React.ComponentType<SliderProps>|null}|undefined;
export function vanilla(){
 return cache??={InfoSection:lookup(SECTION,'InfoSection',FallbackSection),InfoRow:lookup(ROW,'InfoRow',FallbackRow),
  Slider:lookup<React.ComponentType<SliderProps>|null>(SLIDER,'Slider',null)};
}
// 1.6.2f1 District policy building blocks (see docs/verification/area-policy-ui-contracts.md). Each lookup may be
// null in a future version; AreaPoliciesSection then renders its InfoRow fallback instead.
export type VanillaRequirement={__Type:'prefabs.PrefabUnlockedRequirement';locked:boolean;labelId:string};
export type VanillaPolicyData={id:string;icon:string|null;entity:{index:number;version:number};active:boolean;locked:boolean;uiTag:string;
 requirements:{requireAll:VanillaRequirement[];requireAny:VanillaRequirement[]};data:null};
type AdjustPolicy=(entity:unknown,active:boolean,value:number)=>void;
export type PolicyProps={policy:VanillaPolicyData;onAdjust:AdjustPolicy;className?:string};
export type CompactPolicyProps={policy:VanillaPolicyData;onAdjust:AdjustPolicy;onMouseOver?:()=>void;onMouseLeave?:()=>void};
export type InfoSectionFoldoutProps=React.PropsWithChildren<{header:React.ReactNode;initialExpanded?:boolean;expandFromContent?:boolean;disableFocus?:boolean;
 tooltip?:React.ReactNode;className?:string;onToggleExpanded?:(expanded:boolean)=>void}>;
const POLICY='game-ui/game/components/policy/policy.tsx';
const POLICY_CLASSES='game-ui/game/components/policy/policy.module.scss';
const POLICIES_SECTION_CLASSES='game-ui/game/components/selected-info-panel/selected-info-sections/shared-sections/policies-section/policies-section.module.scss';
const FOLDOUT='game-ui/game/components/selected-info-panel/shared-components/info-section/info-section-foldout.tsx';
const FOLDOUT_CLASSES='game-ui/common/foldout/themes/info-section-foldout.module.scss';
const NAVIGATION='game-ui/common/focus/auto-navigation-scope.tsx';
let policyCache:{Policy:React.ComponentType<PolicyProps>|null;CompactPolicy:React.ComponentType<CompactPolicyProps>|null;
 InfoSectionFoldout:React.ComponentType<InfoSectionFoldoutProps>|null;AutoNavigationScope:React.ComponentType<React.PropsWithChildren>|null;
 policyClasses:Record<string,string>;sectionClasses:Record<string,string>;foldoutClasses:Record<string,string>}|undefined;
export function vanillaPolicies(){
 return policyCache??={Policy:lookup<React.ComponentType<PolicyProps>|null>(POLICY,'Policy',null),
  CompactPolicy:lookup<React.ComponentType<CompactPolicyProps>|null>(POLICY,'CompactPolicy',null),
  InfoSectionFoldout:lookup<React.ComponentType<InfoSectionFoldoutProps>|null>(FOLDOUT,'InfoSectionFoldout',null),
  AutoNavigationScope:lookup<React.ComponentType<React.PropsWithChildren>|null>(NAVIGATION,'AutoNavigationScope',null),
  policyClasses:lookup<Record<string,string>>(POLICY_CLASSES,'classes',{}),
  sectionClasses:lookup<Record<string,string>>(POLICIES_SECTION_CLASSES,'classes',{}),
  foldoutClasses:lookup<Record<string,string>>(FOLDOUT_CLASSES,'classes',{})};
}
export function resetVanillaCache(){cache=undefined;policyCache=undefined;}
