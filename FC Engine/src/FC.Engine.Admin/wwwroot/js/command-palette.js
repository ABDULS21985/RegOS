window.commandPalette = {
    _handler: null,
    _dotnetRef: null,

    init: function (dotnetRef) {
        this._dotnetRef = dotnetRef;
        this._handler = function (e) {
            if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
                e.preventDefault();
                e.stopPropagation();
                dotnetRef.invokeMethodAsync('Toggle');
            }
        };
        document.addEventListener('keydown', this._handler, true);
    },

    focusInput: function (elementId) {
        requestAnimationFrame(function () {
            var el = document.getElementById(elementId);
            if (el) el.focus();
        });
    },

    scrollIntoView: function (elementId) {
        var el = document.getElementById(elementId);
        if (el) el.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    },

    dispose: function () {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler, true);
            this._handler = null;
        }
        if (this._dotnetRef) {
            this._dotnetRef = null;
        }
    }
};
