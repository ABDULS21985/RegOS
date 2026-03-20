namespace FC.Engine.Admin.Components.Shared.Forms;

/// <summary>Represents the current lifecycle state of an auto-save operation.</summary>
public enum AutoSaveState
{
    /// <summary>No pending changes.</summary>
    Idle,
    /// <summary>Changes have been made but not yet saved.</summary>
    Dirty,
    /// <summary>A save operation is in flight.</summary>
    Saving,
    /// <summary>Most recent save succeeded (clears to Idle after 2 s).</summary>
    Saved,
    /// <summary>Most recent save failed; retry is available.</summary>
    Failed
}
