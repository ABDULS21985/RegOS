using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.Notifications;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class DataBreachService : IDataBreachService
{
    private readonly MetadataDbContext _db;
    private readonly INotificationOrchestrator _notificationOrchestrator;

    public DataBreachService(
        MetadataDbContext db,
        INotificationOrchestrator notificationOrchestrator)
    {
        _db = db;
        _notificationOrchestrator = notificationOrchestrator;
    }

    public async Task<DataBreachIncident> ReportBreach(DataBreachReport report, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var incident = new DataBreachIncident
        {
            TenantId = report.TenantId,
            Severity = report.Severity,
            Status = DataBreachStatus.Detected,
            Title = report.Title.Trim(),
            Description = report.Description.Trim(),
            DataSubjectsAffected = report.DataSubjectsAffected,
            DataCategoriesAffected = report.DataCategoriesAffected.Count == 0
                ? null
                : JsonSerializer.Serialize(report.DataCategoriesAffected),
            DetectedAt = now,
            NitdaNotificationDeadline = report.Severity is DataBreachSeverity.HIGH or DataBreachSeverity.CRITICAL
                ? now.AddHours(72)
                : null,
            CreatedAt = now
        };

        _db.DataBreachIncidents.Add(incident);
        await _db.SaveChangesAsync(ct);

        if (incident.TenantId.HasValue)
        {
            await _notificationOrchestrator.Notify(new NotificationRequest
            {
                TenantId = incident.TenantId.Value,
                EventType = NotificationEvents.BreachDetected,
                Title = $"Data Breach Detected: {incident.Title}",
                Message = report.Severity is DataBreachSeverity.HIGH or DataBreachSeverity.CRITICAL
                    ? $"Severity {report.Severity}. NITDA notification deadline: {incident.NitdaNotificationDeadline:dd MMM yyyy HH:mm} UTC."
                    : $"Severity {report.Severity}. Immediate assessment required.",
                Priority = report.Severity is DataBreachSeverity.HIGH or DataBreachSeverity.CRITICAL
                    ? NotificationPriority.Critical
                    : NotificationPriority.High,
                IsMandatory = true,
                RecipientRoles = ["Admin", "Approver"],
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["IncidentId"] = incident.Id.ToString(),
                    ["Severity"] = incident.Severity.ToString()
                }
            }, ct);
        }

        return incident;
    }

    public async Task<DataBreachIncident> MarkNitdaNotified(
        int incidentId,
        int processedByUserId,
        string? notes,
        CancellationToken ct = default)
    {
        var incident = await _db.DataBreachIncidents
            .FirstOrDefaultAsync(x => x.Id == incidentId, ct)
            ?? throw new InvalidOperationException($"Breach incident {incidentId} not found.");

        var now = DateTime.UtcNow;
        incident.NitdaNotifiedAt = now;
        incident.Status = DataBreachStatus.NitdaNotified;
        incident.DpoNotes = string.IsNullOrWhiteSpace(notes)
            ? $"NITDA notified by user #{processedByUserId} at {now:O}"
            : $"{notes.Trim()} (Notified by user #{processedByUserId} at {now:O})";

        await _db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task<IReadOnlyList<DataBreachIncident>> GetOpenIncidents(Guid? tenantId, CancellationToken ct = default)
    {
        var query = _db.DataBreachIncidents
            .AsNoTracking()
            .Where(x => x.Status != DataBreachStatus.Closed);

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(x => x.DetectedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }
}
