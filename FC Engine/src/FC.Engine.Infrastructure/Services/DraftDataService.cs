using System.Globalization;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class DraftDataService : IDraftDataService
{
    private readonly MetadataDbContext _db;
    private readonly ITemplateMetadataCache _templateCache;
    private readonly IGenericDataRepository _dataRepository;

    public DraftDataService(
        MetadataDbContext db,
        ITemplateMetadataCache templateCache,
        IGenericDataRepository dataRepository)
    {
        _db = db;
        _templateCache = templateCache;
        _dataRepository = dataRepository;
    }

    public async Task<int> GetOrCreateDraftSubmission(
        Guid tenantId,
        string returnCode,
        int institutionId,
        int returnPeriodId,
        int? submittedByUserId = null,
        CancellationToken ct = default)
    {
        var existingDraft = await _db.Submissions
            .Where(x => x.TenantId == tenantId
                        && x.ReturnCode == returnCode
                        && x.InstitutionId == institutionId
                        && x.ReturnPeriodId == returnPeriodId
                        && x.Status == SubmissionStatus.Draft)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (existingDraft is not null)
        {
            if (submittedByUserId.HasValue && !existingDraft.SubmittedByUserId.HasValue)
            {
                existingDraft.SubmittedByUserId = submittedByUserId;
                await _db.SaveChangesAsync(ct);
            }

            return existingDraft.Id;
        }

        var template = await _templateCache.GetPublishedTemplate(tenantId, returnCode, ct);
        var submission = Submission.Create(institutionId, returnPeriodId, returnCode, tenantId);
        submission.SetTemplateVersion(template.CurrentVersion.Id);
        submission.Status = SubmissionStatus.Draft;
        submission.SubmittedByUserId = submittedByUserId;
        submission.ApprovalRequired = false;
        submission.SubmittedAt = DateTime.UtcNow;
        submission.CreatedAt = DateTime.UtcNow;

        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync(ct);
        return submission.Id;
    }

    public async Task SaveDraftRows(
        Guid tenantId,
        int submissionId,
        string returnCode,
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        var template = await _templateCache.GetPublishedTemplate(tenantId, returnCode, ct);
        var category = Enum.Parse<StructuralCategory>(template.StructuralCategory);
        var fields = template.CurrentVersion.Fields.OrderBy(x => x.FieldOrder).ToList();

        var record = new ReturnDataRecord(returnCode, template.CurrentVersion.Id, category);
        foreach (var inputRow in rows)
        {
            var row = new ReturnDataRow();
            var hasValue = false;

            foreach (var field in fields)
            {
                if (!inputRow.TryGetValue(field.FieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var converted = ConvertRawValue(raw, field.DataType);
                if (converted is null)
                {
                    continue;
                }

                row.SetValue(field.FieldName, converted);
                hasValue = true;
                if (field.IsKeyField)
                {
                    row.RowKey = converted.ToString();
                }
            }

            if (hasValue)
            {
                record.AddRow(row);
            }
        }

        if (record.Rows.Count == 0)
        {
            record.AddRow(new ReturnDataRow());
        }

        await _dataRepository.DeleteBySubmission(returnCode, submissionId, ct);
        await _dataRepository.Save(record, submissionId, ct);

        var submission = await _db.Submissions.FirstOrDefaultAsync(x => x.Id == submissionId, ct);
        if (submission is not null)
        {
            submission.Status = SubmissionStatus.Draft;
            submission.SubmittedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static object? ConvertRawValue(string rawValue, FieldDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return dataType switch
        {
            FieldDataType.Integer => int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
            FieldDataType.Money or FieldDataType.Decimal or FieldDataType.Percentage =>
                decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
            FieldDataType.Date => DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt) ? dt.Date : null,
            FieldDataType.Boolean => bool.TryParse(rawValue, out var b) ? b : null,
            _ => rawValue.Trim()
        };
    }
}
