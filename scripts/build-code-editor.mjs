import { build } from 'esbuild';
import { mkdir, copyFile } from 'node:fs/promises';
const outdir='src/DevSync.Web/wwwroot/vendor/code';
await mkdir(outdir,{recursive:true});
await build({entryPoints:{'note-code':'src/DevSync.Web/Assets/code-editor.js','rich-text':'src/DevSync.Web/Assets/rich-text.js','editor.worker':'node_modules/monaco-editor/esm/vs/editor/editor.worker.js'},outdir,bundle:true,splitting:true,format:'esm',minify:true,target:'es2022',loader:{'.ttf':'file'},assetNames:'assets/[name]-[hash]',chunkNames:'chunks/[name]-[hash]',logLevel:'info'});
await copyFile('node_modules/monaco-editor/LICENSE',outdir+'/MONACO-LICENSE.txt');
await copyFile('node_modules/prettier/LICENSE',outdir+'/PRETTIER-LICENSE.txt');

await copyFile('node_modules/@tiptap/core/LICENSE.md',outdir+'/TIPTAP-LICENSE.md');
