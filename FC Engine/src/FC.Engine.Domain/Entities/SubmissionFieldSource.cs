namespace FC.Engine.Domain.Entities;

/// <summary>
/// Tracks provenance for dynamic field values written into generated return tables.
/// Used by inter-module auto-population and subsequent manual overrides.
/// </summary>
public class SubmissionFieldSource
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int SubmissionId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string DataSource { get; set; } = "Manual";
    public string? SourceDetail { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
