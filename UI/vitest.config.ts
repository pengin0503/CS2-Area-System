import { defineConfig } from 'vitest/config';
import path from 'node:path';
export default defineConfig({css:{preprocessorOptions:{scss:{api:'modern'}}},resolve:{alias:{'cs2/api':path.resolve(__dirname,'test/cs2-api.ts')}},test:{environment:'jsdom',setupFiles:['./test/setup.ts'],maxWorkers:1,minWorkers:1}});
