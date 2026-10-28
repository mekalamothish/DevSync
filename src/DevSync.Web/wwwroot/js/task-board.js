(() => {
    const board = document.getElementById('task-board');
    if (board && board.dataset.dragEnabled === 'true') {
        const status = document.getElementById('task-board-status');
        let dragging = null, moving = false;
        const message = text => { status.textContent = text; };
        const clearTargets = () => board.querySelectorAll('[data-task-column]').forEach(column => column.classList.remove('ring-2', 'ring-brand-100'));
        board.addEventListener('dragstart', event => {
            const card = event.target.closest?.('[data-task-card]');
            if (!card || !board.contains(card) || moving || event.target.closest?.('input, select, button, textarea')) { event.preventDefault(); return; }
            dragging = card;
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', card.dataset.taskId);
            message('Drop the task on a column to move it, or on another task to place it before that task.');
        });
        board.addEventListener('dragover', event => {
            const column = event.target.closest?.('[data-task-column]');
            if (!dragging || !column || !board.contains(column) || moving) return;
            event.preventDefault();
            event.dataTransfer.dropEffect = 'move';
            clearTargets();
            column.classList.add('ring-2', 'ring-brand-100');
        });
        board.addEventListener('drop', event => {
            const column = event.target.closest?.('[data-task-column]');
            const target = event.target.closest?.('[data-task-card]');
            if (!dragging || !column || !board.contains(column) || moving) return;
            event.preventDefault();
            clearTargets();
            if (target === dragging) { message('The task stays in its current position.'); dragging = null; return; }
            const source = dragging.closest('[data-task-column]');
            const sourcePosition = Number(dragging.dataset.taskPosition);
            let position = target ? Number(target.dataset.taskPosition) : column.querySelectorAll('[data-task-card]').length;
            if (!Number.isInteger(position) || position < 0 || !Number.isInteger(sourcePosition) || sourcePosition < 0) {
                message('This position could not be confirmed. Reload the board before moving the task.');
                dragging = null;
                return;
            }
            if (source === column && sourcePosition < position) position--;
            if (source === column && sourcePosition === position) { message('The task stays in its current position.'); dragging = null; return; }
            const form = dragging.querySelector('[data-task-move]');
            if (!form || !form.elements.ColumnId || !form.elements.TargetPosition) {
                message('Use the column selector to move this task.');
                dragging = null;
                return;
            }
            form.elements.ColumnId.value = column.dataset.columnId;
            form.elements.TargetPosition.value = String(position);
            moving = true;
            dragging = null;
            message('Moving task… The page will show the saved result or a conflict.');
            form.requestSubmit();
        });
        board.addEventListener('dragend', () => {
            dragging = null;
            clearTargets();
            if (!moving) message('Drag tasks to move them. Column selectors and Up/Down buttons work with a keyboard.');
        });
        // Restore interaction when navigating back to a cached board page.
        window.addEventListener('pageshow', () => { moving = false; dragging = null; clearTargets(); });
    }

    const form = document.getElementById('task-editor');
    if (!form || form.dataset.archived === 'true') return;
    const status = document.getElementById('task-editor-status');
    const items = document.getElementById('task-checklist');
    const empty = document.getElementById('task-checklist-empty');
    const paused = form.dataset.paused === 'true';
    let dirty = form.dataset.unsaved === 'true', submitting = false;
    const message = text => { status.textContent = text; };
    const changed = () => { dirty = true; message(paused ? 'This task changed elsewhere. Copy your edits before reloading.' : 'Unsaved changes. Save task to keep them.'); };
    function reindex() {
        const rows = [...items.querySelectorAll('[data-checklist-item]')];
        rows.forEach((row, index) => {
            row.querySelectorAll('[data-field]').forEach(field => {
                field.name = `Checklist[${index}].${field.dataset.field}`;
                field.id = `task-checklist-${row.dataset.itemId}-${field.dataset.field}`;
            });
            row.querySelectorAll('[data-for-field]').forEach(label => { label.htmlFor = `task-checklist-${row.dataset.itemId}-${label.dataset.forField}`; });
            row.querySelector('[data-item-number]').textContent = `Item ${index + 1}`;
            row.querySelectorAll('[data-checklist-action]').forEach(button => {
                const action = button.dataset.checklistAction;
                button.setAttribute('aria-label', `${action === 'remove' ? 'Remove' : action === 'up' ? 'Move up' : 'Move down'} checklist item ${index + 1}`);
                button.setAttribute('formaction', `${form.dataset.checklist}?operation=${encodeURIComponent(action)}&itemId=${encodeURIComponent(row.dataset.itemId)}`);
                button.disabled = action === 'up' && index === 0 || action === 'down' && index === rows.length - 1;
            });
        });
        empty.hidden = rows.length !== 0;
    }
    form.addEventListener('input', changed);
    form.addEventListener('click', event => {
        const button = event.target.closest?.('[data-checklist-action]');
        if (!button || !form.contains(button)) return;
        event.preventDefault();
        if (submitting || paused) { if (paused) message('This task changed elsewhere. Copy your edits before reloading.'); return; }
        const action = button.dataset.checklistAction;
        const row = button.closest('[data-checklist-item]');
        let focusTarget;
        if (action === 'add') {
            if (items.querySelectorAll('[data-checklist-item]').length >= 30) { message('A task can contain up to 30 checklist items.'); return; }
            const item = document.getElementById('task-checklist-template').content.firstElementChild.cloneNode(true);
            item.dataset.itemId = crypto.randomUUID();
            item.querySelector('[data-field="Id"]').value = item.dataset.itemId;
            items.appendChild(item);
            focusTarget = item.querySelector('[data-field="Text"]');
        } else if (row && action === 'remove') {
            focusTarget = row.nextElementSibling?.querySelector('[data-field="Text"]') || row.previousElementSibling?.querySelector('[data-field="Text"]') || document.getElementById('task-add-checklist');
            row.remove();
        } else if (row && action === 'up' && row.previousElementSibling) {
            items.insertBefore(row, row.previousElementSibling);
            focusTarget = row.querySelector('[data-field="Text"]');
        } else if (row && action === 'down' && row.nextElementSibling) {
            items.insertBefore(row, row.nextElementSibling.nextElementSibling);
            focusTarget = row.querySelector('[data-field="Text"]');
        } else return;
        reindex();
        changed();
        focusTarget?.focus();
    });
    form.addEventListener('submit', event => {
        if (submitting || paused) {
            event.preventDefault();
            if (paused) message('This task changed elsewhere. Copy your edits before reloading.');
            return;
        }
        submitting = true;
        message('Saving task…');
    });
    window.addEventListener('beforeunload', event => { if (dirty && !submitting) { event.preventDefault(); event.returnValue = ''; } });
    window.addEventListener('pageshow', () => { submitting = false; });
    reindex();
    if (paused) message('This task changed elsewhere. Copy your edits before reloading.');
    else if (dirty) message('Unsaved changes. Save task to keep them.');
})();
