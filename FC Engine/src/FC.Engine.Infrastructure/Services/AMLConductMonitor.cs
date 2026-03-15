using System.Text.Json;
using Dapper;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public sealed class AMLConductMonitor : IAMLConductMonitor
{
    private readonly IDbConnectionFactory _db;
    private readonly IRegulatorTenantResolver _tenantResolver;
    private readonly ILogger<AMLConductMonitor> _log;

    public AMLConductMonitor(
        IDbConnectionFactory db,
        IRegulatorTenantResolver tenantResolver,
        ILogger<AMLConductMonitor> log)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _log = log;
    }

    public async Task<int> DetectLowSTRFilersAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "AML_LOW_STR", context.RegulatorCode);
        var zThreshold = RuleParamLoader.Get(parameters, "STRZScoreThreshold", -2m);

        var lowFilers = await conn.QueryAsync<LowStrRow>(
            """
            WITH PeerStats AS (
                SELECT InstitutionType,
                       AVG(CAST(STRFilingCount AS FLOAT)) AS PeerMean,
                       STDEV(CAST(STRFilingCount AS FLOAT)) AS PeerStDev
                FROM dbo.AMLConductMetrics
                WHERE TenantId = @TenantId
                  AND RegulatorCode = @RegulatorCode
                  AND PeriodCode = @PeriodCode
                GROUP BY InstitutionType
            )
            SELECT m.InstitutionId,
                   m.InstitutionType,
                   m.STRFilingCount,
                   ps.PeerMean,
                   ps.PeerStDev,
                   CASE
                       WHEN ISNULL(ps.PeerStDev, 0) > 0
                       THEN (CAST(m.STRFilingCount AS FLOAT) - ps.PeerMean) / ps.PeerStDev
                       ELSE 0
                   END AS ZScore
            FROM dbo.AMLConductMetrics m
            INNER JOIN PeerStats ps ON ps.InstitutionType = m.InstitutionType
            WHERE m.TenantId = @TenantId
              AND m.RegulatorCode = @RegulatorCode
              AND m.PeriodCode = @PeriodCode
              AND (
                    (ps.PeerStDev > 0
                     AND ((CAST(m.STRFilingCount AS FLOAT) - ps.PeerMean) / ps.PeerStDev) <= @ZThreshold)
                    OR (ps.PeerMean >= 5
                        AND CAST(m.STRFilingCount AS FLOAT) <= ps.PeerMean * 0.25)
                  )
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                ZThreshold = zThreshold
            });

        var count = 0;
        foreach (var row in lowFilers)
        {
            var extremelyLowRelativeToPeers = row.PeerMean > 0 && row.STRFilingCount <= row.PeerMean * 0.15;
            var severity = row.ZScore <= (double)zThreshold * 1.5 || extremelyLowRelativeToPeers
                ? "CRITICAL"
                : "HIGH";
            var evidence = JsonSerializer.Serialize(new AMLConductEvidence(
                Convert.ToDecimal(row.ZScore),
                Convert.ToDecimal(row.PeerMean),
                row.STRFilingCount,
                0m,
                0));

            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "AML_LOW_STR",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "AML",
                $"Low STR filing intensity detected for {row.InstitutionType} institution",
                $"Institution filed {row.STRFilingCount} STRs against a peer average of {row.PeerMean:F1} (z-score {row.ZScore:F2}).",
                evidence,
                periodCode,
                runId);

            count++;
        }

        _log.LogInformation(
            "AML low STR surveillance completed. Regulator={RegulatorCode} Period={PeriodCode} Alerts={Count}",
            context.RegulatorCode,
            periodCode,
            count);

        return count;
    }

    public async Task<int> DetectStructuringPatternsAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "AML_STRUCTURING", context.RegulatorCode);
        var ctrThreshold = RuleParamLoader.Get(parameters, "CTRThresholdNGN", 5_000_000m);
        var windowDays = (int)RuleParamLoader.Get(parameters, "StructuringWindowDays", 3m);

        var rows = await conn.QueryAsync<StructuringRow>(
            """
            WITH PeerStats AS (
                SELECT InstitutionType,
                       AVG(CAST(StructuringAlertCount AS FLOAT)) AS PeerMean
                FROM dbo.AMLConductMetrics
                WHERE TenantId = @TenantId
                  AND RegulatorCode = @RegulatorCode
                  AND PeriodCode = @PeriodCode
                GROUP BY InstitutionType
            )
            SELECT m.InstitutionId,
                   m.InstitutionType,
                   m.StructuringAlertCount,
                   ps.PeerMean,
                   CAST(m.StructuringAlertCount / NULLIF(ps.PeerMean, 0) AS DECIMAL(12,2)) AS PeerRatio
            FROM dbo.AMLConductMetrics m
            INNER JOIN PeerStats ps ON ps.InstitutionType = m.InstitutionType
            WHERE m.TenantId = @TenantId
              AND m.RegulatorCode = @RegulatorCode
              AND m.PeriodCode = @PeriodCode
              AND m.StructuringAlertCount >= 5
              AND m.StructuringAlertCount / NULLIF(ps.PeerMean, 0) >= 3.0
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode
            });

        var count = 0;
        foreach (var row in rows)
        {
            var severity = row.PeerRatio >= 5m ? "CRITICAL" : "HIGH";
            var evidence = JsonSerializer.Serialize(new AMLConductEvidence(
                0m,
                Convert.ToDecimal(row.PeerMean),
                0,
                0m,
                row.StructuringAlertCount));

            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "AML_STRUCTURING",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "AML",
                $"Elevated structuring alerts detected ({row.StructuringAlertCount} alerts)",
                $"Structuring alert count was {row.PeerRatio:F1}x peer behaviour within the {windowDays}-day review window around the NGN {ctrThreshold:N0} CTR benchmark.",
                evidence,
                periodCode,
                runId);

            count++;
        }

        return count;
    }

    public async Task<int> DetectTFSIneffectivenessAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "AML_TFS_FALSE_POS", context.RegulatorCode);

        var maxRate = RuleParamLoader.Get(parameters, "TFSFalsePositiveRateMax", 0.95m);
        var minRate = RuleParamLoader.Get(parameters, "TFSFalsePositiveRateMin", 0.05m);

        var rows = await conn.QueryAsync<TfsRow>(
            """
            SELECT InstitutionId,
                   TFSFalsePositiveRate,
                   TFSTruePositiveCount,
                   TFSScreeningCount
            FROM dbo.AMLConductMetrics
            WHERE TenantId = @TenantId
              AND RegulatorCode = @RegulatorCode
              AND PeriodCode = @PeriodCode
              AND TFSFalsePositiveRate IS NOT NULL
              AND TFSScreeningCount >= 100
              AND (
                    TFSFalsePositiveRate > @MaxRate
                    OR TFSFalsePositiveRate < @MinRate
                  )
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                MaxRate = maxRate,
                MinRate = minRate
            });

        var count = 0;
        foreach (var row in rows)
        {
            var rate = row.TFSFalsePositiveRate ?? 0m;
            var tooHigh = rate > maxRate;
            var evidence = JsonSerializer.Serialize(new AMLConductEvidence(
                0m,
                0m,
                0,
                rate,
                0));

            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "AML_TFS_INEFFECTIVE",
                context.RegulatorCode,
                row.InstitutionId,
                "HIGH",
                "AML",
                tooHigh
                    ? $"Sanctions screening false-positive rate of {rate:P1} indicates weak tuning"
                    : $"Sanctions screening false-positive rate of {rate:P1} indicates possible non-screening",
                tooHigh
                    ? $"False-positive rate exceeded the configured ceiling of {maxRate:P0}, suggesting ineffective sanctions/TFS screening quality."
                    : $"False-positive rate fell below the configured floor of {minRate:P0}, suggesting the institution may not be screening properly.",
                evidence,
                periodCode,
                runId);

            count++;
        }

        return count;
    }

    private sealed record LowStrRow(
        int InstitutionId,
        string InstitutionType,
        int STRFilingCount,
        double PeerMean,
        double PeerStDev,
        double ZScore
    );

    private sealed record StructuringRow(
        int InstitutionId,
        string InstitutionType,
        int StructuringAlertCount,
        double PeerMean,
        decimal PeerRatio
    );

    private sealed record TfsRow(
        int InstitutionId,
        decimal? TFSFalsePositiveRate,
        int TFSTruePositiveCount,
        int TFSScreeningCount
    );
}
