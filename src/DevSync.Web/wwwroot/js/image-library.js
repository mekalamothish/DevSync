document.querySelectorAll("[data-image-copy]").forEach((button) => {
    button.addEventListener("click", async () => {
        const input = document.getElementById(button.dataset.imageCopy);
        const status = document.getElementById("image-copy-status");
        if (!input || !status) return;
        try {
            await navigator.clipboard.writeText(input.value);
            status.textContent = button.dataset.imageCopy.startsWith("markdown-") ? "Markdown copied. Paste it into your article." : "Image URL copied.";
        } catch {
            input.focus();
            input.select();
            status.textContent = "Select and copy the highlighted text using your keyboard or context menu.";
        }
    });
});
