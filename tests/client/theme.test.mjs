import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const script = readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/theme.js', import.meta.url), 'utf8');
function setup({ saved = null, darkSystem = false, blocked = false } = {}) {
    const events = {}, clicks = {}, attrs = {}, root = { dataset: {} }, emitted = [];
    const button = { hidden: true, setAttribute: (name, value) => { attrs[name] = value; }, addEventListener: (name, handler) => { clicks[name] = handler; } };
    const media = { matches: darkSystem, addEventListener: (_, handler) => { events.system = handler; } };
    const storage = new Map(saved ? [['devsync-theme', saved]] : []);
    vm.runInNewContext(script, {
        document: { documentElement: root, readyState: 'loading', querySelectorAll: () => events.ready ? [button] : [], addEventListener: (_, handler) => { events.ready = handler; } },
        window: { matchMedia: () => media, localStorage: {
            getItem: key => { if (blocked) throw Error('Unavailable'); return storage.get(key); },
            setItem: (key, value) => { if (blocked) throw Error('Unavailable'); storage.set(key, value); }
        }, dispatchEvent: event => emitted.push(event), addEventListener: (name, handler) => { events[name] = handler; } },
        CustomEvent: class { constructor(type, init) { this.type = type; this.detail = init.detail; } }
    });
    return { events, clicks, attrs, root, button, storage, media, emitted };
}
test('saved theme applies before DOM ready and wins over system theme', () => {
    const app = setup({ saved: 'light', darkSystem: true });
    assert.equal(app.root.dataset.theme, 'light');
    app.events.ready(); app.events.system({ matches: true });
    assert.equal(app.root.dataset.theme, 'light');
    assert.equal(app.attrs['aria-label'], 'Switch to dark mode');
});
test('without a saved choice the theme follows system changes', () => {
    const app = setup({ darkSystem: true });
    assert.equal(app.root.dataset.theme, 'dark');
    app.events.system({ matches: false });
    assert.equal(app.root.dataset.theme, 'light');
});
test('toggle persists preference and updates accessible action and theme event', () => {
    const app = setup(); app.events.ready(); app.clicks.click();
    assert.equal(app.root.dataset.theme, 'dark');
    assert.equal(app.storage.get('devsync-theme'), 'dark');
    assert.equal(app.attrs['aria-pressed'], 'true');
    assert.equal(app.attrs['aria-label'], 'Switch to light mode');
    assert.equal(app.button.hidden, false);
    assert.equal(app.emitted.at(-1).detail.theme, 'dark');
    app.events.system({ matches: false });
    assert.equal(app.root.dataset.theme, 'dark');
});
test('storage changes sync other tabs and clearing preferences restores system theme', () => {
    const app = setup({ saved: 'dark' }); app.events.ready();
    app.events.storage({ key: 'unrelated', newValue: 'light' });
    assert.equal(app.root.dataset.theme, 'dark');
    app.events.storage({ key: 'devsync-theme', newValue: 'light' });
    assert.equal(app.attrs['aria-pressed'], 'false');
    app.media.matches = true;
    app.events.storage({ key: null, newValue: null });
    assert.equal(app.root.dataset.theme, 'dark');
});
test('blocked storage and invalid stored preferences do not break switching', () => {
    for (const options of [{ blocked: true }, { saved: 'invalid' }]) {
        const app = setup(options); app.events.ready(); app.clicks.click();
        assert.equal(app.root.dataset.theme, 'dark');
        app.clicks.click(); assert.equal(app.root.dataset.theme, 'light');
    }
});
