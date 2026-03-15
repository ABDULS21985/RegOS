using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services.CrossBorder;

public sealed class ConsolidationEngine : IConsolidationEngine
{
    private static readonly string[] TotalAssetsKeys = ["totalassets", "totalasset", "assets"];
    private static readonly string[] TotalLiabilitiesKeys = ["totalliabilities", "totalliability", "liabilities"];
    private static readonly string[] CapitalKeys = ["shareholdersfunds", "totalcapital", "capital", "tier1capital", "tier1"];
    private static readonly string[] RwaKeys = ["riskweightedassets", "rwa"];
    private static readonly string[] CarKeys = ["car", "carratio", "capitaladequacyratio", "capitalratio", "capitaladequacy"];

    private readonly MetadataDbContext _db;
    private readonly ICurrencyConversionEngine _fx;
    private readonly IHarmonisationAuditLogger _audit;
    private readonly ILogger<ConsolidationEngine> _log;

    public ConsolidationEngine(
        MetadataDbContext db, ICurrencyConversionEngine fx,
        IHarmonisationAuditLogger audit, ILogger<ConsolidationEngine> log)
    {
        _db = db; _fx = fx; _audit = audit; _log = log;
    }

    public async Task<ConsolidationResult> RunConsolidationAsync(
        int groupId, string reportingPeriod, DateOnly snapshotDate,
        int userId, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _log.LogInformation("Starting consolidation: GroupId={GroupId}, Period={Period}, CorrelationId={Corr}",
            groupId, reportingPeriod, correlationId);

        var group = await _db.FinancialGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && g.IsActive, ct)
            ?? throw new InvalidOperationException($"Financial group {groupId} not found.");

        var subsidiaries = await _db.GroupSubsidiaries
            .AsNoTracking()
            .Where(s => s.GroupId == groupId && s.IsActive)
            .OrderBy(s => s.JurisdictionCode)
            .ToListAsync(ct);

        // Create consolidation run
        var maxRunNumber = await _db.ConsolidationRuns
            .Where(r => r.GroupId == groupId)
            .MaxAsync(r => (int?)r.RunNumber, ct) ?? 0;

        var run = new ConsolidationRun
        {
            GroupId = groupId, RunNumber = maxRunNumber + 1,
            ReportingPeriod = reportingPeriod, SnapshotDate = snapshotDate,
            BaseCurrency = group.BaseCurrency,
            Status = ConsolidationRunStatus.Collecting,
            TotalSubsidiaries = subsidiaries.Count,
            CorrelationId = correlationId, CreatedByUserId = userId
        };
        _db.ConsolidationRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(groupId, null, correlationId, "CONSOLIDATION_STARTED",
            new { runId = run.Id, runNumber = run.RunNumber, subsidiaryCount = subsidiaries.Count }, userId, ct);

        // Collect subsidiary data and convert
        decimal totalAdjustedAssets = 0m, totalAdjustedCapital = 0m, totalAdjustedRWA = 0m;
        int collected = 0;

        foreach (var sub in subsidiaries)
        {
            try
            {
                var localSnapshot = await LoadSubsidiarySnapshotAsync(sub, reportingPeriod, ct);
                var localAssets = localSnapshot.TotalAssets;
                var localLiabilities = localSnapshot.TotalLiabilities;
                var localCapital = localSnapshot.TotalCapital;
                var localRWA = localSnapshot.RiskWeightedAssets;
                var localCAR = localSnapshot.CapitalAdequacyRatio;

                // Currency conversion
                var convertedAssets = await _fx.ConvertAsync(localAssets, sub.LocalCurrency, group.BaseCurrency, snapshotDate, FxRateType.PeriodEnd, ct);
                var convertedLiabilities = await _fx.ConvertAsync(localLiabilities, sub.LocalCurrency, group.BaseCurrency, snapshotDate, FxRateType.PeriodEnd, ct);
                var convertedCapital = await _fx.ConvertAsync(localCapital, sub.LocalCurrency, group.BaseCurrency, snapshotDate, FxRateType.PeriodEnd, ct);
                var convertedRWA = await _fx.ConvertAsync(localRWA, sub.LocalCurrency, group.BaseCurrency, snapshotDate, FxRateType.PeriodEnd, ct);

                // Apply ownership and consolidation method
                var ownershipFactor = sub.OwnershipPercentage / 100m;
                var consolMethod = sub.ConsolidationMethod.ToString();
                var adjustedAssets = sub.ConsolidationMethod == ConsolidationMethod.Full
                    ? convertedAssets.ConvertedValue : convertedAssets.ConvertedValue * ownershipFactor;
                var adjustedCapital = sub.ConsolidationMethod == ConsolidationMethod.Full
                    ? convertedCapital.ConvertedValue : convertedCapital.ConvertedValue * ownershipFactor;
                var adjustedRWA = sub.ConsolidationMethod == ConsolidationMethod.Full
                    ? convertedRWA.ConvertedValue : convertedRWA.ConvertedValue * ownershipFactor;

                // Minority interest adjustment for FULL consolidation with < 100% ownership
                if (sub.ConsolidationMethod == ConsolidationMethod.Full && sub.OwnershipPercentage < 100m)
                {
                    var minorityPct = (100m - sub.OwnershipPercentage) / 100m;
                    _db.GroupConsolidationAdjustments.Add(new GroupConsolidationAdjustment
                    {
                        RunId = run.Id, GroupId = groupId,
                        AdjustmentType = "MINORITY_INTEREST",
                        Description = $"Minority interest ({sub.OwnershipPercentage}% ownership) for {sub.SubsidiaryCode}",
                        AffectedSubsidiaryId = sub.Id,
                        DebitAccount = "EQUITY", CreditAccount = "MINORITY_INTEREST_RESERVE",
                        Amount = Math.Round(convertedCapital.ConvertedValue * minorityPct, 2),
                        Currency = group.BaseCurrency
                    });
                }

                // Persist snapshot
                _db.ConsolidationSubsidiarySnapshots.Add(new ConsolidationSubsidiarySnapshot
                {
                    RunId = run.Id, SubsidiaryId = sub.Id, GroupId = groupId,
                    JurisdictionCode = sub.JurisdictionCode, LocalCurrency = sub.LocalCurrency,
                    LocalTotalAssets = localAssets, LocalTotalLiabilities = localLiabilities,
                    LocalTotalCapital = localCapital, LocalRWA = localRWA,
                    LocalCAR = localCAR, LocalLCR = null, LocalNSFR = null,
                    FxRateUsed = convertedAssets.FxRate, FxRateDate = convertedAssets.RateDate,
                    FxRateSource = convertedAssets.RateSource,
                    ConvertedTotalAssets = convertedAssets.ConvertedValue,
                    ConvertedTotalLiabilities = convertedLiabilities.ConvertedValue,
                    ConvertedTotalCapital = convertedCapital.ConvertedValue,
                    ConvertedRWA = convertedRWA.ConvertedValue,
                    OwnershipPercentage = sub.OwnershipPercentage,
                    ConsolidationMethodUsed = consolMethod,
                    AdjustedTotalAssets = adjustedAssets, AdjustedTotalCapital = adjustedCapital,
                    AdjustedRWA = adjustedRWA,
                    SourceReturnInstanceId = localSnapshot.SubmissionId
                });

                totalAdjustedAssets += adjustedAssets;
                totalAdjustedCapital += adjustedCapital;
                totalAdjustedRWA += adjustedRWA;
                collected++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to collect data for subsidiary {SubCode}.", sub.SubsidiaryCode);
            }
        }

        await _db.SaveChangesAsync(ct);

        if (collected == 0)
        {
            sw.Stop();
            run.Status = ConsolidationRunStatus.Failed;
            run.ErrorMessage = $"No accepted submission data with the required prudential metrics was found for group {groupId} in reporting period {reportingPeriod}.";
            run.ExecutionTimeMs = sw.ElapsedMilliseconds;
            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException(run.ErrorMessage);
        }

        // Compute consolidated metrics
        var consolidatedCAR = totalAdjustedRWA > 0
            ? Math.Round(totalAdjustedCapital / totalAdjustedRWA * 100m, 4) : 0m;

        var adjustmentCount = await _db.GroupConsolidationAdjustments
            .CountAsync(a => a.RunId == run.Id, ct);

        sw.Stop();

        run.Status = ConsolidationRunStatus.Completed;
        run.SubsidiariesCollected = collected;
        run.TotalAdjustments = adjustmentCount;
        run.ConsolidatedTotalAssets = totalAdjustedAssets;
        run.ConsolidatedTotalCapital = totalAdjustedCapital;
        run.ConsolidatedCAR = consolidatedCAR;
        run.ExecutionTimeMs = sw.ElapsedMilliseconds;
        run.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(groupId, null, correlationId, "CONSOLIDATION_COMPLETED",
            new { runId = run.Id, collected, adjustmentCount, consolidatedCAR, totalAssets = totalAdjustedAssets, executionMs = sw.ElapsedMilliseconds },
            userId, ct);

        _log.LogInformation("Consolidation completed: RunId={RunId}, Collected={Collected}/{Total}, CAR={CAR}%, ElapsedMs={Ms}",
            run.Id, collected, subsidiaries.Count, consolidatedCAR, sw.ElapsedMilliseconds);

        return MapToResult(run);
    }

    public async Task<ConsolidationResult?> GetRunResultAsync(
        long runId, int groupId, CancellationToken ct = default)
    {
        var run = await _db.ConsolidationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId && r.GroupId == groupId, ct);

        return run is null ? null : MapToResult(run);
    }

    public async Task<IReadOnlyList<ConsolidationSubsidiaryResult>> GetSubsidiarySnapshotsAsync(
        long runId, int groupId, CancellationToken ct = default)
    {
        var snapshots = await _db.ConsolidationSubsidiarySnapshots
            .AsNoTracking()
            .Include(s => s.Subsidiary)
            .Where(s => s.RunId == runId && s.GroupId == groupId)
            .OrderBy(s => s.JurisdictionCode)
            .ToListAsync(ct);

        return snapshots.Select(s => new ConsolidationSubsidiaryResult
        {
            SubsidiaryId = s.SubsidiaryId,
            SubsidiaryCode = s.Subsidiary?.SubsidiaryCode ?? string.Empty,
            SubsidiaryName = s.Subsidiary?.SubsidiaryName ?? string.Empty,
            JurisdictionCode = s.JurisdictionCode, LocalCurrency = s.LocalCurrency,
            LocalTotalAssets = s.LocalTotalAssets, LocalTotalCapital = s.LocalTotalCapital,
            LocalCAR = s.LocalCAR, FxRateUsed = s.FxRateUsed,
            FxRateDate = s.FxRateDate, FxRateSource = s.FxRateSource,
            ConvertedTotalAssets = s.ConvertedTotalAssets, ConvertedTotalCapital = s.ConvertedTotalCapital,
            OwnershipPercentage = s.OwnershipPercentage, ConsolidationMethod = s.ConsolidationMethodUsed,
            AdjustedTotalAssets = s.AdjustedTotalAssets, AdjustedTotalCapital = s.AdjustedTotalCapital,
            AdjustedRWA = s.AdjustedRWA
        }).ToList();
    }

    public async Task<IReadOnlyList<ConsolidationAdjustmentDto>> GetAdjustmentsAsync(
        long runId, int groupId, CancellationToken ct = default)
    {
        var adjustments = await _db.GroupConsolidationAdjustments
            .AsNoTracking()
            .Include(a => a.AffectedSubsidiary)
            .Where(a => a.RunId == runId && a.GroupId == groupId)
            .ToListAsync(ct);

        return adjustments.Select(a => new ConsolidationAdjustmentDto
        {
            Id = a.Id, AdjustmentType = a.AdjustmentType, Description = a.Description,
            AffectedSubsidiaryCode = a.AffectedSubsidiary?.SubsidiaryCode,
            DebitAccount = a.DebitAccount, CreditAccount = a.CreditAccount,
            Amount = a.Amount, Currency = a.Currency, IsAutomatic = a.IsAutomatic
        }).ToList();
    }

    public async Task AddManualAdjustmentAsync(
        long runId, int groupId, ConsolidationAdjustmentInput adj,
        int userId, CancellationToken ct = default)
    {
        var group = await _db.FinancialGroups.FindAsync([groupId], ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found.");

        _db.GroupConsolidationAdjustments.Add(new GroupConsolidationAdjustment
        {
            RunId = runId, GroupId = groupId,
            AdjustmentType = adj.AdjustmentType, Description = adj.Description,
            AffectedSubsidiaryId = adj.AffectedSubsidiaryId,
            DebitAccount = adj.DebitAccount, CreditAccount = adj.CreditAccount,
            Amount = adj.Amount, Currency = group.BaseCurrency,
            IsAutomatic = false, AppliedByUserId = userId
        });
        await _db.SaveChangesAsync(ct);
    }

    private static ConsolidationResult MapToResult(ConsolidationRun run) => new()
    {
        RunId = run.Id, GroupId = run.GroupId, RunNumber = run.RunNumber,
        ReportingPeriod = run.ReportingPeriod, Status = run.Status,
        SnapshotDate = run.SnapshotDate, BaseCurrency = run.BaseCurrency,
        TotalSubsidiaries = run.TotalSubsidiaries, SubsidiariesCollected = run.SubsidiariesCollected,
        TotalAdjustments = run.TotalAdjustments,
        ConsolidatedTotalAssets = run.ConsolidatedTotalAssets,
        ConsolidatedTotalCapital = run.ConsolidatedTotalCapital,
        ConsolidatedCAR = run.ConsolidatedCAR,
        ExecutionTimeMs = run.ExecutionTimeMs, CorrelationId = run.CorrelationId
    };

    private async Task<SubsidiaryMetricSnapshot> LoadSubsidiarySnapshotAsync(
        GroupSubsidiary subsidiary,
        string reportingPeriod,
        CancellationToken ct)
    {
        var submissionQuery = _db.Submissions
            .AsNoTracking()
            .Include(x => x.ReturnPeriod)
            .Where(x => x.InstitutionId == subsidiary.InstitutionId
                && x.ParsedDataJson != null
                && (x.Status == SubmissionStatus.Accepted || x.Status == SubmissionStatus.AcceptedWithWarnings));

        if (RegulatorAnalyticsSupport.TryParsePeriodCode(reportingPeriod, out var filter) && filter is not null)
        {
            submissionQuery = submissionQuery.Where(x => x.ReturnPeriod != null
                && x.ReturnPeriod.Year == filter.Year
                && (!filter.Quarter.HasValue || x.ReturnPeriod.Quarter == filter.Quarter.Value)
                && (!filter.Month.HasValue || x.ReturnPeriod.Month == filter.Month.Value));
        }

        var submission = await submissionQuery
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"No accepted submission data was found for subsidiary institution {subsidiary.InstitutionId} in reporting period {reportingPeriod}.");

        var totalAssets = RegulatorAnalyticsSupport.ExtractFirstMetric(submission.ParsedDataJson, TotalAssetsKeys);
        var totalLiabilities = RegulatorAnalyticsSupport.ExtractFirstMetric(submission.ParsedDataJson, TotalLiabilitiesKeys);
        var totalCapital = RegulatorAnalyticsSupport.ExtractFirstMetric(submission.ParsedDataJson, CapitalKeys);
        var rwa = RegulatorAnalyticsSupport.ExtractFirstMetric(submission.ParsedDataJson, RwaKeys);
        var car = RegulatorAnalyticsSupport.ExtractFirstMetric(submission.ParsedDataJson, CarKeys);

        if (!totalAssets.HasValue && totalLiabilities.HasValue && totalCapital.HasValue)
        {
            totalAssets = totalLiabilities.Value + totalCapital.Value;
        }

        if (!totalLiabilities.HasValue && totalAssets.HasValue && totalCapital.HasValue)
        {
            totalLiabilities = Math.Max(totalAssets.Value - totalCapital.Value, 0m);
        }

        if (!rwa.HasValue && totalCapital.HasValue && car.HasValue && car.Value > 0)
        {
            rwa = Math.Round(totalCapital.Value / (car.Value / 100m), 2);
        }

        if (!car.HasValue && totalCapital.HasValue && rwa.HasValue && rwa.Value > 0)
        {
            car = Math.Round(totalCapital.Value / rwa.Value * 100m, 4);
        }

        if (!totalAssets.HasValue || !totalLiabilities.HasValue || !totalCapital.HasValue || !rwa.HasValue)
        {
            throw new InvalidOperationException(
                $"Submission {submission.Id} for subsidiary institution {subsidiary.InstitutionId} does not contain the metrics required for consolidation.");
        }

        return new SubsidiaryMetricSnapshot(
            submission.Id,
            totalAssets.Value,
            totalLiabilities.Value,
            totalCapital.Value,
            rwa.Value,
            car ?? 0m);
    }

    private sealed record SubsidiaryMetricSnapshot(
        int SubmissionId,
        decimal TotalAssets,
        decimal TotalLiabilities,
        decimal TotalCapital,
        decimal RiskWeightedAssets,
        decimal CapitalAdequacyRatio);
}
