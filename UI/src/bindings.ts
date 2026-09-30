import {bindValue} from 'cs2/api';
export interface AreaView {
 stableId:string;name:string;typeKey:string;state:string;level:number|null;experience:string|null;
 tabs:{id:string;labelKey:string}[];summary:string;anchorRole:'Entrance'|'Headquarters'|null;pendingAnchor:boolean;
}
export interface AreaListItem {stableId:string;name:string;typeKey:string;state:string;}
type Binding<T>=ReturnType<typeof bindValue<T>>;
let bindings: {selectedArea:Binding<AreaView|null>;panelVisible:Binding<boolean>;areaList:Binding<AreaListItem[]>;areaListAvailable:Binding<boolean>;areaListOpen:Binding<boolean>;confirmDelete:Binding<boolean>;panelSide:Binding<'left'|'right'>}|undefined;
export function getBindings(){return bindings??=( {
 selectedArea:bindValue<AreaView|null>('cs2AreaSystem','selectedArea',null),
 panelVisible:bindValue('cs2AreaSystem','panelVisible',false),
 areaList:bindValue<AreaListItem[]>('cs2AreaSystem','areaList',[]),
 areaListAvailable:bindValue('cs2AreaSystem','areaListAvailable',false),
 areaListOpen:bindValue('cs2AreaSystem','areaListOpen',false),
 confirmDelete:bindValue('cs2AreaSystem','confirmDelete',true),
 panelSide:bindValue<'left'|'right'>('cs2AreaSystem','panelSide','right')
} );}
