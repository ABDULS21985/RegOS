using System.Collections.Concurrent;

namespace FC.Engine.Admin.Services;

/// <summary>
/// Scoped in-memory cache with stale-while-revalidate and hover-prefetch support.
/// Lifetime is per Blazor circuit (one per user session).
/// </summary>
public sealed class DataCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Dictionary<string, Func<Task>> _loaders = new(StringComparer.OrdinalIgnoreCase);

    // ── SWR Get ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the cached value if still fresh. If past half-TTL, triggers a
    /// background refresh and calls <paramref name="onBackgroundUpdate"/> when done.
    /// If expired (or no entry), fetches synchronously and caches the result.
    /// </summary>
    public async Task<T> GetOrFetchAsync<T>(
        string key,
        Func<Task<T>> fetcher,
        TimeSpan ttl,
        Action<T>? onBackgroundUpdate = null)
    {
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired(ttl))
        {
            // Past half-life → warm up in background while returning stale data
            if (onBackgroundUpdate is not null && entry.IsStale(ttl / 2))
                _ = RefreshAsync(key, fetcher, onBackgroundUpdate);

            return entry.As<T>();
        }

        var data = await fetcher();
        _cache[key] = new CacheEntry(data!);
        return data;
    }

    // ── Direct Set ────────────────────────────────────────────────────────────

    /// <summary>Stores a value directly (e.g. after a background refresh or prefetch).</summary>
    public void Set<T>(string key, T data)
        => _cache[key] = new CacheEntry(data!);

    // ── Prefetch Registry ─────────────────────────────────────────────────────

    /// <summary>
    /// Pages call this in OnInitialized to register a data loader for their route.
    /// NavMenu triggers <see cref="PrefetchAsync"/> on hover.
    /// </summary>
    public void RegisterLoader(string route, Func<Task> loader)
        => _loaders[route] = loader;

    /// <summary>
    /// Fires the registered loader for the given route. No-op if not registered.
    /// Fire-and-forget safe — exceptions are swallowed.
    /// </summary>
    public async Task PrefetchAsync(string route)
    {
        if (!_loaders.TryGetValue(route, out var loader)) return;
        try { await loader(); }
        catch { /* prefetch failures are non-fatal */ }
    }

    // ── Invalidation ─────────────────────────────────────────────────────────

    /// <summary>Removes a single cache entry. Call after successful mutations.</summary>
    public void Invalidate(string key) => _cache.TryRemove(key, out _);

    /// <summary>Clears all cached data.</summary>
    public void InvalidateAll() => _cache.Clear();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RefreshAsync<T>(string key, Func<Task<T>> fetcher, Action<T> callback)
    {
        try
        {
            var data = await fetcher();
            _cache[key] = new CacheEntry(data!);
            callback(data);
        }
        catch { /* silent — stale data stays in cache */ }
    }

    // ── Cache entry ───────────────────────────────────────────────────────────

    private sealed class CacheEntry(object value)
    {
        private readonly object _value = value;
        private readonly DateTime _created = DateTime.UtcNow;

        public T As<T>() => (T)_value;
        public bool IsExpired(TimeSpan ttl) => DateTime.UtcNow - _created > ttl;
        public bool IsStale(TimeSpan halfLife) => DateTime.UtcNow - _created > halfLife;
    }
}
