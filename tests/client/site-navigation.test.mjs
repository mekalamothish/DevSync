import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const script = readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/site.js', import.meta.url), 'utf8');
function navigation() {
    const events = {}, attributes = { 'aria-expanded': 'false' };
    const state = { hidden: true, focused: 'toggle' };
    const toggle = {
        getAttribute: name => attributes[name],
        setAttribute: (name, value) => { attributes[name] = value; },
        addEventListener: (name, listener) => { events[name] = listener; },
        focus: () => { state.focused = 'toggle'; }
    };
    const sidebar = {
        classList: { toggle: (_, hidden) => { state.hidden = hidden; } },
        querySelector: () => ({ focus: () => { state.focused = 'navigation'; } })
    };
    vm.runInNewContext(script, { document: {
        querySelector: selector => selector === '#navigation-toggle' ? toggle : sidebar,
        addEventListener: (name, listener) => { events[name] = listener; }
    }});
    return { state, events, attributes };
}

test('opening navigation exposes links and focuses the first destination', () => {
    const { state, events, attributes } = navigation(); events.click();
    assert.equal(state.hidden, false);
    assert.equal(state.focused, 'navigation');
    assert.equal(attributes['aria-expanded'], 'true');
    assert.equal(attributes['aria-label'], 'Close navigation');
});

test('Escape closes navigation and restores focus to the toggle', () => {
    const { state, events, attributes } = navigation(); events.click(); events.keydown({ key: 'Escape' });
    assert.equal(state.hidden, true);
    assert.equal(state.focused, 'toggle');
    assert.equal(attributes['aria-expanded'], 'false');
    assert.equal(attributes['aria-label'], 'Open navigation');
});

test('account pages without navigation do not install handlers', () => {
    vm.runInNewContext(script, { document: {
        querySelector: () => null,
        addEventListener: () => assert.fail('No navigation handler is needed')
    }});
});
