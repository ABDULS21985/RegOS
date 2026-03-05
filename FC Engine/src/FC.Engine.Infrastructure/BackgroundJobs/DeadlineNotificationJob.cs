using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Notifications;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

public class DeadlineNotificationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly MetadataDbContext _db;
    private readonly INotificationOrchestrator _notificationOrchestrator;
    private readonly ILogger<DeadlineNotificationJob> _logger;

    public DeadlineNotificationJob(
        MetadataDbContext db,
        INotificationOrchestrator notificationOrchestrator,
        ILogger<DeadlineNotificationJob> logger)
    {
        _db = db;
        _notificationOrchestrator = notificationOrchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDeadlines(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Deadline notification cycle failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task ProcessDeadlines(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dueDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
        var daysRemaining = (dueDate.Date - now.Date).Days;
        var (eventType, priority, roles, mandatory) = ResolveDeadlineEnvelope(daysRemaining);

        if (eventType is null)
        {
            return;
        }

        var periodLabel = new DateTime(now.Year, now.Month, 1).ToString("MMM yyyy");

        var activeInstitutions = await _db.Institutions
            .Where(i => i.IsActive)
            .ToListAsync(ct);

        var templates = await _db.ReturnTemplates
            .Where(t => t.Versions.Any(v => v.Status == TemplateStatus.Published))
            .ToListAsync(ct);

        foreach (var institution in activeInstitutions)
        {
            foreach (var template in templates)
            {
                var hasSubmission = await _db.Submissions.AnyAsync(s =>
                    s.TenantId == institution.TenantId &&
                    s.InstitutionId == institution.Id &&
                    s.ReturnCode == template.ReturnCode &&
                    s.SubmittedAt.Year == now.Year &&
                    s.SubmittedAt.Month == now.Month &&
                    s.Status != SubmissionStatus.Rejected &&
                    s.Status != SubmissionStatus.ApprovalRejected,
                    ct);

                if (hasSubmission)
                {
                    continue;
                }

                var alreadySent = await IsAlreadySentToday(
                    institution.TenantId,
                    institution.Id,
                    eventType,
                    template.ReturnCode,
                    periodLabel,
                    ct);

                if (alreadySent)
                {
                    continue;
                }

                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstitutionName"] = institution.InstitutionName,
                    ["ModuleName"] = template.Name,
                    ["ReturnCode"] = template.ReturnCode,
                    ["PeriodLabel"] = periodLabel,
                    ["Deadline"] = dueDate.ToString("dd MMM yyyy"),
                    ["DaysRemaining"] = daysRemaining.ToString(),
                    ["DaysOverdue"] = Math.Max(0, -daysRemaining).ToString(),
                    ["SubmitUrl"] = "https://portal.regos.app/submit"
                };

                var title = eventType == NotificationEvents.DeadlineOverdue
                    ? $"OVERDUE: {template.ReturnCode} return past deadline"
                    : $"{Math.Max(0, daysRemaining)} day(s) until deadline - {template.ReturnCode}";

                var message = eventType == NotificationEvents.DeadlineOverdue
                    ? $"{template.Name} for {periodLabel} is overdue. Submit immediately."
                    : $"{template.Name} for {periodLabel} is due on {dueDate:dd MMM yyyy}.";

                await _notificationOrchestrator.Notify(new NotificationRequest
                {
                    TenantId = institution.TenantId,
                    EventType = eventType,
                    Title = title,
                    Message = message,
                    Priority = priority,
                    IsMandatory = mandatory,
                    ActionUrl = "/submit",
                    RecipientInstitutionId = institution.Id,
                    RecipientRoles = roles,
                    Data = data
                }, ct);
            }
        }
    }

    private async Task<bool> IsAlreadySentToday(
        Guid tenantId,
        int institutionId,
        string eventType,
        string returnCode,
        string periodLabel,
        CancellationToken ct)
    {
        var todayStart = DateTime.UtcNow.Date;
        return await _db.PortalNotifications.AnyAsync(n =>
            n.TenantId == tenantId &&
            n.InstitutionId == institutionId &&
            n.EventType == eventType &&
            n.CreatedAt >= todayStart &&
            n.Metadata != null &&
            n.Metadata.Contains(returnCode) &&
            n.Metadata.Contains(periodLabel), ct);
    }

    private static (string? EventType, NotificationPriority Priority, List<string> Roles, bool Mandatory)
        ResolveDeadlineEnvelope(int daysRemaining)
    {
        return daysRemaining switch
        {
            30 => (NotificationEvents.DeadlineT30, NotificationPriority.Low, new List<string> { "Maker" }, false),
            14 => (NotificationEvents.DeadlineT14, NotificationPriority.Normal, new List<string> { "Maker", "Admin" }, false),
            7 => (NotificationEvents.DeadlineT7, NotificationPriority.High, new List<string> { "Maker", "Checker", "Admin" }, false),
            3 => (NotificationEvents.DeadlineT3, NotificationPriority.High, new List<string> { "Maker", "Checker", "Admin", "Viewer", "Approver" }, false),
            1 => (NotificationEvents.DeadlineT1, NotificationPriority.Critical, new List<string> { "Maker", "Checker", "Admin", "Viewer", "Approver" }, false),
            < 0 => (NotificationEvents.DeadlineOverdue, NotificationPriority.Critical, new List<string> { "Maker", "Checker", "Admin", "Viewer", "Approver" }, true),
            _ => (null, NotificationPriority.Low, new List<string>(), false)
        };
    }
}
