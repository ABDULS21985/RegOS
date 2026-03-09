namespace FC.Engine.Domain.Entities;

/// <summary>Groups one or more regulatory returns submitted together in a single batch.</summary>
public class SubmissionBatch
{
    public long Id { get; set; }
    public int InstitutionId { get; set; }
    public string BatchReference { get; set; } = string.Empty;     // BATCH-00001-CBN-20260309-A1B2C3
    public string RegulatorCode { get; set; } = string.Empty;
    public int ChannelId { get; set; }
    public string Status { get; set; } = "PENDING";
    public int SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? FinalStatusAt { get; set; }
    public Guid CorrelationId { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RegulatoryChannel? Channel { get; set; }
    public ICollection<SubmissionItem> Items { get; set; } = new List<SubmissionItem>();
    public ICollection<SubmissionBatchReceipt> Receipts { get; set; } = new List<SubmissionBatchReceipt>();
    public ICollection<RegulatoryQueryRecord> Queries { get; set; } = new List<RegulatoryQueryRecord>();
    public ICollection<SubmissionBatchAuditLog> AuditLogs { get; set; } = new List<SubmissionBatchAuditLog>();
}
