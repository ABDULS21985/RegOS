// ── Migration Experience JS ──
(function () {
    'use strict';

    window.portalMigration = {

        // ── Visual Bezier Mapper ──────────────────────────────────────
        _selectedSourceIdx: null,
        _dotNetRef: null,
        _mapperInitialised: false,

        initMapper: function (dotNetRef) {
            window.portalMigration._dotNetRef = dotNetRef;
            window.portalMigration._mapperInitialised = true;
            window.portalMigration.redrawCurves();
        },

        disposeMapper: function () {
            window.portalMigration._dotNetRef = null;
            window.portalMigration._selectedSourceIdx = null;
            window.portalMigration._mapperInitialised = false;
        },

        selectSource: function (idx) {
            window.portalMigration._selectedSourceIdx = idx;
            document.querySelectorAll('.portal-mig-field-row--selected').forEach(function (el) {
                el.classList.remove('portal-mig-field-row--selected');
            });
            var el = document.querySelector('[data-src-idx="' + idx + '"]');
            if (el) el.classList.add('portal-mig-field-row--selected');
        },

        acceptTarget: function (fieldName) {
            var srcIdx = window.portalMigration._selectedSourceIdx;
            if (srcIdx === null || srcIdx === undefined) return;
            var ref = window.portalMigration._dotNetRef;
            if (ref) {
                ref.invokeMethodAsync('AcceptVisualMapping', srcIdx, fieldName).then(function () {
                    window.portalMigration._selectedSourceIdx = null;
                    document.querySelectorAll('.portal-mig-field-row--selected').forEach(function (el) {
                        el.classList.remove('portal-mig-field-row--selected');
                    });
                    window.portalMigration.redrawCurves();
                });
            }
        },

        redrawCurves: function () {
            var svg = document.getElementById('mig-mapper-svg');
            if (!svg) return;

            var svgRect = svg.getBoundingClientRect();
            if (svgRect.width === 0) return;

            svg.setAttribute('width', svgRect.width);
            svg.setAttribute('height', Math.max(svgRect.height, 400));

            // Remove old paths
            while (svg.firstChild) svg.removeChild(svg.firstChild);

            var defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
            var grad = document.createElementNS('http://www.w3.org/2000/svg', 'linearGradient');
            grad.setAttribute('id', 'mig-curve-grad');
            grad.setAttribute('x1', '0%'); grad.setAttribute('y1', '0%');
            grad.setAttribute('x2', '100%'); grad.setAttribute('y2', '0%');
            var stop1 = document.createElementNS('http://www.w3.org/2000/svg', 'stop');
            stop1.setAttribute('offset', '0%'); stop1.setAttribute('stop-color', '#006B3F'); stop1.setAttribute('stop-opacity', '0.9');
            var stop2 = document.createElementNS('http://www.w3.org/2000/svg', 'stop');
            stop2.setAttribute('offset', '100%'); stop2.setAttribute('stop-color', '#C8A415'); stop2.setAttribute('stop-opacity', '0.9');
            grad.appendChild(stop1); grad.appendChild(stop2);
            defs.appendChild(grad);
            svg.appendChild(defs);

            var sources = document.querySelectorAll('.portal-mig-field-row[data-src-idx]');
            sources.forEach(function (srcEl) {
                var srcIdx = srcEl.getAttribute('data-src-idx');
                var tgtName = srcEl.getAttribute('data-mapped-to');
                if (!tgtName) return;

                var tgtEl = document.querySelector('.portal-mig-field-row[data-tgt-name="' + tgtName + '"]');
                if (!tgtEl) return;

                var srcDot = srcEl.querySelector('.portal-mig-dot--right');
                var tgtDot = tgtEl.querySelector('.portal-mig-dot--left');
                if (!srcDot || !tgtDot) return;

                var srcRect = srcDot.getBoundingClientRect();
                var tgtRect = tgtDot.getBoundingClientRect();

                var x1 = srcRect.left + srcRect.width / 2 - svgRect.left;
                var y1 = srcRect.top + srcRect.height / 2 - svgRect.top;
                var x2 = tgtRect.left + tgtRect.width / 2 - svgRect.left;
                var y2 = tgtRect.top + tgtRect.height / 2 - svgRect.top;

                var cp1x = x1 + (x2 - x1) * 0.5;
                var cp2x = x1 + (x2 - x1) * 0.5;

                var path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                path.setAttribute('d', 'M ' + x1 + ' ' + y1 + ' C ' + cp1x + ' ' + y1 + ', ' + cp2x + ' ' + y2 + ', ' + x2 + ' ' + y2);
                path.setAttribute('fill', 'none');
                path.setAttribute('stroke', 'url(#mig-curve-grad)');
                path.setAttribute('stroke-width', '2.5');
                path.setAttribute('stroke-linecap', 'round');
                path.setAttribute('opacity', '0.85');
                svg.appendChild(path);

                // Animated dot on path
                var circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                circle.setAttribute('r', '4');
                circle.setAttribute('fill', '#006B3F');
                circle.setAttribute('opacity', '0.7');
                var anim = document.createElementNS('http://www.w3.org/2000/svg', 'animateMotion');
                anim.setAttribute('dur', '2s');
                anim.setAttribute('repeatCount', 'indefinite');
                anim.setAttribute('path', 'M ' + x1 + ' ' + y1 + ' C ' + cp1x + ' ' + y1 + ', ' + cp2x + ' ' + y2 + ', ' + x2 + ' ' + y2);
                circle.appendChild(anim);
                svg.appendChild(circle);
            });
        },

        // ── Progress Polling ──────────────────────────────────────────
        _pollTimers: {},

        startProgressPoll: function (dotNetRef, jobId, intervalMs) {
            window.portalMigration.stopProgressPoll(jobId);
            window.portalMigration._pollTimers[jobId] = setInterval(function () {
                dotNetRef.invokeMethodAsync('OnProgressPoll', jobId);
            }, intervalMs || 2000);
        },

        stopProgressPoll: function (jobId) {
            var timer = window.portalMigration._pollTimers[jobId];
            if (timer) { clearInterval(timer); delete window.portalMigration._pollTimers[jobId]; }
        },

        stopAllPolls: function () {
            Object.keys(window.portalMigration._pollTimers).forEach(function (k) {
                clearInterval(window.portalMigration._pollTimers[k]);
            });
            window.portalMigration._pollTimers = {};
        },

        // ── Commit Progress Overlay ───────────────────────────────────
        showCommitProgress: function (totalRows) {
            var overlay = document.getElementById('mig-commit-overlay');
            if (!overlay) return;
            overlay.style.display = 'flex';
            var fill = overlay.querySelector('.portal-mig-commit-fill');
            var label = overlay.querySelector('.portal-mig-commit-label');
            var i = 0;
            var step = Math.max(1, Math.floor(totalRows / 60));
            var timer = setInterval(function () {
                i = Math.min(i + step, totalRows);
                var pct = totalRows > 0 ? (i / totalRows * 100) : 100;
                if (fill) fill.style.width = pct + '%';
                if (label) label.textContent = i.toLocaleString() + ' / ' + totalRows.toLocaleString() + ' rows processed';
                if (i >= totalRows) clearInterval(timer);
            }, 50);
            return timer;
        },

        hideCommitProgress: function () {
            var overlay = document.getElementById('mig-commit-overlay');
            if (overlay) overlay.style.display = 'none';
        },

        // ── Resize observer for SVG ───────────────────────────────────
        _resizeObserver: null,

        observeMapperResize: function () {
            var svg = document.getElementById('mig-mapper-svg');
            if (!svg) return;
            if (window.portalMigration._resizeObserver) {
                window.portalMigration._resizeObserver.disconnect();
            }
            window.portalMigration._resizeObserver = new ResizeObserver(function () {
                window.portalMigration.redrawCurves();
            });
            window.portalMigration._resizeObserver.observe(svg.parentElement || svg);
        }
    };
})();
