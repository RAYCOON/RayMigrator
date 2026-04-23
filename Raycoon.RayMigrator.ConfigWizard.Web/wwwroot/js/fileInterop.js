// File download interop for Blazor WASM
window.downloadFileFromBytes = (fileName, contentType, byteArray) => {
    const blob = new Blob([byteArray], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
};

// localStorage interop
window.getLocalStorage = (key) => localStorage.getItem(key);
window.setLocalStorage = (key, value) => localStorage.setItem(key, value);
