const toggle = document.querySelector("#navigation-toggle");
const sidebar = document.querySelector("#sidebar");

if (toggle && sidebar) {
    const setExpanded = (expanded) => {
        sidebar.classList.toggle("hidden", !expanded);
        toggle.setAttribute("aria-expanded", String(expanded));
        toggle.setAttribute("aria-label", expanded ? "Close navigation" : "Open navigation");
        if (expanded) sidebar.querySelector("a[href]")?.focus();
    };

    toggle.addEventListener("click", () => setExpanded(toggle.getAttribute("aria-expanded") !== "true"));
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && toggle.getAttribute("aria-expanded") === "true") {
            setExpanded(false);
            toggle.focus();
        }
    });
}
