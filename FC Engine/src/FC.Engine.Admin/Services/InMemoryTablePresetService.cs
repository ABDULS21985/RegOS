using System.Collections.Concurrent;
using FC.Engine.Admin.Components.Shared.DataTable;

namespace FC.Engine.Admin.Services;

/// <summary>
/// In-memory implementation of <see cref="ITablePresetService"/>.
/// Suitable for development and single-server deployments.
/// Replace with a database-backed implementation for production multi-server deployments.
/// </summary>
public sealed class InMemoryTablePresetService : ITablePresetService
{
    private readonly ConcurrentDictionary<string, List<TablePreset>> _store = new();

    public Task<IReadOnlyList<TablePreset>> GetSharedPresetsAsync(string pageKey, CancellationToken ct = default)
    {
        var presets = _store.TryGetValue(pageKey, out var list)
            ? (IReadOnlyList<TablePreset>)list.AsReadOnly()
            : Array.Empty<TablePreset>();
        return Task.FromResult(presets);
    }

    public Task SaveSharedPresetAsync(string pageKey, TablePreset preset, CancellationToken ct = default)
    {
        var list = _store.GetOrAdd(pageKey, _ => new List<TablePreset>());
        lock (list)
        {
            var idx = list.FindIndex(p => p.Id == preset.Id);
            if (idx >= 0) list[idx] = preset;
            else list.Add(preset);
        }
        return Task.CompletedTask;
    }

    public Task DeleteSharedPresetAsync(string pageKey, string presetId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(pageKey, out var list))
        {
            lock (list) { list.RemoveAll(p => p.Id == presetId); }
        }
        return Task.CompletedTask;
    }
}
