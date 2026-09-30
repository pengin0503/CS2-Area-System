import React from 'react';
import {trigger} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import type {AreaListItem} from './bindings';
import {localize} from './localization';
import styles from './AreaSystem.module.scss';

// Mod-owned route to completed managed areas: the vanilla default tool does not raycast Space areas.
export function AreaListPanel({open,areas}:{open:boolean;areas:AreaListItem[]}){
 const {translate}=useLocalization();
 const text=(key:string)=>localize(translate,key);
 return <nav className={styles.list} aria-label={text('CS2AS.UI.AreaList')}>
  <button aria-expanded={open} onClick={()=>trigger('cs2AreaSystem','setAreaListOpen',!open)}>{text('CS2AS.UI.AreaList')}</button>
  {open&&(areas.length===0?<div role="status" className={styles.empty}>{text('CS2AS.UI.NoAreas')}</div>:
   <ul className={styles.items}>{areas.map(area=><li key={area.stableId}>
    <button onClick={()=>trigger('cs2AreaSystem','selectArea',area.stableId)}><span>{area.name}</span><small>{text(area.typeKey)} · {text(area.state)}</small></button>
   </li>)}</ul>)}
 </nav>;
}
