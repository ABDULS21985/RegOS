namespace FC.Engine.Portal.Services;

using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models.BatchSubmission;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

public sealed class SubmissionBatchPortalService
{
    private readonly MetadataDbContext _db;
    private readonly ISubmissionOrchestrator _orchestrator;
    private readonly IRegulatorQueryService _queryService;

    public SubmissionBatchPortalService(
        MetadataDbContext db,
        ISubmissionOrchestrator orchestrator,
        IRegulatorQueryService queryService)
    {
        _db = db;
        _orchestrator = orchestrator;
        _queryService = queryService;
    }

    public async Task<List<RegulatoryChannelOption>> GetAvailableRegulatorsAsync(CancellationToken ct = default)
    {
        return await _db.RegulatoryChannels
            .AsNoTracking()
            .Where(channel => channel.IsActive)
            .OrderBy(channel => channel.RegulatorName)
            .ThenBy(channel => channel.RegulatorCode)
            .Select(channel => new RegulatoryChannelOption
            {
                RegulatorCode = channel.RegulatorCode,
                RegulatorName = channel.RegulatorName,
                PortalName = channel.PortalName,
                IntegrationMethod = channel.IntegrationMethod,
                RequiresCertificate = channel.RequiresCertificate
            })
            .ToListAsync(ct);
    }

    public async Task<SubmissionBatchListResult> GetBatchesAsync(
        int institutionId,
        string? regulatorCode,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.SubmissionBatches
            .AsNoTracking()
            .Where(batch => batch.InstitutionId == institutionId);

        if (!string.IsNullOrWhiteSpace(regulatorCode))
        {
            var normalizedRegulatorCode = regulatorCode.Trim().ToUpperInvariant();
            query = query.Where(batch => batch.RegulatorCode == normalizedRegulatorCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(batch => batch.Status == normalizedStatus);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(batch => batch.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(batch => new SubmissionBatchListItem
            {
                Id = batch.Id,
                BatchReference = batch.BatchReference,
                RegulatorCode = batch.RegulatorCode,
                RegulatorName = batch.Channel != null && !string.IsNullOrWhiteSpace(batch.Channel.RegulatorName)
                    ? batch.Channel.RegulatorName
                    : batch.RegulatorCode,
                Status = batch.Status,
                ItemCount = batch.Items.Count,
                SubmittedAt = batch.SubmittedAt,
                AcknowledgedAt = batch.AcknowledgedAt,
                FinalStatusAt = batch.FinalStatusAt,
                RetryCount = batch.RetryCount,
                LatestReceipt = batch.Receipts
                    .OrderByDescending(receipt => receipt.ReceivedAt)
                    .Select(receipt => receipt.ReceiptReference)
                    .FirstOrDefault(),
                OpenQueries = batch.Queries.Count(query => query.Status == "OPEN" || query.Status == "IN_PROGRESS"),
                CreatedAt = batch.CreatedAt
            })
            .ToListAsync(ct);

        return new SubmissionBatchListResult
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items
        };
    }

    public async Task<int> GetOpenQueryCountAsync(int institutionId, CancellationToken ct = default)
    {
        return await _db.RegulatoryQueryRecords
            .AsNoTracking()
            .CountAsync(query => query.InstitutionId == institutionId && query.Status != "CLOSED", ct);
    }

    public async Task<List<RegulatoryQueryListItem>> GetOpenQueriesAsync(
        int institutionId,
        string? regulatorCode,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 250);

        var query = _db.RegulatoryQueryRecords
            .AsNoTracking()
            .Where(item => item.InstitutionId == institutionId && item.Status != "CLOSED");

        if (!string.IsNullOrWhiteSpace(regulatorCode))
        {
            var normalizedRegulatorCode = regulatorCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.RegulatorCode == normalizedRegulatorCode);
        }

        var items = await query
            .OrderBy(item => item.Priority == "CRITICAL" ? 0
                : item.Priority == "HIGH" ? 1
                : item.Priority == "NORMAL" ? 2 : 3)
            .ThenBy(item => item.DueDate)
            .ThenByDescending(item => item.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new RegulatoryQueryListItem
            {
                QueryId = item.Id,
                BatchId = item.BatchId,
                BatchReference = item.Batch != null ? item.Batch.BatchReference : string.Empty,
                RegulatorCode = item.RegulatorCode,
                RegulatorName = item.Batch != null && item.Batch.Channel != null && !string.IsNullOrWhiteSpace(item.Batch.Channel.RegulatorName)
                    ? item.Batch.Channel.RegulatorName
                    : item.RegulatorCode,
                QueryReference = item.QueryReference,
                QueryType = item.QueryType,
                QueryText = item.QueryText,
                DueDate = item.DueDate,
                Priority = item.Priority,
                Status = item.Status,
                AssignedToUserId = item.AssignedToUserId,
                ReceivedAt = item.ReceivedAt,
                RespondedAt = item.RespondedAt,
                ResponseCount = item.Responses.Count,
                LastResponseAt = item.Responses
                    .OrderByDescending(response => response.CreatedAt)
                    .Select(response => (DateTime?)response.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        await PopulateUserNamesAsync(
            items.Select(item => item.AssignedToUserId).OfType<int>(),
            nameMap =>
            {
                foreach (var item in items)
                {
                    if (item.AssignedToUserId.HasValue && nameMap.TryGetValue(item.AssignedToUserId.Value, out var assignedToName))
                    {
                        item.AssignedToName = assignedToName;
                    }
                }
            },
            ct);

        return items;
    }

    public async Task<SubmissionBatchDetailModel?> GetBatchDetailAsync(
        int institutionId,
        long batchId,
        CancellationToken ct = default)
    {
        var batch = await _db.SubmissionBatches
            .AsNoTracking()
            .Where(item => item.Id == batchId && item.InstitutionId == institutionId)
            .Select(item => new SubmissionBatchDetailModel
            {
                Id = item.Id,
                InstitutionId = item.InstitutionId,
                BatchReference = item.BatchReference,
                RegulatorCode = item.RegulatorCode,
                RegulatorName = item.Channel != null && !string.IsNullOrWhiteSpace(item.Channel.RegulatorName)
                    ? item.Channel.RegulatorName
                    : item.RegulatorCode,
                Status = item.Status,
                CorrelationId = item.CorrelationId,
                LastError = item.LastError,
                RetryCount = item.RetryCount,
                CreatedAt = item.CreatedAt,
                SubmittedAt = item.SubmittedAt,
                AcknowledgedAt = item.AcknowledgedAt,
                FinalStatusAt = item.FinalStatusAt
            })
            .FirstOrDefaultAsync(ct);

        if (batch is null)
        {
            return null;
        }

        batch.Items = await _db.SubmissionItems
            .AsNoTracking()
            .Where(item => item.BatchId == batchId)
            .OrderBy(item => item.ReturnCode)
            .Select(item => new SubmissionBatchItemModel
            {
                Id = item.Id,
                SubmissionId = item.SubmissionId,
                ReturnCode = item.ReturnCode,
                ReportingPeriod = item.ReportingPeriod,
                ExportFormat = item.ExportFormat,
                ExportPayloadSize = item.ExportPayloadSize,
                ExportPayloadHash = item.ExportPayloadHash,
                Status = item.Status
            })
            .ToListAsync(ct);

        batch.Receipts = await _db.SubmissionBatchReceipts
            .AsNoTracking()
            .Where(receipt => receipt.BatchId == batchId)
            .OrderByDescending(receipt => receipt.ReceivedAt)
            .Select(receipt => new SubmissionBatchReceiptModel
            {
                RegulatorCode = receipt.RegulatorCode,
                ReceiptReference = receipt.ReceiptReference,
                ReceiptTimestamp = receipt.ReceiptTimestamp,
                HttpStatusCode = receipt.HttpStatusCode,
                ReceivedAt = receipt.ReceivedAt
            })
            .ToListAsync(ct);

        batch.Queries = await _db.RegulatoryQueryRecords
            .AsNoTracking()
            .Where(query => query.BatchId == batchId && query.Status != "CLOSED")
            .OrderBy(query => query.DueDate)
            .ThenByDescending(query => query.ReceivedAt)
            .Select(query => new SubmissionBatchQueryModel
            {
                QueryId = query.Id,
                QueryReference = query.QueryReference,
                QueryType = query.QueryType,
                QueryText = query.QueryText,
                DueDate = query.DueDate,
                Priority = query.Priority,
                Status = query.Status,
                AssignedToUserId = query.AssignedToUserId,
                ReceivedAt = query.ReceivedAt,
                RespondedAt = query.RespondedAt,
                ResponseCount = query.Responses.Count,
                LastResponseAt = query.Responses
                    .OrderByDescending(response => response.CreatedAt)
                    .Select(response => (DateTime?)response.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        batch.AuditLogs = await _db.SubmissionBatchAuditLogs
            .AsNoTracking()
            .Where(log => log.BatchId == batchId)
            .OrderByDescending(log => log.PerformedAt)
            .Take(20)
            .Select(log => new SubmissionBatchAuditModel
            {
                Action = log.Action,
                Detail = log.Detail,
                PerformedBy = log.PerformedBy,
                PerformedAt = log.PerformedAt
            })
            .ToListAsync(ct);

        var userIds = batch.Queries
            .Select(query => query.AssignedToUserId)
            .Concat(batch.AuditLogs.Select(log => log.PerformedBy))
            .OfType<int>()
            .Distinct()
            .ToList();

        await PopulateUserNamesAsync(
            userIds,
            nameMap =>
            {
                foreach (var query in batch.Queries)
                {
                    if (query.AssignedToUserId.HasValue && nameMap.TryGetValue(query.AssignedToUserId.Value, out var assignedToName))
                    {
                        query.AssignedToName = assignedToName;
                    }
                }

                foreach (var audit in batch.AuditLogs)
                {
                    if (audit.PerformedBy.HasValue && nameMap.TryGetValue(audit.PerformedBy.Value, out var performedByName))
                    {
                        audit.PerformedByName = performedByName;
                    }
                }
            },
            ct);

        return batch;
    }

    public async Task<RegulatoryQueryDetailModel?> GetQueryDetailAsync(
        int institutionId,
        long queryId,
        CancellationToken ct = default)
    {
        var query = await _db.RegulatoryQueryRecords
            .AsNoTracking()
            .Where(item => item.Id == queryId && item.InstitutionId == institutionId)
            .Select(item => new RegulatoryQueryDetailModel
            {
                QueryId = item.Id,
                BatchId = item.BatchId,
                BatchReference = item.Batch != null ? item.Batch.BatchReference : string.Empty,
                BatchStatus = item.Batch != null ? item.Batch.Status : string.Empty,
                RegulatorCode = item.RegulatorCode,
                RegulatorName = item.Batch != null && item.Batch.Channel != null && !string.IsNullOrWhiteSpace(item.Batch.Channel.RegulatorName)
                    ? item.Batch.Channel.RegulatorName
                    : item.RegulatorCode,
                QueryReference = item.QueryReference,
                QueryType = item.QueryType,
                QueryText = item.QueryText,
                DueDate = item.DueDate,
                Priority = item.Priority,
                Status = item.Status,
                AssignedToUserId = item.AssignedToUserId,
                ReceivedAt = item.ReceivedAt,
                RespondedAt = item.RespondedAt
            })
            .FirstOrDefaultAsync(ct);

        if (query is null)
        {
            return null;
        }

        query.Responses = await _db.QueryResponses
            .AsNoTracking()
            .Where(response => response.QueryId == queryId)
            .OrderByDescending(response => response.CreatedAt)
            .Select(response => new RegulatoryQueryResponseModel
            {
                ResponseId = response.Id,
                ResponseText = response.ResponseText,
                AttachmentCount = response.AttachmentCount,
                SubmittedToRegulator = response.SubmittedToRegulator,
                SubmittedAt = response.SubmittedAt,
                RegulatorAckRef = response.RegulatorAckRef,
                CreatedBy = response.CreatedBy,
                CreatedAt = response.CreatedAt,
                Attachments = response.Attachments
                    .OrderBy(attachment => attachment.FileName)
                    .Select(attachment => new RegulatoryQueryAttachmentModel
                    {
                        Id = attachment.Id,
                        FileName = attachment.FileName,
                        ContentType = attachment.ContentType,
                        FileSizeBytes = attachment.FileSizeBytes,
                        FileHash = attachment.FileHash,
                        CreatedAt = attachment.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        var userIds = query.Responses
            .Select(response => response.CreatedBy)
            .Append(query.AssignedToUserId ?? 0)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        await PopulateUserNamesAsync(
            userIds,
            nameMap =>
            {
                if (query.AssignedToUserId.HasValue && nameMap.TryGetValue(query.AssignedToUserId.Value, out var assignedToName))
                {
                    query.AssignedToName = assignedToName;
                }

                foreach (var response in query.Responses)
                {
                    if (nameMap.TryGetValue(response.CreatedBy, out var createdByName))
                    {
                        response.CreatedByName = createdByName;
                    }
                }
            },
            ct);

        return query;
    }

    public async Task<Dictionary<int, SubmissionBatchEligibilityState>> GetExistingBatchStatesAsync(
        int institutionId,
        string regulatorCode,
        IReadOnlyCollection<int> submissionIds,
        CancellationToken ct = default)
    {
        if (submissionIds.Count == 0 || string.IsNullOrWhiteSpace(regulatorCode))
        {
            return new Dictionary<int, SubmissionBatchEligibilityState>();
        }

        var normalizedRegulatorCode = regulatorCode.Trim().ToUpperInvariant();
        var items = await _db.SubmissionItems
            .AsNoTracking()
            .Where(item =>
                item.InstitutionId == institutionId
                && item.RegulatorCode == normalizedRegulatorCode
                && submissionIds.Contains(item.SubmissionId))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new SubmissionBatchEligibilityState
            {
                SubmissionId = item.SubmissionId,
                BatchId = item.BatchId,
                BatchReference = item.Batch != null ? item.Batch.BatchReference : string.Empty,
                BatchStatus = item.Batch != null ? item.Batch.Status : string.Empty,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(ct);

        return items
            .GroupBy(item => item.SubmissionId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Task<BatchSubmissionResult> CreateBatchAsync(
        int institutionId,
        int submittedByUserId,
        string regulatorCode,
        IReadOnlyList<int> submissionIds,
        CancellationToken ct = default)
    {
        return _orchestrator.SubmitBatchAsync(institutionId, regulatorCode, submissionIds, submittedByUserId, ct);
    }

    public Task<BatchSubmissionResult> RetryBatchAsync(
        int institutionId,
        long batchId,
        CancellationToken ct = default)
    {
        return _orchestrator.RetryBatchAsync(institutionId, batchId, ct);
    }

    public Task<BatchStatusRefreshResult> RefreshStatusAsync(
        int institutionId,
        long batchId,
        CancellationToken ct = default)
    {
        return _orchestrator.RefreshStatusAsync(institutionId, batchId, ct);
    }

    public Task AssignQueryAsync(
        int institutionId,
        long queryId,
        int assignToUserId,
        CancellationToken ct = default)
    {
        return _queryService.AssignQueryAsync(institutionId, queryId, assignToUserId, ct);
    }

    public Task<long> SubmitQueryResponseAsync(
        int institutionId,
        long queryId,
        string responseText,
        IReadOnlyList<AttachmentPayload> attachments,
        int respondedByUserId,
        CancellationToken ct = default)
    {
        return _queryService.SubmitResponseAsync(institutionId, queryId, responseText, attachments, respondedByUserId, ct);
    }

    private async Task PopulateUserNamesAsync(
        IEnumerable<int> userIds,
        Action<Dictionary<int, string>> apply,
        CancellationToken ct)
    {
        var distinctUserIds = userIds
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        if (distinctUserIds.Count == 0)
        {
            apply(new Dictionary<int, string>());
            return;
        }

        var nameMap = await _db.InstitutionUsers
            .AsNoTracking()
            .Where(user => distinctUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, ct);

        apply(nameMap);
    }
}
