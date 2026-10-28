import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const script = readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/blog-editor.js', import.meta.url), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));
function editor() {
    const listeners = {}, timers = new Map(), requests = [], submissions = [];
    let nextTimer = 0;
    const form = {
        dataset: { autosave: '/blogs/example/autosave' }, elements: { Version: { value: 'v1' }, Title: { value: 'First' } },
        reportValidity: () => true,
        addEventListener: (name, listener) => { listeners[name] = listener; },
        requestSubmit: button => { submissions.push({ button, version: form.elements.Version.value }); }
    };
    const status = { textContent: '' };
    vm.runInNewContext(script, {
        document: { getElementById: id => id === 'article-editor' ? form : status },
        window: { addEventListener: () => {} },
        FormData: class { constructor(f) { this.fields = Object.fromEntries(Object.entries(f.elements).map(([k, v]) => [k, v.value])); } },
        setTimeout: (callback, delay) => { const id = ++nextTimer; timers.set(id, { callback, delay }); return id; },
        clearTimeout: id => timers.delete(id),
        fetch: (_, options) => new Promise(resolve => requests.push({ fields: options.body.fields, resolve }))
    });
    return {
        form, status, requests, submissions,
        input(title) { form.elements.Title.value = title; listeners.input(); },
        submit(button) { let prevented = false; const completion = listeners.submit({ submitter: button, preventDefault() { prevented = true; } }); return { completion, prevented }; },
        tick() { const entry = timers.entries().next().value; if (!entry) return false; timers.delete(entry[0]); entry[1].callback(); return true; },
        respond(index, statusCode, payload) { requests[index].resolve({ ok: statusCode === 200, status: statusCode, redirected: false, json: async () => payload }); }
    };
}

test('editing during autosave retains changes and sends the returned version on the next save', async () => {
    const e = editor(); e.input('First change'); e.tick();
    assert.equal(e.requests[0].fields.Version, 'v1');
    e.input('Second change');
    e.respond(0, 200, { version: 'v2' }); await flush();
    e.tick();
    assert.equal(e.requests[1].fields.Version, 'v2');
    assert.equal(e.requests[1].fields.Title, 'Second change');
    e.respond(1, 200, { version: 'v3' }); await flush();
    assert.equal(e.status.textContent, 'Draft saved.');
});

test('a conflict stops autosave and manual publishing without adopting another draft version', async () => {
    const e = editor(); e.input('My work'); e.tick();
    e.respond(0, 409, { error: 'Changed elsewhere' }); await flush();
    assert.equal(e.form.elements.Version.value, 'v1');
    e.input('Still my work'); assert.equal(e.tick(), false);
    assert.equal(e.submit('publish').prevented, true);
    assert.equal(e.requests.length, 1);
});

test('manual publish waits for an in-flight autosave and uses its new version', async () => {
    const e = editor(); e.input('Ready'); e.tick();
    const pending = e.submit('publish'); assert.equal(pending.prevented, true);
    e.respond(0, 200, { version: 'v2' }); await flush();
    e.tick(); await pending.completion;
    assert.equal(e.submissions.length, 1);
    assert.equal(e.submissions[0].version, 'v2');
});
