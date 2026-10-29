import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFileSync} from 'node:fs';
const script=readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/cover-editor.js',import.meta.url),'utf8');
const first='11111111-1111-4111-8111-111111111111',second='22222222-2222-4222-8222-222222222222';
function setup(fetcher) {
    const listeners={},formEvents={},windowEvents={},changes=[];
    const node=name=>({value:'',hidden:false,disabled:false,addEventListener:(event,handler)=>{listeners[name+':'+event]=handler;}});
    const input=node('id'),file=node('file'),image=node('image'),remove=node('remove'),status=node('status');
    input.value=first;image.src='/media/images/'+first;image.removeAttribute=()=>{delete image.src;};input.dispatchEvent=event=>changes.push(event.type);
    file.files=[new File(['image bytes'],'cover.png',{type:'image/png'})];
    const form={querySelector:()=>({value:'test-csrf'}),addEventListener:(name,handler)=>{formEvents[name]=handler;}};
    const nodes={'[data-cover-id]':input,'[data-cover-file]':file,'[data-cover-preview]':image,'[data-cover-remove]':remove,'[data-cover-status]':status};
    vm.runInNewContext(script,{document:{querySelectorAll:()=>[{closest:()=>form,querySelector:key=>nodes[key]}]},window:{addEventListener:(key,handler)=>{windowEvents[key]=handler;}},Event,FormData,fetch:fetcher});
    return {input,file,image,remove,status,changes,formEvents,windowEvents,listeners,upload:()=>listeners['file:change']({stopPropagation(){}})};
}
const response=result=>({ok:true,status:200,headers:{get:()=> 'application/json'},json:async()=>result});
test('upload sends CSRF, selects the returned asset and marks the document dirty only after success',async()=>{
    let request;const app=setup(async(path,options)=>{request={path,options};return response({id:second});});
    await app.upload();assert.equal(request.path,'/images/cover-upload');assert.equal(request.options.body.get('__RequestVerificationToken'),'test-csrf');
    assert.equal(app.input.value,second);assert.equal(app.image.src,'/media/images/'+second);assert.deepEqual(app.changes,['input']);assert.equal(app.file.disabled,false);
});
test('failed upload preserves the previous cover and does not mark the document dirty',async()=>{
    const app=setup(async()=>({...response({error:'Invalid image bytes'}),ok:false,status:422}));await app.upload();
    assert.equal(app.input.value,first);assert.equal(app.image.src,'/media/images/'+first);assert.deepEqual(app.changes,[]);assert.match(app.status.textContent,/previous cover is unchanged/);
});
test('remove clears the selected asset and marks the document dirty without deleting library content',()=>{
    const app=setup(()=>assert.fail('Removing a selection should not delete an uploaded asset'));
    app.listeners['remove:click']();assert.equal(app.input.value,'');assert.equal(app.image.hidden,true);assert.equal(app.remove.hidden,true);assert.deepEqual(app.changes,['input']);
});
test('pending upload prevents manual submission until the selected cover can be saved',async()=>{
    let finish;const app=setup(()=>new Promise(resolve=>{finish=resolve;}));const pending=app.upload();
    let prevented=false;app.formEvents.submit({preventDefault(){prevented=true;},stopImmediatePropagation(){}});assert.equal(prevented,true);
    finish(response({id:second}));await pending;prevented=false;app.formEvents.submit({preventDefault(){prevented=true;}});assert.equal(prevented,false);
});
test('authentication redirects and oversized files keep the previous cover',async()=>{
    const app=setup(async()=>({redirected:true,status:200}));await app.upload();assert.equal(app.input.value,first);assert.deepEqual(app.changes,[]);
    app.file.files=[{size:6*1024*1024}];await app.upload();assert.match(app.status.textContent,/5 MB/);assert.equal(app.input.value,first);
});
