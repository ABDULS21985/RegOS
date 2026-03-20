using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetPreference(Guid tenantId, int userId, string eventType, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationPreference>> GetByUser(Guid tenantId, int userId, CancellationToken ct = default);
    Task<NotificationPreference> Upsert(NotificationPreference preference, CancellationToken ct = default);
    Task UpsertRange(IEnumerable<NotificationPreference> preferences, CancellationToken ct = default);
}
