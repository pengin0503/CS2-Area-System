// Adapted from tomkpunkt/ParkManager UI/types/api.d.ts and modding.d.ts (GPL-3.0).
declare module 'cs2/api' {
 export interface ValueBinding<T> { readonly value:T; }
 export function bindValue<T>(group:string,name:string,fallbackValue?:T):ValueBinding<T>;
 export function useValue<T>(binding:ValueBinding<T>):T;
 export function trigger(group:string,name:string,...args:unknown[]):void;
}
declare module 'cs2/modding' {
 import {ComponentType} from 'react';
 export type ModRegistrar=(registry:{append(target:'Game',component:ComponentType):void})=>void;
}
declare module 'cs2/l10n' {
 export function useLocalization():{translate(key:string,fallback?:string|null):string|null};
}
declare module '*.module.scss' { const classes:Record<string,string>;export default classes; }
