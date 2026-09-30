import React from 'react';
import {getModule} from 'cs2/modding';

// Vanilla selected-info building blocks, looked up through the public module registry so the section looks
// like the district panel. If a path changes in a future game version a plain fallback keeps the section usable.
type InfoSectionProps=React.PropsWithChildren<{focusKey?:unknown;tooltip?:React.ReactNode;disableFocus?:boolean;className?:string}>;
type InfoRowProps={icon?:string;left?:React.ReactNode;right?:React.ReactNode;tooltip?:React.ReactNode;uppercase?:boolean;subRow?:boolean;disableFocus?:boolean;className?:string};
const SECTION='game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx';
const ROW='game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx';
function lookup<T>(path:string,name:string,fallback:T):T{try{return (getModule(path,name) as T)??fallback;}catch{return fallback;}}
const FallbackSection=({children,className}:InfoSectionProps)=><div className={className}>{children}</div>;
const FallbackRow=({left,right}:InfoRowProps)=><div style={{display:'flex',justifyContent:'space-between'}}><span>{left}</span><span>{right}</span></div>;
let cache:{InfoSection:React.ComponentType<InfoSectionProps>;InfoRow:React.ComponentType<InfoRowProps>}|undefined;
export function vanilla(){
 return cache??={InfoSection:lookup(SECTION,'InfoSection',FallbackSection),InfoRow:lookup(ROW,'InfoRow',FallbackRow)};
}
export function resetVanillaCache(){cache=undefined;}
