namespace FC.Engine.Admin.Services;

/// <summary>
/// Dispatches page-context keyboard shortcut events to registered page handlers.
/// Pages subscribe to the events they support in OnInitialized and unsubscribe on disposal.
///
/// Usage example on a list page:
///   protected override void OnInitialized()
///   {
///       ShortcutService.OnNewItem    += HandleNewItem;
///       ShortcutService.OnFocusSearch += HandleFocusSearch;
///       ShortcutService.OnExport     += HandleExport;
///       ShortcutService.OnNavigateRows += HandleNavigateRows;
///       ShortcutService.OnOpenRow    += HandleOpenRow;
///   }
///   public void Dispose() => UnsubscribeAll();
/// </summary>
public sealed class KeyboardShortcutService
{
    // ── Page-context shortcuts ───────────────────────────────────────────────
    // Multiple events may fire for a single key (e.g. E → Export on list pages,
    // Edit on detail pages). Pages register only for what they support.

    /// <summary>N — create a new item on the current page.</summary>
    public event Action? OnNewItem;

    /// <summary>F — focus the page-level search / filter input.</summary>
    public event Action? OnFocusSearch;

    /// <summary>E — export data (list pages).</summary>
    public event Action? OnExport;

    /// <summary>E — enter edit mode (detail pages).</summary>
    public event Action? OnEditMode;

    /// <summary>S — save changes (detail pages in edit mode).</summary>
    public event Action? OnSave;

    /// <summary>⌘⌫ / Delete — delete the current item after confirmation.</summary>
    public event Action? OnDeleteItem;

    /// <summary>↑ / ↓ — navigate rows in a list. direction: -1 = up, +1 = down.</summary>
    public event Action<int>? OnNavigateRows;

    /// <summary>Enter — open the currently focused/selected row.</summary>
    public event Action? OnOpenRow;

    // ── Command-palette discoverability ─────────────────────────────────────

    private int _paletteOpenCount;

    public int PaletteOpenCount => _paletteOpenCount;

    /// <summary>
    /// Fired exactly once when the palette has been opened 3 times, signalling
    /// that the ⌘K hint tooltip should be shown on the search button.
    /// </summary>
    public event Action? OnPaletteHintReached;

    /// <summary>
    /// Call this every time the command palette is opened (keyboard or mouse).
    /// </summary>
    public void TrackPaletteOpen()
    {
        _paletteOpenCount++;
        if (_paletteOpenCount == 3)
            OnPaletteHintReached?.Invoke();
    }

    // ── Trigger helpers — called by KeyboardShortcutsOverlay JSInvokable methods ──

    public void TriggerNewItem()           => OnNewItem?.Invoke();
    public void TriggerFocusSearch()       => OnFocusSearch?.Invoke();
    public void TriggerExport()            => OnExport?.Invoke();
    public void TriggerEditMode()          => OnEditMode?.Invoke();
    public void TriggerSave()              => OnSave?.Invoke();
    public void TriggerDeleteItem()        => OnDeleteItem?.Invoke();
    public void TriggerNavigateRows(int d) => OnNavigateRows?.Invoke(d);
    public void TriggerOpenRow()           => OnOpenRow?.Invoke();
}
