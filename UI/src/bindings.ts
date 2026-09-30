import {bindValue} from 'cs2/api';
// Props of the selected-info section, written by CS2AreaSystem.Core.UI.AreaInfoSection.
export interface AreaSectionData {
 stableId:string;typeKey:string;state:string;level:number|null;experience:string|null;summary:string;
 anchorRole:'Entrance'|'Headquarters'|null;anchorName:string|null;pendingAnchor:boolean;confirmDelete:boolean;
 tabs:{id:string;labelKey:string}[];
}
export interface AreaListItem {stableId:string;name:string;typeKey:string;state:string;}
export interface PendingAssignment {stableId:string;name:string;role:'Entrance'|'Headquarters';}
type Binding<T>=ReturnType<typeof bindValue<T>>;
let bindings:{areaList:Binding<AreaListItem[]>;areaListAvailable:Binding<boolean>;areaListOpen:Binding<boolean>;panelSide:Binding<'left'|'right'>;pendingAssignment:Binding<PendingAssignment|null>}|undefined;
export function getBindings(){return bindings??=( {
 areaList:bindValue<AreaListItem[]>('cs2AreaSystem','areaList',[]),
 areaListAvailable:bindValue('cs2AreaSystem','areaListAvailable',false),
 areaListOpen:bindValue('cs2AreaSystem','areaListOpen',false),
 panelSide:bindValue<'left'|'right'>('cs2AreaSystem','panelSide','right'),
 pendingAssignment:bindValue<PendingAssignment|null>('cs2AreaSystem','pendingAssignment',null)
} );}
