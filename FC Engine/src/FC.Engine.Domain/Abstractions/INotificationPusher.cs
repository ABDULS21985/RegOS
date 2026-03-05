using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Abstractions;

public interface INotificationPusher
{
    Task PushToUser(int userId, NotificationPayload payload, CancellationToken ct = default);
    Task PushToTenant(Guid tenantId, NotificationPayload payload, CancellationToken ct = default);
    Task PushToRole(Guid tenantId, string role, NotificationPayload payload, CancellationToken ct = default);
}

public class NotificationPayload
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string? ActionUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
