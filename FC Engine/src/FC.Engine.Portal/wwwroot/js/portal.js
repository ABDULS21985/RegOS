// FC Engine Portal — JS interop functions

window.portalCopyToClipboard = function (text) {
    return navigator.clipboard.writeText(text);
};

window.portalDownloadFile = function (content, filename, contentType) {
    var blob = new Blob([content], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
