using FC.Engine.Admin.Components.Shared.DataTable;

namespace FC.Engine.Admin.Services;

/// <summary>
/// Provides server-side storage for shared DataTable presets.
/// Shared presets are saved by platform admins and visible to all users for a given page key.
/// </summary>
public interface ITablePresetService
{
    /// <summary>Returns all shared presets for the given page key.</summary>
    Task<IReadOnlyList<TablePreset>> GetSharedPresetsAsync(string pageKey, CancellationToken ct = default);

    /// <summary>Saves or replaces a shared preset for the given page key.</summary>
    Task SaveSharedPresetAsync(string pageKey, TablePreset preset, CancellationToken ct = default);

    /// <summary>Removes a shared preset by ID.</summary>
    Task DeleteSharedPresetAsync(string pageKey, string presetId, CancellationToken ct = default);
}
