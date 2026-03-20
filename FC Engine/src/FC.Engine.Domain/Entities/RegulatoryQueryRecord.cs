namespace FC.Engine.Domain.Entities;

/// <summary>A query raised by a regulator against a submitted batch.</summary>
public class RegulatoryQueryRecord
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public int InstitutionId { get; set; }
    public string RegulatorCode { get; set; } = string.Empty;
    public string QueryReference { get; set; } = string.Empty;     // regulator's query ID
    public string QueryType { get; set; } = "CLARIFICATION";       // CLARIFICATION | AMENDMENT | ADDITIONAL_DATA
    public string QueryText { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public string Priority { get; set; } = "NORMAL";               // LOW | NORMAL | HIGH | CRITICAL
    public string Status { get; set; } = "OPEN";                   // OPEN | IN_PROGRESS | RESPONDED | CLOSED
    public int? AssignedToUserId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public SubmissionBatch? Batch { get; set; }
    public ICollection<QueryResponse> Responses { get; set; } = new List<QueryResponse>();
}
