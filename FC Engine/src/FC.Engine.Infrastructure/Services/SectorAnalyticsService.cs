using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public class SectorAnalyticsService : ISectorAnalyticsService
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

    private static readonly string[] DemandDepositKeys =
    {
        "demanddeposit",
        "currentdeposit"
    };

    private static readonly string[] SavingsDepositKeys =
    {
        "savingsdeposit",
        "savingdeposit"
    };

    private static readonly string[] TimeDepositKeys =
    {
        "timedeposit",
        "fixeddeposit",
        "termdeposit"
    };

    private static readonly string[] OtherDepositKeys =
    {
        "otherdeposit",
        "miscdeposit"
    };

    private readonly MetadataDbContext _db;
    private readonly ILogger<SectorAnalyticsService> _logger;

    public SectorAnalyticsService(MetadataDbContext db, ILogger<SectorAnalyticsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SectorCarDistribution> GetCarDistribution(string regulatorCode, string periodCode, CancellationToken ct = default)
    {
        var query = ApplyPeriodFilter(BuildScopedSubmissionQuery(regulatorCode), periodCode);

        var rows = await query
            .Select(s => new { s.InstitutionId, s.ParsedDataJson })
            .ToListAsync(ct);

        var carValues = rows
            .Select(x => RegulatorAnalyticsSupport.ExtractFirstMetric(x.ParsedDataJson, CarKeys))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        var buckets = new List<HistogramBucket>
        {
            new() { Label = "< 5%", Count = carValues.Count(v => v < 5m) },
            new() { Label = "5% - 10%", Count = carValues.Count(v => v >= 5m && v < 10m) },
            new() { Label = "10% - 15%", Count = carValues.Count(v => v >= 10m && v < 15m) },
            new() { Label = "15% - 20%", Count = carValues.Count(v => v >= 15m && v < 20m) },
            new() { Label = ">= 20%", Count = carValues.Count(v => v >= 20m) }
        };

        return new SectorCarDistribution
        {
            PeriodCode = periodCode,
            AverageCar = carValues.Count == 0 ? 0 : decimal.Round(carValues.Average(), 2),
            MedianCar = decimal.Round(RegulatorAnalyticsSupport.Median(carValues), 2),
            InstitutionCount = rows.Select(x => x.InstitutionId).Distinct().Count(),
            Buckets = buckets
        };
    }

    public async Task<SectorNplTrend> GetNplTrend(string regulatorCode, int quarters = 8, CancellationToken ct = default)
    {
        var rows = await BuildScopedSubmissionQuery(regulatorCode)
            .Where(s => s.ReturnPeriod != null)
            .Select(s => new
            {
                s.ReturnPeriod!.Year,
                s.ReturnPeriod.Month,
                s.ReturnPeriod.Quarter,
                s.ParsedDataJson
            })
            .ToListAsync(ct);

        var grouped = rows
            .Select(x => new
            {
                x.Year,
                Quarter = RegulatorAnalyticsSupport.ResolveQuarter(x.Month, x.Quarter),
                Npl = RegulatorAnalyticsSupport.ExtractFirstMetric(x.ParsedDataJson, NplKeys)
            })
            .Where(x => x.Npl.HasValue)
            .GroupBy(x => new { x.Year, x.Quarter })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Quarter)
            .Take(Math.Max(1, quarters))
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Quarter)
            .ToList();

        var result = new SectorNplTrend();
        foreach (var bucket in grouped)
        {
            result.PeriodLabels.Add($"{bucket.Key.Year}-Q{bucket.Key.Quarter}");
            result.AverageNplRatios.Add(decimal.Round(bucket.Average(x => x.Npl!.Value), 2));
        }

        return result;
    }

    public async Task<SectorDepositStructure> GetDepositStructure(string regulatorCode, string periodCode, CancellationToken ct = default)
    {
        var query = ApplyPeriodFilter(BuildScopedSubmissionQuery(regulatorCode), periodCode);

        var rows = await query
            .Select(s => s.ParsedDataJson)
            .ToListAsync(ct);

        decimal demand = 0;
        decimal savings = 0;
        decimal time = 0;
        decimal other = 0;

        foreach (var json in rows)
        {
            demand += RegulatorAnalyticsSupport.ExtractSumMetric(json, DemandDepositKeys);
            savings += RegulatorAnalyticsSupport.ExtractSumMetric(json, SavingsDepositKeys);
            time += RegulatorAnalyticsSupport.ExtractSumMetric(json, TimeDepositKeys);
            other += RegulatorAnalyticsSupport.ExtractSumMetric(json, OtherDepositKeys);
        }

        var slices = new List<DepositSlice>
        {
            new() { Label = "Demand", Value = decimal.Round(demand, 2) },
            new() { Label = "Savings", Value = decimal.Round(savings, 2) },
            new() { Label = "Time", Value = decimal.Round(time, 2) },
            new() { Label = "Other", Value = decimal.Round(other, 2) }
        };

        return new SectorDepositStructure
        {
            PeriodCode = periodCode,
            TotalAmount = slices.Sum(x => x.Value),
            Slices = slices
        };
    }

    public async Task<FilingTimeliness> GetFilingTimeliness(string regulatorCode, string periodCode, CancellationToken ct = default)
    {
        var records = await _db.FilingSlaRecords
            .AsNoTracking()
            .Include(x => x.Module)
            .Include(x => x.Period)
            .Include(x => x.Submission)
                .ThenInclude(s => s!.Institution)
            .Where(x => x.Module != null && x.Module.RegulatorCode == regulatorCode)
            .ToListAsync(ct);

        var filtered = records
            .Where(x => x.Period != null)
            .Where(x => MatchesPeriodCode(x.Period!, periodCode))
            .ToList();

        var grouped = filtered
            .GroupBy(x => new
            {
                InstitutionId = x.Submission?.InstitutionId ?? 0,
                InstitutionName = x.Submission?.Institution?.InstitutionName ?? "Unknown"
            })
            .Select(g => new InstitutionTimelinessItem
            {
                InstitutionId = g.Key.InstitutionId,
                InstitutionName = g.Key.InstitutionName,
                OnTime = g.Count(x => x.OnTime == true),
                Late = g.Count(x => x.OnTime == false)
            })
            .OrderBy(x => x.InstitutionName)
            .ToList();

        return new FilingTimeliness
        {
            PeriodCode = periodCode,
            OnTimeCount = filtered.Count(x => x.OnTime == true),
            LateCount = filtered.Count(x => x.OnTime == false),
            Institutions = grouped
        };
    }

    public async Task<FilingHeatmap> GetFilingHeatmap(string regulatorCode, string periodCode, CancellationToken ct = default)
    {
        var modules = await _db.Modules
            .AsNoTracking()
            .Where(m => m.IsActive && m.RegulatorCode == regulatorCode)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.ModuleName)
            .Select(m => m.ModuleCode)
            .Distinct()
            .ToListAsync(ct);

        var submissions = await ApplyPeriodFilter(BuildScopedSubmissionQuery(regulatorCode), periodCode)
            .Select(s => new
            {
                InstitutionName = s.Institution != null ? s.Institution.InstitutionName : "Unknown",
                ModuleCode = s.ReturnPeriod != null && s.ReturnPeriod.Module != null
                    ? s.ReturnPeriod.Module.ModuleCode
                    : string.Empty,
                Filed = s.Status != Domain.Enums.SubmissionStatus.Draft
            })
            .ToListAsync(ct);

        var institutions = submissions
            .Select(x => x.InstitutionName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var filedPairs = submissions
            .Where(x => !string.IsNullOrWhiteSpace(x.ModuleCode) && x.Filed)
            .Select(x => (Institution: x.InstitutionName, Module: x.ModuleCode))
            .ToHashSet();

        var cells = new List<FilingHeatmapCell>();
        foreach (var institution in institutions)
        {
            foreach (var module in modules)
            {
                cells.Add(new FilingHeatmapCell
                {
                    Institution = institution,
                    Module = module,
                    Filed = filedPairs.Contains((institution, module))
                });
            }
        }

        return new FilingHeatmap
        {
            PeriodCode = periodCode,
            Institutions = institutions,
            Modules = modules,
            Cells = cells
        };
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

    private IQueryable<Submission> ApplyPeriodFilter(IQueryable<Submission> query, string periodCode)
    {
        if (!RegulatorAnalyticsSupport.TryParsePeriodCode(periodCode, out var filter) || filter is null)
        {
            _logger.LogWarning("Failed to parse period code {PeriodCode}; returning unfiltered analytics dataset", periodCode);
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

    private static bool MatchesPeriodCode(ReturnPeriod period, string periodCode)
    {
        if (!RegulatorAnalyticsSupport.TryParsePeriodCode(periodCode, out var filter) || filter is null)
        {
            return true;
        }

        if (period.Year != filter.Year)
        {
            return false;
        }

        if (filter.Quarter.HasValue)
        {
            var quarter = RegulatorAnalyticsSupport.ResolveQuarter(period.Month, period.Quarter);
            return quarter == filter.Quarter.Value;
        }

        if (filter.Month.HasValue)
        {
            return period.Month == filter.Month.Value;
        }

        return true;
    }
}
