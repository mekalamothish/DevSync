(() => {
    const key = 'devsync-theme';
    const root = document.documentElement;
    const system = window.matchMedia?.('(prefers-color-scheme: dark)');
    const valid = value => value === 'dark' || value === 'light' ? value : null;
    let preferred = null;
    try { preferred = valid(window.localStorage.getItem(key)); } catch { /* Private browsing can block storage. */ }

    function syncButtons() {
        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            const dark = root.dataset.theme === 'dark';
            button.hidden = false;
            button.setAttribute('aria-pressed', String(dark));
            button.setAttribute('aria-label', dark ? 'Switch to light mode' : 'Switch to dark mode');
            button.title = dark ? 'Switch to light mode' : 'Switch to dark mode';
        });
    }
    function apply(theme) {
        root.dataset.theme = theme;
        syncButtons();
        window.dispatchEvent(new CustomEvent('devsync:theme-change', { detail: { theme } }));
    }
    // Loaded synchronously before CSS in both layouts to avoid a light-theme flash.
    apply(preferred || (system?.matches ? 'dark' : 'light'));
    function bind() {
        syncButtons();
        document.querySelectorAll('[data-theme-toggle]').forEach(button => {
            button.addEventListener('click', () => {
                preferred = root.dataset.theme === 'dark' ? 'light' : 'dark';
                try { window.localStorage.setItem(key, preferred); } catch { /* The current page still switches. */ }
                apply(preferred);
            });
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind, { once: true });
    else bind();

    const systemChanged = event => { if (!preferred) apply(event.matches ? 'dark' : 'light'); };
    if (system?.addEventListener) system.addEventListener('change', systemChanged);
    else system?.addListener?.(systemChanged);
    window.addEventListener('storage', event => {
        if (event.key !== key && event.key !== null) return;
        preferred = valid(event.newValue);
        apply(preferred || (system?.matches ? 'dark' : 'light'));
    });
})();
