import {bindValue} from 'cs2/api';
export interface AreaView {
 stableId:string;name:string;typeKey:string;state:string;level:number|null;experience:string|null;
 tabs:{id:string;labelKey:string}[];summary:string;anchorRole:'Entrance'|'Headquarters'|null;pendingAnchor:boolean;
}
let bindings: {selectedArea:ReturnType<typeof bindValue<AreaView|null>>;panelVisible:ReturnType<typeof bindValue<boolean>>}|undefined;
export function getBindings(){return bindings??=( {selectedArea:bindValue<AreaView|null>('cs2AreaSystem','selectedArea',null),panelVisible:bindValue('cs2AreaSystem','panelVisible',false)} );}
