import { defineConfig } from 'vitest/config';
import path from 'node:path';
export default defineConfig({css:{preprocessorOptions:{scss:{api:'modern'}}},resolve:{alias:{'cs2/api':path.resolve(__dirname,'test/cs2-api.ts'),'cs2/l10n':path.resolve(__dirname,'test/cs2-l10n.ts'),'cs2/modding':path.resolve(__dirname,'test/cs2-modding.ts'),'cs2/ui':path.resolve(__dirname,'test/cs2-ui.ts')}},test:{environment:'jsdom',setupFiles:['./test/setup.ts'],maxWorkers:1,minWorkers:1}});
