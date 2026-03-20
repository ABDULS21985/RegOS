namespace FC.Engine.Admin.Services;

public enum ToastVariant { Success, Warning, Error, Info, Loading }

public sealed class ToastItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public ToastVariant Variant { get; set; }
    public string? Title { get; set; }
    public string Message { get; set; } = "";
    /// <summary>Auto-dismiss duration in ms. 0 = no auto-dismiss.</summary>
    public int DurationMs { get; set; }
    public bool Dismissible { get; set; } = true;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    /// <summary>Incremented when the same message arrives within the dedup window.</summary>
    public int DuplicateCount { get; set; } = 1;
    public string? ActionLabel { get; init; }
    public Action? ActionCallback { get; init; }
    internal bool IsExiting { get; set; }

    /// <summary>Lower number = higher priority (renders at top of stack).</summary>
    internal int Priority => Variant switch
    {
        ToastVariant.Error   => 0,
        ToastVariant.Warning => 1,
        ToastVariant.Loading => 2,
        ToastVariant.Info    => 3,
        ToastVariant.Success => 4,
        _                    => 5
    };
}

/// <summary>
/// Intelligent toast queue with priority ordering, deduplication, max-stack enforcement,
/// loading toasts, action buttons, and exit-animation support.
/// </summary>
public sealed class ToastService : IDisposable
{
    private const int MaxVisible    = 4;
    private const int DedupWindowMs = 2_000;
    private const int ExitAnimMs    = 320; // must match CSS fcToastOut duration

    private readonly List<ToastItem>           _visible    = new();
    private readonly Queue<ToastItem>          _queue      = new();
    private readonly Dictionary<string, Timer> _timers     = new();
    private readonly Dictionary<string, Timer> _exitTimers = new();

    public event Action? OnChange;

    /// <summary>Visible toasts ordered by priority then creation time.</summary>
    public IReadOnlyList<ToastItem> Toasts =>
        _visible.OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt).ToList();

    /// <summary>Number of notifications waiting in the overflow queue.</summary>
    public int QueuedCount => _queue.Count;

    // ── Duration defaults ─────────────────────────────────────────────────

    private static int DefaultDuration(ToastVariant v) => v switch
    {
        ToastVariant.Error   => 0,     // persists until manually dismissed
        ToastVariant.Warning => 6_000,
        ToastVariant.Loading => 0,     // persists until updated/dismissed
        _                    => 4_000  // Success / Info
    };

    // ── Public API ────────────────────────────────────────────────────────

    public void Show(ToastItem toast)
    {
        if (toast.DurationMs == 0)
            toast.DurationMs = DefaultDuration(toast.Variant);

        if (TryDeduplicate(toast))
            return;

        if (ActiveCount >= MaxVisible)
            _queue.Enqueue(toast);
        else
            AddToVisible(toast);

        Notify();
    }

    public void Success(string message, string? title = null,
                        string? actionLabel = null, Action? actionCallback = null)
        => Show(new ToastItem
        {
            Variant        = ToastVariant.Success,
            Message        = message,
            Title          = title,
            DurationMs     = DefaultDuration(ToastVariant.Success),
            ActionLabel    = actionLabel,
            ActionCallback = actionCallback
        });

    public void Warning(string message, string? title = null,
                        string? actionLabel = null, Action? actionCallback = null)
        => Show(new ToastItem
        {
            Variant        = ToastVariant.Warning,
            Message        = message,
            Title          = title,
            DurationMs     = DefaultDuration(ToastVariant.Warning),
            ActionLabel    = actionLabel,
            ActionCallback = actionCallback
        });

    public void Error(string message, string? title = null,
                      string? actionLabel = null, Action? actionCallback = null)
        => Show(new ToastItem
        {
            Variant        = ToastVariant.Error,
            Message        = message,
            Title          = title,
            DurationMs     = 0,
            Dismissible    = true,
            ActionLabel    = actionLabel,
            ActionCallback = actionCallback
        });

    public void Info(string message, string? title = null,
                     string? actionLabel = null, Action? actionCallback = null)
        => Show(new ToastItem
        {
            Variant        = ToastVariant.Info,
            Message        = message,
            Title          = title,
            DurationMs     = DefaultDuration(ToastVariant.Info),
            ActionLabel    = actionLabel,
            ActionCallback = actionCallback
        });

    /// <summary>
    /// Shows a persistent loading toast. Returns an ID token to pass to
    /// <see cref="UpdateToast"/> when the operation completes.
    /// </summary>
    public string ShowLoading(string message, string? title = null)
    {
        var toast = new ToastItem
        {
            Variant     = ToastVariant.Loading,
            Message     = message,
            Title       = title,
            DurationMs  = 0,
            Dismissible = false
        };
        Show(toast);
        return toast.Id;
    }

    /// <summary>
    /// Transitions a loading toast (by <paramref name="id"/>) to a result variant
    /// and begins its auto-dismiss timer.
    /// </summary>
    public void UpdateToast(string id, ToastVariant variant, string message, string? title = null)
    {
        var toast = _visible.FirstOrDefault(t => t.Id == id);
        if (toast == null) return;

        if (_timers.Remove(id, out var old)) old.Dispose();

        toast.Variant     = variant;
        toast.Message     = message;
        if (title != null) toast.Title = title;
        toast.Dismissible = true;
        toast.DurationMs  = DefaultDuration(variant);

        if (toast.DurationMs > 0)
            _timers[id] = new Timer(_ => BeginDismiss(id), null, toast.DurationMs, Timeout.Infinite);

        Notify();
    }

    public void Dismiss(string toastId) => BeginDismiss(toastId);

    // ── Internal helpers ──────────────────────────────────────────────────

    private int ActiveCount => _visible.Count(t => !t.IsExiting);

    private bool TryDeduplicate(ToastItem incoming)
    {
        var match =
            _visible.FirstOrDefault(t =>
                t.Variant == incoming.Variant &&
                t.Message == incoming.Message &&
                !t.IsExiting &&
                (DateTime.UtcNow - t.CreatedAt).TotalMilliseconds <= DedupWindowMs)
            ?? (ToastItem?)_queue.FirstOrDefault(t =>
                t.Variant == incoming.Variant &&
                t.Message == incoming.Message &&
                (DateTime.UtcNow - t.CreatedAt).TotalMilliseconds <= DedupWindowMs);

        if (match == null) return false;

        match.DuplicateCount++;
        Notify();
        return true;
    }

    private void AddToVisible(ToastItem toast)
    {
        _visible.Add(toast);
        if (toast.DurationMs > 0)
            _timers[toast.Id] = new Timer(_ => BeginDismiss(toast.Id), null, toast.DurationMs, Timeout.Infinite);
    }

    private void BeginDismiss(string id)
    {
        var toast = _visible.FirstOrDefault(t => t.Id == id);
        if (toast == null || toast.IsExiting) return;

        if (_timers.Remove(id, out var t)) t.Dispose();

        toast.IsExiting = true;
        Notify();

        _exitTimers[id] = new Timer(_ => FinishRemove(id), null, ExitAnimMs, Timeout.Infinite);
    }

    private void FinishRemove(string id)
    {
        _visible.RemoveAll(t => t.Id == id);
        if (_exitTimers.Remove(id, out var et)) et.Dispose();

        while (ActiveCount < MaxVisible && _queue.Count > 0)
            AddToVisible(_queue.Dequeue());

        Notify();
    }

    private void Notify() => OnChange?.Invoke();

    public void Dispose()
    {
        foreach (var t in _timers.Values)     t.Dispose();
        foreach (var t in _exitTimers.Values) t.Dispose();
        _timers.Clear();
        _exitTimers.Clear();
    }
}
