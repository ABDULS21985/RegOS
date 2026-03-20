using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class DataBreachIncident
{
    public int Id { get; set; }
    public Guid? TenantId { get; set; }
    public DataBreachSeverity Severity { get; set; }
    public DataBreachStatus Status { get; set; } = DataBreachStatus.Detected;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DataSubjectsAffected { get; set; }
    public string? DataCategoriesAffected { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ContainedAt { get; set; }
    public DateTime? NitdaNotifiedAt { get; set; }
    public DateTime? NitdaNotificationDeadline { get; set; }
    public DateTime? RemediatedAt { get; set; }
    public DateTime? Escalation24hSentAt { get; set; }
    public DateTime? Escalation48hSentAt { get; set; }
    public DateTime? Escalation68hSentAt { get; set; }
    public string? DpoNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
