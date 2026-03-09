using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Services;

namespace FC.Engine.Api.Endpoints;

public static class ComplianceEndpoints
{
    public static void MapComplianceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/compliance")
            .WithTags("Compliance Health Score")
            .RequireAuthorization();

        // ── Institution-level endpoints ──

        group.MapGet("/score/{tenantId:guid}", async (
            Guid tenantId,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var score = await chsService.GetCurrentScore(tenantId, ct);
            return Results.Ok(new
            {
                score.TenantId,
                score.TenantName,
                score.LicenceType,
                score.OverallScore,
                Rating = ComplianceHealthService.RatingLabel(score.Rating),
                RatingDescription = ComplianceHealthService.RatingDescription(score.Rating),
                Trend = score.Trend.ToString(),
                TrendArrow = ComplianceHealthService.TrendArrow(score.Trend),
                Pillars = new
                {
                    FilingTimeliness = new { Score = score.FilingTimeliness, Weight = "25%" },
                    DataQuality = new { Score = score.DataQuality, Weight = "25%" },
                    RegulatoryCapital = new { Score = score.RegulatoryCapital, Weight = "20%" },
                    AuditGovernance = new { Score = score.AuditGovernance, Weight = "15%" },
                    Engagement = new { Score = score.Engagement, Weight = "15%" }
                },
                score.ComputedAt,
                score.PeriodLabel
            });
        })
        .WithName("GetComplianceScore")
        .WithSummary("Get current CHS score with pillar breakdown for a tenant")
        .Produces(200);

        group.MapGet("/trend/{tenantId:guid}", async (
            Guid tenantId,
            int? periods,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var p = periods ?? 12;
            if (p <= 0) p = 12;
            if (p > 52) p = 52;
            var trend = await chsService.GetTrend(tenantId, p, ct);
            return Results.Ok(new
            {
                trend.TenantId,
                OverallTrend = trend.OverallTrend.ToString(),
                trend.ConsecutiveDeclines,
                Snapshots = trend.Snapshots.Select(s => new
                {
                    s.PeriodLabel,
                    s.Date,
                    s.OverallScore,
                    Rating = ComplianceHealthService.RatingLabel(s.Rating),
                    s.FilingTimeliness,
                    s.DataQuality,
                    s.RegulatoryCapital,
                    s.AuditGovernance,
                    s.Engagement
                })
            });
        })
        .WithName("GetComplianceTrend")
        .WithSummary("Get weekly CHS trend data for a tenant")
        .Produces(200);

        group.MapGet("/dashboard/{tenantId:guid}", async (
            Guid tenantId,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var result = await chsService.GetDashboard(tenantId, ct);
            return Results.Ok(result);
        })
        .Produces<ChsDashboardData>()
        .WithName("GetComplianceDashboard")
        .WithSummary("Get full CHS dashboard (score + pillars + trend + peers + alerts)");

        group.MapGet("/peers/{tenantId:guid}", async (
            Guid tenantId,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var result = await chsService.GetPeerComparison(tenantId, ct);
            return Results.Ok(result);
        })
        .Produces<ChsPeerComparison>()
        .WithName("GetCompliancePeers")
        .WithSummary("Get anonymized peer comparison for a tenant");

        group.MapGet("/alerts/{tenantId:guid}", async (
            Guid tenantId,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var result = await chsService.GetAlerts(tenantId, ct);
            return Results.Ok(result);
        })
        .Produces<List<ChsAlert>>()
        .WithName("GetComplianceAlerts")
        .WithSummary("Get active compliance alerts for a tenant");

        // ── Regulator-level endpoints ──

        group.MapGet("/sector/{regulatorCode}", async (
            string regulatorCode,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var result = await chsService.GetSectorSummary(regulatorCode, ct);
            return Results.Ok(result);
        })
        .Produces<SectorChsSummary>()
        .WithName("GetSectorSummary")
        .WithSummary("Get sector-wide CHS summary for a regulator");

        group.MapGet("/watchlist/{regulatorCode}", async (
            string regulatorCode,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var result = await chsService.GetWatchList(regulatorCode, ct);
            return Results.Ok(result);
        })
        .Produces<List<ChsWatchListItem>>()
        .WithName("GetWatchList")
        .WithSummary("Get institutions on CHS watch list (score < 60 or 3+ declines)");

        group.MapGet("/heatmap/{regulatorCode}", async (
            string regulatorCode,
            IComplianceHealthService chsService,
            CancellationToken ct) =>
        {
            var result = await chsService.GetSectorHeatmap(regulatorCode, ct);
            return Results.Ok(result);
        })
        .Produces<List<ChsHeatmapItem>>()
        .WithName("GetSectorHeatmap")
        .WithSummary("Get heatmap of all institutions ranked by pillar scores");
    }
}
