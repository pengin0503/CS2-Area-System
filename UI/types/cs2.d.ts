// Adapted from tomkpunkt/ParkManager UI/types/api.d.ts and modding.d.ts (GPL-3.0).
// Only the members used by this mod are declared; shapes follow the 1.6.2f1 game UI (index.js).
declare module 'cs2/api' {
 export interface ValueBinding<T> { readonly value:T; }
 export function bindValue<T>(group:string,name:string,fallbackValue?:T):ValueBinding<T>;
 export function useValue<T>(binding:ValueBinding<T>):T;
 export function trigger(group:string,name:string,...args:unknown[]):void;
}
declare module 'cs2/modding' {
 import {ComponentType} from 'react';
 export interface ModuleRegistry {
  append(target:'Game',component:ComponentType):void;
  extend<T=any>(modulePath:string,exportName:string,extension:(current:T)=>T):void;
 }
 export type ModRegistrar=(registry:ModuleRegistry)=>void;
 export function getModule(modulePath:string,exportName:string):unknown;
}
declare module 'cs2/l10n' {
 export function useLocalization():{translate(key:string,fallback?:string|null):string|null};
}
declare module 'cs2/ui' {
 import {PropsWithChildren,ReactElement} from 'react';
 export function Button(props:PropsWithChildren<{variant?:'flat'|'primary'|'round'|'menu'|'default'|'icon'|'floating'|'text';selected?:boolean;disabled?:boolean;className?:string;onSelect?:()=>void}>):ReactElement;
}
declare module '*.module.scss' { const classes:Record<string,string>;export default classes; }
