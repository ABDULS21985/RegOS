/**
 * data-table.js — JS interop for the DataTable<TItem> Blazor component.
 * Handles: column resize (with width callback), keyboard navigation, file download,
 * localStorage preference persistence, column width read/apply.
 * Pattern mirrors command-palette.js (window.namespace.init / dispose).
 */
window.dataTable = (function () {
    const instances = {};

    function getTable(tableId) {
        return document.getElementById(tableId);
    }

    return {
        /**
         * Initialise keyboard navigation for a DataTable instance.
         * @param {string} tableId   - id of the <table> element (not the wrapper)
         * @param {object} dotnetRef - DotNetObjectReference for JSInvokable callbacks
         */
        init: function (tableId, dotnetRef) {
            if (instances[tableId]) this.dispose(tableId);

            const keyHandler = function (e) {
                const table = getTable(tableId);
                if (!table) return;

                const active = document.activeElement;
                const tbody = table.querySelector('tbody');
                if (!tbody || !tbody.contains(active)) return;

                if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('HandleKeyNav', e.key).catch(() => {});
                } else if (e.key === 'Enter') {
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('HandleKeyAction', 'Enter').catch(() => {});
                } else if (e.key === ' ') {
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('HandleKeyAction', ' ').catch(() => {});
                }
            };

            document.addEventListener('keydown', keyHandler, { passive: false });
            instances[tableId] = { dotnetRef, keyHandler };
        },

        /**
         * Move DOM focus to a specific row and scroll it into view.
         * @param {string} tableId
         * @param {number} rowIndex - 0-based index within tbody
         */
        focusRow: function (tableId, rowIndex) {
            const table = getTable(tableId);
            if (!table) return;
            const rows = table.querySelectorAll('tbody tr');
            const row = rows[rowIndex];
            if (row) {
                row.focus({ preventScroll: true });
                row.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            }
        },

        /**
         * Start a column resize drag. Calls OnColumnResized on the .NET side when done.
         * @param {string} tableId
         * @param {number} columnIndex - 0-based <th> index
         * @param {number} startX      - MouseEvent.clientX at mousedown
         */
        initResize: function (tableId, columnIndex, startX) {
            const table = getTable(tableId);
            if (!table) return;
            const th = table.querySelectorAll('thead th')[columnIndex];
            if (!th) return;

            const startWidth = th.getBoundingClientRect().width;

            const onMouseMove = function (e) {
                const newWidth = Math.max(48, startWidth + (e.clientX - startX));
                th.style.width = newWidth + 'px';
                th.style.minWidth = newWidth + 'px';
            };

            const onMouseUp = function () {
                document.removeEventListener('mousemove', onMouseMove);
                document.removeEventListener('mouseup', onMouseUp);
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
                const handle = th.querySelector('.fc-dt-resize-handle');
                if (handle) handle.classList.remove('fc-dt-resizing');

                // Notify .NET of the final width so it can be persisted
                const finalWidth = Math.round(th.getBoundingClientRect().width);
                const colId = th.dataset.colId;
                const inst = instances[tableId];
                if (colId && inst && inst.dotnetRef) {
                    inst.dotnetRef.invokeMethodAsync('OnColumnResized', colId, finalWidth).catch(() => {});
                }
            };

            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
            const handle = th.querySelector('.fc-dt-resize-handle');
            if (handle) handle.classList.add('fc-dt-resizing');

            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseUp);
        },

        /**
         * Read current column widths from the DOM.
         * Returns an object keyed by data-col-id attribute value.
         * @param {string} tableId
         * @returns {Object.<string,number>}
         */
        getColumnWidths: function (tableId) {
            const table = getTable(tableId);
            if (!table) return {};
            const widths = {};
            table.querySelectorAll('thead th[data-col-id]').forEach(th => {
                const id = th.dataset.colId;
                if (id) widths[id] = Math.round(th.getBoundingClientRect().width);
            });
            return widths;
        },

        /**
         * Apply stored column widths to the table header cells.
         * @param {string} tableId
         * @param {Object.<string,number>} widths - column id → width in px
         */
        applyColumnWidths: function (tableId, widths) {
            const table = getTable(tableId);
            if (!table) return;
            table.querySelectorAll('thead th[data-col-id]').forEach(th => {
                const id = th.dataset.colId;
                if (id && widths[id]) {
                    th.style.width = widths[id] + 'px';
                    th.style.minWidth = widths[id] + 'px';
                }
            });
        },

        /**
         * Save preferences JSON to localStorage.
         * @param {string} key
         * @param {string} prefsJson
         */
        savePrefs: function (key, prefsJson) {
            try { localStorage.setItem(key, prefsJson); } catch (e) {}
        },

        /**
         * Load preferences JSON from localStorage.
         * @param {string} key
         * @returns {string|null}
         */
        loadPrefs: function (key) {
            try { return localStorage.getItem(key); } catch (e) { return null; }
        },

        /**
         * Trigger a browser file download from base64-encoded content.
         * @param {string} fileName
         * @param {string} contentType - MIME type
         * @param {string} base64Content
         */
        downloadFile: function (fileName, contentType, base64Content) {
            try {
                const bytes = Uint8Array.from(atob(base64Content), c => c.charCodeAt(0));
                const blob = new Blob([bytes], { type: contentType });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = fileName;
                a.style.display = 'none';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                setTimeout(() => URL.revokeObjectURL(url), 10000);
            } catch (err) {
                console.error('[dataTable] downloadFile error:', err);
            }
        },

        /**
         * Clean up listeners and references for a DataTable instance.
         * @param {string} tableId
         */
        dispose: function (tableId) {
            const inst = instances[tableId];
            if (!inst) return;
            if (inst.keyHandler) {
                document.removeEventListener('keydown', inst.keyHandler);
            }
            inst.dotnetRef = null;
            delete instances[tableId];
        }
    };
})();
