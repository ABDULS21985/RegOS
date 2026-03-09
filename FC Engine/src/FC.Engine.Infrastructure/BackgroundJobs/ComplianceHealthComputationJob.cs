using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Events;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

/// <summary>
/// RG-32: Weekly CHS computation job.
/// For each active tenant: compute CHS, persist snapshot, publish event if rating changed.
/// </summary>
public class ComplianceHealthComputationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(168); // Weekly

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComplianceHealthComputationJob> _logger;

    public ComplianceHealthComputationJob(
        IServiceProvider serviceProvider,
        ILogger<ComplianceHealthComputationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let the application warm up
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        // Run immediately on first iteration, then weekly
        do
        {
            try
            {
                await ComputeAllScores(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "CHS computation job failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ComputeAllScores(CancellationToken ct)
    {
        _logger.LogInformation("Starting weekly CHS computation");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        var chsService = scope.ServiceProvider.GetRequiredService<IComplianceHealthService>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

        var activeTenants = await db.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => t.TenantId)
            .ToListAsync(ct);

        _logger.LogInformation("Computing CHS for {Count} active tenants", activeTenants.Count);

        var computed = 0;
        var failed = 0;

        foreach (var tenantId in activeTenants)
        {
            try
            {
                var score = await chsService.GetCurrentScore(tenantId, ct);

                // Get previous snapshot for comparison
                var previousSnapshot = await db.ChsScoreSnapshots
                    .Where(s => s.TenantId == tenantId)
                    .OrderByDescending(s => s.ComputedAt)
                    .FirstOrDefaultAsync(ct);

                // Persist new snapshot
                var snapshot = new ChsScoreSnapshot
                {
                    TenantId = tenantId,
                    PeriodLabel = score.PeriodLabel,
                    ComputedAt = score.ComputedAt,
                    OverallScore = score.OverallScore,
                    Rating = (int)score.Rating,
                    FilingTimeliness = score.FilingTimeliness,
                    DataQuality = score.DataQuality,
                    RegulatoryCapital = score.RegulatoryCapital,
                    AuditGovernance = score.AuditGovernance,
                    Engagement = score.Engagement
                };

                db.ChsScoreSnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);

                // Publish event if rating band changed
                var previousRating = previousSnapshot is not null
                    ? ComplianceHealthService.ToRating(previousSnapshot.OverallScore)
                    : score.Rating;

                if (previousSnapshot is not null && previousRating != score.Rating)
                {
                    var evt = new ComplianceScoreChangedEvent(
                        TenantId: tenantId,
                        PreviousScore: previousSnapshot.OverallScore,
                        NewScore: score.OverallScore,
                        Rating: ComplianceHealthService.RatingLabel(score.Rating),
                        Trend: score.Trend.ToString(),
                        ComputedAt: score.ComputedAt,
                        OccurredAt: DateTime.UtcNow,
                        CorrelationId: Guid.NewGuid());

                    await eventPublisher.PublishAsync(evt, ct);

                    _logger.LogInformation(
                        "CHS rating changed for tenant {TenantId}: {PrevRating} → {NewRating} ({PrevScore} → {NewScore})",
                        tenantId,
                        ComplianceHealthService.RatingLabel(previousRating),
                        ComplianceHealthService.RatingLabel(score.Rating),
                        previousSnapshot.OverallScore,
                        score.OverallScore);
                }

                computed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute CHS for tenant {TenantId}", tenantId);
                failed++;
            }
        }

        _logger.LogInformation(
            "CHS computation complete: {Computed} succeeded, {Failed} failed out of {Total} tenants",
            computed, failed, activeTenants.Count);
    }
}
