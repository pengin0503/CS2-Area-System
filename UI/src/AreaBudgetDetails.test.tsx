import React from 'react';
import {render} from '@testing-library/react';
import {describe,it,expect,vi,afterEach,beforeEach} from 'vitest';
import type {VanillaBudgetItem} from './AreaBudgetDetails';
// A stand-in for the 1.6.2f1 module registry: exports are looked up at render time, as the bundle's module-local
// variables are after a setter override (docs/verification/area-budget-ui-contracts.md).
const env=vi.hoisted(()=>({modules:{} as Record<string,Record<string,unknown>>,values:{} as Record<string,unknown>,selected:null as string|null}));
vi.mock('cs2/api',()=>({bindValue:(group:string,name:string,fallback?:unknown)=>({key:`${group}.${name}`,fallback}),
 useValue:(binding:{key:string;fallback:unknown})=>binding.key in env.values?env.values[binding.key]:binding.fallback,trigger:vi.fn()}));
vi.mock('cs2/modding',()=>({getModule:(path:string,name:string)=>{
 const module=env.modules[path];
 if(!module||module[name]==null)throw `Module ${path}@${name} was not found.`;
 return module[name];
}}));
const budget=await import('./AreaBudgetDetails');
const {BUDGET_PAGE,BUDGET_SECTION,BUDGET_ITEM_DETAIL,MONEY_FIELD,BUDGET_PAGE_CLASSES,AREA_MANAGEMENT_SOURCE,AREA_SERVICE_FEES_SOURCE}=budget;
const {bindValue,useValue}=await import('cs2/api');
type Values=number[];
const item=(id:string,...sources:[string,number][]):VanillaBudgetItem=>({id,color:{r:1,g:1,b:1,a:1},icon:'',active:true,sources:sources.map(([sourceId,index])=>({id:sourceId,index}))});
// IncomeSource / ExpenseSource indices of 1.6.2f1 (Game.City).
const incomeItems=[item('Residential',['TaxResidential',0]),item('Service Fees',['FeeHealthcare',3],['FeeElectricity',4],['FeeEducation',6],['FeeParking',9],['FeePublicTransport',10],['FeeGarbage',12],['FeeWater',13]),
 item('Exports',['ExportElectricity',7],['ExportWater',8])];
const incomeValues:Values=[1000,0,0,30,40,0,60,70,80,90,100,0,120,130];
const expenseItems=[item('Service Upkeep',['ServiceUpkeep',5]),item('Loan Interest',['LoanInterest',1]),item('Imports',['ImportElectricity',2],['ImportWater',3])];
const expenseValues:Values=[0,-50,-20,-10,0,-900,0,0,0,0,0,0,0,0,0];
const sum=(values:Values)=>values.reduce((a,b)=>a+b,0);
const valueOf=(item:VanillaBudgetItem,values:Values)=>item.sources.reduce((a,source)=>a+values[source.index],0);
const TOTAL_FIELD='total-field_zjM';
// Fakes shaped like the 1.6.2f1 components: BudgetPage reads the budget bindings and renders the current exports.
const current=(path:string,name:string)=>env.modules[path][name] as React.ComponentType<any>;
const NumberField=({value,className}:{value:number;className?:string})=><span className={className} data-value={value}>{value}</span>;
const BudgetSection=React.memo(({title,total,items,values}:{title:string;total:number;items:VanillaBudgetItem[];values:Values})=>{
 const Field=current(MONEY_FIELD,'NumberField');
 return <section data-section={title}><header><Field value={total} className="value-field"/></header>
  {items.map(i=>i.active?<div key={i.id} data-item={i.id}><Field value={valueOf(i,values)} className="value-field"/></div>:null)}</section>;
});
const BudgetItemDetail=({item,values}:{item:VanillaBudgetItem|null;values:Values})=>
 <aside data-detail={item?.id??''}>{item?.sources.map((s,n)=><div key={n} data-source={s.id} data-value={values[s.index]}/>)}</aside>;
const BudgetPage=()=>{
 const income=useValue(bindValue<VanillaBudgetItem[]>('budget','incomeItems')),incomeV=useValue(bindValue<Values>('budget','incomeValues')),
  expense=useValue(bindValue<VanillaBudgetItem[]>('budget','expenseItems')),expenseV=useValue(bindValue<Values>('budget','expenseValues')),
  totalIncome=useValue(bindValue<number>('budget','totalIncome')),totalExpenses=useValue(bindValue<number>('budget','totalExpenses'));
 const Section=current(BUDGET_SECTION,'BudgetSection'),Field=current(MONEY_FIELD,'NumberField'),Detail=current(BUDGET_ITEM_DETAIL,'BudgetItemDetail');
 const incomeSelected=income.find(i=>i.id===env.selected),expenseSelected=expense.find(i=>i.id===env.selected);
 return <div>
  <Section title="income" total={totalIncome} items={income} values={incomeV}/>
  <Section title="expenses" total={totalExpenses} items={expense} values={expenseV}/>
  <div data-balance=""><Field value={totalIncome+totalExpenses} importance="primary" className={TOTAL_FIELD}/></div>
  <Detail item={incomeSelected??expenseSelected??null} values={incomeSelected!=null?incomeV:expenseV}/>
 </div>;
};
const vanillaModules=()=>({[BUDGET_PAGE]:{BudgetPage},[BUDGET_SECTION]:{BudgetSection},[BUDGET_ITEM_DETAIL]:{BudgetItemDetail},
 [MONEY_FIELD]:{NumberField,sign:()=>''},[BUDGET_PAGE_CLASSES]:{classes:{totalField:TOTAL_FIELD,totalSection:'total-section_TGf'}}});
const registry={extend:vi.fn((path:string,name:string,fn:(current:unknown)=>unknown)=>{const module=env.modules[path];if(!module)throw `Module ${path} was not found.`;module[name]=fn(module[name]);})};
const asRegistry=registry as unknown as Parameters<typeof budget.registerAreaBudget>[0];
const setBudget=(areaManagement:number,areaServiceFees:number)=>{env.values['cs2AreaSystem.areaBudget']={areaManagement,areaServiceFees};};
beforeEach(()=>{
 env.modules=vanillaModules();env.selected=null;
 env.values={'budget.incomeItems':incomeItems,'budget.incomeValues':incomeValues,'budget.expenseItems':expenseItems,'budget.expenseValues':expenseValues,
  'budget.totalIncome':sum(incomeValues),'budget.totalExpenses':sum(expenseValues)};
 setBudget(1200,450);registry.extend.mockClear();
});
afterEach(()=>vi.restoreAllMocks());
const renderPage=()=>{const Page=current(BUDGET_PAGE,'BudgetPage');return render(<Page/>);};
const number=(element:Element|null)=>Number(element!.getAttribute('data-value'));
const sectionTotal=(view:ReturnType<typeof render>,title:string)=>number(view.container.querySelector(`[data-section="${title}"] > header > span`));
const row=(view:ReturnType<typeof render>,title:string,id:string)=>number(view.container.querySelector(`[data-section="${title}"] [data-item="${id}"] span`));
const balance=(view:ReturnType<typeof render>)=>number(view.container.querySelector('[data-balance] span'));
const detailRows=(view:ReturnType<typeof render>)=>[...view.container.querySelectorAll('[data-source]')].map(e=>[e.getAttribute('data-source'),Number(e.getAttribute('data-value'))]);

describe('vanilla budget targets',()=>{
 it('finds Service Upkeep as the one expense item carrying the ServiceUpkeep source',()=>{
  expect(budget.resolveBudgetTargets(incomeItems,expenseItems).expense?.id).toBe('Service Upkeep');
  expect(budget.resolveBudgetTargets(incomeItems,[expenseItems[1]]).expense).toBeNull();
  expect(budget.resolveBudgetTargets(incomeItems,[...expenseItems,item('Upkeep Copy',['ServiceUpkeep',5])]).expense).toBeNull();
 });
 it('finds the Service Fees group without naming any single fee source',()=>{
  expect(budget.resolveBudgetTargets(incomeItems,expenseItems).income?.id).toBe('Service Fees');
  const unnamed=[incomeItems[0],item('Fees',['FeeHealthcare',3],['FeeWater',13]),incomeItems[2]];
  expect(budget.resolveBudgetTargets(unnamed,expenseItems).income?.id).toBe('Fees');
 });
 it('fails closed when the fee group is ambiguous or missing',()=>{
  const twoFeeGroups=[incomeItems[0],item('Utility Fees',['FeeWater',13]),item('Transport Fees',['FeeParking',9],['FeePublicTransport',10])];
  expect(budget.resolveBudgetTargets(twoFeeGroups,expenseItems).income).toBeNull();
  const mixed=[incomeItems[0],item('Utilities',['FeeWater',13],['ExportWater',8])];
  expect(budget.resolveBudgetTargets(mixed,expenseItems).income).toBeNull();
  expect(budget.resolveBudgetTargets([],expenseItems).income).toBeNull();
 });
 it('never adopts a lone parking or transport fee item as the Service Fees group',()=>{
  const parkingOnly=[incomeItems[0],item('Parking',['FeeParking',9])];
  expect(budget.resolveBudgetTargets(parkingOnly,expenseItems).income).toBeNull();
  const transportOnly=[incomeItems[0],item('Transport',['FeePublicTransport',10])];
  expect(budget.resolveBudgetTargets(transportOnly,expenseItems).income).toBeNull();
 });
});

describe('area budget data',()=>{
 it('accepts only finite non-negative settled amounts',()=>{
  expect(budget.sanitizeAreaBudget({areaManagement:1200,areaServiceFees:450})).toEqual({areaManagement:1200,areaServiceFees:450});
  expect(budget.sanitizeAreaBudget({areaManagement:-5,areaServiceFees:Number.NaN})).toEqual({areaManagement:0,areaServiceFees:0});
  expect(budget.sanitizeAreaBudget(null)).toBeNull();
  expect(budget.sanitizeAreaBudget('1200')).toBeNull();
 });
});

describe('registered budget page',()=>{
 it('registers through the verified 1.6.2f1 exports only',()=>{
  expect(budget.registerAreaBudget(asRegistry)).toBe(true);
  expect(registry.extend.mock.calls.map(c=>`${c[0]}@${c[1]}`).sort()).toEqual([
   'game-ui/game/components/economy-panel/budget-page/budget-item-detail/budget-item-detail.tsx@BudgetItemDetail',
   'game-ui/game/components/economy-panel/budget-page/budget-page.tsx@BudgetPage',
   'game-ui/game/components/economy-panel/budget-page/budget-section/budget-section.tsx@BudgetSection',
   'game-ui/game/components/economy-panel/components/money-field/money-field.tsx@NumberField']);
  expect(BUDGET_PAGE_CLASSES).toBe('game-ui/game/components/economy-panel/budget-page/budget-page.module.scss');
 });
 it('adds Area Management to Service Upkeep and reconciles the expense total and balance once',()=>{
  budget.registerAreaBudget(asRegistry);
  const view=renderPage();
  expect(sectionTotal(view,'expenses')).toBe(sum(expenseValues)-1200);
  expect(row(view,'expenses','Service Upkeep')).toBe(-900-1200);
  expect(row(view,'expenses','Loan Interest')).toBe(-50);
  expect(row(view,'expenses','Imports')).toBe(-30);
  expect(balance(view)).toBe(sum(incomeValues)+sum(expenseValues)+450-1200);
 });
 it('adds Area Service Fees to Service Fees and reconciles the income total once, leaving every fixed fee source untouched',()=>{
  budget.registerAreaBudget(asRegistry);
  const view=renderPage();
  const vanillaFees=valueOf(incomeItems[1],incomeValues);
  expect(sectionTotal(view,'income')).toBe(sum(incomeValues)+450);
  expect(row(view,'income','Service Fees')).toBe(vanillaFees+450);
  expect(row(view,'income','Residential')).toBe(1000);
  expect(row(view,'income','Exports')).toBe(150);
 });
 it('shows the Area rows as their own sub items of the intended groups only',()=>{
  budget.registerAreaBudget(asRegistry);
  env.selected='Service Upkeep';
  expect(detailRows(renderPage())).toEqual([['ServiceUpkeep',-900],[AREA_MANAGEMENT_SOURCE,-1200]]);
  env.selected='Service Fees';
  const fees=detailRows(renderPage());
  expect(fees.slice(-1)).toEqual([[AREA_SERVICE_FEES_SOURCE,450]]);
  expect(fees.filter(([id])=>id==='FeeParking')).toEqual([['FeeParking',90]]);
  expect(fees.filter(([id])=>id==='FeePublicTransport')).toEqual([['FeePublicTransport',100]]);
  for(const other of ['Residential','Exports','Loan Interest','Imports']){
   env.selected=other;
   const rows=detailRows(renderPage()).map(([id])=>id);
   expect(rows).not.toContain(AREA_MANAGEMENT_SOURCE);
   expect(rows).not.toContain(AREA_SERVICE_FEES_SOURCE);
  }
  expect(AREA_MANAGEMENT_SOURCE).toBe('CS2AS.AreaManagement');
  expect(AREA_SERVICE_FEES_SOURCE).toBe('CS2AS.AreaServiceFees');
 });
 it('does not compound the Area amounts across re-renders or mutate the vanilla binding data',()=>{
  budget.registerAreaBudget(asRegistry);
  const before=JSON.stringify([incomeItems,incomeValues,expenseItems,expenseValues]);
  const view=renderPage();
  const Page=current(BUDGET_PAGE,'BudgetPage');
  view.rerender(<Page/>);view.rerender(<Page/>);
  expect(sectionTotal(view,'expenses')).toBe(sum(expenseValues)-1200);
  expect(sectionTotal(view,'income')).toBe(sum(incomeValues)+450);
  expect(balance(view)).toBe(sum(incomeValues)+sum(expenseValues)+450-1200);
  expect(JSON.stringify([incomeItems,incomeValues,expenseItems,expenseValues])).toBe(before);
 });
 it('follows the published settled amounts',()=>{
  budget.registerAreaBudget(asRegistry);
  setBudget(0,0);
  const view=renderPage();
  expect(sectionTotal(view,'expenses')).toBe(sum(expenseValues));
  expect(sectionTotal(view,'income')).toBe(sum(incomeValues));
  expect(balance(view)).toBe(sum(incomeValues)+sum(expenseValues));
 });
 it('reconciles only the side whose vanilla group was found',()=>{
  env.values['budget.incomeItems']=[incomeItems[0],item('Parking',['FeeParking',9])];
  budget.registerAreaBudget(asRegistry);
  const view=renderPage();
  expect(sectionTotal(view,'income')).toBe(sum(incomeValues));
  expect(row(view,'income','Parking')).toBe(90);
  expect(sectionTotal(view,'expenses')).toBe(sum(expenseValues)-1200);
  expect(balance(view)).toBe(sum(incomeValues)+sum(expenseValues)-1200);
 });
 it('leaves money fields and sections outside the budget page untouched',()=>{
  budget.registerAreaBudget(asRegistry);
  const Field=current(MONEY_FIELD,'NumberField'),Section=current(BUDGET_SECTION,'BudgetSection');
  const view=render(<div><Field value={77} className={TOTAL_FIELD}/><Section title="other" total={5} items={expenseItems} values={expenseValues}/></div>);
  expect(number(view.container.querySelector(`.${TOTAL_FIELD}`))).toBe(77);
  expect(sectionTotal(view,'other')).toBe(5);
  expect(row(view,'other','Service Upkeep')).toBe(-900);
 });
});

describe('fail closed',()=>{
 for(const [path,name] of [['game-ui/game/components/economy-panel/budget-page/budget-item-detail/budget-item-detail.tsx','BudgetItemDetail'],
  ['game-ui/game/components/economy-panel/budget-page/budget-page.module.scss','classes'],
  ['game-ui/game/components/economy-panel/components/money-field/money-field.tsx','NumberField']] as const){
  it(`extends nothing when ${name} cannot be verified`,()=>{
   const warn=vi.spyOn(console,'warn').mockImplementation(()=>{});
   delete env.modules[path][name];
   expect(budget.registerAreaBudget(asRegistry)).toBe(false);
   expect(registry.extend).not.toHaveBeenCalled();
   expect(current(BUDGET_PAGE,'BudgetPage')).toBe(BudgetPage);
   expect(warn).toHaveBeenCalledTimes(1);
  });
 }
 it('shows the vanilla page when an extension fails part-way, because the page that enables the rows is extended last',()=>{
  const warn=vi.spyOn(console,'warn').mockImplementation(()=>{});
  const failing={extend:vi.fn((path:string,name:string,fn:(current:unknown)=>unknown)=>{
   if(name==='NumberField')throw `Module ${path} was not found.`;
   env.modules[path][name]=fn(env.modules[path][name]);
  })};
  expect(budget.registerAreaBudget(failing as unknown as Parameters<typeof budget.registerAreaBudget>[0])).toBe(false);
  expect(current(BUDGET_PAGE,'BudgetPage')).toBe(BudgetPage);
  env.selected='Service Upkeep';
  const view=renderPage();
  expect(sectionTotal(view,'expenses')).toBe(sum(expenseValues));
  expect(row(view,'expenses','Service Upkeep')).toBe(-900);
  expect(detailRows(view)).toEqual([['ServiceUpkeep',-900]]);
  expect(warn).toHaveBeenCalledTimes(1);
 });
 it('extends nothing when the total field class is missing',()=>{
  const warn=vi.spyOn(console,'warn').mockImplementation(()=>{});
  env.modules[BUDGET_PAGE_CLASSES].classes={totalSection:'total-section_TGf'};
  expect(budget.registerAreaBudget(asRegistry)).toBe(false);
  expect(registry.extend).not.toHaveBeenCalled();
  expect(warn).toHaveBeenCalledTimes(1);
 });
 it('renders the vanilla budget unchanged when the Area budget is unavailable',()=>{
  budget.registerAreaBudget(asRegistry);
  delete env.values['cs2AreaSystem.areaBudget'];
  const view=renderPage();
  expect(sectionTotal(view,'expenses')).toBe(sum(expenseValues));
  expect(balance(view)).toBe(sum(incomeValues)+sum(expenseValues));
  env.selected='Service Upkeep';
  expect(detailRows(renderPage())).toEqual([['ServiceUpkeep',-900]]);
 });
});
