/* ── Portal Branding Editor JS ─────────────────────────────────────── */
window.portalBranding = (function () {
    const _loadedFonts = new Set();

    function loadGoogleFont(fontName) {
        if (!fontName) return;
        const systemFonts = ['Arial', 'Georgia', 'Times New Roman', 'Courier New', 'Verdana'];
        if (systemFonts.includes(fontName) || _loadedFonts.has(fontName)) return;
        const encoded = fontName.replace(/ /g, '+');
        if (document.querySelector(`link[href*="${encoded}"]`)) {
            _loadedFonts.add(fontName);
            return;
        }
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = `https://fonts.googleapis.com/css2?family=${encoded}:wght@400;500;600;700&display=swap`;
        document.head.appendChild(link);
        _loadedFonts.add(fontName);
    }

    function exportCss(cssContent, filename) {
        const blob = new Blob([cssContent], { type: 'text/css' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'theme.css';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(url), 2000);
    }

    return { loadGoogleFont, exportCss };
})();
