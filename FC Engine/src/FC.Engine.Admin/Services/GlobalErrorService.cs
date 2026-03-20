namespace FC.Engine.Admin.Services;

/// <summary>
/// Scoped service for broadcasting non-fatal errors to the global error banner in MainLayout.
/// Call ReportError() from pages, services, or components to surface a dismissible top banner
/// without replacing page content.
/// </summary>
public sealed class GlobalErrorService
{
    public event Action<string>? OnError;

    public void ReportError(string message) => OnError?.Invoke(message);
}
