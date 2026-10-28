import {Editor} from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import TaskList from '@tiptap/extension-task-list';
import TaskItem from '@tiptap/extension-task-item';

const mounted=new Map();
const safeLink=value=>{try{return value.length<=2048 && ['http:','https:'].includes(new URL(value).protocol) && !new URL(value).username && !new URL(value).password;}catch{return false;}};
function imageUrl(value) {
    if(/^\/media\/images\/[0-9a-f-]{36}$/i.test(value)) return value;
    try {const url=new URL(value);if(url.origin===location.origin && /^\/media\/images\/[0-9a-f-]{36}$/i.test(url.pathname) && !url.search && !url.hash) return url.pathname;
        return url.protocol==='https:' && !url.username && !url.password ? url.href:null;
    } catch {return null;}
}
function mount(host) {
    if(mounted.has(host)) return;
    const source=host.querySelector('[data-rich-source]'),surface=host.querySelector('[data-rich-surface]'),toolbar=host.querySelector('[data-rich-toolbar]');
    const status=host.querySelector('[data-rich-status]'),toggle=host.querySelector('[data-rich-toggle]'),label=host.querySelector('[data-rich-source-label]');
    const panel=host.querySelector('[data-rich-url]'),readonly=host.dataset.readonly==='true';
    const wasFocused=document.activeElement===source;
    let editor;
    try {
        const content=source.value.trim()?JSON.parse(source.value):{type:'doc',content:[{type:'paragraph'}]};
        editor=new Editor({element:surface,extensions:[StarterKit.configure({heading:{levels:[1,2,3]},trailingNode:false,
            link:{openOnClick:false,autolink:false,isAllowedUri:safeLink}}),Image.configure({allowBase64:false}),TaskList,TaskItem.configure({nested:true})],
            content,editable:!readonly,enableContentCheck:true,
            editorProps:{attributes:{class:'tiptap article-content',role:'textbox','aria-multiline':'true','aria-readonly':String(readonly),'aria-label':'Rich text content',spellcheck:'true'}},
            onUpdate:({editor})=>{
                source.value=JSON.stringify(editor.getJSON());source.dispatchEvent(new Event('input',{bubbles:true}));
                status.textContent=source.value.length>source.maxLength?`Formatting and text exceed ${source.maxLength.toLocaleString()} characters. Shorten this document before saving.`:'Rich text updated. Save or wait for autosave.';
            }});
    } catch {status.textContent='The visual editor could not load this document. Its original source is preserved below.';return;}
    mounted.set(host,editor);source.hidden=true;label.hidden=true;surface.hidden=false;toolbar.hidden=false;toggle.hidden=false;
    const buttons=[];
    const button=(text,title,run,active)=>{
        const b=document.createElement('button');b.type='button';b.className='rich-tool';b.textContent=text;b.title=title;b.setAttribute('aria-label',title);
        if(active)b.setAttribute('aria-pressed','false');
        b.disabled=readonly;b.addEventListener('click',()=>{run();refresh();});toolbar.append(b);buttons.push({b,active});return b;
    };
    const style=document.createElement('select');style.className='form-input rich-style';style.setAttribute('aria-label','Paragraph style');style.disabled=readonly;
    for(const [value,text] of [['paragraph','Paragraph'],['h1','Heading 1'],['h2','Heading 2'],['h3','Heading 3']]) {const o=document.createElement('option');o.value=value;o.textContent=text;style.append(o);}
    style.addEventListener('change',()=>{const chain=editor.chain().focus();style.value==='paragraph'?chain.setParagraph().run():chain.toggleHeading({level:Number(style.value.slice(1))}).run();});toolbar.append(style);
    button('B','Bold',()=>editor.chain().focus().toggleBold().run(),()=>editor.isActive('bold'));
    button('I','Italic',()=>editor.chain().focus().toggleItalic().run(),()=>editor.isActive('italic'));
    button('U','Underline',()=>editor.chain().focus().toggleUnderline().run(),()=>editor.isActive('underline'));
    button('S','Strikethrough',()=>editor.chain().focus().toggleStrike().run(),()=>editor.isActive('strike'));
    button('• List','Bullet list',()=>editor.chain().focus().toggleBulletList().run(),()=>editor.isActive('bulletList'));
    button('1. List','Numbered list',()=>editor.chain().focus().toggleOrderedList().run(),()=>editor.isActive('orderedList'));
    button('☑','Checklist',()=>editor.chain().focus().toggleTaskList().run(),()=>editor.isActive('taskList'));
    button('“ ”','Block quote',()=>editor.chain().focus().toggleBlockquote().run(),()=>editor.isActive('blockquote'));
    button('</>','Inline code',()=>editor.chain().focus().toggleCode().run(),()=>editor.isActive('code'));
    button('{ }','Code block',()=>editor.chain().focus().toggleCodeBlock().run(),()=>editor.isActive('codeBlock'));
    button('—','Horizontal rule',()=>editor.chain().focus().setHorizontalRule().run());
    function urlPanel(kind) {
        panel.replaceChildren();panel.hidden=false;
        const urlLabel=document.createElement('label');urlLabel.textContent=kind==='link'?'Link URL':'Image URL';urlLabel.className='form-label';
        const input=document.createElement('input');input.type='text';input.className='form-input';input.id=source.id+'-url';input.maxLength=2048;urlLabel.htmlFor=input.id;input.value=kind==='link'?editor.getAttributes('link').href||'':'';
        const altLabel=document.createElement('label'),alt=document.createElement('input');altLabel.textContent='Image description';altLabel.className='form-label';alt.id=source.id+'-alt';altLabel.htmlFor=alt.id;alt.className='form-input';alt.maxLength=500;
        const apply=document.createElement('button');apply.type='button';apply.textContent=kind==='link'?'Apply link':'Insert image';apply.className='rich-tool';
        const cancel=document.createElement('button');cancel.type='button';cancel.textContent='Cancel';cancel.className='rich-tool';cancel.addEventListener('click',()=>{panel.hidden=true;editor.commands.focus();});
        apply.addEventListener('click',()=>{
            const value=input.value.trim(),url=kind==='image'?imageUrl(value):value;
            if(kind==='link'&&!safeLink(url)||kind==='image'&&!url){status.textContent='Use an HTTP/HTTPS link, or an HTTPS/uploaded-library image URL.';input.focus();return;}
            if(kind==='link')editor.chain().focus().extendMarkRange('link').setLink({href:url}).run();
            else editor.chain().focus().setImage({src:url,alt:alt.value}).run();
            panel.hidden=true;
        });
        panel.append(urlLabel,input);if(kind==='image')panel.append(altLabel,alt);panel.append(apply,cancel);input.focus();
        for(const field of [input,alt])field.addEventListener('keydown',event=>{
            if(event.key==='Enter'){event.preventDefault();apply.click();}
            if(event.key==='Escape'){event.preventDefault();event.stopPropagation();cancel.click();}
        });
    }
    button('Link','Insert or edit link',()=>urlPanel('link'),()=>editor.isActive('link'));
    button('Unlink','Remove link',()=>editor.chain().focus().unsetLink().run());
    button('Image','Insert image from URL',()=>urlPanel('image'));
    const undo=button('↶','Undo',()=>editor.chain().focus().undo().run());
    const redo=button('↷','Redo',()=>editor.chain().focus().redo().run());
    function refresh() {
        for(const {b,active} of buttons) if(active)b.setAttribute('aria-pressed',String(active()));
        style.value=editor.isActive('heading',{level:1})?'h1':editor.isActive('heading',{level:2})?'h2':editor.isActive('heading',{level:3})?'h3':'paragraph';
        undo.disabled=readonly||!editor.can().undo();redo.disabled=readonly||!editor.can().redo();
    }
    editor.on('selectionUpdate',refresh);editor.on('transaction',refresh);refresh();
    toggle.addEventListener('click',()=>{
        panel.hidden=true;
        if(source.hidden){editor.setEditable(false);source.hidden=false;label.hidden=false;surface.hidden=true;toolbar.hidden=true;toggle.textContent='Use visual editor';source.focus();}
        else {
            try {editor.commands.setContent(source.value.trim()?JSON.parse(source.value):{type:'doc',content:[{type:'paragraph'}]},{emitUpdate:false,errorOnInvalidContent:true});}
            catch {status.textContent='This source is not valid for the visual editor. Your text has been kept.';return;}
            editor.setEditable(!readonly);source.hidden=true;label.hidden=true;surface.hidden=false;toolbar.hidden=false;toggle.textContent='Show source';editor.commands.focus();
        }
    });
    if(wasFocused)editor.commands.focus();
    status.textContent=readonly?'Read-only rich text.':'Rich text · formatting is saved with your document.';
}
function sync() {
    for(const [host,editor] of mounted)if(!host.isConnected){editor.destroy();mounted.delete(host);}
    document.querySelectorAll('[data-rich-text]').forEach(mount);
}
sync();new MutationObserver(sync).observe(document.getElementById('note-blocks')||document.body,{childList:true,subtree:false});

const form=document.getElementById('article-editor'),selector=document.getElementById('EditorFormat');
if(form&&selector) {
    const update=()=>{
        form.querySelectorAll('[data-editor-panel]').forEach(panel=>{
            const active=panel.dataset.editorPanel===selector.value;panel.hidden=!active;
            panel.querySelectorAll('input,textarea,select').forEach(input=>{if(input.required)input.dataset.editorRequired='true';if(input.dataset.editorRequired)input.required=active;});
        });
        window.dispatchEvent(new Event('resize'));
    };
    selector.addEventListener('change',()=>{update();selector.dispatchEvent(new Event('input',{bubbles:true}));});update();
}
