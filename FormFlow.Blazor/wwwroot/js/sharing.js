// Helpers for the survey Share page.
window.formFlow = window.formFlow || {};

// Copies text to the clipboard. Returns false when the browser doesn't allow it (for example over plain HTTP).
window.formFlow.copyText = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
};

// Minutes to add to the browser's local time to get UTC, as Date.getTimezoneOffset reports it.
window.formFlow.timezoneOffset = () => new Date().getTimezoneOffset();
