import React, {useEffect,useState} from 'react';
import {trigger,useValue} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {getBindings,AreaView} from './bindings';
import styles from './AreaManagementPanel.module.scss';

export function AreaManagementPanel({area}:{area:AreaView}){
 const {translate}=useLocalization();
 const text=(key:string)=>translate(key)??key;
 const [name,setName]=useState(area.name);
 const [tab,setTab]=useState(area.tabs[0]?.id??'');
 const [confirmDelete,setConfirmDelete]=useState(false);
 useEffect(()=>setName(area.name),[area.name]);
 useEffect(()=>{
  if(!area.tabs.some(item=>item.id===tab))setTab(area.tabs[0]?.id??'');
 },[area.tabs,tab]);
 const action=(name:string,...args:unknown[])=>trigger('cs2AreaSystem',name,area.stableId,...args);
 return <aside className={styles.panel} aria-label={text('CS2AS.UI.AreaManagement')}>
  <header className={styles.header}><h2>{area.name}</h2><div>{text(area.typeKey)}</div><span className={styles.status}>{text(area.state)}</span></header>
  {area.level!==null&&<section className={styles.section}>{text('CS2AS.UI.Level')} {area.level}<div className={styles.progress}>{text('CS2AS.UI.Experience')} {area.experience}</div></section>}
  <section className={styles.section}>
   <div className={styles.actions}><button onClick={()=>action('editArea')}>{text('CS2AS.UI.EditArea')}</button><button onClick={()=>action('locateArea')}>{text('CS2AS.UI.Locate')}</button></div>
   <form onSubmit={event=>{event.preventDefault();action('renameArea',name);}}><input aria-label={text('CS2AS.UI.AreaName')} value={name} maxLength={128} onChange={event=>setName(event.target.value)}/><button type="submit">{text('CS2AS.UI.Rename')}</button></form>
   {area.anchorRole&&!area.pendingAnchor&&<button onClick={()=>action(area.anchorRole==='Headquarters'?'assignHeadquarters':'assignEntrance')}>{text(area.anchorRole==='Headquarters'?'CS2AS.UI.AssignHeadquarters':'CS2AS.UI.AssignEntrance')}</button>}
   {area.pendingAnchor&&<div role="status">{text('CS2AS.UI.SelectAnchor')}<button onClick={()=>action('cancelAnchorAssignment')}>{text('CS2AS.UI.CancelAssignment')}</button></div>}
  </section>
  <div role="tablist" className={styles.tabs}>{area.tabs.map(item=><button role="tab" key={item.id} aria-selected={tab===item.id} onClick={()=>setTab(item.id)}>{text(item.labelKey)}</button>)}</div>
  <section role="tabpanel" className={styles.section}>{text(area.summary)}</section>
  <footer className={styles.section}>{confirmDelete?<><span>{text('CS2AS.UI.DeleteConfirm')}</span><button onClick={()=>action('deleteArea')}>{text('CS2AS.UI.ConfirmDelete')}</button><button onClick={()=>setConfirmDelete(false)}>{text('CS2AS.UI.Cancel')}</button></>:<button onClick={()=>setConfirmDelete(true)}>{text('CS2AS.UI.DeleteArea')}</button>}</footer>
 </aside>;
}
class PanelBoundary extends React.Component<React.PropsWithChildren,{failed:boolean}>{
 state={failed:false};
 static getDerivedStateFromError(){return {failed:true};}
 render(){return this.state.failed?null:this.props.children;}
}
function BoundPanel(){
 const bindings=getBindings();const area=useValue(bindings.selectedArea);const visible=useValue(bindings.panelVisible);
 return visible&&area?<AreaManagementPanel key={area.stableId} area={area}/>:null;
}
export function AreaManagementPanelHost(){return <PanelBoundary><BoundPanel/></PanelBoundary>;}
