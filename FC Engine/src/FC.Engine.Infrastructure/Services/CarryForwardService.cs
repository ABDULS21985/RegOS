using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class CarryForwardService : ICarryForwardService
{
    private readonly ITemplateMetadataCache _templateCache;
    private readonly IGenericDataRepository _dataRepository;
    private readonly MetadataDbContext _db;

    public CarryForwardService(
        ITemplateMetadataCache templateCache,
        IGenericDataRepository dataRepository,
        MetadataDbContext db)
    {
        _templateCache = templateCache;
        _dataRepository = dataRepository;
        _db = db;
    }

    public async Task<CarryForwardResult> GetCarryForwardValues(
        Guid tenantId,
        string returnCode,
        int currentPeriodId,
        CancellationToken ct = default)
    {
        var result = new CarryForwardResult();
        var template = await _templateCache.GetPublishedTemplate(tenantId, returnCode, ct);
        var carryForwardFields = template.CurrentVersion.Fields
            .Where(x => x.IsYtdField)
            .OrderBy(x => x.FieldOrder)
            .ToList();

        if (carryForwardFields.Count == 0)
        {
            return result;
        }

        var previousSubmission = await _db.Submissions
            .Include(x => x.ReturnPeriod)
            .Where(x => x.TenantId == tenantId
                        && x.ReturnCode == returnCode
                        && x.ReturnPeriodId < currentPeriodId
                        && (x.Status == SubmissionStatus.Accepted
                            || x.Status == SubmissionStatus.AcceptedWithWarnings
                            || x.Status == SubmissionStatus.Historical))
            .OrderByDescending(x => x.ReturnPeriodId)
            .FirstOrDefaultAsync(ct);

        if (previousSubmission is null)
        {
            return result;
        }

        foreach (var field in carryForwardFields)
        {
            var value = await _dataRepository.ReadFieldValue(returnCode, previousSubmission.Id, field.FieldName, ct);
            if (value is not null)
            {
                result.Values[field.FieldName] = value;
            }
        }

        result.SourceSubmissionId = previousSubmission.Id;
        result.SourceReturnPeriodId = previousSubmission.ReturnPeriodId;
        result.SourcePeriodLabel = previousSubmission.ReturnPeriod is null
            ? $"Period #{previousSubmission.ReturnPeriodId}"
            : FormatPeriodLabel(previousSubmission.ReturnPeriod);

        return result;
    }

    private static string FormatPeriodLabel(ReturnPeriod period)
    {
        var frequency = period.Frequency?.Trim() ?? string.Empty;
        if (frequency.Equals("Quarterly", StringComparison.OrdinalIgnoreCase) && period.Quarter.HasValue)
        {
            return $"Q{period.Quarter.Value} {period.Year}";
        }

        if (frequency.Equals("SemiAnnual", StringComparison.OrdinalIgnoreCase))
        {
            var half = period.Month <= 6 ? 1 : 2;
            return $"H{half} {period.Year}";
        }

        if (frequency.Equals("Annual", StringComparison.OrdinalIgnoreCase))
        {
            return $"FY {period.Year}";
        }

        var month = period.Month is >= 1 and <= 12 ? period.Month : 1;
        return new DateTime(period.Year, month, 1).ToString("MMMM yyyy");
    }
}
