namespace FC.Engine.Domain.Entities;

/// <summary>Acknowledgement receipt returned by a regulator's system after dispatch.</summary>
public class SubmissionBatchReceipt
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public int InstitutionId { get; set; }
    public string RegulatorCode { get; set; } = string.Empty;
    public string ReceiptReference { get; set; } = string.Empty;   // regulator's ack number
    public DateTime ReceiptTimestamp { get; set; }                 // timestamp from regulator
    public string? RawResponse { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SubmissionBatch? Batch { get; set; }
}
