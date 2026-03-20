using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class PortalNotificationRepository : IPortalNotificationRepository
{
    private readonly MetadataDbContext _db;

    public PortalNotificationRepository(MetadataDbContext db) => _db = db;

    public async Task<List<PortalNotification>> GetForUser(
        int userId, int institutionId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return await _db.PortalNotifications
            .Where(n => n.InstitutionId == institutionId
                     && (n.UserId == userId || n.UserId == null))
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCount(int userId, int institutionId, CancellationToken ct = default)
    {
        return await _db.PortalNotifications
            .CountAsync(n => n.InstitutionId == institutionId
                          && (n.UserId == userId || n.UserId == null)
                          && !n.IsRead, ct);
    }

    public async Task<List<PortalNotification>> GetRecentUnread(
        int userId, int institutionId, int take = 5, CancellationToken ct = default)
    {
        return await _db.PortalNotifications
            .Where(n => n.InstitutionId == institutionId
                     && (n.UserId == userId || n.UserId == null)
                     && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task MarkAsRead(int notificationId, CancellationToken ct = default)
    {
        var notification = await _db.PortalNotifications.FindAsync(new object[] { notificationId }, ct);
        if (notification is not null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllAsRead(int userId, int institutionId, CancellationToken ct = default)
    {
        var unread = await _db.PortalNotifications
            .Where(n => n.InstitutionId == institutionId
                     && (n.UserId == userId || n.UserId == null)
                     && !n.IsRead)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearRead(int userId, int institutionId, DateTime olderThan, CancellationToken ct = default)
    {
        var toDelete = await _db.PortalNotifications
            .Where(n => n.InstitutionId == institutionId
                     && (n.UserId == userId || n.UserId == null)
                     && n.IsRead
                     && n.CreatedAt < olderThan)
            .ToListAsync(ct);

        _db.PortalNotifications.RemoveRange(toDelete);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PortalNotification> Add(PortalNotification notification, CancellationToken ct = default)
    {
        _db.PortalNotifications.Add(notification);
        await _db.SaveChangesAsync(ct);
        return notification;
    }

    public async Task AddRange(IEnumerable<PortalNotification> notifications, CancellationToken ct = default)
    {
        _db.PortalNotifications.AddRange(notifications);
        await _db.SaveChangesAsync(ct);
    }
}
