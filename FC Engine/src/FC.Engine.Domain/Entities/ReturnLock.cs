namespace FC.Engine.Domain.Entities;

public class ReturnLock
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int SubmissionId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime HeartbeatAt { get; set; } = DateTime.UtcNow;
}
