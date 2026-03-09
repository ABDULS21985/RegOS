using System.Data;
using Dapper;
using FC.Engine.Domain.Models;

namespace FC.Engine.Infrastructure.Services;

public sealed record RegulatorTenantContext(Guid TenantId, string RegulatorCode);

internal static class SurveillanceSqlMapping
{
    public static string ToDbValue(AlertCategory category) => category switch
    {
        AlertCategory.BdcFx => "BDC_FX",
        AlertCategory.Cmo => "CMO",
        AlertCategory.Insurance => "INSURANCE",
        AlertCategory.Aml => "AML",
        AlertCategory.Conduct => "CONDUCT",
        _ => "CONDUCT"
    };

    public static string ToDbValue(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Low => "LOW",
        AlertSeverity.Medium => "MEDIUM",
        AlertSeverity.High => "HIGH",
        AlertSeverity.Critical => "CRITICAL",
        _ => "LOW"
    };
}

public interface IRegulatorTenantResolver
{
    Task<RegulatorTenantContext> ResolveAsync(string regulatorCode, CancellationToken ct = default);
}

internal static class RuleParamLoader
{
    public static async Task<Dictionary<string, decimal>> LoadAsync(
        IDbConnection conn,
        Guid? tenantId,
        string ruleCode,
        string regulatorCode,
        string institutionType = "ALL")
    {
        var rows = await conn.QueryAsync<RuleParamRow>(
            """
            SELECT ParamName AS Name, ParamValue AS Value
            FROM   dbo.SurveillanceRuleParameters
            WHERE  RuleCode = @Code
              AND  RegulatorCode = @Regulator
              AND  InstitutionType IN (@Type, 'ALL')
              AND  IsActive = 1
              AND  EffectiveFrom <= CAST(GETUTCDATE() AS DATE)
              AND  (TenantId = @TenantId OR TenantId IS NULL)
            ORDER BY
                CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END,
                CASE WHEN InstitutionType = @Type THEN 0 ELSE 1 END,
                EffectiveFrom DESC
            """,
            new { Code = ruleCode, Regulator = regulatorCode, Type = institutionType, TenantId = tenantId });

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            result[row.Name] = row.Value;
        }

        return result;
    }

    public static decimal Get(
        Dictionary<string, decimal> values,
        string key,
        decimal fallback)
        => values.TryGetValue(key, out var value) ? value : fallback;

    private sealed record RuleParamRow(string Name, decimal Value);
}

internal static class AlertWriter
{
    public static async Task<long> WriteAsync(
        IDbConnection conn,
        Guid tenantId,
        string alertCode,
        string regulatorCode,
        int? institutionId,
        string severity,
        string category,
        string title,
        string? detail,
        string? evidenceJson,
        string? periodCode,
        Guid runId)
    {
        var existing = await conn.ExecuteScalarAsync<long?>(
            """
            SELECT Id
            FROM   dbo.SurveillanceAlerts
            WHERE  TenantId = @TenantId
              AND  AlertCode = @Code
              AND  RegulatorCode = @Regulator
              AND  ISNULL(InstitutionId, -1) = ISNULL(@InstitutionId, -1)
              AND  DetectionRunId = @RunId
            """,
            new
            {
                TenantId = tenantId,
                Code = alertCode,
                Regulator = regulatorCode,
                InstitutionId = institutionId,
                RunId = runId
            });

        if (existing.HasValue)
        {
            return existing.Value;
        }

        return await conn.ExecuteScalarAsync<long>(
            """
            INSERT INTO dbo.SurveillanceAlerts
                (TenantId, AlertCode, RegulatorCode, InstitutionId, Severity, Category,
                 Title, Detail, EvidenceJson, PeriodCode, DetectionRunId)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @Code, @Regulator, @InstitutionId, @Severity, @Category,
                 @Title, @Detail, @EvidenceJson, @PeriodCode, @RunId)
            """,
            new
            {
                TenantId = tenantId,
                Code = alertCode,
                Regulator = regulatorCode,
                InstitutionId = institutionId,
                Severity = severity,
                Category = category,
                Title = title,
                Detail = detail,
                EvidenceJson = evidenceJson,
                PeriodCode = periodCode,
                RunId = runId
            });
    }
}
