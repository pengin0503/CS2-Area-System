import React,{createContext,useContext,useMemo} from 'react';
import {useValue} from 'cs2/api';
import {getModule} from 'cs2/modding';
import type {ModuleRegistry} from 'cs2/modding';
import {getBudgetBindings} from './bindings';
import type {AreaBudgetData,VanillaBudgetItem} from './bindings';
export type {VanillaBudgetItem} from './bindings';

// Area rows on the vanilla Budget page (docs/verification/area-budget-ui-contracts.md). The settled amounts come from
// cs2AreaSystem.areaBudget; this file only places them in the Service Upkeep and Service Fees groups and adds each once
// to the displayed subtotals and balance. It never moves money and never borrows a vanilla income or expense source.
export const BUDGET_PAGE='game-ui/game/components/economy-panel/budget-page/budget-page.tsx';
export const BUDGET_SECTION='game-ui/game/components/economy-panel/budget-page/budget-section/budget-section.tsx';
export const BUDGET_ITEM_DETAIL='game-ui/game/components/economy-panel/budget-page/budget-item-detail/budget-item-detail.tsx';
export const MONEY_FIELD='game-ui/game/components/economy-panel/components/money-field/money-field.tsx';
export const BUDGET_PAGE_CLASSES='game-ui/game/components/economy-panel/budget-page/budget-page.module.scss';
// Source ids of the two Area rows; the vanilla detail labels them EconomyPanel.BUDGET_SUB_ITEM[<id>].
export const AREA_MANAGEMENT_SOURCE='CS2AS.AreaManagement';
export const AREA_SERVICE_FEES_SOURCE='CS2AS.AreaServiceFees';
const SERVICE_UPKEEP_SOURCE='ServiceUpkeep';
const SERVICE_FEES_ID='Service Fees';

type Values=readonly number[];
interface Targets {expense:VanillaBudgetItem|null;income:VanillaBudgetItem|null}
interface AreaBudgetState extends Targets {budget:AreaBudgetData;totalField:string}
// A settled amount from C#; null when the binding has not published one.
export function sanitizeAreaBudget(value:unknown):AreaBudgetData|null{
 if(value==null||typeof value!=='object')return null;
 const amount=(v:unknown)=>typeof v==='number'&&Number.isFinite(v)&&v>0?v:0;
 const raw=value as Partial<AreaBudgetData>;
 return {areaManagement:amount(raw.areaManagement),areaServiceFees:amount(raw.areaServiceFees)};
}
const isFee=(source:{id:string})=>source.id.startsWith('Fee');
const unique=<T,>(items:T[])=>items.length===1?items[0]:null;
// The vanilla groups, by their sources: anything ambiguous or missing resolves to null and that side stays vanilla.
export function resolveBudgetTargets(incomeItems:readonly VanillaBudgetItem[]|null|undefined,expenseItems:readonly VanillaBudgetItem[]|null|undefined):Targets{
 const expense=unique((expenseItems??[]).filter(i=>i.sources.some(s=>s.id===SERVICE_UPKEEP_SOURCE)));
 const income=incomeItems??[];
 const named=unique(income.filter(i=>i.id===SERVICE_FEES_ID&&i.sources.some(isFee)));
 const feeOnly=unique(income.filter(i=>i.sources.length>0&&i.sources.every(isFee)));
 return {expense,income:named??(feeOnly&&feeOnly.sources.length>=2?feeOnly:null)};
}
const sameItem=(a:VanillaBudgetItem,b:VanillaBudgetItem|null)=>b!=null&&a.id===b.id&&a.sources.length===b.sources.length&&a.sources.every((s,n)=>s.id===b.sources[n].id&&s.index===b.sources[n].index);
interface AreaRow {target:VanillaBudgetItem;source:string;value:number}
function rows(state:AreaBudgetState):AreaRow[]{
 const result:AreaRow[]=[];
 if(state.expense)result.push({target:state.expense,source:AREA_MANAGEMENT_SOURCE,value:-state.budget.areaManagement});
 if(state.income)result.push({target:state.income,source:AREA_SERVICE_FEES_SOURCE,value:state.budget.areaServiceFees});
 return result;
}
// Copies the item and values with the Area row appended; the vanilla binding data is never modified.
function withRow(item:VanillaBudgetItem,values:Values,row:AreaRow){
 return {item:{...item,sources:[...item.sources,{id:row.source,index:values.length}]},values:[...values,row.value]};
}
export function reconcileSection<P extends {total:number;items:readonly VanillaBudgetItem[];values:Values}>(props:P,state:AreaBudgetState|null):P{
 if(!state)return props;
 const row=rows(state).find(r=>props.items.some(i=>sameItem(i,r.target)));
 if(!row)return props;
 const index=props.items.findIndex(i=>sameItem(i,row.target));
 const added=withRow(props.items[index],props.values,row);
 const items=props.items.map((i,n)=>n===index?added.item:i);
 return {...props,total:props.total+row.value,items,values:added.values};
}
export function reconcileDetail<P extends {item:VanillaBudgetItem|null;values:Values}>(props:P,state:AreaBudgetState|null):P{
 if(!state||!props.item||!props.values)return props;
 const row=rows(state).find(r=>sameItem(props.item!,r.target));
 return row?{...props,...withRow(props.item,props.values,row)}:props;
}
export function reconcileBalance(value:number,state:AreaBudgetState|null){
 return state?rows(state).reduce((sum,row)=>sum+row.value,value):value;
}

const AreaBudgetContext=createContext<AreaBudgetState|null>(null);
let warned=false;
function contained<T>(fallback:T,compute:()=>T):T{
 try{return compute();}
 catch(error){if(!warned){warned=true;console.warn('Area budget rows disabled after an error; the vanilla budget is shown.',error);}return fallback;}
}
type Component<P>=React.ComponentType<P>;
const isComponent=(value:unknown)=>typeof value==='function'||(typeof value==='object'&&value!==null&&'$$typeof' in value);
function lookup(path:string,name:string):unknown{
 const value=getModule(path,name);
 if(value==null)throw new Error(`${path}@${name} is missing`);
 return value;
}
// Registers only when every 1.6.2f1 extension point is present; otherwise nothing is extended (fail closed).
export function registerAreaBudget(registry:Pick<ModuleRegistry,'extend'>):boolean{
 let totalField:string;
 try{
  for(const [path,name] of [[BUDGET_PAGE,'BudgetPage'],[BUDGET_SECTION,'BudgetSection'],[BUDGET_ITEM_DETAIL,'BudgetItemDetail'],[MONEY_FIELD,'NumberField']]){
   if(!isComponent(lookup(path,name)))throw new Error(`${path}@${name} is not a component`);
  }
  const classes=lookup(BUDGET_PAGE_CLASSES,'classes') as Record<string,unknown>;
  if(typeof classes.totalField!=='string'||!classes.totalField)throw new Error(`${BUDGET_PAGE_CLASSES} has no totalField class`);
  totalField=classes.totalField;
 }catch(error){
  console.warn('Area budget rows are not shown: the 1.6.2f1 Budget page extension point could not be verified.',error);
  return false;
 }
 // The page wrapper provides the context that enables the other three, so it is extended last: if any extension fails,
 // the wrappers already in place pass everything through and the page stays vanilla.
 try{
  registry.extend(BUDGET_SECTION,'BudgetSection',(Section:Component<any>)=>{
   const AreaBudgetSection=(props:{total:number;items:VanillaBudgetItem[];values:Values})=>{
    const state=useContext(AreaBudgetContext);
    return <Section {...contained(props,()=>reconcileSection(props,state))}/>;
   };
   return AreaBudgetSection;
  });
  registry.extend(BUDGET_ITEM_DETAIL,'BudgetItemDetail',(Detail:Component<any>)=>{
   const AreaBudgetItemDetail=(props:{item:VanillaBudgetItem|null;values:Values})=>{
    const state=useContext(AreaBudgetContext);
    return <Detail {...contained(props,()=>reconcileDetail(props,state))}/>;
   };
   return AreaBudgetItemDetail;
  });
  registry.extend(MONEY_FIELD,'NumberField',(Field:Component<any>)=>{
   // Only the page's balance carries classes.totalField; every other money field passes through.
   const AreaBudgetNumberField=(props:{value:number;className?:string})=>{
    const state=useContext(AreaBudgetContext);
    if(!state||props.className!==state.totalField)return <Field {...props}/>;
    return <Field {...props} value={contained(props.value,()=>reconcileBalance(props.value,state))}/>;
   };
   return AreaBudgetNumberField;
  });
  registry.extend(BUDGET_PAGE,'BudgetPage',(Page:Component<Record<string,never>>)=>{
   const AreaBudgetPage=(props:Record<string,never>)=>{
    const bindings=getBudgetBindings();
    const budget=useValue(bindings.areaBudget),incomeItems=useValue(bindings.incomeItems),expenseItems=useValue(bindings.expenseItems);
    const state=useMemo(()=>contained(null,()=>{
     const settled=sanitizeAreaBudget(budget);
     return settled?{...resolveBudgetTargets(incomeItems,expenseItems),budget:settled,totalField}:null;
    }),[budget,incomeItems,expenseItems]);
    return <AreaBudgetContext.Provider value={state}><Page {...props}/></AreaBudgetContext.Provider>;
   };
   return AreaBudgetPage;
  });
 }catch(error){
  console.warn('Area budget rows are not shown: extending the 1.6.2f1 Budget page failed.',error);
  return false;
 }
 return true;
}
