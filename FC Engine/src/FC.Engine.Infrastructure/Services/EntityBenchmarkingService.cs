using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public class EntityBenchmarkingService : IEntityBenchmarkingService
{
    private static readonly string[] CarKeys =
    {
        "car",
        "carratio",
        "capitaladequacyratio",
        "capitalratio",
        "capitaladequacy"
    };

    private static readonly string[] NplKeys =
    {
        "npl",
        "nplratio",
        "nonperformingloanratio",
        "nonperformingloansratio"
    };

    private readonly MetadataDbContext _db;
    private readonly ILogger<EntityBenchmarkingService> _logger;

    public EntityBenchmarkingService(MetadataDbContext db, ILogger<EntityBenchmarkingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EntityBenchmarkResult?> GetEntityBenchmark(
        string regulatorCode,
        int institutionId,
        string? periodCode = null,
        CancellationToken ct = default)
    {
        var submissionsQuery = BuildScopedSubmissionQuery(regulatorCode);
        if (!string.IsNullOrWhiteSpace(periodCode))
        {
            submissionsQuery = ApplyPeriodFilter(submissionsQuery, periodCode!);
        }

        var rows = await submissionsQuery
            .Select(s => new
            {
                s.Id,
                s.InstitutionId,
                InstitutionName = s.Institution != null ? s.Institution.InstitutionName : "Unknown",
                s.ParsedDataJson
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return null;
        }

        var targetRows = rows.Where(x => x.InstitutionId == institutionId).ToList();
        if (targetRows.Count == 0)
        {
            return null;
        }

        var institutionName = targetRows.Select(x => x.InstitutionName).FirstOrDefault() ?? $"Institution #{institutionId}";

        var carByInstitution = rows
            .Select(x => new
            {
                x.InstitutionId,
                Value = RegulatorAnalyticsSupport.ExtractFirstMetric(x.ParsedDataJson, CarKeys)
            })
            .Where(x => x.Value.HasValue)
            .GroupBy(x => x.InstitutionId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Value!.Value));

        var nplByInstitution = rows
            .Select(x => new
            {
                x.InstitutionId,
                Value = RegulatorAnalyticsSupport.ExtractFirstMetric(x.ParsedDataJson, NplKeys)
            })
            .Where(x => x.Value.HasValue)
            .GroupBy(x => x.InstitutionId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Value!.Value));

        var submissionIds = rows.Select(x => x.Id).ToList();
        var reportRows = await _db.ValidationReports
            .AsNoTracking()
            .Include(r => r.Errors)
            .Where(r => submissionIds.Contains(r.SubmissionId))
            .Select(r => new
            {
                r.SubmissionId,
                ErrorCount = r.Errors.Count(e => e.Severity == Domain.Enums.ValidationSeverity.Error),
                WarningCount = r.Errors.Count(e => e.Severity == Domain.Enums.ValidationSeverity.Warning)
            })
            .ToListAsync(ct);

        var reportMap = reportRows.ToDictionary(x => x.SubmissionId, x => x);

        var qualityScores = rows
            .Select(row =>
            {
                if (!reportMap.TryGetValue(row.Id, out var report))
                {
                    return new { row.InstitutionId, Score = 100m };
                }

                var penalty = (report.ErrorCount * 7m) + (report.WarningCount * 2m);
                var score = Math.Max(0m, 100m - penalty);
                return new { row.InstitutionId, Score = score };
            })
            .GroupBy(x => x.InstitutionId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Score));

        var slaQuery = _db.FilingSlaRecords
            .AsNoTracking()
            .Include(x => x.Module)
            .Include(x => x.Period)
            .Include(x => x.Submission)
            .Where(x => x.Module != null && x.Module.RegulatorCode == regulatorCode);

        if (!string.IsNullOrWhiteSpace(periodCode))
        {
            var filter = periodCode!;
            if (RegulatorAnalyticsSupport.TryParsePeriodCode(filter, out var periodFilter) && periodFilter is not null)
            {
                var year = periodFilter.Year;
                slaQuery = slaQuery.Where(x => x.Period != null && x.Period.Year == year);

                if (periodFilter.Quarter.HasValue)
                {
                    var quarter = periodFilter.Quarter.Value;
                    slaQuery = slaQuery.Where(x => x.Period != null && (x.Period.Quarter ?? ((x.Period.Month - 1) / 3 + 1)) == quarter);
                }
                else if (periodFilter.Month.HasValue)
                {
                    var month = periodFilter.Month.Value;
                    slaQuery = slaQuery.Where(x => x.Period != null && x.Period.Month == month);
                }
            }
        }

        var slaRows = await slaQuery
            .Select(x => new
            {
                InstitutionId = x.Submission != null ? x.Submission.InstitutionId : 0,
                x.OnTime,
                x.DaysToDeadline
            })
            .ToListAsync(ct);

        var timelinessByInstitution = slaRows
            .Where(x => x.InstitutionId > 0)
            .GroupBy(x => x.InstitutionId)
            .ToDictionary(
                g => g.Key,
                g => g.Any(x => x.OnTime.HasValue)
                    ? 100m * g.Count(x => x.OnTime == true) / Math.Max(1, g.Count(x => x.OnTime.HasValue))
                    : 0m);

        var carPeerValues = carByInstitution.Values.ToList();

        var result = new EntityBenchmarkResult
        {
            InstitutionId = institutionId,
            InstitutionName = institutionName,

            CarValue = Round2(carByInstitution.GetValueOrDefault(institutionId)),
            CarPeerAverage = Round2(carPeerValues.Count == 0 ? 0 : carPeerValues.Average()),
            CarPeerMedian = Round2(RegulatorAnalyticsSupport.Median(carPeerValues)),
            CarPeerP25 = Round2(RegulatorAnalyticsSupport.Percentile(carPeerValues, 25)),
            CarPeerP75 = Round2(RegulatorAnalyticsSupport.Percentile(carPeerValues, 75)),

            NplValue = Round2(nplByInstitution.GetValueOrDefault(institutionId)),
            NplPeerAverage = Round2(nplByInstitution.Count == 0 ? 0 : nplByInstitution.Values.Average()),

            TimelinessScore = Round2(timelinessByInstitution.GetValueOrDefault(institutionId)),
            TimelinessPeerAverage = Round2(timelinessByInstitution.Count == 0 ? 0 : timelinessByInstitution.Values.Average()),

            DataQualityScore = Round2(qualityScores.GetValueOrDefault(institutionId)),
            DataQualityPeerAverage = Round2(qualityScores.Count == 0 ? 0 : qualityScores.Values.Average())
        };

        return result;
    }

    private IQueryable<Submission> BuildScopedSubmissionQuery(string regulatorCode)
    {
        var code = regulatorCode.Trim();
        return _db.Submissions
            .AsNoTracking()
            .Include(s => s.Institution)
            .Include(s => s.ReturnPeriod)
                .ThenInclude(rp => rp!.Module)
            .Where(s => s.ReturnPeriod != null
                        && s.ReturnPeriod.Module != null
                        && s.ReturnPeriod.Module.RegulatorCode == code);
    }

    private static IQueryable<Submission> ApplyPeriodFilter(IQueryable<Submission> query, string periodCode)
    {
        if (!RegulatorAnalyticsSupport.TryParsePeriodCode(periodCode, out var filter) || filter is null)
        {
            return query;
        }

        query = query.Where(s => s.ReturnPeriod != null && s.ReturnPeriod.Year == filter.Year);
        if (filter.Quarter.HasValue)
        {
            var quarter = filter.Quarter.Value;
            query = query.Where(s => s.ReturnPeriod != null && (s.ReturnPeriod.Quarter ?? ((s.ReturnPeriod.Month - 1) / 3 + 1)) == quarter);
        }
        else if (filter.Month.HasValue)
        {
            var month = filter.Month.Value;
            query = query.Where(s => s.ReturnPeriod != null && s.ReturnPeriod.Month == month);
        }

        return query;
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2);
}
