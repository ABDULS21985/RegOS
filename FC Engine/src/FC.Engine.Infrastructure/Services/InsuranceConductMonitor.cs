using System.Text.Json;
using Dapper;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public sealed class InsuranceConductMonitor : IInsuranceConductMonitor
{
    private readonly IDbConnectionFactory _db;
    private readonly IRegulatorTenantResolver _tenantResolver;
    private readonly ILogger<InsuranceConductMonitor> _log;

    public InsuranceConductMonitor(
        IDbConnectionFactory db,
        IRegulatorTenantResolver tenantResolver,
        ILogger<InsuranceConductMonitor> log)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _log = log;
    }

    public async Task<int> DetectClaimsSuppressionAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "INS_CLAIMS_SUPPRESSION", context.RegulatorCode, "INSURER");

        var minClaimsRatio = RuleParamLoader.Get(parameters, "MinClaimsRatioPct", 30m);
        var peerDeviation = RuleParamLoader.Get(parameters, "PeerDeviation", 20m);

        var peerAvg = await conn.ExecuteScalarAsync<decimal?>(
            """
            SELECT CAST(AVG(ClaimsRatio) AS DECIMAL(8,4))
            FROM dbo.InsuranceConductMetrics
            WHERE TenantId = @TenantId
              AND RegulatorCode = @RegulatorCode
              AND PeriodCode = @PeriodCode
              AND ClaimsRatio IS NOT NULL
            """,
            new { TenantId = context.TenantId, RegulatorCode = context.RegulatorCode, PeriodCode = periodCode }) ?? 0m;

        var suppressors = await conn.QueryAsync<ClaimsSuppressionRow>(
            """
            SELECT InstitutionId,
                   ClaimsRatio,
                   GrossClaimsNGN,
                   GrossPremiumNGN,
                   ComplaintCount,
                   ClaimsDenialRate
            FROM dbo.InsuranceConductMetrics
            WHERE TenantId = @TenantId
              AND RegulatorCode = @RegulatorCode
              AND PeriodCode = @PeriodCode
              AND (
                    ClaimsRatio < @MinClaimsRatio
                    OR @PeerAvg - ISNULL(ClaimsRatio, 0) > @PeerDeviation
                  )
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                MinClaimsRatio = minClaimsRatio,
                PeerAvg = peerAvg,
                PeerDeviation = peerDeviation
            });

        var count = 0;
        foreach (var row in suppressors)
        {
            var claimsRatio = row.ClaimsRatio ?? 0m;
            var deviation = peerAvg - claimsRatio;
            var materiallyBelowFloor = claimsRatio <= minClaimsRatio * 0.75m;
            var materiallyBelowPeers = claimsRatio < minClaimsRatio && deviation >= peerDeviation;
            var severity = materiallyBelowFloor || materiallyBelowPeers || deviation > peerDeviation * 2
                ? "CRITICAL"
                : deviation > peerDeviation ? "HIGH" : "MEDIUM";

            var evidence = JsonSerializer.Serialize(new InsuranceConductEvidence(
                claimsRatio,
                peerAvg,
                deviation,
                0m,
                0m));

            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "INS_CLAIMS_SUPPRESSION",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "INSURANCE",
                $"Claims suppression indicator - claims ratio {claimsRatio:F1}% versus peer average {peerAvg:F1}%",
                $"Claims ratio was {deviation:F1} percentage points below peer behaviour and below the surveillance floor of {minClaimsRatio:F1}%.",
                evidence,
                periodCode,
                runId);

            count++;
        }

        _log.LogInformation(
            "Insurance claims suppression surveillance completed. Regulator={RegulatorCode} Period={PeriodCode} Alerts={Count}",
            context.RegulatorCode,
            periodCode,
            count);

        return count;
    }

    public async Task<int> DetectPremiumUnderReportingAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "INS_PREMIUM_UNDER", context.RegulatorCode, "INSURER");

        var gapThreshold = RuleParamLoader.Get(parameters, "PremiumGapThresholdPct", 15m);
        var rows = await conn.QueryAsync<PremiumGapRow>(
            """
            SELECT InstitutionId,
                   GrossPremiumReported,
                   GrossPremiumExpected,
                   PremiumUnderReportingGap,
                   CAST(PremiumUnderReportingGap * 100.0 / NULLIF(GrossPremiumExpected, 0) AS DECIMAL(12,2)) AS GapPct
            FROM dbo.InsuranceConductMetrics
            WHERE TenantId = @TenantId
              AND RegulatorCode = @RegulatorCode
              AND PeriodCode = @PeriodCode
              AND GrossPremiumExpected > 0
              AND PremiumUnderReportingGap > 0
              AND PremiumUnderReportingGap * 100.0 / NULLIF(GrossPremiumExpected, 0) > @GapThreshold
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                GapThreshold = gapThreshold
            });

        var count = 0;
        foreach (var row in rows)
        {
            var severity = row.GapPct > gapThreshold * 2 ? "HIGH" : "MEDIUM";
            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "INS_PREMIUM_UNDER",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "INSURANCE",
                $"Premium under-reporting gap of {row.GapPct:F1}% detected",
                $"Reported premium was NGN {row.GrossPremiumReported:N0} versus expected NGN {row.GrossPremiumExpected:N0}, leaving a gap of NGN {row.PremiumUnderReportingGap:N0}.",
                null,
                periodCode,
                runId);

            count++;
        }

        return count;
    }

    public async Task<int> DetectRelatedPartyReinsuranceAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "INS_RELATED_REINS", context.RegulatorCode, "INSURER");

        var capPct = RuleParamLoader.Get(parameters, "RelatedPartyReinsCapPct", 30m);
        var rows = await conn.QueryAsync<RelatedReinsRow>(
            """
            SELECT InstitutionId,
                   RelatedPartyReinsurancePct,
                   ReinsuranceRecoveries
            FROM dbo.InsuranceConductMetrics
            WHERE TenantId = @TenantId
              AND RegulatorCode = @RegulatorCode
              AND PeriodCode = @PeriodCode
              AND RelatedPartyReinsurancePct > @CapPct
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                CapPct = capPct
            });

        var count = 0;
        foreach (var row in rows)
        {
            var relatedPartyPct = row.RelatedPartyReinsurancePct ?? 0m;
            var severity = relatedPartyPct > capPct * 1.5m ? "CRITICAL" : "HIGH";
            var evidence = JsonSerializer.Serialize(new InsuranceConductEvidence(
                0m,
                0m,
                0m,
                0m,
                relatedPartyPct));

            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "INS_RELATED_REINS",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "INSURANCE",
                $"Related-party reinsurance exposure of {relatedPartyPct:F1}% exceeds conduct threshold",
                $"Related-party reinsurance exposure exceeded the configured cap of {capPct:F1}% with recoveries of NGN {row.ReinsuranceRecoveries:N0}.",
                evidence,
                periodCode,
                runId);

            count++;
        }

        return count;
    }

    private sealed record ClaimsSuppressionRow(
        int InstitutionId,
        decimal? ClaimsRatio,
        decimal? GrossClaimsNGN,
        decimal? GrossPremiumNGN,
        int ComplaintCount,
        decimal? ClaimsDenialRate
    );

    private sealed record PremiumGapRow(
        int InstitutionId,
        decimal? GrossPremiumReported,
        decimal? GrossPremiumExpected,
        decimal? PremiumUnderReportingGap,
        decimal GapPct
    );

    private sealed record RelatedReinsRow(
        int InstitutionId,
        decimal? RelatedPartyReinsurancePct,
        decimal? ReinsuranceRecoveries
    );
}
