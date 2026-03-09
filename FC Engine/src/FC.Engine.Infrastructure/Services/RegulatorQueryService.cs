using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models.BatchSubmission;
using FC.Engine.Infrastructure.Export.ChannelAdapters;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

/// <summary>
/// Manages regulatory queries and institution responses per RG-34.
/// Tenant-isolated (R-05): all queries filtered by institutionId.
/// </summary>
public sealed class RegulatorQueryService : IRegulatorQueryService
{
    private readonly MetadataDbContext _db;
    private readonly IEnumerable<IRegulatoryChannelAdapter> _adapters;
    private readonly ILogger<RegulatorQueryService> _logger;

    public RegulatorQueryService(
        MetadataDbContext db,
        IEnumerable<IRegulatoryChannelAdapter> adapters,
        ILogger<RegulatorQueryService> logger)
    {
        _db = db;
        _adapters = adapters;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RegulatoryQuerySummary>> GetOpenQueriesAsync(
        int institutionId, string? regulatorCode,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.RegulatoryQueryRecords
            .AsNoTracking()
            .Where(q => q.InstitutionId == institutionId
                && q.Status != "CLOSED")
            .Include(q => q.Batch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(regulatorCode))
            query = query.Where(q => q.RegulatorCode == regulatorCode.ToUpperInvariant());

        var items = await query
            .OrderBy(q => q.Priority == "CRITICAL" ? 0
                : q.Priority == "HIGH" ? 1
                : q.Priority == "NORMAL" ? 2 : 3)
            .ThenBy(q => q.DueDate)
            .ThenByDescending(q => q.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new RegulatoryQuerySummary(
                q.Id,
                q.BatchId,
                q.Batch != null ? q.Batch.BatchReference : string.Empty,
                q.RegulatorCode,
                q.QueryReference,
                q.QueryType,
                q.QueryText,
                q.DueDate,
                q.Priority,
                q.Status,
                q.AssignedToUserId,
                q.ReceivedAt,
                q.RespondedAt))
            .ToListAsync(ct);

        return items;
    }

    public async Task AssignQueryAsync(
        int institutionId, long queryId, int assignToUserId, CancellationToken ct = default)
    {
        var query = await _db.RegulatoryQueryRecords
            .FirstOrDefaultAsync(q => q.Id == queryId && q.InstitutionId == institutionId, ct)
            ?? throw new InvalidOperationException(
                $"Query {queryId} not found for institution {institutionId}.");

        if (query.Status == "CLOSED")
            throw new InvalidOperationException($"Cannot assign a closed query (id={queryId}).");

        query.AssignedToUserId = assignToUserId;
        if (query.Status == "OPEN")
            query.Status = "IN_PROGRESS";

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Query {QueryId} ({RegulatorCode}) assigned to user {UserId}",
            queryId, query.RegulatorCode, assignToUserId);
    }

    public async Task<long> SubmitResponseAsync(
        int institutionId, long queryId, string responseText,
        IReadOnlyList<AttachmentPayload> attachments,
        int respondedByUserId, CancellationToken ct = default)
    {
        var queryRecord = await _db.RegulatoryQueryRecords
            .Include(q => q.Responses)
            .FirstOrDefaultAsync(q => q.Id == queryId && q.InstitutionId == institutionId, ct)
            ?? throw new InvalidOperationException(
                $"Query {queryId} not found for institution {institutionId}.");

        if (queryRecord.Status == "CLOSED")
            throw new InvalidOperationException($"Query {queryId} is already closed.");

        // Persist the response + attachments
        var response = new QueryResponse
        {
            QueryId = queryId,
            InstitutionId = institutionId,
            ResponseText = responseText,
            AttachmentCount = attachments.Count,
            SubmittedToRegulator = false,
            CreatedBy = respondedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var att in attachments)
        {
            response.Attachments.Add(new QueryResponseAttachment
            {
                FileName = att.FileName,
                ContentType = att.ContentType,
                FileHash = att.FileHash,
                FileSizeBytes = att.Content.Length,
                BlobStoragePath = string.Empty   // populated by blob storage service if configured
            });
        }

        _db.QueryResponses.Add(response);
        await _db.SaveChangesAsync(ct);

        // Dispatch to regulator via the appropriate channel adapter
        var adapter = _adapters.FirstOrDefault(a =>
            string.Equals(a.RegulatorCode, queryRecord.RegulatorCode, StringComparison.OrdinalIgnoreCase));

        if (adapter is not null)
        {
            try
            {
                var emptySignature = new BatchSignatureInfo(
                    string.Empty, string.Empty, Array.Empty<byte>(),
                    string.Empty, DateTimeOffset.UtcNow, null);
                var responsePayload = new QueryResponsePayload(
                    QueryReference: queryRecord.QueryReference,
                    ResponseText: responseText,
                    Attachments: attachments.Select(a => new AttachmentPayload(
                        a.FileName, a.ContentType, a.Content, a.FileHash)).ToList(),
                    Signature: emptySignature);

                var receipt = await adapter.SubmitQueryResponseAsync(responsePayload, ct);

                response.SubmittedToRegulator = true;
                response.SubmittedAt = DateTime.UtcNow;
                response.RegulatorAckRef = receipt.ReceiptReference;

                queryRecord.Status = "RESPONDED";
                queryRecord.RespondedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Query {QueryId} response submitted to {RegulatorCode}. AckRef: {AckRef}",
                    queryId, queryRecord.RegulatorCode, receipt.ReceiptReference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch query response to {RegulatorCode} for query {QueryId}",
                    queryRecord.RegulatorCode, queryId);
                // Response is persisted; dispatch can be retried separately
            }
        }
        else
        {
            _logger.LogWarning(
                "No adapter found for regulator {RegulatorCode}; query response saved locally only",
                queryRecord.RegulatorCode);
        }

        return response.Id;
    }
}
