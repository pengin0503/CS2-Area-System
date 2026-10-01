import type {ModRegistrar} from 'cs2/modding';
import {AreaSystemHost} from './AreaSystemHost';
import {AREA_INFO_SECTION_TYPE,AreaInfoSection} from './AreaInfoSection';
import {AREA_POLICY_SECTION_TYPE,AreaPoliciesSection} from './AreaPoliciesSection';

export const SELECTED_INFO_SECTIONS='game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx';
const register:ModRegistrar=registry=>{
 registry.append('Game',AreaSystemHost);
 // Public module-registry extension: the vanilla selected-info panel renders sections by their C# type name.
 registry.extend(SELECTED_INFO_SECTIONS,'selectedInfoSectionComponents',(components:Record<string,unknown>)=>({...components,[AREA_INFO_SECTION_TYPE]:AreaInfoSection,[AREA_POLICY_SECTION_TYPE]:AreaPoliciesSection}));
};
export default register;
