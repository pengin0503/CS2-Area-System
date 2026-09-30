import React, {useState} from 'react';
import {trigger,useValue} from 'cs2/api';
import {getBindings,AreaView} from './bindings';
import styles from './AreaManagementPanel.module.scss';
const labels:Record<string,string>={'CS2AS.Type.Park':'Park Area','CS2AS.Type.Industrial':'Industrial Area','CS2AS.Type.Tourism':'Tourism Area','CS2AS.Tab.Overview':'Overview','CS2AS.Summary.Active':'Active','CS2AS.Summary.AnchorRequired':'Assign a required facility to activate this area.'};
const label=(key:string)=>labels[key]??key;
export function AreaManagementPanel({area}:{area:AreaView}){
 const [name,setName]=useState(area.name);
 const [tab,setTab]=useState(area.tabs[0]?.id??'');
 const [confirmDelete,setConfirmDelete]=useState(false);
 const action=(name:string,...args:unknown[])=>trigger('cs2AreaSystem',name,area.stableId,...args);
 return <aside className={styles.panel} aria-label="Area management">
  <header className={styles.header}><h2>{area.name}</h2><div>{label(area.typeKey)}</div><span className={styles.status}>{area.state}</span></header>
  {area.level!==null&&<section className={styles.section}>Level {area.level}<div className={styles.progress}>Experience {area.experience}</div></section>}
  <section className={styles.section}>
   <div className={styles.actions}><button onClick={()=>action('editArea')}>Edit area</button><button onClick={()=>action('locateArea')}>Locate</button></div>
   <form onSubmit={event=>{event.preventDefault();action('renameArea',name);}}><input aria-label="Area name" value={name} maxLength={128} onChange={event=>setName(event.target.value)}/><button type="submit">Rename</button></form>
   {area.anchorRole&&!area.pendingAnchor&&<button onClick={()=>action(area.anchorRole==='Headquarters'?'assignHeadquarters':'assignEntrance')}>{area.anchorRole==='Headquarters'?'Assign headquarters':'Assign entrance'}</button>}
   {area.pendingAnchor&&<div role="status">Select an existing building inside this area.<button onClick={()=>action('cancelAnchorAssignment')}>Cancel assignment</button></div>}
  </section>
  <div role="tablist" className={styles.tabs}>{area.tabs.map(item=><button role="tab" key={item.id} aria-selected={tab===item.id} onClick={()=>setTab(item.id)}>{label(item.labelKey)}</button>)}</div>
  <section role="tabpanel" className={styles.section}>{label(area.summary)}</section>
  <footer className={styles.section}>{confirmDelete?<><span>Delete this management boundary?</span><button onClick={()=>action('deleteArea')}>Confirm delete</button><button onClick={()=>setConfirmDelete(false)}>Cancel</button></>:<button onClick={()=>setConfirmDelete(true)}>Delete area</button>}</footer>
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
