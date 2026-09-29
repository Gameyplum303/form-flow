// Saves bytes sent from .NET as a file, for downloads that need the admin's token.
window.formFlow = window.formFlow || {};
window.formFlow.downloadFile = (fileName, contentType, bytes) => {
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};
