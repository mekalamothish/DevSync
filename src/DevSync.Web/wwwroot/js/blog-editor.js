(() => {
    const form = document.getElementById('article-editor');
    if (!form) return;
    const status = document.getElementById('save-status');
    let timer, saving = false, dirty = false, stopped = false, submitting = false;
    let editCount = 0;
    const message = text => { status.textContent = text; };
    async function save() {
        if (!dirty || saving || stopped || submitting || !form.reportValidity()) return;
        saving = true;
        const revision = editCount;
        message('Saving draft…');
        try {
            const response = await fetch(form.dataset.autosave, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin',
                headers: { 'Accept': 'application/json' }
            });
            if (response.redirected) {
                stopped = true;
                throw new Error('Your session ended. Copy your work and sign in again.');
            }
            const result = await response.json();
            if (!response.ok) {
                if (response.status === 409) stopped = true;
                throw new Error(result.error || 'Draft could not be saved. Try Save draft.');
            }
            form.elements.Version.value = result.version;
            dirty = revision !== editCount;
            message(dirty ? 'New changes waiting to save…' : 'Draft saved.');
        } catch (error) { message(error.message || 'Connection lost. Your changes remain in this editor.'); }
        finally {
            saving = false;
            if (dirty && !stopped && !submitting) timer = setTimeout(save, 10000);
        }
    }
    form.addEventListener('input', () => {
        editCount++; dirty = true; clearTimeout(timer);
        if (!stopped) { message('Unsaved changes…'); timer = setTimeout(save, 2500); }
    });
    form.addEventListener('submit', async event => {
        // Let an in-flight autosave finish updating Version before a manual save/publish.
        if (saving) {
            event.preventDefault();
            const button = event.submitter;
            message('Finishing autosave before submitting…');
            while (saving) await new Promise(resolve => setTimeout(resolve, 50));
            form.requestSubmit(button);
            return;
        }
        if (stopped) {
            event.preventDefault();
            message('Saving is paused because this draft changed elsewhere. Copy your work before reloading.');
            return;
        }
        submitting = true; clearTimeout(timer);
    });
    window.addEventListener('beforeunload', event => {
        if (dirty && !submitting) { event.preventDefault(); event.returnValue = ''; }
    });
})();
