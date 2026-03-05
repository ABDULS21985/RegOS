using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class NotificationDelivery
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string NotificationEventType { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public int RecipientId { get; set; }
    public string RecipientAddress { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Queued;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTime? NextRetryAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Payload { get; set; } // JSON template variables/request payload
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
