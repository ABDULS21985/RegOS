using Microsoft.JSInterop;
using System.Text.Json;

namespace FC.Engine.Portal.Services;

/// <summary>
/// Tracks which feature tours the user has seen, persisted to localStorage under "fc_tours_seen".
/// </summary>
public class TourService(IJSRuntime js)
{
    private const string StorageKey = "fc_tours_seen";
    private HashSet<string>? _cache;

    public async Task<bool> HasSeenAsync(string tourKey)
    {
        var seen = await GetSeenSetAsync();
        return seen.Contains(tourKey);
    }

    public async Task MarkSeenAsync(string tourKey)
    {
        var seen = await GetSeenSetAsync();
        if (seen.Add(tourKey))
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.setItem", StorageKey,
                    JsonSerializer.Serialize(seen));
            }
            catch { /* JS not yet available or storage blocked */ }
        }
    }

    private async Task<HashSet<string>> GetSeenSetAsync()
    {
        if (_cache is not null) return _cache;
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(raw))
            {
                _cache = JsonSerializer.Deserialize<HashSet<string>>(raw) ?? [];
                return _cache;
            }
        }
        catch { /* first render, JS not ready */ }
        _cache = [];
        return _cache;
    }
}

/// <summary>
/// Scoped event bus so the topbar ? button can trigger the active page's tour.
/// </summary>
public class TourStateService
{
    public event Action? HelpButtonPressed;
    public void TriggerHelpButton() => HelpButtonPressed?.Invoke();
}

/// <summary>Defines a pulsing beacon shown on a page element for first-time feature discovery.</summary>
public record TourBeaconDefinition(
    string TargetSelector,
    string Title,
    string Description,
    string Position = "bottom");
