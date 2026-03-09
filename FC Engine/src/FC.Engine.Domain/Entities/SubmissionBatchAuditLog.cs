namespace FC.Engine.Domain.Entities;

/// <summary>Immutable append-only audit record for every submission pipeline action.</summary>
public class SubmissionBatchAuditLog
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public int InstitutionId { get; set; }
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// One of: BATCH_CREATED, ITEM_ADDED, SIGNING_STARTED, SIGNED,
    /// DISPATCH_STARTED, DISPATCHED, ACK_RECEIVED, STATUS_UPDATED,
    /// QUERY_RECEIVED, QUERY_RESPONDED, RETRY_ATTEMPTED, FAILED.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON context blob.</summary>
    public string? Detail { get; set; }

    /// <summary>NULL = system-initiated action.</summary>
    public int? PerformedBy { get; set; }

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
}
