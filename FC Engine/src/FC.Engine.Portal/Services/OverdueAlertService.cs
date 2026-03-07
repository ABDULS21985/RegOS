namespace FC.Engine.Portal.Services;

/// <summary>
/// Scoped service that shares the overdue deadline count across components
/// (populated by Home.razor after dashboard load, consumed by NotificationBell)
/// within the same Blazor circuit.
/// </summary>
public class OverdueAlertService
{
    public int OverdueCount { get; private set; }
    public event Action? OnChange;

    public void Update(int count)
    {
        if (OverdueCount == count) return;
        OverdueCount = count;
        OnChange?.Invoke();
    }
}
