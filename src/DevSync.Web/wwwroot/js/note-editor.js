(() => {
    const form = document.getElementById('note-editor') || (document.getElementById('article-editor')?.dataset.blocks ? document.getElementById('article-editor') : null);
    if (!form || form.dataset.archived === 'true') return;
    const status = document.getElementById('note-save-status') || document.getElementById('save-status');
    const blocks = document.getElementById('note-blocks');
    const empty = document.getElementById('note-blocks-empty');
    let timer, dirty = form.dataset.unsaved === 'true', stopped = form.dataset.paused === 'true';
    let editCount = 0, submitting = false, resumeSubmit = false, savePromise = null;
    let mutationPromise = Promise.resolve(), pendingMutations = 0;
    let pauseReason = 'This note changed elsewhere. Copy your work before reloading.';
    const message = text => { status.textContent = form.dataset.documentLabel ? text.replace(/\bnote\b/g,form.dataset.documentLabel) : text; };
    const schedule = delay => {
        clearTimeout(timer);
        if (dirty && !stopped && !submitting && !pendingMutations) timer = setTimeout(save, delay);
    };
    const changed = () => {
        editCount++;
        dirty = true;
        message(stopped ? pauseReason : 'Unsaved changes…');
        schedule(2500);
    };
    const pause = reason => {
        stopped = true;
        pauseReason = reason;
        clearTimeout(timer);
        message(reason);
    };

    function save() {
        if (savePromise) return savePromise;
        if (!dirty || stopped || submitting || pendingMutations) return Promise.resolve();
        if (!form.reportValidity()) {
            message('Complete the required fields before saving. Your edits remain here.');
            return Promise.resolve();
        }
        const revision = editCount;
        const body = new FormData(form);
        message('Saving note…');
        savePromise = (async () => {
            try {
                const response = await fetch(form.dataset.autosave, {
                    method: 'POST', body, credentials: 'same-origin', headers: { Accept: 'application/json' }
                });
                if (response.redirected || response.status === 401 || response.status === 403) {
                    pause('Your session ended. Copy your work and sign in again.');
                    return;
                }
                if (response.status === 404) {
                    pause('This note is no longer available. Copy your work before leaving.');
                    return;
                }
                const result = await response.json();
                if (response.status === 409) {
                    pause(result.error || 'This note changed elsewhere. Copy your work before reloading.');
                    return;
                }
                if (!response.ok) throw new Error(result.error || 'The note could not be saved. Your edits remain here.');
                if (typeof result.version !== 'string' || !result.version) {
                    pause('The saved version could not be confirmed. Copy your work before reloading.');
                    return;
                }
                form.elements.Version.value = result.version;
                dirty = revision !== editCount;
                message(dirty ? 'New changes waiting to save…' : 'All changes saved.');
            } catch (error) {
                message(error.message || 'Connection lost. Your edits remain in this editor.');
            } finally {
                savePromise = null;
                schedule(10000);
            }
        })();
        return savePromise;
    }

    function reindex() {
        const items = [...blocks.querySelectorAll('[data-note-block]')];
        items.forEach((block, index) => {
            const label = `${block.dataset.blockKind === 'richtext' ? 'rich text' : block.dataset.blockKind} block ${index + 1}`;
            block.querySelector('[data-block-label]').textContent = label;
            block.querySelectorAll('[data-field]').forEach(field => {
                field.name = `Blocks[${index}].${field.dataset.field}`;
                field.id = `note-block-${block.dataset.blockId}-${field.dataset.field}`;
            });
            block.querySelectorAll('[data-for-field]').forEach(fieldLabel => {
                fieldLabel.htmlFor = `note-block-${block.dataset.blockId}-${fieldLabel.dataset.forField}`;
            });
            block.querySelectorAll('[data-block-action]').forEach(button => {
                button.setAttribute('aria-label', `${button.dataset.blockAction === 'remove' ? 'Remove' : button.dataset.blockAction === 'up' ? 'Move up' : 'Move down'} ${label}`);
                button.setAttribute('formaction', `${form.dataset.blocks}?operation=${encodeURIComponent(button.dataset.blockAction)}&blockId=${encodeURIComponent(block.dataset.blockId)}`);
                button.disabled = button.dataset.blockAction === 'up' && index === 0 || button.dataset.blockAction === 'down' && index === items.length - 1;
            });
        });
        empty.hidden = items.length !== 0;
    }

    function mutate(button) {
        const action = button.dataset.blockAction;
        const block = button.closest('[data-note-block]');
        let focusTarget;
        if (action === 'add') {
            if (blocks.querySelectorAll('[data-note-block]').length >= Number(form.dataset.maxBlocks || 50)) {
                message('This note has reached its 50-block limit. Remove a block before adding another.');
                return;
            }
            const kind = form.elements.kind.value;
            const template = document.getElementById(`note-template-${kind}`);
            if (!template) return;
            const newBlock = template.content.firstElementChild.cloneNode(true);
            const id = crypto.randomUUID();
            newBlock.dataset.blockId = id;
            newBlock.querySelector('[data-field="Id"]').value = id;
            blocks.appendChild(newBlock);
            focusTarget = newBlock.querySelector('[data-field="Content"], [data-field="LinkedPostId"]');
        } else if (block && action === 'remove') {
            focusTarget = block.nextElementSibling?.querySelector('[data-field="Content"], [data-field="LinkedPostId"]') ||
                block.previousElementSibling?.querySelector('[data-field="Content"], [data-field="LinkedPostId"]') || document.getElementById('note-add-block');
            block.remove();
        } else if (block && action === 'up' && block.previousElementSibling) {
            blocks.insertBefore(block, block.previousElementSibling);
            focusTarget = block.querySelector('[data-field="Content"], [data-field="LinkedPostId"]');
        } else if (block && action === 'down' && block.nextElementSibling) {
            blocks.insertBefore(block, block.nextElementSibling.nextElementSibling);
            focusTarget = block.querySelector('[data-field="Content"], [data-field="LinkedPostId"]');
        } else return;
        reindex();
        changed();
        focusTarget?.focus();
    }

    form.addEventListener('input', event => {
        // Choosing the type of the next block does not change this note.
        if (event.target?.name !== 'kind' && !event.target?.closest?.('[data-rich-url]')) changed();
    });
    form.addEventListener('click', event => {
        const button = event.target.closest?.('[data-block-action]');
        if (!button || !form.contains(button)) return;
        event.preventDefault();
        if (submitting || stopped) {
            if (stopped) message(pauseReason);
            return;
        }
        pendingMutations++;
        clearTimeout(timer);
        mutationPromise = mutationPromise.then(async () => {
            if (savePromise) {
                message('Finishing autosave before changing blocks…');
                await savePromise;
            }
            if (!stopped) mutate(button);
        }).finally(() => {
            pendingMutations--;
            if (!pendingMutations) schedule(2500);
        });
    });
    form.addEventListener('submit', async event => {
        if (resumeSubmit) {
            resumeSubmit = false;
            submitting = true;
            clearTimeout(timer);
            return;
        }
        if (submitting) {
            event.preventDefault();
            return;
        }
        if (stopped) {
            event.preventDefault();
            message(pauseReason);
            return;
        }
        if (savePromise || pendingMutations) {
            event.preventDefault();
            const button = event.submitter;
            submitting = true;
            clearTimeout(timer);
            message('Finishing your changes before saving…');
            await mutationPromise;
            if (savePromise) await savePromise;
            if (stopped) {
                submitting = false;
                message(pauseReason);
                return;
            }
            submitting = false;
            resumeSubmit = true;
            form.requestSubmit(button);
            // requestSubmit runs browser validation; if it fails no submit event fires.
            if (resumeSubmit) {
                resumeSubmit = false;
                schedule(2500);
            }
            return;
        }
        submitting = true;
        clearTimeout(timer);
    });
    window.addEventListener('beforeunload', event => {
        if (dirty && !submitting) { event.preventDefault(); event.returnValue = ''; }
    });
    reindex();
    if (stopped) message(pauseReason);
    else if (dirty) { message('Unsaved changes…'); schedule(2500); }
})();
