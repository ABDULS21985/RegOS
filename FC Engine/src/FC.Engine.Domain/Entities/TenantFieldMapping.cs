namespace FC.Engine.Domain.Entities;

public class TenantFieldMapping
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string IntegrationName { get; set; } = string.Empty;
    public string ReturnCode { get; set; } = string.Empty;
    public string ExternalFieldName { get; set; } = string.Empty;
    public string TemplateFieldName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
