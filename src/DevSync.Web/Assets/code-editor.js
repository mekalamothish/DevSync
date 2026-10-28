import * as monaco from 'monaco-editor/editor/editor.api.js';
import 'monaco-editor/editor/browser/coreCommands.js';
import 'monaco-editor/editor/contrib/tokenization/browser/tokenization.js';
import 'monaco-editor/editor/contrib/bracketMatching/browser/bracketMatching.js';
import 'monaco-editor/editor/contrib/folding/browser/folding.js';
import 'monaco-editor/editor/contrib/find/browser/findController.js';
import 'monaco-editor/editor/contrib/toggleTabFocusMode/browser/toggleTabFocusMode.js';
import 'monaco-editor/editor/contrib/wordOperations/browser/wordOperations.js';
import { language as csharpGrammar,conf as csharpConfiguration } from 'monaco-editor/languages/definitions/csharp/csharp.js';
import 'monaco-editor/languages/definitions/javascript/register.js';
import 'monaco-editor/languages/definitions/typescript/register.js';
import 'monaco-editor/languages/definitions/html/register.js';
import 'monaco-editor/languages/definitions/css/register.js';
import 'monaco-editor/languages/definitions/sql/register.js';
import 'monaco-editor/languages/definitions/python/register.js';
import 'monaco-editor/languages/definitions/xml/register.js';
import 'monaco-editor/languages/definitions/yaml/register.js';
import 'monaco-editor/languages/definitions/markdown/register.js';
import { languageId,canFormat,formatLocalCode,formatIfCurrent } from './code-formatting.js';

self.MonacoEnvironment={getWorker:()=>new Worker(new URL('./editor.worker.js',import.meta.url),{type:'module'})};
monaco.languages.register({id:'csharp'});
monaco.languages.setLanguageConfiguration('csharp',csharpConfiguration);
monaco.languages.setMonarchTokensProvider('csharp',{...csharpGrammar,tokenizer:{...csharpGrammar.tokenizer,root:[
    [/\b(class|struct|interface|enum|record)(\s+)(@?[A-Za-z_]\w*)/,['keyword','white','type.identifier']],
    [/[A-Za-z_]\w*(?=\s*\()/,{cases:{'@keywords':'keyword','@default':'function'}}],...csharpGrammar.tokenizer.root
]}});
// JSON uses the JavaScript lexer; formatting still uses Prettier's strict JSON parser.
monaco.languages.register({id:'json'});
import('monaco-editor/languages/definitions/javascript/javascript.js').then(({language,conf})=>{
    monaco.languages.setMonarchTokensProvider('json',language); monaco.languages.setLanguageConfiguration('json',conf);
});
function applyCodeTheme() {
const palette=getComputedStyle(document.documentElement);
monaco.editor.defineTheme('devsync-code',{base:'vs-dark',inherit:true,rules:[
    {token:'identifier',foreground:'9CDCFE'},{token:'type.identifier',foreground:'4EC9B0'},
    {token:'function',foreground:'DCDCAA'},{token:'keyword',foreground:'569CD6'},{token:'string',foreground:'CE9178'},
    {token:'comment',foreground:'6A9955'},{token:'number',foreground:'B5CEA8'}
],colors:{'editor.background':palette.getPropertyValue('--code-bg').trim()||'#1e1e1e','editor.foreground':palette.getPropertyValue('--code-fg').trim()||'#d4d4d4'}});
monaco.editor.setTheme('devsync-code');
}
applyCodeTheme();
window.addEventListener('devsync:theme-change',applyCodeTheme);

const form=document.getElementById('note-editor')||document.getElementById('article-editor'),entries=new Map();
let submitting=false;
form?.addEventListener('submit',event=>{queueMicrotask(()=>{if(!event.defaultPrevented) submitting=true;});});
function install(block) {
    if(entries.has(block)) return;
    const input=block.querySelector('[data-field="Content"]'),language=block.querySelector('[data-field="Language"]');
    const host=block.querySelector('[data-code-editor]'),button=block.querySelector('[data-format-code]');
    const toggle=block.querySelector('[data-code-plain]'),status=block.querySelector('[data-code-status]');
    if(!input||!language||!host||!button||!status||!toggle) return;
    const model=monaco.editor.createModel(input.value,languageId(language.value));
    const editor=monaco.editor.create(host,{model,theme:'devsync-code',automaticLayout:true,minimap:{enabled:false},fontSize:14,
        fontFamily:'SFMono-Regular, Consolas, monospace',tabSize:4,insertSpaces:true,scrollBeyondLastLine:false,wordWrap:'on',
        lineNumbers:'on',roundedSelection:false,accessibilitySupport:'auto',ariaLabel:'Code editor',readOnly:form.dataset.archived==='true',
        padding:{top:12,bottom:12},bracketPairColorization:{enabled:true}});
    const entry={editor,model}; entries.set(block,entry);
    input.hidden=true; host.hidden=false; toggle.hidden=false;
    model.onDidChangeContent(()=>{
        input.value=model.getValue(); input.dispatchEvent(new Event('input',{bubbles:true}));
        if(input.value.length>60000) status.textContent='Code exceeds 60,000 characters. Shorten it before saving.';
    });
    input.addEventListener('input',()=>{if(input.value!==model.getValue()) model.setValue(input.value);});
    const refresh=()=>{
        monaco.editor.setModelLanguage(model,languageId(language.value));
        button.disabled=!canFormat(model.getLanguageId())||form.dataset.archived==='true';
        status.textContent=button.disabled ? 'Select C#, JavaScript, TypeScript, JSON, HTML, or CSS to format.' : 'Format code when ready. Undo restores your previous code.';
    };
    language.addEventListener('input',refresh); language.addEventListener('change',refresh); refresh();
    toggle.addEventListener('click',()=>{
        const plain=input.hidden; input.hidden=!plain; host.hidden=plain;
        toggle.textContent=plain?'Use color editor':'Use plain text';
        if(plain) input.focus(); else {editor.layout();editor.focus();}
    });
    button.addEventListener('click',async()=>{
        if(button.disabled||submitting) return;
        button.disabled=true; status.textContent='Formatting code…';
        try {
            const applied=await formatIfCurrent({getValue:()=>model.getValue(),getVersion:()=>model.getVersionId(),getLanguage:()=>model.getLanguageId(),
                isAlive:()=>block.isConnected&&!model.isDisposed()&&!submitting,
                apply:code=>{editor.pushUndoStop();editor.executeEdits('format-code',[{range:model.getFullModelRange(),text:code}]);editor.pushUndoStop();}
            },async(kind,source)=>{
                if(kind!=='csharp') return formatLocalCode(kind,source);
                const body=new FormData(); body.set('Language','csharp');body.set('Code',source);
                body.set('__RequestVerificationToken',form.elements.__RequestVerificationToken.value);
                const response=await fetch(form.dataset.codeFormat||`/notes/${form.elements.Id.value}/format-code`,{method:'POST',body,credentials:'same-origin',headers:{Accept:'application/json'}});
                if(response.redirected||response.status===401||response.status===403) throw new Error('Sign in again before formatting. Your code is unchanged.');
                if(response.status===404) throw new Error('This note is no longer available.');
                const result=await response.json(); if(!response.ok) throw new Error(result.error||'Code could not be formatted.');
                return result.code;
            });
            status.textContent=applied?`Code formatted. Save your ${form.dataset.documentLabel||'note'} or wait for autosave.`:'Code changed while formatting. Click Format code again.';
        } catch(error) {status.textContent=(error.message||'Formatting failed.')+' Your code is unchanged.';}
        finally {if(!model.isDisposed()) button.disabled=!canFormat(model.getLanguageId())||form.dataset.archived==='true';}
    });
}
function syncEditors() {
    for(const [block,entry] of entries) if(!block.isConnected) {entry.editor.dispose();entry.model.dispose();entries.delete(block);}
    form?.querySelectorAll('[data-block-kind="code"]').forEach(install);
}
if(form) {
    syncEditors();
    new MutationObserver(syncEditors).observe(document.getElementById('note-blocks'),{childList:true});
}
for(const block of document.querySelectorAll('[data-highlight-code]')) {
    const code=block.querySelector('code'); if(!code) continue;
    monaco.editor.colorize(code.textContent,languageId(block.dataset.highlightCode),{theme:'devsync-code'}).then(html=>{code.innerHTML=html;}).catch(()=>{});
}
