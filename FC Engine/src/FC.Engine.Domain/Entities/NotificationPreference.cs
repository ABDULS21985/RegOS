namespace FC.Engine.Domain.Entities;

public class NotificationPreference
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;

    // Optional user-level quiet hours (WAT) for SMS sends.
    public TimeSpan? SmsQuietHoursStart { get; set; } // e.g., 22:00
    public TimeSpan? SmsQuietHoursEnd { get; set; } // e.g., 07:00
}
