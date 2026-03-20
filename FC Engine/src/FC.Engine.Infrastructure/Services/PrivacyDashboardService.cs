using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FC.Engine.Infrastructure.Services;

public class PrivacyDashboardService : IPrivacyDashboardService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly MetadataDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly PrivacyComplianceOptions _options;

    public PrivacyDashboardService(
        MetadataDbContext db,
        IMemoryCache cache,
        IOptions<PrivacyComplianceOptions> options)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
    }

    public Task<DpoDashboardData> GetDashboard(Guid? tenantId, CancellationToken ct = default)
    {
        var tenantKey = tenantId?.ToString() ?? "platform";
        return _cache.GetOrCreateAsync($"privacy:dpo:{tenantKey}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await BuildDashboard(tenantId, ct);
        })!;
    }

    private async Task<DpoDashboardData> BuildDashboard(Guid? tenantId, CancellationToken ct)
    {
        var dsarQuery = _db.DataSubjectRequests.AsNoTracking();
        var breachQuery = _db.DataBreachIncidents.AsNoTracking();
        var consentQuery = _db.ConsentRecords.AsNoTracking();
        var submissionQuery = _db.Submissions.AsNoTracking();

        if (tenantId.HasValue)
        {
            dsarQuery = dsarQuery.Where(x => x.TenantId == tenantId.Value);
            breachQuery = breachQuery.Where(x => x.TenantId == tenantId.Value);
            consentQuery = consentQuery.Where(x => x.TenantId == tenantId.Value);
            submissionQuery = submissionQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var dsars = await dsarQuery.ToListAsync(ct);
        var breaches = await breachQuery.ToListAsync(ct);
        var activities = await _db.DataProcessingActivities
            .AsNoTracking()
            .ToListAsync(ct);

        var completedDsars = dsars
            .Where(x => x.Status == DataSubjectRequestStatus.Completed && x.CompletedAt.HasValue)
            .ToList();
        var withinSla = completedDsars.Count == 0
            ? 0
            : (decimal)completedDsars.Count(x => x.CompletedAt!.Value <= x.DueDate) / completedDsars.Count * 100m;

        var activeInstitutionUsersQuery = _db.InstitutionUsers.AsNoTracking().Where(x => x.IsActive);
        var activePortalUsersQuery = _db.PortalUsers.AsNoTracking().Where(x => x.IsActive);
        if (tenantId.HasValue)
        {
            activeInstitutionUsersQuery = activeInstitutionUsersQuery.Where(x => x.TenantId == tenantId.Value);
            activePortalUsersQuery = activePortalUsersQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var activeInstitutionUsers = await activeInstitutionUsersQuery
            .Select(x => new { x.Id, UserType = "InstitutionUser" })
            .ToListAsync(ct);
        var activePortalUsers = await activePortalUsersQuery
            .Select(x => new { x.Id, UserType = "PortalUser" })
            .ToListAsync(ct);
        var activeUsers = activeInstitutionUsers
            .Select(x => (x.Id, x.UserType))
            .Concat(activePortalUsers.Select(x => (x.Id, x.UserType)))
            .ToList();

        var latestConsents = await consentQuery
            .Where(x => x.ConsentType == ConsentType.DataProcessing)
            .GroupBy(x => new { x.UserId, x.UserType })
            .Select(g => g.OrderByDescending(x => x.ConsentedAt).FirstOrDefault())
            .ToListAsync(ct);

        var consentedUsers = latestConsents
            .Where(x =>
                x != null &&
                x.ConsentGiven &&
                !x.WithdrawnAt.HasValue &&
                x.PolicyVersion == _options.PolicyVersion)
            .Select(x => (x!.UserId, x.UserType))
            .ToHashSet();

        var consentRate = activeUsers.Count == 0
            ? 0
            : decimal.Round((decimal)activeUsers.Count(consentedUsers.Contains) / activeUsers.Count * 100m, 2);

        var openBreaches = breaches.Count(x => x.Status != DataBreachStatus.Closed);
        var pendingNitda = breaches.Count(x =>
            x.Severity is DataBreachSeverity.HIGH or DataBreachSeverity.CRITICAL &&
            !x.NitdaNotifiedAt.HasValue &&
            x.Status != DataBreachStatus.Closed);

        var now = DateTime.UtcNow;
        var countdowns = breaches
            .Where(x => x.NitdaNotificationDeadline.HasValue && !x.NitdaNotifiedAt.HasValue)
            .OrderBy(x => x.NitdaNotificationDeadline)
            .Take(10)
            .Select(x => new BreachCountdownItem
            {
                IncidentId = x.Id,
                Title = x.Title,
                Severity = x.Severity,
                DetectedAt = x.DetectedAt,
                NitdaNotificationDeadline = x.NitdaNotificationDeadline,
                HoursRemaining = (x.NitdaNotificationDeadline!.Value - now).TotalHours
            })
            .ToList();

        var activityCounts = activities
            .GroupBy(x => x.ModuleCode)
            .Select(g => new ProcessingActivityCountItem
            {
                ModuleCode = g.Key,
                ActivityCount = g.Count()
            })
            .OrderBy(x => x.ModuleCode)
            .ToList();

        var piaStatuses = activities
            .GroupBy(x => x.ModuleCode)
            .Select(g =>
            {
                var total = g.Count();
                var reviewed = g.Count(x => !x.IsAutoGenerated);
                var status = reviewed == 0 ? "Pending" : reviewed < total ? "InProgress" : "Complete";
                return new PrivacyImpactAssessmentItem
                {
                    ModuleCode = g.Key,
                    Status = status,
                    ActivityCount = total
                };
            })
            .OrderBy(x => x.ModuleCode)
            .ToList();

        var cutoff = now.AddYears(-Math.Max(1, _options.RetentionYears));
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var retentionDueThisMonth = await submissionQuery
            .Where(x =>
                !x.IsRetentionAnonymised &&
                x.CreatedAt >= cutoff.AddMonths(-1) &&
                x.CreatedAt < cutoff.AddMonths(1))
            .CountAsync(ct);

        return new DpoDashboardData
        {
            GeneratedAt = now,
            DsarMetrics = dsars
                .GroupBy(x => new { x.RequestType, x.Status })
                .Select(g => new DsarMetricItem
                {
                    RequestType = g.Key.RequestType,
                    Status = g.Key.Status,
                    Count = g.Count()
                })
                .OrderBy(x => x.RequestType)
                .ThenBy(x => x.Status)
                .ToList(),
            DsarSlaWithinDeadlinePercent = decimal.Round(withinSla, 2),
            ConsentRateCurrentPolicyPercent = consentRate,
            OpenBreachCount = openBreaches,
            PendingNitdaNotifications = pendingNitda,
            BreachCountdowns = countdowns,
            ActivityCountsByModule = activityCounts,
            RetentionDueThisMonth = retentionDueThisMonth,
            PrivacyImpactStatusByModule = piaStatuses
        };
    }
}
