using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class NotificationDeliveryRepository : INotificationDeliveryRepository
{
    private readonly MetadataDbContext _db;

    public NotificationDeliveryRepository(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationDelivery> Add(NotificationDelivery delivery, CancellationToken ct = default)
    {
        _db.NotificationDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
        return delivery;
    }

    public async Task Update(NotificationDelivery delivery, CancellationToken ct = default)
    {
        _db.NotificationDeliveries.Update(delivery);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDelivery>> GetRetryBatch(int take = 50, CancellationToken ct = default)
    {
        return await _db.NotificationDeliveries
            .Where(d => d.Status == DeliveryStatus.Failed)
            .Where(d => d.AttemptCount < d.MaxAttempts)
            .Where(d => d.NextRetryAt == null || d.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(d => d.NextRetryAt)
            .ThenBy(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}
