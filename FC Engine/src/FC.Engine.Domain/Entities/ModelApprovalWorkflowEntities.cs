namespace FC.Engine.Domain.Entities;

public class ModelApprovalWorkflowStateRecord
{
    public int Id { get; set; }
    public string WorkflowKey { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Artifact { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ModelApprovalAuditRecord
{
    public int Id { get; set; }
    public string WorkflowKey { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Artifact { get; set; } = string.Empty;
    public string PreviousStage { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
