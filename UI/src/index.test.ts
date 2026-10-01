import {describe,it,expect,vi} from 'vitest';
vi.mock('cs2/api',()=>({bindValue:()=>({}),useValue:()=>null,trigger:vi.fn()}));
vi.mock('cs2/l10n',()=>({useLocalization:()=>({translate:()=>null})}));
vi.mock('cs2/modding',()=>({getModule:()=>undefined}));
const {default:register,SELECTED_INFO_SECTIONS}=await import('./index');
const {AreaInfoSection,AREA_INFO_SECTION_TYPE}=await import('./AreaInfoSection');
const {AreaSystemHost}=await import('./AreaSystemHost');
const {AreaPoliciesSection,AREA_POLICY_SECTION_TYPE}=await import('./AreaPoliciesSection');
describe('mod registration',()=>{
 it('appends the floating host and adds the section without replacing vanilla sections',()=>{
  const append=vi.fn();let extension:((current:Record<string,unknown>)=>Record<string,unknown>)|undefined;
  const extend=vi.fn((_path:string,_name:string,fn:(current:Record<string,unknown>)=>Record<string,unknown>)=>{extension=fn;});
  register({append,extend} as unknown as Parameters<typeof register>[0]);
  expect(append).toHaveBeenCalledWith('Game',AreaSystemHost);
  expect(extend).toHaveBeenCalledWith(SELECTED_INFO_SECTIONS,'selectedInfoSectionComponents',expect.any(Function));
  expect(SELECTED_INFO_SECTIONS).toBe('game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx');
  const vanillaSection=()=>null;
  const result=extension!({'Game.UI.InGame.ActionsSection':vanillaSection});
  expect(result['Game.UI.InGame.ActionsSection']).toBe(vanillaSection);
  expect(result[AREA_INFO_SECTION_TYPE]).toBe(AreaInfoSection);
  expect(result[AREA_POLICY_SECTION_TYPE]).toBe(AreaPoliciesSection);
 });
});
