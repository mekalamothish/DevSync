import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFileSync} from 'node:fs';
const script=readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/register.js',import.meta.url),'utf8');
function setup(valid=true) {
    const handlers={},button={disabled:false};
    const form={reportValidity:()=>valid,querySelector:()=>button,addEventListener:(key,handler)=>{handlers[key]=handler;}};
    vm.runInNewContext(script,{document:{querySelector:()=>form},window:{addEventListener:(key,handler)=>{handlers[key]=handler;}}});
    return {handlers,button};
}
test('valid registration submits once and back navigation restores its button',()=>{
    const app=setup();let stopped=false;
    app.handlers.submit({preventDefault(){stopped=true;}});assert.equal(stopped,false);assert.equal(app.button.disabled,true);
    app.handlers.submit({preventDefault(){stopped=true;}});assert.equal(stopped,true);
    app.handlers.pageshow();assert.equal(app.button.disabled,false);
});
test('invalid registration stays editable and never disables retry',()=>{
    const app=setup(false);let stopped=false;app.handlers.submit({preventDefault(){stopped=true;}});
    assert.equal(stopped,true);assert.equal(app.button.disabled,false);
});
