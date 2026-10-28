import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const script = readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/note-editor.js', import.meta.url), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));

function editor({ kinds = ['paragraph', 'code'], paused = false, unsaved = false, article = false } = {}) {
    const listeners = {}, windowListeners = {}, timers = new Map(), requests = [], submissions = [];
    let nextTimer = 0, nextId = 100, valid = true, focused;
    const blocks = {
        items: [],
        querySelectorAll() { return this.items; },
        appendChild(block) { block.container = this; this.items.push(block); },
        insertBefore(block, reference) {
            this.items.splice(this.items.indexOf(block), 1);
            const index = reference ? this.items.indexOf(reference) : this.items.length;
            this.items.splice(index, 0, block);
        }
    };
    function makeBlock(kind, id) {
        const block = {
            dataset: { blockId: id, blockKind: kind }, label: { textContent: '' }, container: null,
            fields: ['Id', 'Kind', 'Content', ...(kind === 'code' ? ['Language'] : []), ...(kind === 'article' ? ['LinkedPostId'] : [])].map(name => ({
                dataset: { field: name }, value: name === 'Id' ? id : name === 'Kind' ? kind : '',
                focus() { focused = this; }
            })),
            labels: [{ dataset: { forField: 'Content' } }],
            get previousElementSibling() { return this.container.items[this.container.items.indexOf(this) - 1] || null; },
            get nextElementSibling() { return this.container.items[this.container.items.indexOf(this) + 1] || null; },
            remove() { this.container.items.splice(this.container.items.indexOf(this), 1); },
            cloneNode() { return makeBlock(kind, id); },
            querySelector(selector) {
                if (selector === '[data-block-label]') return this.label;
                if (selector === '[data-field="Id"]') return this.fields.find(field => field.dataset.field === 'Id');
                return this.fields.find(field => field.dataset.field === (kind === 'article' ? 'LinkedPostId' : 'Content'));
            },
            querySelectorAll(selector) { return selector === '[data-field]' ? this.fields : selector === '[data-for-field]' ? this.labels : this.buttons; }
        };
        block.buttons = ['up', 'down', 'remove'].map(action => ({
            dataset: { blockAction: action }, attributes: {}, disabled: false,
            closest(selector) { return selector === '[data-block-action]' ? this : block; },
            setAttribute(name, value) { this.attributes[name] = value; },
            focus() { focused = this; }
        }));
        return block;
    }
    kinds.forEach((kind, index) => blocks.appendChild(makeBlock(kind, `b${index + 1}`)));
    const add = { dataset: { blockAction: 'add' }, closest(selector) { return selector === '[data-block-action]' ? this : null; }, focus() { focused = this; } };
    const form = {
        dataset: { autosave: '/notes/n1/autosave', blocks: '/notes/n1/blocks', maxBlocks: '50', paused: String(paused), unsaved: String(unsaved) },
        elements: { Version: { value: 'v1' }, Title: { value: 'Learning' }, kind: { value: 'paragraph' } },
        reportValidity: () => valid,
        contains: () => true,
        addEventListener: (name, listener) => { listeners[name] = listener; },
        requestSubmit(button) {
            if (!valid) return;
            const event = { submitter: button, prevented: false, preventDefault() { this.prevented = true; } };
            listeners.submit(event);
            if (!event.prevented) submissions.push({ button, fields: fields() });
        }
    };
    function fields() {
        const result = Object.fromEntries(Object.entries(form.elements).map(([name, field]) => [name, field.value]));
        for (const block of blocks.items) for (const field of block.fields) result[field.name] = field.value;
        return result;
    }
    if(article) {form.dataset.autosave='/blogs/b1/autosave';form.dataset.blocks='/blogs/b1/blocks';form.elements.EditorFormat={value:'richtext'};form.elements.RichText={value:''};}
    const status = { textContent: '' }, empty = { hidden: false };
    vm.runInNewContext(script, {
        document: { getElementById: id => id === 'note-editor' ? (article ? null : form) : id === 'article-editor' ? (article ? form : null) : id === 'note-save-status' ? (article ? null : status) : id === 'save-status' ? status : id === 'note-blocks' ? blocks : id === 'note-blocks-empty' ? empty : id === 'note-add-block' ? add : { content: { firstElementChild: makeBlock(id.replace('note-template-', ''), 'template') } } },
        window: { addEventListener: (name, listener) => { windowListeners[name] = listener; } },
        crypto: { randomUUID: () => `new-${++nextId}` },
        FormData: class { constructor() { this.fields = fields(); } },
        setTimeout: (callback, delay) => { const id = ++nextTimer; timers.set(id, { callback, delay }); return id; },
        clearTimeout: id => timers.delete(id),
        fetch: (_, options) => new Promise((resolve, reject) => requests.push({ fields: options.body.fields, resolve, reject }))
    });
    return {
        form, status, blocks, requests, submissions, add, empty,
        input(title) { form.elements.Title.value = title; listeners.input({ target: form.elements.Title }); },
        content(index, value) { const field = blocks.items[index].fields.find(field => field.dataset.field === 'Content'); field.value = value; listeners.input({ target: field }); },
        chooseKind(kind) { form.elements.kind.value = kind; listeners.input({ target: { name: 'kind' } }); },
        click(button) { listeners.click({ target: button, preventDefault() {} }); },
        submit(button = 'save') {
            let prevented = false;
            const completion = listeners.submit({ submitter: button, preventDefault() { prevented = true; } });
            return { completion, prevented };
        },
        tick() { const entry = timers.entries().next().value; if (!entry) return false; timers.delete(entry[0]); entry[1].callback(); return true; },
        respond(index, code, payload, redirected = false) { requests[index].resolve({ ok: code === 200, status: code, redirected, json: async () => payload }); },
        setValid(value) { valid = value; },
        leaving() { let prevented = false; windowListeners.beforeunload({ preventDefault() { prevented = true; } }); return prevented; },
        get focused() { return focused; }
    };
}

test('edits made during autosave survive and the next snapshot adopts only the returned version', async () => {
    const e = editor();
    e.content(0, 'First snapshot'); e.tick();
    e.content(0, '<script>My newer text</script>');
    e.respond(0, 200, { version: 'v2' }); await flush();
    assert.equal(e.status.textContent, 'New changes waiting to save…');
    assert.equal(e.leaving(), true);
    e.tick();
    assert.equal(e.requests[1].fields.Version, 'v2');
    assert.equal(e.requests[1].fields['Blocks[0].Content'], '<script>My newer text</script>');
    e.respond(1, 200, { version: 'v3' }); await flush();
    assert.equal(e.status.textContent, 'All changes saved.');
    assert.equal(e.leaving(), false);
});

test('reordering waits for autosave, reindexes every block field, and saves current content with the new version', async () => {
    const e = editor(); e.content(0, 'Keep me'); e.tick();
    const moving = e.blocks.items[1];
    e.click(moving.buttons[0]); await flush();
    assert.equal(e.blocks.items[0].dataset.blockId, 'b1');
    e.content(0, 'Typed while waiting');
    e.respond(0, 200, { version: 'v2' }); await flush();
    assert.equal(e.blocks.items[0], moving);
    assert.equal(moving.fields[0].name, 'Blocks[0].Id');
    assert.equal(e.blocks.items[1].fields[2].name, 'Blocks[1].Content');
    assert.equal(moving.buttons[0].disabled, true);
    assert.equal(e.blocks.items[1].buttons[1].disabled, true);
    assert.equal(e.focused.dataset.field, 'Content');
    e.tick();
    assert.equal(e.requests[1].fields.Version, 'v2');
    assert.equal(e.requests[1].fields['Blocks[0].Kind'], 'code');
    assert.equal(e.requests[1].fields['Blocks[1].Content'], 'Typed while waiting');
});

test('adding and removing blocks keep field indexes contiguous and restore a useful focus target', async () => {
    const e = editor({ kinds: [] });
    assert.equal(e.empty.hidden, false);
    e.chooseKind('code'); assert.equal(e.tick(), false);
    e.click(e.add); await flush();
    assert.equal(e.empty.hidden, true);
    assert.equal(e.blocks.items[0].fields[0].value, 'new-101');
    assert.equal(e.blocks.items[0].fields[3].name, 'Blocks[0].Language');
    assert.equal(e.focused.dataset.field, 'Content');
    e.click(e.blocks.items[0].buttons[2]); await flush();
    assert.equal(e.blocks.items.length, 0);
    assert.equal(e.empty.hidden, false);
    assert.equal(e.focused, e.add);
});

test('manual save waits for both autosave and queued block changes without submitting twice', async () => {
    const e = editor(); e.input('Updated title'); e.tick();
    e.click(e.blocks.items[1].buttons[0]);
    const pending = e.submit();
    assert.equal(pending.prevented, true);
    assert.equal(e.submit().prevented, true);
    e.respond(0, 200, { version: 'v2' }); await flush(); await pending.completion;
    assert.equal(e.submissions.length, 1);
    assert.equal(e.submissions[0].fields.Version, 'v2');
    assert.equal(e.submissions[0].fields['Blocks[0].Id'], 'b2');
});

test('a conflict pauses queued mutations, autosave, and manual submission while preserving edited fields', async () => {
    const e = editor(); e.content(0, 'My own work'); e.tick();
    e.click(e.blocks.items[1].buttons[0]);
    const pending = e.submit();
    e.respond(0, 409, { error: 'Changed elsewhere. Copy your work.' }); await flush(); await pending.completion;
    assert.equal(e.form.elements.Version.value, 'v1');
    assert.equal(e.blocks.items[0].dataset.blockId, 'b1');
    assert.equal(e.submissions.length, 0);
    e.input('Still my work');
    assert.equal(e.tick(), false);
    assert.equal(e.submit().prevented, true);
    assert.equal(e.status.textContent, 'Changed elsewhere. Copy your work.');
    assert.equal(e.leaving(), true);
});

for (const code of [401, 403, 404]) {
    test(`HTTP ${code} pauses saving before parsing an HTML error page`, async () => {
        const e = editor(); e.input('Keep this'); e.tick();
        e.respond(0, code, null); await flush();
        assert.equal(e.form.elements.Version.value, 'v1');
        e.input('Keep this too'); assert.equal(e.tick(), false);
        assert.equal(e.submit().prevented, true);
        assert.match(e.status.textContent, /Copy your work/);
    });
}

test('an authentication redirect pauses saving and does not adopt a version from the redirected page', async () => {
    const e = editor(); e.input('Private work'); e.tick();
    e.respond(0, 200, { version: 'wrong' }, true); await flush();
    assert.equal(e.form.elements.Version.value, 'v1');
    assert.equal(e.submit().prevented, true);
    assert.match(e.status.textContent, /sign in again/);
});

test('temporary save failures retain dirty state and clear their message after a successful retry', async () => {
    const e = editor(); e.input('Valid work'); e.tick();
    e.respond(0, 422, { error: 'Too many characters' }); await flush();
    assert.equal(e.status.textContent, 'Too many characters');
    assert.equal(e.leaving(), true);
    e.input('Shorter valid work'); e.tick();
    e.respond(1, 200, { version: 'v2' }); await flush();
    assert.equal(e.status.textContent, 'All changes saved.');
    assert.equal(e.leaving(), false);
});

test('a note returned by the server with a conflict starts paused', () => {
    const e = editor({ paused: true, unsaved: true });
    e.input('Do not overwrite');
    assert.equal(e.tick(), false);
    assert.equal(e.submit().prevented, true);
    assert.match(e.status.textContent, /changed elsewhere/);
});

test('required field failures retain edits and resume autosave once the fields are corrected', async () => {
    const e = editor(); e.setValid(false); e.input(''); e.tick();
    assert.equal(e.requests.length, 0);
    assert.match(e.status.textContent, /required fields/);
    e.setValid(true); e.input('Now complete'); e.tick();
    e.respond(0, 200, { version: 'v2' }); await flush();
    assert.equal(e.status.textContent, 'All changes saved.');
});


test('the shared article editor preserves rich JSON and blocks while waiting for autosave before publishing',async()=>{
    const e=editor({article:true,kinds:['richtext','code']});
    const json=JSON.stringify({type:'doc',content:[{type:'paragraph',content:[{type:'text',text:'Formatted',marks:[{type:'bold'}]}]}]});
    e.form.elements.RichText.value=json;e.content(0,json);e.tick();
    e.form.elements.RichText.value=json.replace('Formatted','Newer');e.content(0,e.form.elements.RichText.value);
    const submit=e.submit('publish');assert.equal(submit.prevented,true);
    e.respond(0,200,{version:'v2'});await flush();await submit.completion;
    assert.equal(e.submissions.length,1);assert.equal(e.submissions[0].fields.RichText,e.form.elements.RichText.value);
    assert.equal(e.submissions[0].fields['Blocks[0].Content'],e.form.elements.RichText.value);assert.equal(e.submissions[0].fields.Version,'v2');
});
