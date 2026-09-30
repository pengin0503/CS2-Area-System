import type {ModRegistrar} from 'cs2/modding';
import {AreaManagementPanelHost} from './AreaManagementPanel';
const register:ModRegistrar=registry=>registry.append('Game',AreaManagementPanelHost);
export default register;
