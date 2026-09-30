import React from 'react';
import {trigger,useValue} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {getBindings} from './bindings';
import {localize} from './localization';
import {AreaListPanel} from './AreaListPanel';
import {PanelBoundary} from './boundary';
import styles from './AreaSystem.module.scss';

// Area details live in the vanilla selected-info panel (AreaInfoSection). This floating host only holds the
// managed area list (the route to select completed Space areas) and the anchor-assignment banner shown while
// the selection is on a building.
function BoundList(){
 const bindings=getBindings();const available=useValue(bindings.areaListAvailable);const open=useValue(bindings.areaListOpen);const areas=useValue(bindings.areaList);
 return available?<AreaListPanel open={open} areas={areas??[]}/>:null;
}
function PendingBanner(){
 const {translate}=useLocalization();const text=(key:string)=>localize(translate,key);
 const pending=useValue(getBindings().pendingAssignment);
 if(!pending)return null;
 return <div role="status" className={styles.banner}>
  <strong>{pending.name}</strong>
  <span>{text(pending.role==='Headquarters'?'CS2AS.UI.SelectHeadquarters':'CS2AS.UI.SelectEntrance')}</span>
  <button onClick={()=>trigger('cs2AreaSystem','cancelAnchorAssignment',pending.stableId)}>{text('CS2AS.UI.CancelAssignment')}</button>
 </div>;
}
function Host({children}:React.PropsWithChildren){
 const side=useValue(getBindings().panelSide)==='left'?'left':'right';
 return <div className={side==='left'?`${styles.host} ${styles.left}`:styles.host}>{children}</div>;
}
export function AreaSystemHost(){return <PanelBoundary><Host><PanelBoundary><BoundList/></PanelBoundary><PanelBoundary><PendingBanner/></PanelBoundary></Host></PanelBoundary>;}
