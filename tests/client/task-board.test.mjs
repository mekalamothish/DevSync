import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';

const script = readFileSync(new URL('../../src/DevSync.Web/wwwroot/js/task-board.js', import.meta.url), 'utf8');

function board({ enabled = true, counts = [3, 2, 0] } = {}) {
    const listeners = {}, windowListeners = {}, submissions = [];
    const status = { textContent: '' };
    const surface = {
        dataset: { dragEnabled: String(enabled) },
        contains: node => node?.surface === surface,
        addEventListener(name, listener) { listeners[name] = listener; },
        querySelectorAll: () => columns
    };
    const columns = counts.map((count, index) => {
        const column = {
            surface, dataset: { columnId: `column-${index}` }, cards: [],
            classList: { add() {}, remove() {} },
            closest(selector) { return selector === '[data-task-column]' ? this : null; },
            querySelectorAll() { return this.cards; }
        };
        for (let position = 0; position < count; position++) {
            const form = {
                elements: {
                    Version: { value: `task-version-${index}-${position}` }, BoardVersion: { value: 'board-version-1' },
                    ColumnId: { value: column.dataset.columnId }, TargetPosition: { value: '' },
                    __RequestVerificationToken: { value: 'antiforgery-token' }, returnUrl: { value: '/tasks/board?boardId=b1' }
                },
                requestSubmit() { submissions.push(Object.fromEntries(Object.entries(this.elements).map(([key, value]) => [key, value.value]))); }
            };
            column.cards.push({
                surface, dataset: { taskId: `task-${index}-${position}`, taskPosition: String(position) }, form,
                closest(selector) { return selector === '[data-task-card]' ? this : selector === '[data-task-column]' ? column : null; },
                querySelector: () => form
            });
        }
        return column;
    });
    vm.runInNewContext(script, {
        document: { getElementById: id => id === 'task-board' ? surface : id === 'task-board-status' ? status : null },
        window: { addEventListener: (name, listener) => { windowListeners[name] = listener; } }
    });
    const transfer = { data: {}, setData(type, value) { this.data[type] = value; } };
    function event(name, target) {
        let prevented = false;
        listeners[name]?.({ target, dataTransfer: transfer, preventDefault() { prevented = true; } });
        return prevented;
    }
    return { columns, submissions, status, listeners, transfer,
        start: card => event('dragstart', card), over: target => event('dragover', target), drop: target => event('drop', target),
        pageshow: () => windowListeners.pageshow?.()
    };
}

test('dropping before a task submits the destination and position with the original versions and CSRF token', () => {
    const b = board(); const card = b.columns[0].cards[0];
    b.start(card);
    assert.equal(b.transfer.data['text/plain'], card.dataset.taskId);
    assert.equal(b.over(b.columns[1]), true);
    b.drop(b.columns[1].cards[1]);
    assert.deepEqual(b.submissions[0], {
        Version: 'task-version-0-0', BoardVersion: 'board-version-1', ColumnId: 'column-1', TargetPosition: '1',
        __RequestVerificationToken: 'antiforgery-token', returnUrl: '/tasks/board?boardId=b1'
    });
    assert.equal(b.columns[0].cards[0], card, 'native submission must not optimistically move DOM cards');
});

test('same-column drops adjust the insertion index after the source is removed', () => {
    const b = board(); b.start(b.columns[0].cards[0]); b.drop(b.columns[0].cards[2]);
    assert.equal(b.submissions[0].TargetPosition, '1');
    assert.equal(b.submissions[0].ColumnId, 'column-0');
});

test('dropping at the end uses the destination count, including an empty column', () => {
    const b = board(); b.start(b.columns[0].cards[1]); b.drop(b.columns[2]);
    assert.equal(b.submissions[0].TargetPosition, '0');
    assert.equal(b.submissions[0].ColumnId, 'column-2');
    const same = board(); same.start(same.columns[0].cards[0]); same.drop(same.columns[0]);
    assert.equal(same.submissions[0].TargetPosition, '2');
});

test('self drops and unchanged positions do not submit a redundant move', () => {
    const b = board(); const card = b.columns[0].cards[0];
    b.start(card); b.drop(card); assert.equal(b.submissions.length, 0);
    b.start(card); b.drop(b.columns[0].cards[1]); assert.equal(b.submissions.length, 0);
    assert.equal(card.form.elements.TargetPosition.value, '');
});

test('external drops and malformed positions never mutate move-form fields', () => {
    const b = board(); b.drop(b.columns[1]); assert.equal(b.submissions.length, 0);
    const card = b.columns[0].cards[0]; b.columns[1].cards[0].dataset.taskPosition = 'invalid';
    b.start(card); b.drop(b.columns[1].cards[0]);
    assert.equal(b.submissions.length, 0);
    assert.equal(card.form.elements.ColumnId.value, 'column-0');
    assert.equal(card.form.elements.TargetPosition.value, '');
    assert.match(b.status.textContent, /Reload the board/);
});

test('a filtered board does not install drag handlers or change versioned forms', () => {
    const b = board({ enabled: false });
    b.start(b.columns[0].cards[0]); b.drop(b.columns[1]);
    assert.equal(Object.keys(b.listeners).length, 0);
    assert.equal(b.submissions.length, 0);
    assert.equal(b.columns[0].cards[0].form.elements.BoardVersion.value, 'board-version-1');
});

test('repeated drops submit once, and returning to a cached page restores interaction', () => {
    const b = board(); b.start(b.columns[0].cards[0]); b.drop(b.columns[1]);
    b.start(b.columns[0].cards[1]); b.drop(b.columns[2]);
    assert.equal(b.submissions.length, 1);
    b.pageshow(); b.start(b.columns[0].cards[1]); b.drop(b.columns[2]);
    assert.equal(b.submissions.length, 2);
});

test('dragging a card form control cannot start a task move', () => {
    const b = board(); const card = b.columns[0].cards[0];
    const control = { surface: card.surface, closest: selector => selector === '[data-task-card]' ? card : selector === 'input, select, button, textarea' ? {} : null };
    assert.equal(b.start(control), true);
    b.drop(b.columns[1]);
    assert.equal(b.submissions.length, 0);
});

function taskEditor({ paused = false } = {}) {
    const listeners = {}, windowListeners = {};
    let focused;
    const items = {
        rows: [],
        querySelectorAll() { return this.rows; },
        appendChild(row) { row.container = this; this.rows.push(row); },
        insertBefore(row, reference) {
            this.rows.splice(this.rows.indexOf(row), 1);
            this.rows.splice(reference ? this.rows.indexOf(reference) : this.rows.length, 0, row);
        }
    };
    function makeItem(id, text) {
        const row = {
            dataset: { itemId: id }, number: { textContent: '' },
            fields: ['Id', 'Text', 'IsCompleted'].map(field => ({ dataset: { field }, value: field === 'Id' ? id : field === 'Text' ? text : 'true', checked: field === 'IsCompleted', focus() { focused = this; } })),
            labels: [{ dataset: { forField: 'Text' } }],
            get previousElementSibling() { return items.rows[items.rows.indexOf(this) - 1] || null; },
            get nextElementSibling() { return items.rows[items.rows.indexOf(this) + 1] || null; },
            remove() { items.rows.splice(items.rows.indexOf(this), 1); },
            cloneNode() { return makeItem(id, text); },
            querySelector(selector) { return selector === '[data-item-number]' ? this.number : this.fields.find(field => field.dataset.field === (selector === '[data-field="Id"]' ? 'Id' : 'Text')); },
            querySelectorAll(selector) { return selector === '[data-field]' ? this.fields : selector === '[data-for-field]' ? this.labels : this.buttons; }
        };
        row.buttons = ['up', 'down', 'remove'].map(action => ({
            dataset: { checklistAction: action }, attributes: {},
            closest(selector) { return selector === '[data-checklist-action]' ? this : row; },
            setAttribute(name, value) { this.attributes[name] = value; }
        }));
        return row;
    }
    items.appendChild(makeItem('item-1', 'First step')); items.appendChild(makeItem('item-2', '<b>Second step</b>'));
    const add = { dataset: { checklistAction: 'add' }, closest(selector) { return selector === '[data-checklist-action]' ? this : null; }, focus() { focused = this; } };
    const form = {
        dataset: { checklist: '/tasks/t1/checklist', paused: String(paused) },
        elements: { Version: { value: 'task-v1' }, BoardVersion: { value: 'board-v1' } },
        contains: () => true, addEventListener: (name, listener) => { listeners[name] = listener; }
    };
    const status = { textContent: '' }, empty = { hidden: false };
    vm.runInNewContext(script, {
        document: { getElementById: id => id === 'task-board' ? null : id === 'task-editor' ? form : id === 'task-editor-status' ? status : id === 'task-checklist' ? items : id === 'task-checklist-empty' ? empty : id === 'task-add-checklist' ? add : { content: { firstElementChild: makeItem('template', '') } } },
        window: { addEventListener: (name, listener) => { windowListeners[name] = listener; } },
        crypto: { randomUUID: () => 'new-item' }
    });
    return { form, items, status, add, empty,
        click(button) { listeners.click({ target: button, preventDefault() {} }); },
        input() { listeners.input(); },
        submit() { let prevented = false; listeners.submit({ preventDefault() { prevented = true; } }); return prevented; },
        leaving() { let prevented = false; windowListeners.beforeunload({ preventDefault() { prevented = true; } }); return prevented; },
        get focused() { return focused; }
    };
}

test('checklist reordering preserves text, completion, task versions, and contiguous field names', () => {
    const e = taskEditor(); const row = e.items.rows[1];
    e.click(row.buttons[0]);
    assert.equal(e.items.rows[0], row);
    assert.equal(row.fields[1].value, '<b>Second step</b>');
    assert.equal(row.fields[2].checked, true);
    assert.equal(row.fields[2].name, 'Checklist[0].IsCompleted');
    assert.equal(e.items.rows[1].fields[0].name, 'Checklist[1].Id');
    assert.equal(e.form.elements.Version.value, 'task-v1');
    assert.equal(e.form.elements.BoardVersion.value, 'board-v1');
    assert.equal(e.focused, row.fields[1]);
    assert.equal(e.leaving(), true);
});

test('checklist addition and removal create stable item IDs without inserting user HTML', () => {
    const e = taskEditor(); e.click(e.add);
    const added = e.items.rows[2];
    assert.equal(added.fields[0].value, 'new-item');
    assert.equal(added.fields[1].name, 'Checklist[2].Text');
    e.click(added.buttons[2]);
    assert.equal(e.items.rows.length, 2);
    assert.equal(e.focused, e.items.rows[1].fields[1]);
});

test('a stale task editor pauses mutations and submissions while retaining typed edits', () => {
    const e = taskEditor({ paused: true }); e.input(); e.click(e.add);
    assert.equal(e.items.rows.length, 2);
    assert.equal(e.submit(), true);
    assert.equal(e.leaving(), true);
    assert.match(e.status.textContent, /Copy your edits/);
});

test('normal save submits once and suppresses the unsaved-changes prompt during navigation', () => {
    const e = taskEditor(); e.input();
    assert.equal(e.leaving(), true);
    assert.equal(e.submit(), false);
    assert.equal(e.submit(), true);
    assert.equal(e.leaving(), false);
});
