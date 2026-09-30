import React,{useEffect,useState} from 'react';
import {trigger} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {Button} from 'cs2/ui';
import type {AreaSectionData} from './bindings';
import {localize} from './localization';
import {PanelBoundary} from './boundary';
import {vanilla} from './vanilla';
import {ModulePanel} from './ModulePanel';
import styles from './AreaSystem.module.scss';

// Must equal the C# section type's full name: the vanilla panel picks components by __Type.
export const AREA_INFO_SECTION_TYPE='CS2AreaSystem.Core.UI.AreaInfoSection';

export function AreaInfoSectionContent(area:AreaSectionData){
 const {translate}=useLocalization();
 // Resolved on every render so a runtime locale change is reflected immediately.
 const text=(key:string)=>localize(translate,key);
 const {InfoSection,InfoRow}=vanilla();
 const [tab,setTab]=useState(area.tabs[0]?.id??'');
 const [confirmDelete,setConfirmDelete]=useState(false);
 useEffect(()=>{if(!area.tabs.some(item=>item.id===tab))setTab(area.tabs[0]?.id??'');},[area.tabs,tab]);
 useEffect(()=>setConfirmDelete(false),[area.stableId]);
 const action=(name:string)=>trigger('cs2AreaSystem',name,area.stableId);
 const headquarters=area.anchorRole==='Headquarters';
 const anchors=area.anchors??[];
 const multiple=area.anchorRole==='Entrance';
 // Ineligible or removed links stay listed (and removable) but never count as entrances.
 const validAnchors=anchors.filter(anchor=>anchor.valid!==false).length;
 const current=area.tabs.find(item=>item.id===tab);
 return <InfoSection disableFocus={true} className={styles.section}>
  <InfoRow left={text('CS2AS.UI.Type')} right={text(area.typeKey)} uppercase={true}/>
  <InfoRow left={text('CS2AS.UI.Status')} right={text(area.state)} tooltip={text(area.summary)}/>
  {area.level!==null&&<InfoRow left={text('CS2AS.UI.Level')} right={String(area.level)}/>}
  {area.level!==null&&area.experience!==null&&<InfoRow left={text('CS2AS.UI.Experience')} right={area.experience} subRow={true}/>}
  {headquarters&&<InfoRow left={text('CS2AS.UI.Headquarters')} right={area.anchorName??text('CS2AS.UI.NotAssigned')}/>}
  {multiple&&(validAnchors>0?<InfoRow left={text('CS2AS.UI.Entrances')} right={String(validAnchors)}/>:<InfoRow left={text('CS2AS.UI.Entrance')} right={text('CS2AS.UI.NotAssigned')}/>)}
  {multiple&&anchors.length>0&&<ul className={styles.anchors}>{anchors.map(anchor=><li key={anchor.key} className={anchor.valid===false?styles.invalidAnchor:undefined}>
   <span>{anchor.name??text('CS2AS.UI.AnchorRemoved')}{anchor.valid===false&&anchor.name!=null&&<small> · {text('CS2AS.UI.AnchorInvalid')}</small>}</span><Button variant="flat" onSelect={()=>trigger('cs2AreaSystem','removeAnchor',area.stableId,anchor.key)}>{text('CS2AS.UI.RemoveEntrance')}</Button></li>)}</ul>}
  {area.tabs.length>1&&<div role="tablist" className={styles.tabs}>{area.tabs.map(item=><Button key={item.id} variant="flat" selected={tab===item.id} onSelect={()=>setTab(item.id)}><span role="tab" aria-selected={tab===item.id}>{text(item.labelKey)}</span></Button>)}</div>}
  <p role="note" className={styles.summary}>{text(area.summary)}</p>
  {current?.items&&current.items.length>0&&<PanelBoundary key={current.id}><ModulePanel stableId={area.stableId} items={current.items}/></PanelBoundary>}
  {area.pendingAnchor?<div role="status" className={styles.pending}>{text('CS2AS.UI.SelectAnchor')}<Button variant="flat" onSelect={()=>action('cancelAnchorAssignment')}>{text('CS2AS.UI.CancelAssignment')}</Button></div>:
   area.anchorRole&&<div className={styles.actions}><Button variant="flat" onSelect={()=>action(headquarters?'assignHeadquarters':'assignEntrance')}>{text(headquarters?(area.anchorName?'CS2AS.UI.ChangeHeadquarters':'CS2AS.UI.AssignHeadquarters'):(validAnchors>0?'CS2AS.UI.AddEntrance':'CS2AS.UI.AssignEntrance'))}</Button></div>}
  <div className={styles.actions}>
   <Button variant="flat" onSelect={()=>action('editArea')}>{text('CS2AS.UI.EditArea')}</Button>
   {confirmDelete?<><span>{text('CS2AS.UI.DeleteConfirm')}</span><Button variant="flat" onSelect={()=>action('deleteArea')}>{text('CS2AS.UI.ConfirmDelete')}</Button><Button variant="flat" onSelect={()=>setConfirmDelete(false)}>{text('CS2AS.UI.Cancel')}</Button></>:
    <Button variant="flat" onSelect={()=>area.confirmDelete!==false?setConfirmDelete(true):action('deleteArea')}>{text('CS2AS.UI.DeleteArea')}</Button>}
  </div>
 </InfoSection>;
}
// A failure inside this section must never take down the vanilla selected-info panel.
export function AreaInfoSection(props:AreaSectionData){return <PanelBoundary><AreaInfoSectionContent {...props}/></PanelBoundary>;}
