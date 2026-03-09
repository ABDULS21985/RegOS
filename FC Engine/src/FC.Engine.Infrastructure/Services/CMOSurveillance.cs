using System.Text.Json;
using Dapper;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public sealed class CMOSurveillance : ICMOSurveillance
{
    private readonly IDbConnectionFactory _db;
    private readonly IRegulatorTenantResolver _tenantResolver;
    private readonly ILogger<CMOSurveillance> _log;

    public CMOSurveillance(
        IDbConnectionFactory db,
        IRegulatorTenantResolver tenantResolver,
        ILogger<CMOSurveillance> log)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _log = log;
    }

    public async Task<int> DetectUnusualTradingPatternsAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "CMO_UNUSUAL_TRADE", context.RegulatorCode, "CMO");

        var windowDays = (int)RuleParamLoader.Get(parameters, "PreAnnouncementWindowDays", 3m);
        var volumeMultiplier = RuleParamLoader.Get(parameters, "VolumeMultiplierThreshold", 5m);

        var suspicious = await conn.QueryAsync<SuspiciousTradeRow>(
            """
            WITH DailyTrades AS (
                SELECT t.InstitutionId,
                       t.SecurityCode,
                       MAX(t.SecurityName) AS SecurityName,
                       t.TradeDate,
                       SUM(t.Quantity) AS DailyQty
                FROM dbo.CMOTradeReports t
                WHERE t.TenantId = @TenantId
                  AND t.RegulatorCode = @RegulatorCode
                GROUP BY t.InstitutionId, t.SecurityCode, t.TradeDate
            ),
            PreAnnouncement AS (
                SELECT ca.Id AS AnnouncementId,
                       dt.InstitutionId,
                       dt.SecurityCode,
                       MAX(dt.SecurityName) AS SecurityName,
                       SUM(dt.DailyQty) AS PreWindowQty,
                       ca.AnnouncementDate
                FROM dbo.CorporateAnnouncements ca
                INNER JOIN DailyTrades dt
                    ON dt.SecurityCode = ca.SecurityCode
                   AND dt.TradeDate BETWEEN DATEADD(DAY, -@WindowDays, ca.AnnouncementDate)
                                        AND DATEADD(DAY, -1, ca.AnnouncementDate)
                WHERE ca.TenantId = @TenantId
                  AND ca.RegulatorCode = @RegulatorCode
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.CMOTradeReports t
                      WHERE t.TenantId = @TenantId
                        AND t.RegulatorCode = @RegulatorCode
                        AND t.PeriodCode = @PeriodCode
                        AND t.InstitutionId = dt.InstitutionId
                        AND t.SecurityCode = dt.SecurityCode
                        AND t.TradeDate BETWEEN DATEADD(DAY, -@WindowDays, ca.AnnouncementDate)
                                             AND DATEADD(DAY, -1, ca.AnnouncementDate)
                  )
                GROUP BY ca.Id, dt.InstitutionId, dt.SecurityCode, ca.AnnouncementDate
            ),
            Baseline AS (
                SELECT pa.AnnouncementId,
                       dt.InstitutionId,
                       dt.SecurityCode,
                       AVG(CAST(dt.DailyQty AS FLOAT)) AS AvgDailyQty
                FROM PreAnnouncement pa
                INNER JOIN DailyTrades dt
                    ON dt.InstitutionId = pa.InstitutionId
                   AND dt.SecurityCode = pa.SecurityCode
                   AND dt.TradeDate BETWEEN DATEADD(DAY, -30 - @WindowDays, pa.AnnouncementDate)
                                        AND DATEADD(DAY, -@WindowDays - 1, pa.AnnouncementDate)
                GROUP BY pa.AnnouncementId, dt.InstitutionId, dt.SecurityCode
                HAVING COUNT(*) >= 5
            )
            SELECT pa.InstitutionId,
                   pa.SecurityCode,
                   pa.SecurityName,
                   CAST(pa.PreWindowQty AS DECIMAL(18,2)) AS PreWindowQty,
                   CAST(b.AvgDailyQty AS DECIMAL(18,2)) AS AvgDailyQty,
                   CASE WHEN b.AvgDailyQty > 0 THEN pa.PreWindowQty / b.AvgDailyQty ELSE 0 END AS VolumeMultiple,
                   pa.AnnouncementDate
            FROM PreAnnouncement pa
            INNER JOIN Baseline b
                ON b.AnnouncementId = pa.AnnouncementId
               AND b.InstitutionId = pa.InstitutionId
               AND b.SecurityCode = pa.SecurityCode
            WHERE CASE WHEN b.AvgDailyQty > 0 THEN pa.PreWindowQty / b.AvgDailyQty ELSE 0 END >= @VolumeMultiplier
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                WindowDays = windowDays,
                VolumeMultiplier = volumeMultiplier
            });

        var count = 0;
        foreach (var row in suspicious)
        {
            var severity = row.VolumeMultiple >= (double)volumeMultiplier * 2 ? "CRITICAL" : "HIGH";
            var evidence = JsonSerializer.Serialize(new CMOUnusualTradeEvidence(
                row.SecurityCode,
                row.SecurityName,
                row.PreWindowQty,
                row.AvgDailyQty,
                row.VolumeMultiple,
                windowDays));

            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "CMO_UNUSUAL_TRADE",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "CMO",
                $"Unusual pre-announcement trading activity in {row.SecurityCode}",
                $"Pre-announcement trading volume reached {row.VolumeMultiple:F1}x the baseline within {windowDays} days of the corporate disclosure date.",
                evidence,
                periodCode,
                runId);

            count++;
        }

        _log.LogInformation(
            "CMO unusual trading surveillance completed. Regulator={RegulatorCode} Period={PeriodCode} Alerts={Count}",
            context.RegulatorCode,
            periodCode,
            count);

        return count;
    }

    public async Task<int> DetectLateReportingAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "CMO_LATE_REPORT", context.RegulatorCode, "CMO");

        var maxHours = (int)RuleParamLoader.Get(parameters, "MaxReportingDelayHours", 24m);
        var lateReporters = await conn.QueryAsync<LateReportRow>(
            """
            SELECT t.InstitutionId,
                   COUNT(*) AS LateCount,
                   CAST(AVG(CAST(DATEDIFF(HOUR, t.TradeTimestamp, t.ReportedAt) AS FLOAT)) AS DECIMAL(12,2)) AS AvgDelayHours,
                   MAX(DATEDIFF(HOUR, t.TradeTimestamp, t.ReportedAt)) AS MaxDelayHours,
                   CAST(COUNT(*) * 100.0 / NULLIF((
                        SELECT COUNT(*)
                        FROM dbo.CMOTradeReports t2
                        WHERE t2.TenantId = @TenantId
                          AND t2.RegulatorCode = @RegulatorCode
                          AND t2.PeriodCode = @PeriodCode
                          AND t2.InstitutionId = t.InstitutionId
                   ), 0) AS DECIMAL(12,2)) AS LateRatePct
            FROM dbo.CMOTradeReports t
            WHERE t.TenantId = @TenantId
              AND t.RegulatorCode = @RegulatorCode
              AND t.PeriodCode = @PeriodCode
              AND DATEDIFF(HOUR, t.TradeTimestamp, t.ReportedAt) > @MaxHours
            GROUP BY t.InstitutionId
            HAVING COUNT(*) >= 3
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                MaxHours = maxHours
            });

        var count = 0;
        foreach (var row in lateReporters)
        {
            var severity = row.LateRatePct > 50 ? "HIGH" : row.LateRatePct > 20 ? "MEDIUM" : "LOW";
            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "CMO_LATE_REPORT",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "CMO",
                $"Systematic late trade reporting ({row.LateRatePct:F1}% late)",
                $"{row.LateCount} trades were reported after the {maxHours}-hour deadline. Average delay was {row.AvgDelayHours:F1} hours.",
                null,
                periodCode,
                runId);

            count++;
        }

        return count;
    }

    public async Task<int> DetectClientConcentrationAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default)
    {
        var context = await _tenantResolver.ResolveAsync(regulatorCode, ct);
        using var conn = await _db.CreateConnectionAsync(context.TenantId, ct);
        var parameters = await RuleParamLoader.LoadAsync(conn, context.TenantId, "CMO_CONCENTRATION", context.RegulatorCode, "CMO");

        var capPct = RuleParamLoader.Get(parameters, "SingleSecurityConcentrationPct", 25m);
        var concentrated = await conn.QueryAsync<ConcentrationRow>(
            """
            WITH TotalBook AS (
                SELECT InstitutionId,
                       SUM(TradeValueNGN) AS TotalValue
                FROM dbo.CMOTradeReports
                WHERE TenantId = @TenantId
                  AND RegulatorCode = @RegulatorCode
                  AND PeriodCode = @PeriodCode
                GROUP BY InstitutionId
            ),
            BySecurity AS (
                SELECT InstitutionId,
                       SecurityCode,
                       MAX(SecurityName) AS SecurityName,
                       SUM(TradeValueNGN) AS SecurityValue
                FROM dbo.CMOTradeReports
                WHERE TenantId = @TenantId
                  AND RegulatorCode = @RegulatorCode
                  AND PeriodCode = @PeriodCode
                GROUP BY InstitutionId, SecurityCode
            )
            SELECT b.InstitutionId,
                   b.SecurityCode,
                   b.SecurityName,
                   b.SecurityValue,
                   tb.TotalValue,
                   CAST(b.SecurityValue * 100.0 / NULLIF(tb.TotalValue, 0) AS DECIMAL(12,2)) AS ConcentrationPct
            FROM BySecurity b
            INNER JOIN TotalBook tb ON tb.InstitutionId = b.InstitutionId
            WHERE b.SecurityValue * 100.0 / NULLIF(tb.TotalValue, 0) > @CapPct
            """,
            new
            {
                TenantId = context.TenantId,
                RegulatorCode = context.RegulatorCode,
                PeriodCode = periodCode,
                CapPct = capPct
            });

        var count = 0;
        foreach (var row in concentrated)
        {
            var severity = row.ConcentrationPct > capPct * 1.5m ? "HIGH" : "MEDIUM";
            await AlertWriter.WriteAsync(
                conn,
                context.TenantId,
                "CMO_CONCENTRATION",
                context.RegulatorCode,
                row.InstitutionId,
                severity,
                "CMO",
                $"Single-security concentration in {row.SecurityCode} reached {row.ConcentrationPct:F1}% of the trade book",
                $"{row.SecurityName} accounted for {row.ConcentrationPct:F1}% of total traded value against a {capPct:F1}% surveillance threshold.",
                null,
                periodCode,
                runId);

            count++;
        }

        return count;
    }

    private sealed record SuspiciousTradeRow(
        int InstitutionId,
        string SecurityCode,
        string SecurityName,
        decimal PreWindowQty,
        decimal AvgDailyQty,
        double VolumeMultiple,
        DateTime AnnouncementDate
    );

    private sealed record LateReportRow(
        int InstitutionId,
        int LateCount,
        decimal AvgDelayHours,
        int MaxDelayHours,
        decimal LateRatePct
    );

    private sealed record ConcentrationRow(
        int InstitutionId,
        string SecurityCode,
        string SecurityName,
        decimal SecurityValue,
        decimal TotalValue,
        decimal ConcentrationPct
    );
}
