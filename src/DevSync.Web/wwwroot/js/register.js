(() => {
    const form = document.querySelector('[data-register-form]');
    if (!form) return;
    let submitted = false;
    form.addEventListener('submit', event => {
        if (submitted) { event.preventDefault(); return; }
        if (!form.reportValidity()) { event.preventDefault(); return; }
        submitted = true;
        form.querySelector('button[type="submit"]').disabled = true;
    });
    window.addEventListener('pageshow', () => {
        submitted = false;
        form.querySelector('button[type="submit"]').disabled = false;
    });
})();
