using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class ReturnLockService : IReturnLockService
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(30);

    private readonly MetadataDbContext _db;

    public ReturnLockService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<ReturnLockResult> AcquireLock(
        Guid tenantId,
        int submissionId,
        int userId,
        string userName,
        CancellationToken ct = default)
    {
        await RemoveExpiredLock(submissionId, ct);

        var existing = await _db.ReturnLocks
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId, ct);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            var lockEntity = new ReturnLock
            {
                TenantId = tenantId,
                SubmissionId = submissionId,
                UserId = userId,
                UserName = userName,
                LockedAt = now,
                ExpiresAt = now.Add(LockTtl),
                HeartbeatAt = now
            };

            _db.ReturnLocks.Add(lockEntity);
            await _db.SaveChangesAsync(ct);
            return ToResult(lockEntity, acquired: true, message: "Lock acquired.");
        }

        if (existing.UserId == userId)
        {
            existing.HeartbeatAt = now;
            existing.ExpiresAt = now.Add(LockTtl);
            await _db.SaveChangesAsync(ct);
            return ToResult(existing, acquired: true, message: "Lock heartbeat extended.");
        }

        return ToResult(existing, acquired: false, message: $"Being edited by {existing.UserName}.");
    }

    public async Task<ReturnLockResult?> GetActiveLock(Guid tenantId, int submissionId, CancellationToken ct = default)
    {
        await RemoveExpiredLock(submissionId, ct);

        var existing = await _db.ReturnLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId, ct);
        return existing is null ? null : ToResult(existing, acquired: true, message: null);
    }

    public async Task<ReturnLockResult> Heartbeat(
        Guid tenantId,
        int submissionId,
        int userId,
        CancellationToken ct = default)
    {
        await RemoveExpiredLock(submissionId, ct);

        var existing = await _db.ReturnLocks
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId, ct);
        if (existing is null)
        {
            return new ReturnLockResult
            {
                Acquired = false,
                SubmissionId = submissionId,
                UserId = userId,
                Message = "Lock not found."
            };
        }

        if (existing.UserId != userId)
        {
            return ToResult(existing, acquired: false, message: $"Being edited by {existing.UserName}.");
        }

        var now = DateTime.UtcNow;
        existing.HeartbeatAt = now;
        existing.ExpiresAt = now.Add(LockTtl);
        await _db.SaveChangesAsync(ct);
        return ToResult(existing, acquired: true, message: "Heartbeat updated.");
    }

    public async Task ReleaseLock(
        Guid tenantId,
        int submissionId,
        int userId,
        CancellationToken ct = default)
    {
        var existing = await _db.ReturnLocks
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId, ct);
        if (existing is null || existing.UserId != userId)
        {
            return;
        }

        _db.ReturnLocks.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    private async Task RemoveExpiredLock(int submissionId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.ReturnLocks
            .Where(x => x.SubmissionId == submissionId && x.ExpiresAt <= now)
            .ToListAsync(ct);
        if (expired.Count == 0)
        {
            return;
        }

        _db.ReturnLocks.RemoveRange(expired);
        await _db.SaveChangesAsync(ct);
    }

    private static ReturnLockResult ToResult(ReturnLock lockEntity, bool acquired, string? message)
    {
        return new ReturnLockResult
        {
            Acquired = acquired,
            SubmissionId = lockEntity.SubmissionId,
            UserId = lockEntity.UserId,
            UserName = lockEntity.UserName,
            LockedAt = lockEntity.LockedAt,
            ExpiresAt = lockEntity.ExpiresAt,
            HeartbeatAt = lockEntity.HeartbeatAt,
            Message = message
        };
    }
}
