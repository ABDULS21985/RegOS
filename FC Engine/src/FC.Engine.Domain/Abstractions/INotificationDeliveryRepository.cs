using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface INotificationDeliveryRepository
{
    Task<NotificationDelivery> Add(NotificationDelivery delivery, CancellationToken ct = default);
    Task Update(NotificationDelivery delivery, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDelivery>> GetRetryBatch(int take = 50, CancellationToken ct = default);
}
