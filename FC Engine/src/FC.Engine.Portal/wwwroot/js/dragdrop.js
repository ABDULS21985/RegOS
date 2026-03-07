(function () {
    'use strict';

    const prefersReducedMotion = () =>
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ── ARIA live region for drag announcements ────────────────────────
    let _announcer = null;
    function announce(msg) {
        if (!_announcer) {
            _announcer = document.createElement('div');
            _announcer.setAttribute('aria-live', 'assertive');
            _announcer.setAttribute('aria-atomic', 'true');
            _announcer.className = 'portal-sr-only';
            _announcer.style.cssText = 'position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;';
            document.body.appendChild(_announcer);
        }
        _announcer.textContent = '';
        requestAnimationFrame(() => { _announcer.textContent = msg; });
    }

    // ── Registry of active instances (for cleanup) ────────────────────
    const registry = {};

    // ══════════════════════════════════════════════════════════════════
    // FCDragDrop.sortable — reorder items within a container
    // ══════════════════════════════════════════════════════════════════
    function sortable(containerSelector, itemSelector, options) {
        const opts = Object.assign({
            storageKey: null,
            onReorder: null,   // function(fromIdx, toIdx, dotNetRef)
            dotNetRef: null,
            handle: null,      // selector for drag handle, null = whole item
            ghostClass: 'portal-sortable-ghost',
            placeholderClass: 'portal-sortable-placeholder',
            draggingClass: 'portal-sortable-dragging',
            direction: 'vertical'
        }, options || {});

        const containers = document.querySelectorAll(containerSelector);
        if (!containers.length) return;

        containers.forEach(function (container) {
            const id = containerSelector + '||' + (opts.storageKey || '');
            if (registry[id]) { registry[id].destroy(); }

            const items = () => Array.from(container.querySelectorAll(itemSelector));
            let dragSrc = null;
            let placeholder = null;
            let kbActive = null;    // item in keyboard pick-up mode

            // ── Drag handle elements ──
            function getHandle(item) {
                return opts.handle ? item.querySelector(opts.handle) : item;
            }

            // ── Setup ──
            function setup() {
                items().forEach(wireItem);
                container.addEventListener('dragover', onContainerDragOver);
                container.addEventListener('drop', onContainerDrop);
            }

            function wireItem(item) {
                item.setAttribute('draggable', 'true');
                item.setAttribute('aria-roledescription', 'sortable item');
                item.addEventListener('dragstart', onDragStart);
                item.addEventListener('dragend', onDragEnd);
                item.addEventListener('dragenter', onDragEnter);
                item.addEventListener('dragleave', onDragLeave);

                // Keyboard support
                const handle = getHandle(item);
                handle.setAttribute('tabindex', '0');
                handle.setAttribute('role', 'button');
                handle.setAttribute('aria-label', 'Drag to reorder');
                handle.addEventListener('keydown', onKeyDown.bind(null, item));
            }

            // ── Mouse / touch drag ──
            function onDragStart(e) {
                dragSrc = this;
                this.classList.add(opts.draggingClass);
                this.setAttribute('aria-grabbed', 'true');
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', '');
                if (prefersReducedMotion()) return;
                // show ghost (transparent clone follows cursor natively)
                this.style.opacity = '0.5';
                announce('Picked up. Use arrow keys or drag to reorder, then release to drop.');
            }

            function onDragEnd() {
                if (dragSrc) {
                    dragSrc.classList.remove(opts.draggingClass);
                    dragSrc.setAttribute('aria-grabbed', 'false');
                    dragSrc.style.opacity = '';
                }
                removePlaceholder();
                dragSrc = null;
            }

            function onDragEnter(e) {
                e.preventDefault();
                if (!dragSrc || dragSrc === this) return;
                insertPlaceholderBefore(this);
            }

            function onDragLeave() { /* placeholder stays until next enter */ }

            function onContainerDragOver(e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
            }

            function onContainerDrop(e) {
                e.preventDefault();
                if (!dragSrc || !placeholder || !placeholder.parentNode) return;
                placeholder.parentNode.insertBefore(dragSrc, placeholder);
                removePlaceholder();
                persistAndNotify();
            }

            // ── Placeholder ──
            function insertPlaceholderBefore(target) {
                if (!placeholder) {
                    placeholder = document.createElement('div');
                    placeholder.className = opts.placeholderClass;
                    placeholder.setAttribute('aria-hidden', 'true');
                    // match height of dragged item
                    if (dragSrc) {
                        placeholder.style.height = dragSrc.offsetHeight + 'px';
                        placeholder.style.width = dragSrc.offsetWidth + 'px';
                    }
                }
                target.parentNode && target.parentNode.insertBefore(placeholder, target);
            }

            function removePlaceholder() {
                if (placeholder && placeholder.parentNode) {
                    placeholder.parentNode.removeChild(placeholder);
                }
                placeholder = null;
            }

            // ── Keyboard drag ──
            function onKeyDown(item, e) {
                const currentItems = items();
                const idx = currentItems.indexOf(item);

                if (e.key === ' ' || e.key === 'Enter') {
                    e.preventDefault();
                    if (kbActive === item) {
                        // Drop
                        item.classList.remove('portal-sortable-kbd-active');
                        item.setAttribute('aria-grabbed', 'false');
                        kbActive = null;
                        persistAndNotify();
                        announce('Dropped.');
                    } else {
                        // Pick up
                        if (kbActive) {
                            kbActive.classList.remove('portal-sortable-kbd-active');
                            kbActive.setAttribute('aria-grabbed', 'false');
                        }
                        kbActive = item;
                        item.classList.add('portal-sortable-kbd-active');
                        item.setAttribute('aria-grabbed', 'true');
                        announce('Picked up item ' + (idx + 1) + ' of ' + currentItems.length + '. Press arrow keys to move, Space or Enter to drop, Escape to cancel.');
                    }
                } else if (e.key === 'Escape') {
                    e.preventDefault();
                    if (kbActive) {
                        kbActive.classList.remove('portal-sortable-kbd-active');
                        kbActive.setAttribute('aria-grabbed', 'false');
                        kbActive = null;
                        announce('Cancelled.');
                    }
                } else if (kbActive === item) {
                    if ((e.key === 'ArrowDown' || e.key === 'ArrowRight') && idx < currentItems.length - 1) {
                        e.preventDefault();
                        swapItems(item, currentItems[idx + 1]);
                        item.querySelector('[tabindex="0"]')?.focus();
                        announce('Moved to position ' + (idx + 2) + ' of ' + currentItems.length);
                    } else if ((e.key === 'ArrowUp' || e.key === 'ArrowLeft') && idx > 0) {
                        e.preventDefault();
                        swapItems(currentItems[idx - 1], item);
                        item.querySelector('[tabindex="0"]')?.focus();
                        announce('Moved to position ' + idx + ' of ' + currentItems.length);
                    }
                }
            }

            function swapItems(before, after) {
                const parent = before.parentNode;
                const nextSibling = after.nextSibling;
                parent.insertBefore(after, before);
                if (nextSibling) {
                    parent.insertBefore(before, nextSibling);
                } else {
                    parent.appendChild(before);
                }
            }

            // ── Persist + notify ──
            function persistAndNotify() {
                const currentItems = items();
                const order = currentItems.map(function (el) {
                    return el.dataset.widgetId || el.dataset.itemId || el.id || '';
                });
                const fromIdx = dragSrc ? currentItems.indexOf(dragSrc) : -1;

                if (opts.storageKey) {
                    try { localStorage.setItem(opts.storageKey, JSON.stringify(order)); } catch (ex) { /* ignore */ }
                }
                if (opts.dotNetRef && fromIdx !== -1) {
                    const toIdx = order.indexOf(order[fromIdx]);
                    opts.dotNetRef.invokeMethodAsync('OnWidgetReordered', fromIdx, toIdx).catch(function () {});
                }
                if (typeof opts.onReorder === 'function') {
                    opts.onReorder(order);
                }
            }

            function destroy() {
                items().forEach(function (item) {
                    item.removeAttribute('draggable');
                    item.removeAttribute('aria-roledescription');
                    item.removeEventListener('dragstart', onDragStart);
                    item.removeEventListener('dragend', onDragEnd);
                    item.removeEventListener('dragenter', onDragEnter);
                    item.removeEventListener('dragleave', onDragLeave);
                    const handle = getHandle(item);
                    handle.removeAttribute('tabindex');
                    handle.removeAttribute('role');
                    handle.removeAttribute('aria-label');
                });
                container.removeEventListener('dragover', onContainerDragOver);
                container.removeEventListener('drop', onContainerDrop);
                removePlaceholder();
            }

            setup();
            registry[id] = { destroy };
        });
    }

    // ══════════════════════════════════════════════════════════════════
    // FCDragDrop.kanban — cross-column card drag with ghost & drop targets
    // ══════════════════════════════════════════════════════════════════
    function kanban(boardSelector, columnSelector, cardSelector, options) {
        var opts = Object.assign({
            storageKey: null,
            dotNetRef: null,       // DotNetObjectReference for OnCardMoved callbacks
            wipLimits: {},         // { columnId: maxCount }
            draggingClass: 'portal-kanban-card-dragging',
            placeholderClass: 'portal-kanban-placeholder',
            dropTargetClass: 'portal-kanban-column--drop-target',
            wipExceededClass: 'portal-kanban-column--wip-exceeded'
        }, options || {});

        var board = document.querySelector(boardSelector);
        if (!board) return;

        var id = 'kanban||' + boardSelector;
        if (registry[id]) { registry[id].destroy(); }

        var dragSrc = null;
        var srcColumn = null;
        var placeholder = null;
        var ghost = null;
        var cleanupFns = [];

        // ── Helpers ──────────────────────────────────────────────────
        function getColumns() { return Array.from(board.querySelectorAll(columnSelector)); }
        function getColId(col) { return col.dataset.columnId || col.id || ''; }

        function updateBadge(col) {
            var badge = col.querySelector('.portal-kanban-col-badge');
            if (!badge) return;
            var count = Array.from(col.querySelectorAll(cardSelector))
                .filter(function (c) { return !c.classList.contains(opts.draggingClass); }).length;
            badge.textContent = count;

            var colId = getColId(col);
            var limit = (opts.wipLimits || {})[colId];
            var wipLabel = col.querySelector('.portal-kanban-col-wip-label');
            if (limit != null && count > limit) {
                col.classList.add(opts.wipExceededClass);
                if (wipLabel) {
                    wipLabel.textContent = 'Limit reached (' + count + '/' + limit + ')';
                    wipLabel.hidden = false;
                }
            } else {
                col.classList.remove(opts.wipExceededClass);
                if (wipLabel) wipLabel.hidden = true;
            }
        }

        function createGhost(card) {
            var g = card.cloneNode(true);
            g.style.cssText = [
                'position:fixed',
                'top:-2000px',
                'left:-2000px',
                'width:' + card.offsetWidth + 'px',
                'pointer-events:none',
                'opacity:0.9',
                'transform:rotate(3deg) scale(1.02)',
                'box-shadow:0 16px 40px rgba(0,0,0,0.22)',
                'border-radius:6px',
                'z-index:9999',
                'background:#fff'
            ].join(';');
            document.body.appendChild(g);
            return g;
        }

        function insertPlaceholderBefore(target) {
            if (!placeholder) {
                placeholder = document.createElement('div');
                placeholder.className = opts.placeholderClass;
                placeholder.setAttribute('aria-hidden', 'true');
                if (dragSrc) {
                    placeholder.style.height = dragSrc.offsetHeight + 'px';
                }
            }
            if (target.parentNode) {
                target.parentNode.insertBefore(placeholder, target);
            }
        }

        function removePlaceholder() {
            if (placeholder && placeholder.parentNode) {
                placeholder.parentNode.removeChild(placeholder);
            }
            placeholder = null;
        }

        function clearDropTargets() {
            getColumns().forEach(function (c) { c.classList.remove(opts.dropTargetClass); });
        }

        // ── Wire a single card ────────────────────────────────────────
        function wireCard(card) {
            if (card._fcKanbanWired) return;
            card._fcKanbanWired = true;
            card.setAttribute('draggable', 'true');
            card.setAttribute('aria-roledescription', 'kanban card');

            function onDragStart(e) {
                dragSrc = card;
                srcColumn = card.closest(columnSelector);
                card.classList.add(opts.draggingClass);
                card.setAttribute('aria-grabbed', 'true');
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', card.dataset.cardId || '');
                if (!prefersReducedMotion()) {
                    ghost = createGhost(card);
                    try { e.dataTransfer.setDragImage(ghost, e.offsetX + 8, e.offsetY + 8); } catch (ex) {}
                    setTimeout(function () {
                        if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
                        ghost = null;
                    }, 100);
                }
                card.style.opacity = '0.4';
                announce('Picked up card. Drag to a column to move it, or press K to use keyboard menu.');
            }

            function onDragEnd() {
                card.classList.remove(opts.draggingClass);
                card.setAttribute('aria-grabbed', 'false');
                card.style.opacity = '';
                if (ghost && ghost.parentNode) { ghost.parentNode.removeChild(ghost); ghost = null; }
                removePlaceholder();
                clearDropTargets();
                dragSrc = null;
                srcColumn = null;
            }

            function onDragEnter(e) {
                e.preventDefault();
                if (!dragSrc || dragSrc === card) return;
                insertPlaceholderBefore(card);
            }

            function onKeyDown(e) {
                if (e.key === 'k' || e.key === 'K') {
                    e.preventDefault();
                    e.stopPropagation();
                    showMoveMenu(card);
                }
            }

            card.addEventListener('dragstart', onDragStart);
            card.addEventListener('dragend', onDragEnd);
            card.addEventListener('dragenter', onDragEnter);
            card.addEventListener('keydown', onKeyDown);

            cleanupFns.push(function () {
                card.removeAttribute('draggable');
                card.removeEventListener('dragstart', onDragStart);
                card.removeEventListener('dragend', onDragEnd);
                card.removeEventListener('dragenter', onDragEnter);
                card.removeEventListener('keydown', onKeyDown);
                delete card._fcKanbanWired;
            });
        }

        // ── Wire a column (drop target + existing cards) ──────────────
        function wireColumn(col) {
            col.querySelectorAll(cardSelector).forEach(wireCard);

            function onDragOver(e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
            }

            function onDragEnter(e) {
                e.preventDefault();
                if (!dragSrc) return;
                clearDropTargets();
                col.classList.add(opts.dropTargetClass);
            }

            function onDragLeave(e) {
                if (!col.contains(e.relatedTarget)) {
                    col.classList.remove(opts.dropTargetClass);
                }
            }

            function onDrop(e) {
                e.preventDefault();
                col.classList.remove(opts.dropTargetClass);
                if (!dragSrc) return;

                var fromColId = srcColumn ? getColId(srcColumn) : '';
                var toColId = getColId(col);

                // Insert card: before placeholder if present, else append
                if (placeholder && placeholder.parentNode === col) {
                    col.insertBefore(dragSrc, placeholder);
                } else {
                    col.appendChild(dragSrc);
                }
                removePlaceholder();

                // Re-wire card in case it's new to this column
                wireCard(dragSrc);

                // Update live count badges
                if (srcColumn) updateBadge(srcColumn);
                updateBadge(col);

                // Persist per-card column override
                if (opts.storageKey) {
                    try {
                        localStorage.setItem(
                            opts.storageKey + '-card-' + (dragSrc.dataset.cardId || ''),
                            toColId
                        );
                    } catch (ex) {}
                }

                // Notify .NET when column actually changed
                if (opts.dotNetRef && fromColId && fromColId !== toColId) {
                    var cardId = dragSrc.dataset.cardId || '';
                    opts.dotNetRef.invokeMethodAsync('OnCardMoved', cardId, fromColId, toColId)
                        .catch(function () {});
                }

                dragSrc = null;
                srcColumn = null;
                announce('Card dropped.');
            }

            col.addEventListener('dragover', onDragOver);
            col.addEventListener('dragenter', onDragEnter);
            col.addEventListener('dragleave', onDragLeave);
            col.addEventListener('drop', onDrop);

            cleanupFns.push(function () {
                col.removeEventListener('dragover', onDragOver);
                col.removeEventListener('dragenter', onDragEnter);
                col.removeEventListener('dragleave', onDragLeave);
                col.removeEventListener('drop', onDrop);
                col.classList.remove(opts.dropTargetClass, opts.wipExceededClass);
            });

            updateBadge(col);
        }

        // ── Keyboard move-to-column menu ──────────────────────────────
        function showMoveMenu(card) {
            var existing = document.querySelector('.portal-kanban-move-menu');
            if (existing) existing.remove();

            var currentCol = card.closest(columnSelector);
            var currentColId = getColId(currentCol);
            var otherCols = getColumns().filter(function (c) { return getColId(c) !== currentColId; });
            if (!otherCols.length) return;

            var menu = document.createElement('div');
            menu.className = 'portal-kanban-move-menu';
            menu.setAttribute('role', 'dialog');
            menu.setAttribute('aria-modal', 'true');
            menu.setAttribute('aria-label', 'Move card to column');

            var title = document.createElement('div');
            title.className = 'portal-kanban-move-menu-title';
            title.textContent = 'Move to column';
            menu.appendChild(title);

            otherCols.forEach(function (col) {
                var colId = getColId(col);
                var colTitleEl = col.querySelector('.portal-kanban-column-title');
                var colTitle = colTitleEl ? colTitleEl.textContent.trim() : colId;

                var btn = document.createElement('button');
                btn.className = 'portal-kanban-move-menu-item';
                btn.textContent = colTitle;
                btn.addEventListener('click', function () {
                    menu.remove();
                    var fromColId = getColId(currentCol);
                    col.appendChild(card);
                    wireCard(card);
                    updateBadge(currentCol);
                    updateBadge(col);
                    if (opts.dotNetRef && fromColId !== colId) {
                        opts.dotNetRef.invokeMethodAsync('OnCardMoved', card.dataset.cardId || '', fromColId, colId)
                            .catch(function () {});
                    }
                    card.focus();
                    announce('Card moved to ' + colTitle);
                });
                menu.appendChild(btn);
            });

            // Keyboard close
            menu.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') { menu.remove(); card.focus(); }
            });

            // Position near the card
            var rect = card.getBoundingClientRect();
            menu.style.cssText = 'position:fixed;top:' + (rect.bottom + 4) + 'px;left:' + rect.left + 'px;z-index:10000;';
            document.body.appendChild(menu);

            // Auto-close on outside click
            setTimeout(function () {
                document.addEventListener('click', function closeMenu(ev) {
                    if (!menu.contains(ev.target)) {
                        menu.remove();
                        document.removeEventListener('click', closeMenu);
                    }
                });
            }, 50);

            var firstBtn = menu.querySelector('.portal-kanban-move-menu-item');
            if (firstBtn) firstBtn.focus();
        }

        // ── Init all columns ──────────────────────────────────────────
        getColumns().forEach(wireColumn);

        function destroy() {
            cleanupFns.forEach(function (fn) { fn(); });
            removePlaceholder();
            if (ghost && ghost.parentNode) ghost.parentNode.removeChild(ghost);
            var menu = document.querySelector('.portal-kanban-move-menu');
            if (menu) menu.remove();
        }

        registry[id] = { destroy: destroy };
    }

    // ══════════════════════════════════════════════════════════════════
    // FCDragDrop.columnReorder — drag <th> to reorder table columns
    // ══════════════════════════════════════════════════════════════════
    function columnReorder(tableSelector, options) {
        const opts = Object.assign({
            storageKey: null,
            draggingClass: 'portal-th-dragging',
            dropIndicatorClass: 'portal-th-drop-indicator'
        }, options || {});

        const table = document.querySelector(tableSelector);
        if (!table) return;

        const id = 'colreorder||' + tableSelector;
        if (registry[id]) { registry[id].destroy(); }

        const headers = Array.from(table.querySelectorAll('thead tr th'));
        if (!headers.length) return;

        let dragIdx = null;
        let dropIdx = null;
        const cleanupFns = [];

        headers.forEach(function (th, idx) {
            th.setAttribute('draggable', 'true');
            th.classList.add('portal-th-draggable');
            th.setAttribute('aria-roledescription', 'sortable column header');

            const onDragStart = function (e) {
                dragIdx = idx;
                th.classList.add(opts.draggingClass);
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', String(idx));
                announce('Column ' + th.textContent.trim() + ' picked up. Drag to reorder.');
            };
            const onDragEnd = function () {
                th.classList.remove(opts.draggingClass);
                headers.forEach(function (h) { h.classList.remove(opts.dropIndicatorClass); });
                dragIdx = null;
                dropIdx = null;
            };
            const onDragEnter = function (e) {
                e.preventDefault();
                if (dragIdx === null || dragIdx === idx) return;
                headers.forEach(function (h) { h.classList.remove(opts.dropIndicatorClass); });
                th.classList.add(opts.dropIndicatorClass);
                dropIdx = idx;
            };
            const onDragOver = function (e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
            };
            const onDrop = function (e) {
                e.preventDefault();
                if (dragIdx === null || dragIdx === idx) return;
                reorderColumns(dragIdx, idx);
                headers.forEach(function (h) { h.classList.remove(opts.dropIndicatorClass); });
            };

            th.addEventListener('dragstart', onDragStart);
            th.addEventListener('dragend', onDragEnd);
            th.addEventListener('dragenter', onDragEnter);
            th.addEventListener('dragover', onDragOver);
            th.addEventListener('drop', onDrop);

            cleanupFns.push(function () {
                th.removeAttribute('draggable');
                th.classList.remove('portal-th-draggable');
                th.removeEventListener('dragstart', onDragStart);
                th.removeEventListener('dragend', onDragEnd);
                th.removeEventListener('dragenter', onDragEnter);
                th.removeEventListener('dragover', onDragOver);
                th.removeEventListener('drop', onDrop);
            });
        });

        function reorderColumns(from, to) {
            // Move TH
            const thead = table.querySelector('thead tr');
            const ths = Array.from(thead.querySelectorAll('th'));
            const movedTh = ths.splice(from, 1)[0];
            ths.splice(to, 0, movedTh);
            // Re-append in new order
            ths.forEach(function (th) { thead.appendChild(th); });

            // Move TDs in each body row
            table.querySelectorAll('tbody tr').forEach(function (row) {
                const tds = Array.from(row.querySelectorAll('td'));
                if (tds.length < ths.length) return;
                const movedTd = tds.splice(from, 1)[0];
                tds.splice(to, 0, movedTd);
                tds.forEach(function (td) { row.appendChild(td); });
            });

            // Persist
            if (opts.storageKey) {
                const order = Array.from(table.querySelectorAll('thead tr th'))
                    .map(function (th) { return th.dataset.colId || th.textContent.trim(); });
                try { localStorage.setItem(opts.storageKey, JSON.stringify(order)); } catch (ex) {}
            }
            announce('Column moved to position ' + (to + 1));
        }

        registry[id] = {
            destroy: function () { cleanupFns.forEach(function (fn) { fn(); }); }
        };
    }

    // ══════════════════════════════════════════════════════════════════
    // FCDragDrop.dropZone — enhanced file drop zone
    // ══════════════════════════════════════════════════════════════════
    function dropZone(selector, options) {
        const opts = Object.assign({
            accept: [],          // accepted MIME types, empty = all
            onDrop: null,        // function(files)
            activeClass: 'portal-drop-zone-active',
            pulseClass: 'portal-bulk-dropzone-pulse',
            invalidClass: 'portal-drop-zone-invalid'
        }, options || {});

        const zones = document.querySelectorAll(selector);
        if (!zones.length) return;

        zones.forEach(function (zone) {
            let dragEnterCount = 0;
            const id = 'dropzone||' + selector;

            function isValid(files) {
                if (!opts.accept.length) return true;
                return Array.from(files).every(function (f) {
                    return opts.accept.some(function (t) {
                        return t === f.type || (t.endsWith('/*') && f.type.startsWith(t.slice(0, -1)));
                    });
                });
            }

            function onDragEnter(e) {
                e.preventDefault();
                dragEnterCount++;
                if (dragEnterCount === 1) {
                    const files = e.dataTransfer && e.dataTransfer.items
                        ? Array.from(e.dataTransfer.items).filter(function (i) { return i.kind === 'file'; })
                        : [];
                    const valid = !files.length || !opts.accept.length ||
                        files.every(function (i) {
                            return opts.accept.some(function (t) {
                                return i.type === t || (t.endsWith('/*') && i.type.startsWith(t.slice(0, -1)));
                            });
                        });
                    zone.classList.remove(opts.invalidClass);
                    if (valid) {
                        zone.classList.add(opts.activeClass);
                        if (!prefersReducedMotion()) zone.classList.add(opts.pulseClass);
                    } else {
                        zone.classList.add(opts.invalidClass);
                    }
                    // Show count indicator
                    const counter = zone.querySelector('.portal-dropzone-count');
                    if (counter && files.length) {
                        counter.textContent = files.length + ' file' + (files.length !== 1 ? 's' : '');
                        counter.hidden = false;
                    }
                }
            }

            function onDragLeave(e) {
                dragEnterCount--;
                if (dragEnterCount <= 0) {
                    dragEnterCount = 0;
                    zone.classList.remove(opts.activeClass, opts.pulseClass, opts.invalidClass);
                    const counter = zone.querySelector('.portal-dropzone-count');
                    if (counter) counter.hidden = true;
                }
            }

            function onDragOver(e) { e.preventDefault(); }

            function onDrop(e) {
                e.preventDefault();
                dragEnterCount = 0;
                zone.classList.remove(opts.activeClass, opts.pulseClass, opts.invalidClass);
                const counter = zone.querySelector('.portal-dropzone-count');
                if (counter) counter.hidden = true;
                if (e.dataTransfer && e.dataTransfer.files.length && typeof opts.onDrop === 'function') {
                    opts.onDrop(e.dataTransfer.files);
                }
            }

            zone.addEventListener('dragenter', onDragEnter);
            zone.addEventListener('dragleave', onDragLeave);
            zone.addEventListener('dragover', onDragOver);
            zone.addEventListener('drop', onDrop);

            if (registry[id]) { registry[id].destroy(); }
            registry[id] = {
                destroy: function () {
                    zone.removeEventListener('dragenter', onDragEnter);
                    zone.removeEventListener('dragleave', onDragLeave);
                    zone.removeEventListener('dragover', onDragOver);
                    zone.removeEventListener('drop', onDrop);
                    zone.classList.remove(opts.activeClass, opts.pulseClass, opts.invalidClass);
                }
            };
        });
    }

    // ══════════════════════════════════════════════════════════════════
    // FCDragDrop.getSavedOrder — read persisted order from localStorage
    // ══════════════════════════════════════════════════════════════════
    function getSavedOrder(storageKey) {
        try { return localStorage.getItem(storageKey); } catch (ex) { return null; }
    }

    // ══════════════════════════════════════════════════════════════════
    // FCDragDrop.destroy — tear down all listeners for a container
    // ══════════════════════════════════════════════════════════════════
    function destroy(containerId) {
        Object.keys(registry).forEach(function (key) {
            if (key.includes(containerId)) {
                registry[key].destroy();
                delete registry[key];
            }
        });
    }

    // ── Backwards-compat wrapper for existing initDragDrop ────────────
    // (charts.js has its own initDragDrop — keep that working)
    window.FCDragDrop = { sortable, kanban, columnReorder, dropZone, getSavedOrder, destroy };

})();

// ══════════════════════════════════════════════════════════════════════════════
// FCDragDrop.initTableRowSort — HTML5 drag-and-drop row reorder for form tables
//
// Uses event delegation on the tbody so Blazor re-renders don't break listeners.
// Drag handles must have class `portal-form-drag-handle` and `draggable="true"`.
// Calls [JSInvokable] OnRowReordered(fromIdx, toIdx) on the .NET component.
// ══════════════════════════════════════════════════════════════════════════════
(function () {
    'use strict';

    var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    window.FCDragDrop = window.FCDragDrop || {};

    window.FCDragDrop.initTableRowSort = function (tbodyId, dotNetRef) {
        var tbody = document.getElementById(tbodyId);
        if (!tbody || tbody._fcRowSortInit) return;
        tbody._fcRowSortInit = true;

        var dragSrcRow = null;
        var overRow = null;

        function getRows() {
            return Array.from(tbody.querySelectorAll('tr'));
        }

        function cleanup() {
            if (dragSrcRow) dragSrcRow.classList.remove('portal-sortable-dragging');
            if (overRow) overRow.classList.remove('portal-sortable-placeholder');
            dragSrcRow = null;
            overRow = null;
        }

        tbody.addEventListener('dragstart', function (e) {
            var handle = e.target.closest('.portal-drag-handle');
            if (!handle) return;
            dragSrcRow = e.target.closest('tr');
            if (!dragSrcRow) return;
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', '');
            if (!prefersReducedMotion) dragSrcRow.classList.add('portal-sortable-dragging');
        });

        tbody.addEventListener('dragover', function (e) {
            e.preventDefault();
            if (!dragSrcRow) return;
            var tr = e.target.closest('tr');
            if (!tr || tr === dragSrcRow) return;
            if (overRow && overRow !== tr) overRow.classList.remove('portal-sortable-placeholder');
            overRow = tr;
            tr.classList.add('portal-sortable-placeholder');
        });

        tbody.addEventListener('dragleave', function (e) {
            if (!e.relatedTarget || !tbody.contains(e.relatedTarget)) {
                if (overRow) overRow.classList.remove('portal-sortable-placeholder');
                overRow = null;
            }
        });

        tbody.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (!dragSrcRow) { cleanup(); return; }

            var dropRow = e.target.closest('tr');
            if (!dropRow || dropRow === dragSrcRow) { cleanup(); return; }

            var rows = getRows();
            var fromIdx = rows.indexOf(dragSrcRow);
            var toIdx = rows.indexOf(dropRow);
            cleanup();

            if (fromIdx !== -1 && toIdx !== -1 && fromIdx !== toIdx) {
                dotNetRef.invokeMethodAsync('OnRowReordered', fromIdx, toIdx);
            }
        });

        tbody.addEventListener('dragend', function () {
            cleanup();
        });
    };

}());
