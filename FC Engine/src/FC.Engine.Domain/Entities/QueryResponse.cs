namespace FC.Engine.Domain.Entities;

/// <summary>An institution's response to a regulator query.</summary>
public class QueryResponse
{
    public long Id { get; set; }
    public long QueryId { get; set; }
    public int InstitutionId { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public int AttachmentCount { get; set; }
    public bool SubmittedToRegulator { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? RegulatorAckRef { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RegulatoryQueryRecord? Query { get; set; }
    public ICollection<QueryResponseAttachment> Attachments { get; set; } = new List<QueryResponseAttachment>();
}
