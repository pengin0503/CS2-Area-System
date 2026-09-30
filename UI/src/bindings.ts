import {bindValue} from 'cs2/api';
// Props of the selected-info section, written by CS2AreaSystem.Core.UI.AreaInfoSection.
export interface AreaSectionData {
 stableId:string;typeKey:string;state:string;level:number|null;experience:string|null;summary:string;
 anchorRole:'Entrance'|'Headquarters'|null;anchorName:string|null;pendingAnchor:boolean;confirmDelete:boolean;
 tabs:AreaPanelTab[];anchors?:AreaAnchor[];
 // True when the module's tabs present level/experience themselves (Core rows are then omitted).
 moduleShowsProgress?:boolean;
}
// Module-neutral structured panel content written by CS2AreaSystem.Core.Modules.AreaPanelDocument.
export interface AreaPanelItem {
 kind:string;id:string;labelKey:string;value:string|null;valueKey:string|null;numericValue:number|null;
 min:number|null;max:number|null;step:number|null;disabled:boolean;tooltipKey:string|null;action:string|null;
}
export interface AreaPanelTab {id:string;labelKey:string;items?:AreaPanelItem[];}
export interface AreaAnchor {key:string;name:string|null;valid?:boolean;}
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
