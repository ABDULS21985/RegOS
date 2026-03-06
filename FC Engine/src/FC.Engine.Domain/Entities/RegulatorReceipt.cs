using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class RegulatorReceipt
{
    public int Id { get; set; }

    /// <summary>Institution tenant ID for RLS visibility.</summary>
    public Guid TenantId { get; set; }
    public Guid RegulatorTenantId { get; set; }

    public int SubmissionId { get; set; }
    public RegulatorReceiptStatus Status { get; set; } = RegulatorReceiptStatus.Received;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? FinalAcceptedAt { get; set; }
    public string? Notes { get; set; }

    public Submission? Submission { get; set; }
}
