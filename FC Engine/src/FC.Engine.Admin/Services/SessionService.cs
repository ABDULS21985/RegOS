namespace FC.Engine.Admin.Services;

/// <summary>
/// Scoped service that holds the current session lifecycle state.
/// Updated by <c>SessionExpiredModal</c> and consumed by <c>MainLayout</c>
/// for the admin topbar session indicator.
/// </summary>
public sealed class SessionService
{
    /// <summary>Seconds remaining in the session (-1 = not yet known).</summary>
    public int RemainingSec { get; private set; } = -1;

    /// <summary>True while the inactivity warning countdown is active.</summary>
    public bool IsWarningActive { get; private set; }

    /// <summary>Formatted MM:SS string suitable for the topbar indicator.</summary>
    public string RemainingDisplay =>
        RemainingSec >= 0 ? FormatSeconds(RemainingSec) : "--:--";

    /// <summary>Fires when any property changes; subscribers should call StateHasChanged.</summary>
    public event Action? OnChange;

    internal void Update(int seconds, bool warningActive)
    {
        RemainingSec    = seconds;
        IsWarningActive = warningActive;
        OnChange?.Invoke();
    }

    internal void Clear()
    {
        RemainingSec    = -1;
        IsWarningActive = false;
        OnChange?.Invoke();
    }

    private static string FormatSeconds(int total)
    {
        var t = Math.Max(0, total);
        return $"{t / 60:D2}:{t % 60:D2}";
    }
}
