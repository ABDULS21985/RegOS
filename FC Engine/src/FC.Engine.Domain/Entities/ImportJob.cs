using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ImportJob
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int TemplateId { get; set; }
    public int InstitutionId { get; set; }
    public int? ReturnPeriodId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public HistoricalSourceFormat SourceFormat { get; set; }
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Uploaded;
    public int? RecordCount { get; set; }
    public int? ErrorCount { get; set; }
    public int? WarningCount { get; set; }
    public string? StagedData { get; set; }
    public string? ValidationReport { get; set; }
    public int ImportedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public FC.Engine.Domain.Metadata.ReturnTemplate? Template { get; set; }
}
