import test from 'node:test';
import assert from 'node:assert/strict';
import {languageId,formatLocalCode,formatIfCurrent} from '../../src/DevSync.Web/Assets/code-formatting.js';

test('language aliases resolve without treating arbitrary text as a parser',()=>{
    assert.equal(languageId('C#'),'csharp'); assert.equal(languageId(' TS '),'typescript');assert.equal(languageId('<script>'),'plaintext');
});
test('JavaScript, TypeScript, JSON, HTML and CSS use real parsers',async()=>{
    assert.equal(await formatLocalCode('javascript','function add(a,b){return a+b}'),'function add(a, b) {\n  return a + b;\n}\n');
    assert.match(await formatLocalCode('typescript','const value:number=42'),/const value: number = 42;/);
    assert.deepEqual(JSON.parse(await formatLocalCode('json','{"name":"Alice","roles":["member"]}')),{name:'Alice',roles:['member']});
    assert.match(await formatLocalCode('html','<main>\n<p>Example</p>\n</main>'),/<main>\n  <p>Example<\/p>\n<\/main>/);
    assert.match(await formatLocalCode('css','.box{color:red;padding:10px}'),/color: red;/);
    await assert.rejects(formatLocalCode('javascript','function broken('));
    await assert.rejects(formatLocalCode('json','{"name":}'));
});
function target() {
    const state={code:'original',version:1,language:'csharp',alive:true,applied:[]};
    return {state,getValue:()=>state.code,getVersion:()=>state.version,getLanguage:()=>state.language,isAlive:()=>state.alive,apply:code=>{state.applied.push(code);state.code=code;}};
}
test('format results never overwrite newer typing, language changes or a removed block',async()=>{
    for(const change of [t=>{t.state.code='new typing';t.state.version++;},t=>{t.state.language='javascript';},t=>{t.state.alive=false;}]) {
        const t=target(); let resolve; const pending=formatIfCurrent(t,()=>new Promise(r=>{resolve=r;})); change(t); resolve('formatted');
        assert.equal(await pending,false); assert.deepEqual(t.state.applied,[]);
    }
});
test('syntax errors preserve the source and successful output is applied once',async()=>{
    const t=target(); await assert.rejects(formatIfCurrent(t,async()=>{throw new Error('syntax error');}));assert.equal(t.state.code,'original');
    assert.equal(await formatIfCurrent(t,async()=> 'formatted'),true);assert.deepEqual(t.state.applied,['formatted']);
});
