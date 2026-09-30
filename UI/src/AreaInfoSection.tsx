import React,{useEffect,useState} from 'react';
import {trigger} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {Button} from 'cs2/ui';
import type {AreaSectionData} from './bindings';
import {localize} from './localization';
import {PanelBoundary} from './boundary';
import {vanilla} from './vanilla';
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
 return <InfoSection disableFocus={true} className={styles.section}>
  <InfoRow left={text('CS2AS.UI.Type')} right={text(area.typeKey)} uppercase={true}/>
  <InfoRow left={text('CS2AS.UI.Status')} right={text(area.state)} tooltip={text(area.summary)}/>
  {area.level!==null&&<InfoRow left={text('CS2AS.UI.Level')} right={String(area.level)}/>}
  {area.level!==null&&area.experience!==null&&<InfoRow left={text('CS2AS.UI.Experience')} right={area.experience} subRow={true}/>}
  {area.anchorRole&&<InfoRow left={text(headquarters?'CS2AS.UI.Headquarters':'CS2AS.UI.Entrance')} right={area.anchorName??text('CS2AS.UI.NotAssigned')}/>}
  {area.tabs.length>1&&<div role="tablist" className={styles.tabs}>{area.tabs.map(item=><Button key={item.id} variant="flat" selected={tab===item.id} onSelect={()=>setTab(item.id)}><span role="tab" aria-selected={tab===item.id}>{text(item.labelKey)}</span></Button>)}</div>}
  <p role="note" className={styles.summary}>{text(area.summary)}</p>
  {area.pendingAnchor?<div role="status" className={styles.pending}>{text('CS2AS.UI.SelectAnchor')}<Button variant="flat" onSelect={()=>action('cancelAnchorAssignment')}>{text('CS2AS.UI.CancelAssignment')}</Button></div>:
   area.anchorRole&&<div className={styles.actions}><Button variant="flat" onSelect={()=>action(headquarters?'assignHeadquarters':'assignEntrance')}>{text(area.anchorName?(headquarters?'CS2AS.UI.ChangeHeadquarters':'CS2AS.UI.ChangeEntrance'):(headquarters?'CS2AS.UI.AssignHeadquarters':'CS2AS.UI.AssignEntrance'))}</Button></div>}
  <div className={styles.actions}>
   <Button variant="flat" onSelect={()=>action('editArea')}>{text('CS2AS.UI.EditArea')}</Button>
   {confirmDelete?<><span>{text('CS2AS.UI.DeleteConfirm')}</span><Button variant="flat" onSelect={()=>action('deleteArea')}>{text('CS2AS.UI.ConfirmDelete')}</Button><Button variant="flat" onSelect={()=>setConfirmDelete(false)}>{text('CS2AS.UI.Cancel')}</Button></>:
    <Button variant="flat" onSelect={()=>area.confirmDelete!==false?setConfirmDelete(true):action('deleteArea')}>{text('CS2AS.UI.DeleteArea')}</Button>}
  </div>
 </InfoSection>;
}
// A failure inside this section must never take down the vanilla selected-info panel.
export function AreaInfoSection(props:AreaSectionData){return <PanelBoundary><AreaInfoSectionContent {...props}/></PanelBoundary>;}
