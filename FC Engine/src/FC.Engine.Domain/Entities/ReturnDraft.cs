namespace FC.Engine.Domain.Entities;

/// <summary>
/// Lightweight JSON snapshot of form data for crash recovery and multi-user conflict detection.
/// One record per (TenantId, InstitutionId, ReturnCode, Period) — upserted on every auto-save.
/// </summary>
public class ReturnDraft
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int InstitutionId { get; set; }

    /// <summary>Return template code, e.g. "BSL01".</summary>
    public string ReturnCode { get; set; } = "";

    /// <summary>Human-readable period label, e.g. "Q4 2025" or "December 2025".</summary>
    public string Period { get; set; } = "";

    /// <summary>JSON-serialized List&lt;Dictionary&lt;string, string&gt;&gt; — the form rows.</summary>
    public string DataJson { get; set; } = "[]";

    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Display name of the user who last saved this draft.</summary>
    public string SavedBy { get; set; } = "";
}
