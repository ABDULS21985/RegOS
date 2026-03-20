namespace FC.Engine.Domain.Abstractions;

public interface IReturnLockService
{
    Task<ReturnLockResult> AcquireLock(
        Guid tenantId,
        int submissionId,
        int userId,
        string userName,
        CancellationToken ct = default);

    Task<ReturnLockResult?> GetActiveLock(Guid tenantId, int submissionId, CancellationToken ct = default);

    Task<ReturnLockResult> Heartbeat(
        Guid tenantId,
        int submissionId,
        int userId,
        CancellationToken ct = default);

    Task ReleaseLock(
        Guid tenantId,
        int submissionId,
        int userId,
        CancellationToken ct = default);
}

public class ReturnLockResult
{
    public bool Acquired { get; set; }
    public int SubmissionId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime LockedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime HeartbeatAt { get; set; }
    public bool IsReadOnly => !Acquired;
    public string? Message { get; set; }
}
