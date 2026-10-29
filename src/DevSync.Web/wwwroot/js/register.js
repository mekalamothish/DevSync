(() => {
    const form = document.querySelector('[data-register-form]');
    if (!form) return;
    const password = form.querySelector('[name="Password"]');
    const confirmation = form.querySelector('[name="ConfirmPassword"]');
    const syncConfirmation = () => {
        if(password && confirmation) confirmation.setCustomValidity(confirmation.value && confirmation.value !== password.value ? 'Passwords must match.' : '');
    };
    password?.addEventListener('input', syncConfirmation);
    confirmation?.addEventListener('input', syncConfirmation);
    let submitted = false;
    form.addEventListener('submit', event => {
        if (submitted) { event.preventDefault(); return; }
        syncConfirmation();
        if (!form.reportValidity()) { event.preventDefault(); return; }
        submitted = true;
        form.querySelector('button[type="submit"]').disabled = true;
    });
    window.addEventListener('pageshow', () => {
        submitted = false;
        syncConfirmation();
        form.querySelector('button[type="submit"]').disabled = false;
    });
})();
