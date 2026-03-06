using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Notifications;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

public class DataBreachEscalationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataBreachEscalationJob> _logger;

    public DataBreachEscalationJob(
        IServiceProvider serviceProvider,
        ILogger<DataBreachEscalationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycle(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Data breach escalation cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    internal async Task RunCycle(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationOrchestrator>();

        var incidents = await db.DataBreachIncidents
            .Where(x =>
                x.Severity == DataBreachSeverity.HIGH || x.Severity == DataBreachSeverity.CRITICAL)
            .Where(x => x.Status != DataBreachStatus.Closed)
            .Where(x => !x.NitdaNotifiedAt.HasValue && x.NitdaNotificationDeadline.HasValue)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var incident in incidents)
        {
            if (!incident.TenantId.HasValue)
            {
                continue;
            }

            var elapsed = now - incident.DetectedAt;
            if (elapsed.TotalHours >= 24 && !incident.Escalation24hSentAt.HasValue)
            {
                await SendEscalation(notifier, incident, "24h", ct);
                incident.Escalation24hSentAt = now;
            }

            if (elapsed.TotalHours >= 48 && !incident.Escalation48hSentAt.HasValue)
            {
                await SendEscalation(notifier, incident, "48h", ct);
                incident.Escalation48hSentAt = now;
            }

            if (elapsed.TotalHours >= 68 && !incident.Escalation68hSentAt.HasValue)
            {
                await SendEscalation(notifier, incident, "68h", ct);
                incident.Escalation68hSentAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Task SendEscalation(
        INotificationOrchestrator notifier,
        Domain.Entities.DataBreachIncident incident,
        string checkpoint,
        CancellationToken ct)
    {
        return notifier.Notify(new NotificationRequest
        {
            TenantId = incident.TenantId!.Value,
            EventType = NotificationEvents.BreachEscalation,
            Title = $"Breach Escalation ({checkpoint}) - Incident #{incident.Id}",
            Message = $"Incident {incident.Title} has reached the {checkpoint} escalation checkpoint. " +
                      $"NITDA deadline: {incident.NitdaNotificationDeadline:dd MMM yyyy HH:mm} UTC.",
            Priority = NotificationPriority.Critical,
            IsMandatory = true,
            RecipientRoles = ["Admin", "Approver"],
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IncidentId"] = incident.Id.ToString(),
                ["Checkpoint"] = checkpoint
            }
        }, ct);
    }
}
