using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class JurisdictionConsolidationService : IJurisdictionConsolidationService
{
    private readonly MetadataDbContext _db;

    public JurisdictionConsolidationService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<CrossJurisdictionConsolidation> GetConsolidation(
        Guid tenantId,
        string reportingCurrency = "NGN",
        CancellationToken ct = default)
    {
        var currency = string.IsNullOrWhiteSpace(reportingCurrency)
            ? "NGN"
            : reportingCurrency.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var relatedTenantIds = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId || t.ParentTenantId == tenantId)
            .Select(t => t.TenantId)
            .ToListAsync(ct);

        if (!relatedTenantIds.Contains(tenantId))
        {
            relatedTenantIds.Add(tenantId);
        }

        var institutions = await _db.Institutions
            .AsNoTracking()
            .Where(i => relatedTenantIds.Contains(i.TenantId))
            .ToListAsync(ct);

        if (institutions.Count == 0)
        {
            return new CrossJurisdictionConsolidation
            {
                TenantId = tenantId,
                ReportingCurrency = currency,
                SubsidiaryCount = Math.Max(relatedTenantIds.Count - 1, 0),
                GeneratedAt = DateTime.UtcNow
            };
        }

        var jurisdictions = await _db.Jurisdictions
            .AsNoTracking()
            .Where(j => institutions.Select(i => i.JurisdictionId).Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, ct);

        var invoiceRows = await _db.Invoices
            .AsNoTracking()
            .Where(i => relatedTenantIds.Contains(i.TenantId) && i.Status != InvoiceStatus.Voided)
            .Select(i => new
            {
                i.TenantId,
                i.TotalAmount,
                i.Currency
            })
            .ToListAsync(ct);

        var submissionRows = await _db.Submissions
            .AsNoTracking()
            .Where(s => relatedTenantIds.Contains(s.TenantId))
            .Select(s => new { s.TenantId, s.InstitutionId })
            .ToListAsync(ct);

        var overdueRows = await _db.ReturnPeriods
            .AsNoTracking()
            .Where(rp => relatedTenantIds.Contains(rp.TenantId) && rp.Status == "Overdue")
            .Select(rp => rp.TenantId)
            .ToListAsync(ct);

        var grouped = institutions
            .GroupBy(i => i.JurisdictionId)
            .OrderBy(g => jurisdictions.TryGetValue(g.Key, out var j) ? j.CountryCode : "ZZ")
            .ToList();

        var result = new CrossJurisdictionConsolidation
        {
            TenantId = tenantId,
            ReportingCurrency = currency,
            SubsidiaryCount = Math.Max(relatedTenantIds.Count - 1, 0),
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var group in grouped)
        {
            if (!jurisdictions.TryGetValue(group.Key, out var jurisdiction))
            {
                continue;
            }

            var tenantIdsInJurisdiction = group
                .Select(i => i.TenantId)
                .Distinct()
                .ToHashSet();

            var invoiceAmount = invoiceRows
                .Where(i => tenantIdsInJurisdiction.Contains(i.TenantId))
                .Sum(i => i.TotalAmount);

            var fxRate = await ResolveFxRate(jurisdiction.Currency, currency, today, ct);

            var jurisdictionItem = new JurisdictionConsolidationItem
            {
                JurisdictionId = jurisdiction.Id,
                CountryCode = jurisdiction.CountryCode,
                CountryName = jurisdiction.CountryName,
                Currency = jurisdiction.Currency,
                FxRateToReportingCurrency = fxRate,
                InstitutionCount = group.Count(),
                SubmissionCount = submissionRows.Count(s =>
                    tenantIdsInJurisdiction.Contains(s.TenantId)
                    && group.Any(i => i.Id == s.InstitutionId)),
                OverdueSubmissionCount = overdueRows.Count(t => tenantIdsInJurisdiction.Contains(t)),
                GrossAmountLocal = decimal.Round(invoiceAmount, 2, MidpointRounding.AwayFromZero),
                GrossAmountReportingCurrency = decimal.Round(invoiceAmount * fxRate, 2, MidpointRounding.AwayFromZero)
            };

            result.Jurisdictions.Add(jurisdictionItem);
        }

        result.GrossAmount = result.Jurisdictions.Sum(x => x.GrossAmountReportingCurrency);

        var adjustments = await _db.ConsolidationAdjustments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EffectiveDate <= today)
            .ToListAsync(ct);

        decimal adjustmentTotal = 0;
        foreach (var adjustment in adjustments)
        {
            var rate = await ResolveFxRate(adjustment.Currency, currency, today, ct);
            adjustmentTotal += adjustment.Amount * rate;
        }

        result.EliminationAdjustments = decimal.Round(adjustmentTotal, 2, MidpointRounding.AwayFromZero);
        result.NetAmount = decimal.Round(result.GrossAmount - result.EliminationAdjustments, 2, MidpointRounding.AwayFromZero);
        return result;
    }

    private async Task<decimal> ResolveFxRate(
        string baseCurrency,
        string quoteCurrency,
        DateOnly rateDate,
        CancellationToken ct)
    {
        if (string.Equals(baseCurrency, quoteCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var exact = await _db.JurisdictionFxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency && r.RateDate <= rateDate)
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync(ct);
        if (exact > 0)
        {
            return exact;
        }

        var inverse = await _db.JurisdictionFxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrency == quoteCurrency && r.QuoteCurrency == baseCurrency && r.RateDate <= rateDate)
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultAsync(ct);

        return inverse > 0 ? decimal.Round(1m / inverse, 8, MidpointRounding.AwayFromZero) : 1m;
    }
}
