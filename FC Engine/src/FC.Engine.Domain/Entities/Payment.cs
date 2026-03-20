using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string PaymentMethod { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderName { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice? Invoice { get; set; }

    public void MarkConfirmed()
    {
        Status = PaymentStatus.Confirmed;
        PaidAt = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
    }
}
