using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Audit;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

/// <summary>
/// RG-14: Nightly audit chain integrity verification.
/// Runs at 3:00 AM WAT (2:00 AM UTC) daily.
/// For each tenant, verifies that the hash chain is intact and no entries have been tampered with.
/// </summary>
public class AuditIntegrityVerificationJob : BackgroundService
{
    private static readonly TimeSpan RunTimeUtc = TimeSpan.FromHours(2); // 3:00 AM WAT = 2:00 AM UTC

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditIntegrityVerificationJob> _logger;

    public AuditIntegrityVerificationJob(
        IServiceProvider serviceProvider,
        ILogger<AuditIntegrityVerificationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelayUntilNextRun();
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
                await RunVerification(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Audit integrity verification cycle failed");
            }
        }
    }

    internal async Task RunVerification(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationOrchestrator>();

        // Get all tenants that have audit entries with hash chains
        var tenantIds = await db.AuditLog
            .Where(a => a.SequenceNumber > 0)
            .Select(a => a.TenantId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tenantId in tenantIds)
        {
            if (!tenantId.HasValue) continue;
            await VerifyTenantChain(db, notifier, tenantId.Value, ct);
        }
    }

    internal async Task VerifyTenantChain(
        MetadataDbContext db,
        INotificationOrchestrator notifier,
        Guid tenantId,
        CancellationToken ct)
    {
        var entries = await db.AuditLog
            .Where(a => a.TenantId == tenantId && a.SequenceNumber > 0)
            .OrderBy(a => a.SequenceNumber)
            .ToListAsync(ct);

        if (entries.Count == 0) return;

        var previousHash = "GENESIS";
        var breachesFound = 0;

        foreach (var entry in entries)
        {
            // Verify chain linkage
            if (entry.PreviousHash != previousHash)
            {
                _logger.LogCritical(
                    "Audit chain break: Tenant {TenantId}, Seq {Seq} — expected PreviousHash {Expected}, got {Actual}",
                    tenantId, entry.SequenceNumber, previousHash, entry.PreviousHash);
                breachesFound++;
            }

            // Recompute hash and verify
            var recomputed = AuditLogger.ComputeHash(
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

            if (entry.Hash != recomputed)
            {
                _logger.LogCritical(
                    "Audit hash mismatch: Tenant {TenantId}, Seq {Seq} — stored {Stored}, computed {Computed}",
                    tenantId, entry.SequenceNumber, entry.Hash, recomputed);
                breachesFound++;
            }

            previousHash = entry.Hash;
        }

        if (breachesFound > 0)
        {
            await notifier.Notify(new NotificationRequest
            {
                TenantId = tenantId,
                EventType = "AuditIntegrityBreach",
                Title = "Audit Trail Integrity Breach Detected",
                Message = $"{breachesFound} integrity breach(es) detected in the audit trail. " +
                          "This may indicate unauthorized data modification.",
                Priority = NotificationPriority.Critical,
                IsMandatory = true,
                RecipientRoles = ["Admin"]
            }, ct);

            _logger.LogCritical(
                "Audit integrity: {Count} breach(es) for tenant {TenantId}. Alert sent.",
                breachesFound, tenantId);
        }
        else
        {
            _logger.LogInformation(
                "Audit integrity: Tenant {TenantId} chain verified — {Count} entries, no breaches",
                tenantId, entries.Count);
        }
    }

    private static TimeSpan ComputeDelayUntilNextRun()
    {
        var now = DateTime.UtcNow;
        var todayRun = now.Date.Add(RunTimeUtc);
        var nextRun = now < todayRun ? todayRun : todayRun.AddDays(1);
        return nextRun - now;
    }
}
