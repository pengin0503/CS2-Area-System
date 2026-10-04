import React,{useEffect,useRef,useState} from 'react';
import {trigger} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {Button} from 'cs2/ui';
import type {AreaPanelItem} from './bindings';
import {localize} from './localization';
import {vanilla} from './vanilla';
import styles from './AreaSystem.module.scss';

// Generic renderer for module-neutral panel items. Every mutation goes through the guarded C# moduleAction
// trigger together with the stable ID the section was rendered for.
type Props={stableId:string;items:AreaPanelItem[]};
const clamp=(value:number,min:number,max:number)=>Math.min(Math.max(value,Math.min(min,max)),Math.max(min,max));
export function snap(value:number,min:number,max:number,step:number){
 const snapped=step>0?min+Math.round((value-min)/step)*step:value;
 return clamp(Math.round(snapped*1e6)/1e6,min,max);
}
export function ModulePanel({stableId,items}:Props){
 const {translate}=useLocalization();
 const text=(key:string)=>localize(translate,key);
 const send=(action:string|null,value:string)=>{if(action)trigger('cs2AreaSystem','moduleAction',stableId,action,value);};
 return <div className={styles.module}>{items.map(item=><Item key={item.kind+':'+item.id} item={item} text={text} send={send}/>)}</div>;
}
type ItemProps={item:AreaPanelItem;text:(key:string)=>string;send:(action:string|null,value:string)=>void};
function valueText(item:AreaPanelItem,text:(key:string)=>string){
 if(item.valueKey)return text(item.valueKey);
 if(item.value!=null)return item.value;
 return item.numericValue!=null?String(Math.round(item.numericValue)):'';
}
function Item({item,text,send}:ItemProps){
 const {InfoRow}=vanilla();
 const tooltip=item.tooltipKey?text(item.tooltipKey):undefined;
 switch(item.kind){
 case 'row':return <InfoRow left={text(item.labelKey)} right={valueText(item,text)} tooltip={tooltip}/>;
 case 'bar':{
  const min=item.min??0,max=item.max??100,value=clamp(item.numericValue??min,min,max);
  const ratio=max!==min?(value-min)/(max-min):0;
  const label=text(item.labelKey);
  return <div className={styles.bar} title={tooltip}>
   <div className={styles.barLabel}><span>{label}</span><span>{valueText(item,text)}</span></div>
   <div role="progressbar" aria-label={label} aria-valuemin={min} aria-valuemax={max} aria-valuenow={value} className={styles.barTrack}>
    <div className={styles.barFill} style={{width:`${Math.round(ratio*100)}%`}}/></div>
  </div>;}
 case 'check':{
  const met=item.numericValue===1;
  return <InfoRow left={text(item.labelKey)} right={`${valueText(item,text)} · ${text(met?'CS2AS.UI.Met':'CS2AS.UI.NotMet')}`} tooltip={tooltip} className={met?styles.met:styles.unmet}/>;}
 case 'warning':return <div role="alert" className={styles.warning} title={tooltip}>{text(item.labelKey)}{item.value?` ${item.value}`:''}</div>;
 case 'note':return <p role="note" className={styles.note}>{text(item.labelKey)}</p>;
 case 'slider':return <SliderItem item={item} text={text} send={send}/>;
 case 'select':return <SelectItem item={item} text={text} send={send}/>;
 case 'button':return <div className={styles.actions}><Button variant="flat" disabled={item.disabled} onSelect={()=>{if(!item.disabled)send(item.action,item.value??'');}}>{text(item.labelKey)}</Button></div>;
 default:return null;
 }
}
function SliderItem({item,text,send}:ItemProps){
 const {InfoRow,Slider}=vanilla();
 const min=item.min??0,max=item.max??100,step=item.step??1;
 const [local,setLocal]=useState(item.numericValue??min);
 const localRef=useRef(local);
 const lastSent=useRef(item.numericValue);
 const dragging=useRef(false);
 const show=(value:number)=>{localRef.current=value;setLocal(value);};
 // A new server value resets both the displayed value (unless dragging) and the last-sent marker.
 useEffect(()=>{lastSent.current=item.numericValue;if(!dragging.current)show(item.numericValue??min);},[item.numericValue,min]);
 const commit=(value:number)=>{
  const next=snap(value,min,max,step);show(next);
  if(!item.disabled&&next!==lastSent.current){lastSent.current=next;send(item.action,String(next));}
 };
 const shown=local===item.numericValue?valueText(item,text):String(local);
 return <div className={styles.slider}>
  <InfoRow left={text(item.labelKey)} right={shown} tooltip={item.tooltipKey?text(item.tooltipKey):undefined}/>
  {Slider?<Slider value={local} start={min} end={max} gamepadStep={step} disabled={item.disabled}
    valueTransformer={(start,end,ratio)=>snap(start+ratio*(end-start),min,max,step)}
    onDragStart={()=>{dragging.current=true;}}
    onChange={value=>{if(dragging.current)show(snap(value,min,max,step));else commit(value);}}
    onDragEnd={()=>{dragging.current=false;commit(localRef.current);}}/>:
   <div className={styles.actions}>
    <Button variant="flat" disabled={item.disabled} onSelect={()=>{if(!item.disabled)commit(localRef.current-step);}}>-</Button>
    <Button variant="flat" disabled={item.disabled} onSelect={()=>{if(!item.disabled)commit(localRef.current+step);}}>+</Button>
   </div>}
 </div>;
}
// Bounded select rendered as a previous/next stepper with cs2/ui buttons only (no undocumented vanilla dropdown API).
// The chosen option value goes through the guarded moduleAction; C# re-validates it against the live state.
function SelectItem({item,text,send}:ItemProps){
 const {InfoRow}=vanilla();
 const options=(item.options??[]).slice(0,64);
 const index=options.findIndex(option=>option.value===item.value);
 const current=index>=0?options[index]:null;
 const label=current?(current.labelKey?text(current.labelKey):current.label??current.value):'—';
 const enabled=!item.disabled&&options.length>0;
 const step=(delta:number)=>{
  if(!enabled)return;
  const next=index<0?(delta>0?0:options.length-1):(index+delta+options.length)%options.length;
  send(item.action,options[next].value);
 };
 return <div className={styles.slider}>
  <InfoRow left={text(item.labelKey)} right={label} tooltip={item.tooltipKey?text(item.tooltipKey):undefined}/>
  <div className={styles.actions}>
   <Button variant="flat" disabled={!enabled} onSelect={()=>step(-1)}>{text('CS2AS.UI.Previous')}</Button>
   <Button variant="flat" disabled={!enabled} onSelect={()=>step(1)}>{text('CS2AS.UI.Next')}</Button>
  </div>
 </div>;
}
