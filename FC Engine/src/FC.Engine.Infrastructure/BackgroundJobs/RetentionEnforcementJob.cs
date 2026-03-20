using FC.Engine.Infrastructure.Audit;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FC.Engine.Infrastructure.Services;

namespace FC.Engine.Infrastructure.BackgroundJobs;

public class RetentionEnforcementJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetentionEnforcementJob> _logger;
    private readonly PrivacyComplianceOptions _options;

    public RetentionEnforcementJob(
        IServiceProvider serviceProvider,
        IOptions<PrivacyComplianceOptions> options,
        ILogger<RetentionEnforcementJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelayToNextMonthlyRun();
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                await EnforceRetention(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Retention enforcement cycle failed");
            }
        }
    }

    internal async Task EnforceRetention(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();

        var cutoff = DateTime.UtcNow.AddYears(-Math.Max(1, _options.RetentionYears));
        var expiredSubmissions = await db.Submissions
            .Where(x => x.CreatedAt < cutoff && !x.IsRetentionAnonymised)
            .OrderBy(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        if (expiredSubmissions.Count == 0)
        {
            return;
        }

        var touchedTenants = new HashSet<Guid>();
        foreach (var submission in expiredSubmissions)
        {
            await AnonymiseSubmissionPii(db, submission.Id, submission.TenantId, ct);
            submission.IsRetentionAnonymised = true;
            touchedTenants.Add(submission.TenantId);
        }

        foreach (var tenantId in touchedTenants)
        {
            await RehashTenantAuditTrail(db, tenantId, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task AnonymiseSubmissionPii(MetadataDbContext db, int submissionId, Guid tenantId, CancellationToken ct)
    {
        var changes = await db.FieldChangeHistory
            .Where(x => x.TenantId == tenantId && x.SubmissionId == submissionId)
            .ToListAsync(ct);
        foreach (var change in changes)
        {
            change.ChangedBy = "ANONYMISED";
        }

        var audits = await db.AuditLog
            .Where(x => x.TenantId == tenantId && x.EntityType == "Submission" && x.EntityId == submissionId)
            .ToListAsync(ct);
        foreach (var audit in audits)
        {
            audit.PerformedBy = "ANONYMISED";
            audit.IpAddress = null;
        }
    }

    private static async Task RehashTenantAuditTrail(MetadataDbContext db, Guid tenantId, CancellationToken ct)
    {
        var entries = await db.AuditLog
            .Where(x => x.TenantId == tenantId && x.SequenceNumber > 0)
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(ct);

        var previousHash = "GENESIS";
        foreach (var entry in entries)
        {
            entry.PreviousHash = previousHash;
            entry.Hash = AuditLogger.ComputeHash(
                entry.SequenceNumber,
                entry.EntityType,
                entry.PerformedAt,
                entry.TenantId,
                entry.PerformedBy,
                entry.EntityType,
                entry.EntityId,
                entry.Action,
                entry.OldValues,
                entry.NewValues,
                entry.PreviousHash);
            previousHash = entry.Hash;
        }
    }

    private static TimeSpan ComputeDelayToNextMonthlyRun()
    {
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, 1, 1, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return next - now;
    }
}
