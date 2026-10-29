import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFileSync} from 'node:fs';
const script=readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/register.js',import.meta.url),'utf8');
function setup(valid=true) {
    const handlers={},button={disabled:false},fields={};
    fields.password={value:'Example-Test-Password42!',addEventListener:(key,handler)=>{handlers['password:'+key]=handler;}};
    fields.confirmation={value:'Example-Test-Password42!',message:'',setCustomValidity(message){this.message=message;},addEventListener:(key,handler)=>{handlers['confirmation:'+key]=handler;}};
    const form={reportValidity:()=>valid && !fields.confirmation.message,querySelector:selector=>selector.includes('ConfirmPassword') ? fields.confirmation : selector.includes('Password') ? fields.password : button,addEventListener:(key,handler)=>{handlers[key]=handler;}};
    vm.runInNewContext(script,{document:{querySelector:()=>form},window:{addEventListener:(key,handler)=>{handlers[key]=handler;}}});
    return {handlers,button,fields};
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

test('password confirmation mismatch blocks registration and correcting it permits retry',()=>{
    const app=setup();app.fields.confirmation.value='Different-Test-Password42!';
    let stopped=false;app.handlers.submit({preventDefault(){stopped=true;}});
    assert.equal(stopped,true);assert.equal(app.button.disabled,false);assert.equal(app.fields.confirmation.message,'Passwords must match.');
    app.fields.confirmation.value=app.fields.password.value;app.handlers['confirmation:input']();
    assert.equal(app.fields.confirmation.message,'');stopped=false;
    app.handlers.submit({preventDefault(){stopped=true;}});assert.equal(stopped,false);assert.equal(app.button.disabled,true);
});
