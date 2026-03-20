/* ================================================================
   FC Performance — Perceived-speed utilities
   - Connection quality detection   → .fc-slow-connection on <html>
   - Lazy image blur-up             → IntersectionObserver + .fc-img-loaded
   ================================================================ */

window.FCPerf = (() => {
    'use strict';

    /* ── 1. Connection Quality ─────────────────────────────────── */

    /**
     * Detects slow network conditions (Save-Data header or slow-2g / 2g
     * effectiveType) and toggles the .fc-slow-connection class on <html>.
     * When active, all CSS animations and transitions are suppressed, and
     * skeleton shimmer is replaced with a flat placeholder.
     */
    function initConnectionQuality() {
        const conn = navigator.connection
            || navigator.mozConnection
            || navigator.webkitConnection;

        if (!conn) return; // API not available (Firefox, Safari)

        function check() {
            const slow = conn.saveData
                || ['slow-2g', '2g'].includes(conn.effectiveType || '');
            document.documentElement.classList.toggle('fc-slow-connection', slow);
        }

        check(); // Run immediately on load
        conn.addEventListener('change', check);
    }

    /* ── 2. Lazy Image Blur-Up ─────────────────────────────────── */

    /**
     * Observes all img.fc-img-lazyload elements. When an image enters the
     * viewport, waits for it to load, then adds .fc-img-loaded to trigger
     * the CSS blur-to-sharp transition.
     *
     * Re-runs on every Blazor enhanced navigation to catch newly rendered images.
     */
    function initLazyImages() {
        const obs = new IntersectionObserver((entries) => {
            entries
                .filter(e => e.isIntersecting)
                .forEach(e => {
                    const img = e.target;
                    // If already loaded (e.g. cached by browser), mark immediately
                    if (img.complete && img.naturalWidth > 0) {
                        img.classList.add('fc-img-loaded');
                    } else {
                        img.addEventListener(
                            'load',
                            () => img.classList.add('fc-img-loaded'),
                            { once: true }
                        );
                    }
                    obs.unobserve(img);
                });
        }, { rootMargin: '50px' }); // Start loading 50px before entering viewport

        function attachAll() {
            document
                .querySelectorAll('img.fc-img-lazyload:not(.fc-img-loaded)')
                .forEach(img => obs.observe(img));
        }

        attachAll();

        // Re-attach after every Blazor navigation
        Blazor.addEventListener('enhancedload', attachAll);
    }

    /* ── Public API ────────────────────────────────────────────── */
    return {
        initConnectionQuality,
        initLazyImages
    };
})();
