// FC Engine Portal — JS interop functions

// ── Scroll to first matching element ──────────────────────────────────────────
window.portalScrollToElement = function (selector) {
    var el = document.querySelector(selector);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
};

// ── Data Entry Form: Ctrl+Enter keyboard shortcut ─────────────────────────────
window.portalDataEntryForm = (function () {
    var _dotNetRef = null;
    var _handler = null;

    return {
        init: function (dotNetRef) {
            _dotNetRef = dotNetRef;
            _handler = function (e) {
                if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                    e.preventDefault();
                    if (_dotNetRef) {
                        _dotNetRef.invokeMethodAsync('OnCtrlEnter');
                    }
                }
            };
            document.addEventListener('keydown', _handler);
        },
        dispose: function () {
            if (_handler) {
                document.removeEventListener('keydown', _handler);
                _handler = null;
            }
            _dotNetRef = null;
        }
    };
})();

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

window.portalDownloadBase64File = function (base64Content, filename, contentType) {
    var binary = atob(base64Content);
    var len = binary.length;
    var bytes = new Uint8Array(len);
    for (var i = 0; i < len; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    var blob = new Blob([bytes], { type: contentType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.portalPrintElement = function (elementId) {
    var element = document.getElementById(elementId);
    if (!element) return;
    var printWindow = window.open("", "_blank", "width=900,height=700");
    if (!printWindow) return;
    var styleSheets = Array.from(document.styleSheets)
        .map(function (sheet) {
            try {
                return sheet.href
                    ? '<link rel="stylesheet" href="' + sheet.href + '">'
                    : '<style>' + Array.from(sheet.cssRules).map(function (r) { return r.cssText; }).join("\n") + '</style>';
            } catch (e) {
                return sheet.href ? '<link rel="stylesheet" href="' + sheet.href + '">' : "";
            }
        }).join("\n");
    printWindow.document.write(
        '<!DOCTYPE html><html><head><title>Print</title>' + styleSheets +
        '<style>body{margin:0;padding:0}@media print{body{-webkit-print-color-adjust:exact;print-color-adjust:exact}}</style>' +
        '</head><body>' + element.outerHTML + '</body></html>'
    );
    printWindow.document.close();
    printWindow.focus();
    setTimeout(function () { printWindow.print(); printWindow.close(); }, 500);
};

// ═══════════════════════════════════════════════════════════════════════
// Validation Preview — Debounce + Field Change Interop
// ═══════════════════════════════════════════════════════════════════════

window.portalDebounce = (function () {
    var timers = {};
    return function (key, dotnetRef, methodName, delayMs) {
        if (timers[key]) clearTimeout(timers[key]);
        timers[key] = setTimeout(function () {
            dotnetRef.invokeMethodAsync(methodName);
        }, delayMs || 300);
    };
})();

window.portalGetFormFieldValues = function (containerId) {
    var container = document.getElementById(containerId);
    if (!container) return {};
    var values = {};
    var inputs = container.querySelectorAll("input[data-field], select[data-field], textarea[data-field]");
    inputs.forEach(function (input) {
        var fieldName = input.getAttribute("data-field");
        if (fieldName) {
            values[fieldName] = input.value || "";
        }
    });
    return values;
};

// portalApplyFieldValidation has two calling conventions:
//   Batch:       portalApplyFieldValidation(containerId, fieldStatusesArray)
//   Single-field: portalApplyFieldValidation(fieldId, 'valid'|'error'|'warning'|'clear')
window.portalApplyFieldValidation = function (firstArg, secondArg) {
    // Single-field mode: secondArg is a string status
    if (typeof secondArg === "string") {
        var input = document.getElementById(firstArg);
        if (!input) return;
        var wrapper = input.closest(".portal-field-wrapper") || input.parentElement;
        // Remove existing state classes from both input and wrapper
        [input, wrapper].forEach(function (el) {
            el.classList.remove("portal-field-valid", "portal-field-error", "portal-field-warning",
                "portal-field-wrapper--valid", "portal-field-wrapper--error", "portal-field-wrapper--warning");
        });
        if (secondArg === "valid") {
            wrapper.classList.add("portal-field-wrapper--valid");
        } else if (secondArg === "error") {
            wrapper.classList.add("portal-field-wrapper--error");
            // Shake on error for immediate feedback
            if (window.FCMotion && typeof window.FCMotion.shakeElement === "function") {
                window.FCMotion.shakeElement(wrapper);
            }
        } else if (secondArg === "warning") {
            wrapper.classList.add("portal-field-wrapper--warning");
        }
        return;
    }

    // Batch mode: firstArg = containerId, secondArg = fieldStatuses array
    var container = document.getElementById(firstArg);
    if (!container) return;
    container.querySelectorAll(".portal-field-status").forEach(function (el) { el.remove(); });
    container.querySelectorAll(".portal-field-valid, .portal-field-error, .portal-field-warning")
        .forEach(function (el) {
            el.classList.remove("portal-field-valid", "portal-field-error", "portal-field-warning");
        });
    secondArg.forEach(function (fs) {
        var input = container.querySelector("[data-field='" + fs.fieldName + "']");
        if (!input) return;
        var wrapper = input.closest(".portal-form-group") || input.parentElement;
        if (fs.status === "Valid") input.classList.add("portal-field-valid");
        else if (fs.status === "Error") input.classList.add("portal-field-error");
        else if (fs.status === "Warning") input.classList.add("portal-field-warning");
        if (fs.status !== "Empty" && fs.message) {
            var indicator = document.createElement("span");
            indicator.className = "portal-field-status portal-field-status-" + fs.status.toLowerCase();
            indicator.textContent = fs.message;
            wrapper.appendChild(indicator);
        }
    });
};

window.portalUpdateFormulaTotals = function (formulaResults) {
    formulaResults.forEach(function (fr) {
        var totalEl = document.querySelector("[data-formula-total='" + fr.fieldName + "']");
        if (totalEl) {
            totalEl.textContent = "Expected: " + (fr.computedValue || "\u2014");
            totalEl.className = "portal-formula-total " +
                (fr.matches ? "portal-formula-match" : "portal-formula-mismatch");
        }
    });
};

// ═══════════════════════════════════════════════════════════════════════
// Accessibility — Focus Trap for Modals
// ═══════════════════════════════════════════════════════════════════════

window.portalTrapFocus = function (elementId) {
    var container = document.getElementById(elementId);
    if (!container) return;
    var focusable = container.querySelectorAll(
        'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    if (focusable.length === 0) return;
    var first = focusable[0];
    var last = focusable[focusable.length - 1];
    first.focus();
    container.addEventListener("keydown", function trapHandler(e) {
        if (e.key !== "Tab") return;
        if (e.shiftKey) {
            if (document.activeElement === first) {
                e.preventDefault();
                last.focus();
            }
        } else {
            if (document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        }
    });
};

// Announce text to screen readers via live region
window.portalAnnounce = function (message) {
    var region = document.querySelector("[aria-live='polite'][role='status']");
    if (region) {
        region.textContent = "";
        setTimeout(function () { region.textContent = message; }, 100);
    }
};

// ═══════════════════════════════════════════════════════════════════════
// Notification Sound — Play/Mute with localStorage persistence
// ═══════════════════════════════════════════════════════════════════════

window.portalNotifPlaySound = function () {
    try {
        var ctx = new (window.AudioContext || window.webkitAudioContext)();
        var osc = ctx.createOscillator();
        var gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.type = "sine";
        osc.frequency.setValueAtTime(880, ctx.currentTime);
        osc.frequency.setValueAtTime(1046.5, ctx.currentTime + 0.08);
        gain.gain.setValueAtTime(0.15, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3);
        osc.start(ctx.currentTime);
        osc.stop(ctx.currentTime + 0.3);
    } catch (e) { /* AudioContext not available */ }
};

window.portalNotifGetSoundPref = function () {
    try {
        return localStorage.getItem("portalNotifSound") === "true";
    } catch (e) { return false; }
};

window.portalNotifSetSoundPref = function (enabled) {
    try {
        localStorage.setItem("portalNotifSound", enabled ? "true" : "false");
    } catch (e) { /* storage not available */ }
};

// Scroll an element into view by ID (used for carry-forward field navigation)
window.portalScrollIntoView = function (elementId) {
    var el = document.getElementById(elementId);
    if (el) {
        el.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
        // Also focus the first non-checkbox input for keyboard accessibility
        var input = el.querySelector("input:not([type='checkbox']), select, textarea");
        if (input) { setTimeout(function () { input.focus(); }, 350); }
    }
};

// Guided Tour overlay helpers
window.portalTour = (function () {
    var activeElement = null;

    function clear() {
        if (activeElement) {
            activeElement.classList.remove("portal-tour-highlight");
            activeElement = null;
        }
    }

    function highlight(selector) {
        clear();
        if (!selector) {
            return null;
        }

        var target = document.querySelector(selector);
        if (!target) {
            return null;
        }

        target.classList.add("portal-tour-highlight");
        target.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
        activeElement = target;

        var rect = target.getBoundingClientRect();
        return {
            top: rect.top + window.scrollY,
            left: rect.left + window.scrollX,
            width: rect.width,
            height: rect.height
        };
    }

    return {
        highlight: highlight,
        clear: clear
    };
})();

// ── Feature-discovery beacons ─────────────────────────────────────────────────
// Adds the .portal-tour-beacon pulsing ring to DOM elements and wires click
// callbacks back into Blazor via DotNetObjectReference.

window.portalTour.initBeacons = function (selectors, dotNetRef) {
    selectors.forEach(function (selector, idx) {
        var el = document.querySelector(selector);
        if (!el) return;
        el.classList.add('portal-tour-beacon');
        el._tourClickHandler = function (e) {
            e.stopPropagation();
            dotNetRef.invokeMethodAsync('OnBeaconClicked', idx);
        };
        el.addEventListener('click', el._tourClickHandler, { capture: true });
    });
};

window.portalTour.removeBeacon = function (selector) {
    var el = document.querySelector(selector);
    if (!el) return;
    el.classList.remove('portal-tour-beacon');
    if (el._tourClickHandler) {
        el.removeEventListener('click', el._tourClickHandler, { capture: true });
        delete el._tourClickHandler;
    }
};

window.portalTour.clearAllBeacons = function () {
    document.querySelectorAll('.portal-tour-beacon').forEach(function (el) {
        el.classList.remove('portal-tour-beacon');
        if (el._tourClickHandler) {
            el.removeEventListener('click', el._tourClickHandler, { capture: true });
            delete el._tourClickHandler;
        }
    });
};

// Smart tooltip positioning: returns { top, left, arrowDir } keeping the card
// inside the viewport. Called by TourBeaconSet.razor OnBeaconClicked.
window.portalTour.getTooltipPosition = function (selector, tooltipW, tooltipH) {
    var el = document.querySelector(selector);
    if (!el) return { top: 120, left: 120, arrowDir: 'top' };

    var rect = el.getBoundingClientRect();
    var vw = window.innerWidth;
    var vh = window.innerHeight;
    var gap = 14;
    var scrollY = window.scrollY;
    var scrollX = window.scrollX;

    function clampLeft(l) { return Math.min(Math.max(l, 8), vw - tooltipW - 8); }

    // Prefer below
    if (rect.bottom + tooltipH + gap < vh) {
        return { top: rect.bottom + gap + scrollY, left: clampLeft(rect.left + scrollX), arrowDir: 'top' };
    }
    // Try above
    if (rect.top - tooltipH - gap > 0) {
        return { top: rect.top - tooltipH - gap + scrollY, left: clampLeft(rect.left + scrollX), arrowDir: 'bottom' };
    }
    // Try right
    if (rect.right + tooltipW + gap < vw) {
        return { top: rect.top + scrollY, left: rect.right + gap + scrollX, arrowDir: 'left' };
    }
    // Fallback left
    return { top: rect.top + scrollY, left: Math.max(8, rect.left - tooltipW - gap + scrollX), arrowDir: 'right' };
};

// ── Spotlight highlight (used by GuidedTour IsSpotlight mode) ─────────────────
// Applies a box-shadow "spotlight" to the target element so the surrounding
// page appears darkened through the CSS .portal-tour-backdrop-spotlight overlay.

window.portalTour.spotlightOn = function (selector) {
    window.portalTour.spotlightOff();
    var el = document.querySelector(selector);
    if (!el) return null;
    el.classList.add('portal-tour-spotlight-target');
    el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
    var rect = el.getBoundingClientRect();
    return { top: rect.top + window.scrollY, left: rect.left + window.scrollX, width: rect.width, height: rect.height };
};

window.portalTour.spotlightOff = function () {
    document.querySelectorAll('.portal-tour-spotlight-target').forEach(function (el) {
        el.classList.remove('portal-tour-spotlight-target');
    });
};

window.portalScrollToSection = function (elementId) {
    var el = document.getElementById(elementId);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};

// ─── Error Summary: Jump-to-Field Navigation ────────────────────────────────
window.portalScrollToField = function (fieldCode) {
    var wrapper = document.querySelector('[data-field="' + CSS.escape(fieldCode) + '"]');
    if (!wrapper) return;

    wrapper.scrollIntoView({ behavior: 'smooth', block: 'center' });

    // Restart highlight animation
    wrapper.classList.remove('portal-field-highlight');
    void wrapper.offsetWidth;
    wrapper.classList.add('portal-field-highlight');

    // Focus the first editable input inside the wrapper
    var focusable = wrapper.querySelector(
        'input:not([type="hidden"]):not([readonly]):not([disabled]),' +
        'select:not([disabled]),' +
        'textarea:not([readonly]):not([disabled])'
    );
    if (focusable) {
        setTimeout(function () { focusable.focus({ preventScroll: true }); }, 350);
    }

    // Remove highlight class after animation completes
    setTimeout(function () { wrapper.classList.remove('portal-field-highlight'); }, 2500);
};

// ═══════════════════════════════════════════════════════════════════════
// Bulk Upload — Client-side Pre-validation Helpers
// ═══════════════════════════════════════════════════════════════════════

window.portalBulkCheckDuplicate = function (hash) {
    try {
        var stored = JSON.parse(localStorage.getItem("portal_bulk_hashes") || "[]");
        var match = stored.find(function (h) { return h.hash === hash; });
        if (match) return { isDuplicate: true, date: match.date, fileName: match.fileName };
    } catch (e) { /* storage unavailable */ }
    return { isDuplicate: false, date: null, fileName: null };
};

window.portalBulkStoreHash = function (hash, fileName) {
    try {
        var stored = JSON.parse(localStorage.getItem("portal_bulk_hashes") || "[]");
        // Remove existing entry for same hash, then prepend new one
        stored = stored.filter(function (h) { return h.hash !== hash; });
        stored.unshift({ hash: hash, fileName: fileName, date: new Date().toISOString() });
        if (stored.length > 50) stored = stored.slice(0, 50);
        localStorage.setItem("portal_bulk_hashes", JSON.stringify(stored));
    } catch (e) { /* storage unavailable */ }
};

window.portalBulkDownloadCsv = function (csvContent, fileName) {
    var bom = "\uFEFF"; // UTF-8 BOM for Excel compatibility
    var blob = new Blob([bom + csvContent], { type: "text/csv;charset=utf-8;" });
    var url = URL.createObjectURL(blob);
    var a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
};

// ── Bulk selection helpers ────────────────────────────────────────────────────

/** Set the indeterminate property on a checkbox element reference */
window.portalSetIndeterminate = function (el, value) {
    try { if (el) el.indeterminate = value; } catch (e) { /* ignore */ }
};

/** Register a document-level Escape key handler for bulk deselect */
let _portalEscHandler = null;
window.portalInitEscHandler = function (dotnetRef) {
    portalDisposeEscHandler();
    _portalEscHandler = function (e) {
        if (e.key === 'Escape') {
            dotnetRef.invokeMethodAsync('HandleEscapeKey').catch(() => {});
        }
    };
    document.addEventListener('keydown', _portalEscHandler);
};

window.portalDisposeEscHandler = function () {
    if (_portalEscHandler) {
        document.removeEventListener('keydown', _portalEscHandler);
        _portalEscHandler = null;
    }
};

// ── Compliance Certificate V2 helpers ────────────────────────────────────────

/**
 * Renders a QR-like canvas pattern for the compliance certificate.
 * Produces proper corner finder patterns and pseudo-random data cells
 * derived from the text. Not a standards-compliant QR code, but
 * visually appropriate for print documents.
 */
window.portalCertQr = function (canvasId, text) {
    var canvas = document.getElementById(canvasId);
    if (!canvas) return;
    var ctx = canvas.getContext('2d');
    var size = canvas.width;
    var M = 21; // 21×21 modules (version 1)
    var cell = Math.floor(size / M);

    // Simple 32-bit hash of text for deterministic pattern
    var h = 0x12345678;
    for (var i = 0; i < text.length; i++) {
        h = (Math.imul(h ^ text.charCodeAt(i), 0x9e3779b9) >>> 0);
    }

    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, size, size);
    ctx.fillStyle = '#1a1a1a';

    function fillCell(r, c) {
        ctx.fillRect(c * cell + 1, r * cell + 1, cell - 1, cell - 1);
    }

    function finder(dr, dc) {
        for (var fr = 0; fr < 7; fr++) {
            for (var fc = 0; fc < 7; fc++) {
                if (fr === 0 || fr === 6 || fc === 0 || fc === 6 ||
                    (fr >= 2 && fr <= 4 && fc >= 2 && fc <= 4)) {
                    fillCell(dr + fr, dc + fc);
                }
            }
        }
    }

    finder(0, 0);
    finder(0, M - 7);
    finder(M - 7, 0);

    for (var t = 8; t < M - 8; t++) {
        if (t % 2 === 0) { fillCell(6, t); fillCell(t, 6); }
    }

    for (var r = 0; r < M; r++) {
        for (var c = 0; c < M; c++) {
            if ((r < 9 && c < 9) || (r < 9 && c > M - 9) ||
                (r > M - 9 && c < 9) || r === 6 || c === 6) continue;
            var s = ((h ^ (r * 31 + c * 37) * 0x9e3779b9) >>> 0);
            s ^= s >>> 17;
            s = (Math.imul(s, 0x45d9f3b) >>> 0);
            s ^= s >>> 16;
            if (s & 1) fillCell(r, c);
        }
    }
};

/**
 * Copies the certificate verification URL to the clipboard.
 * Temporarily updates the button label to confirm the copy.
 */
window.portalCertCopyLink = function (url, buttonId) {
    function doCopy() {
        var btn = buttonId ? document.getElementById(buttonId) : null;
        if (btn) {
            var original = btn.innerHTML;
            btn.innerHTML = '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" aria-hidden="true"><polyline points="20 6 9 17 4 12"/></svg> Copied!';
            btn.classList.add('portal-cert-v2-copy-btn--copied');
            setTimeout(function () {
                btn.innerHTML = original;
                btn.classList.remove('portal-cert-v2-copy-btn--copied');
            }, 2200);
        }
    }
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(url).then(doCopy).catch(function () { doCopy(); });
    }
    var ta = document.createElement('textarea');
    ta.value = url;
    ta.style.cssText = 'position:fixed;opacity:0;top:0;left:0';
    document.body.appendChild(ta);
    ta.focus(); ta.select();
    try { document.execCommand('copy'); } catch (e) { /* ignore */ }
    document.body.removeChild(ta);
    doCopy();
};

// ── Sidebar nav: collapsed sections (localStorage) ─────────────────────────

/**
 * Returns array of section names that are currently collapsed.
 * @returns {string[]}
 */
window.portalNavGetCollapsed = function () {
    try {
        const raw = localStorage.getItem('fc_nav_collapsed');
        return raw ? JSON.parse(raw) : [];
    } catch {
        return [];
    }
};

/**
 * Persists a section's collapsed state to localStorage.
 * @param {string} section
 * @param {boolean} collapsed
 */
window.portalNavSetCollapsed = function (section, collapsed) {
    try {
        const raw = localStorage.getItem('fc_nav_collapsed');
        const list = raw ? JSON.parse(raw) : [];
        const idx = list.indexOf(section);
        if (collapsed && idx === -1) list.push(section);
        else if (!collapsed && idx !== -1) list.splice(idx, 1);
        localStorage.setItem('fc_nav_collapsed', JSON.stringify(list));
    } catch { /* non-fatal */ }
};

// ── Sidebar nav: recent pages history (localStorage) ───────────────────────

const FC_NAV_RECENT_KEY = 'fc_nav_recent_pages';
const FC_NAV_RECENT_MAX = 3;

/**
 * Returns the last N visited pages as [{href, label}].
 * @returns {{href:string, label:string}[]}
 */
window.portalNavGetRecentPages = function () {
    try {
        const raw = localStorage.getItem(FC_NAV_RECENT_KEY);
        return raw ? JSON.parse(raw) : [];
    } catch {
        return [];
    }
};

/**
 * Prepends a page to the recent-pages list, removing duplicates, capping at MAX.
 * Skips pages with empty labels (e.g. dashboard root is not tracked).
 * @param {string} href
 * @param {string} label
 */
window.portalNavAddRecentPage = function (href, label) {
    if (!label) return;
    try {
        const raw = localStorage.getItem(FC_NAV_RECENT_KEY);
        let list = raw ? JSON.parse(raw) : [];
        // Remove existing entry for same href
        list = list.filter(p => p.href !== href);
        // Prepend new entry
        list.unshift({ href, label });
        // Cap at max
        if (list.length > FC_NAV_RECENT_MAX) list = list.slice(0, FC_NAV_RECENT_MAX);
        localStorage.setItem(FC_NAV_RECENT_KEY, JSON.stringify(list));
    } catch { /* non-fatal */ }
};

/**
 * Removes a specific page from the recent-pages list.
 * @param {string} href
 */
window.portalNavRemovePage = function (href) {
    try {
        const raw = localStorage.getItem(FC_NAV_RECENT_KEY);
        let list = raw ? JSON.parse(raw) : [];
        list = list.filter(p => p.href !== href);
        localStorage.setItem(FC_NAV_RECENT_KEY, JSON.stringify(list));
    } catch { /* non-fatal */ }
};

// ── Command Palette ───────────────────────────────────────────────────────────

window.portalCommandPalette = (function () {
    let _ref = null;

    function isEditing() {
        const el = document.activeElement;
        return el && (
            el.tagName === 'INPUT' ||
            el.tagName === 'TEXTAREA' ||
            el.tagName === 'SELECT' ||
            el.isContentEditable
        );
    }

    function onKeyDown(e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            e.stopPropagation();
            _ref?.invokeMethodAsync('OpenPalette').catch(() => {});
        }
    }

    return {
        init(dotNetRef) {
            _ref = dotNetRef;
            document.addEventListener('keydown', onKeyDown, true);
        },

        dispose() {
            document.removeEventListener('keydown', onKeyDown, true);
            _ref = null;
        },

        /** Focus the search input and install Tab-prevention listener */
        focusInput(id) {
            requestAnimationFrame(() => {
                const el = document.getElementById(id);
                if (!el) return;
                el.focus();
                el.select();
                // Prevent Tab from moving focus out of the palette dialog
                if (!el._cmdTabGuard) {
                    el._cmdTabGuard = (e) => {
                        if (e.key === 'Tab') e.preventDefault();
                    };
                    el.addEventListener('keydown', el._cmdTabGuard, { capture: true });
                }
            });
        },

        /** Scroll a result item into view without disrupting scroll position */
        scrollItemIntoView(id) {
            const el = document.getElementById(id);
            if (el) el.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    };
})();

// ── Shortcuts Overlay ─────────────────────────────────────────────────────────

window.portalShortcutsOverlay = (function () {
    let _ref = null;

    function isEditing() {
        const el = document.activeElement;
        return el && (
            el.tagName === 'INPUT' ||
            el.tagName === 'TEXTAREA' ||
            el.tagName === 'SELECT' ||
            el.isContentEditable
        );
    }

    function onKeyDown(e) {
        if (e.key === '?' && !isEditing() && !e.ctrlKey && !e.metaKey && !e.altKey) {
            e.preventDefault();
            _ref?.invokeMethodAsync('OpenOverlay').catch(() => {});
        }
    }

    return {
        init(dotNetRef) {
            _ref = dotNetRef;
            document.addEventListener('keydown', onKeyDown);
        },

        dispose() {
            document.removeEventListener('keydown', onKeyDown);
            _ref = null;
        }
    };
})();

// ── Calendar Reminder Storage ────────────────────────────────
window.portalCalendar = {
    loadReminders: function () {
        return localStorage.getItem('fc_portal_calendar_reminders') || null;
    },
    saveReminders: function (json) {
        localStorage.setItem('fc_portal_calendar_reminders', json);
    }
};

// ── Onboarding Wizard ─────────────────────────────────────────────
window.portalWizard = (() => {
    // ── Confetti particle system ─────────────────────────────────
    function launchConfetti() {
        const canvas = document.getElementById('wiz-confetti');
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        canvas.width  = window.innerWidth;
        canvas.height = window.innerHeight;

        const COLORS = [
            '#006B3F', '#22c55e', '#C8A415', '#f59e0b',
            '#3b82f6', '#a855f7', '#ef4444', '#06b6d4',
        ];
        const COUNT  = 160;
        const GRAVITY = 0.35;
        const DRAG    = 0.97;

        const particles = Array.from({ length: COUNT }, () => ({
            x:    Math.random() * canvas.width,
            y:    -Math.random() * canvas.height * 0.4,
            vx:   (Math.random() - 0.5) * 6,
            vy:   Math.random() * 4 + 2,
            w:    Math.random() * 9 + 5,
            h:    Math.random() * 5 + 3,
            rot:  Math.random() * Math.PI * 2,
            rotV: (Math.random() - 0.5) * 0.25,
            col:  COLORS[Math.floor(Math.random() * COLORS.length)],
            op:   1,
        }));

        let frame;
        let elapsed = 0;

        function tick() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            elapsed++;

            let alive = 0;
            for (const p of particles) {
                p.vy  += GRAVITY;
                p.vx  *= DRAG;
                p.vy  *= DRAG;
                p.x   += p.vx;
                p.y   += p.vy;
                p.rot += p.rotV;
                if (elapsed > 100) p.op = Math.max(0, p.op - 0.012);

                if (p.y < canvas.height + 20 && p.op > 0) {
                    alive++;
                    ctx.save();
                    ctx.translate(p.x, p.y);
                    ctx.rotate(p.rot);
                    ctx.globalAlpha = p.op;
                    ctx.fillStyle   = p.col;
                    ctx.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
                    ctx.restore();
                }
            }

            if (alive > 0) {
                frame = requestAnimationFrame(tick);
            } else {
                ctx.clearRect(0, 0, canvas.width, canvas.height);
            }
        }

        frame = requestAnimationFrame(tick);

        // Auto-cancel after 8s
        setTimeout(() => {
            cancelAnimationFrame(frame);
            ctx.clearRect(0, 0, canvas.width, canvas.height);
        }, 8000);
    }

    // ── Video helper ─────────────────────────────────────────────
    function openVideo() {
        // Open a getting-started video in a new tab (URL configurable)
        const videoUrl = document.documentElement.dataset.wizVideoUrl
            || 'https://www.youtube.com/results?search_query=FCEngine+getting+started';
        window.open(videoUrl, '_blank', 'noopener,noreferrer');
    }

    return { launchConfetti, openVideo };
})();
