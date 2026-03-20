namespace FC.Engine.Admin.Components.Shared.DataTable;

/// <summary>
/// A named snapshot of column visibility, ordering, and widths for a DataTable.
/// </summary>
public sealed class TablePreset
{
    /// <summary>Unique identifier (short random hex).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Display name shown in the preset dropdown.</summary>
    public string Name { get; set; } = "";

    /// <summary>Column IDs that are hidden in this preset.</summary>
    public HashSet<string> HiddenColumns { get; set; } = new();

    /// <summary>Ordered list of all column IDs. Empty = default registration order.</summary>
    public List<string> ColumnOrder { get; set; } = new();

    /// <summary>Column widths in pixels, keyed by column ID.</summary>
    public Dictionary<string, int> ColumnWidths { get; set; } = new();

    /// <summary>True if this is the system default "Default" preset (cannot be deleted).</summary>
    public bool IsDefault { get; set; }

    /// <summary>True if a platform admin saved this preset and it applies to all users.</summary>
    public bool IsShared { get; set; }
}
