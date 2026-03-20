using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ImportMapping
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int InstitutionId { get; set; }
    public int TemplateId { get; set; }
    public HistoricalSourceFormat SourceFormat { get; set; }
    public string? SourceIdentifier { get; set; }
    public string MappingConfig { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public FC.Engine.Domain.Metadata.ReturnTemplate? Template { get; set; }
}
