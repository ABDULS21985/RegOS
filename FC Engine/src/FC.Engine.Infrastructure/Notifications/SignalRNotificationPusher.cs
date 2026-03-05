using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Hubs;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace FC.Engine.Infrastructure.Notifications;

public class SignalRNotificationPusher : INotificationPusher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MetadataDbContext _db;

    public SignalRNotificationPusher(IServiceProvider serviceProvider, MetadataDbContext db)
    {
        _serviceProvider = serviceProvider;
        _db = db;
    }

    public async Task PushToUser(int userId, NotificationPayload payload, CancellationToken ct = default)
    {
        var hubContext = _serviceProvider.GetService<IHubContext<NotificationHub>>();
        if (hubContext is null)
        {
            return;
        }

        await hubContext.Clients.Group($"user:{userId}").SendAsync("ReceiveNotification", payload, ct);
    }

    public async Task PushToTenant(Guid tenantId, NotificationPayload payload, CancellationToken ct = default)
    {
        var hubContext = _serviceProvider.GetService<IHubContext<NotificationHub>>();
        if (hubContext is null)
        {
            return;
        }

        await hubContext.Clients.Group($"tenant:{tenantId}").SendAsync("ReceiveNotification", payload, ct);
    }

    public async Task PushToRole(Guid tenantId, string role, NotificationPayload payload, CancellationToken ct = default)
    {
        var userIds = await _db.InstitutionUsers
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Role.ToString() == role)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            await PushToUser(userId, payload, ct);
        }
    }
}
