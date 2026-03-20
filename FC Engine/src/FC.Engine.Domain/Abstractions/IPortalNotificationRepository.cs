using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IPortalNotificationRepository
{
    /// <summary>Get notifications for a user, including institution-wide broadcasts (UserId = null). Ordered by CreatedAt descending.</summary>
    Task<List<PortalNotification>> GetForUser(int userId, int institutionId, int skip = 0, int take = 20, CancellationToken ct = default);

    /// <summary>Get unread count for a user (including institution-wide broadcasts).</summary>
    Task<int> GetUnreadCount(int userId, int institutionId, CancellationToken ct = default);

    /// <summary>Get the most recent N unread notifications for the bell dropdown.</summary>
    Task<List<PortalNotification>> GetRecentUnread(int userId, int institutionId, int take = 5, CancellationToken ct = default);

    /// <summary>Mark a single notification as read.</summary>
    Task MarkAsRead(int notificationId, CancellationToken ct = default);

    /// <summary>Mark all unread notifications as read for a user.</summary>
    Task MarkAllAsRead(int userId, int institutionId, CancellationToken ct = default);

    /// <summary>Delete all read notifications older than the specified date for a user.</summary>
    Task ClearRead(int userId, int institutionId, DateTime olderThan, CancellationToken ct = default);

    /// <summary>Add a notification.</summary>
    Task<PortalNotification> Add(PortalNotification notification, CancellationToken ct = default);

    /// <summary>Add multiple notifications at once (e.g., broadcasting to all Checkers).</summary>
    Task AddRange(IEnumerable<PortalNotification> notifications, CancellationToken ct = default);
}
