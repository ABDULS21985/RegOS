using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

/// <summary>
/// Appends immutable audit records to submission_batch_audit_log.
/// Uses direct EF Core inserts (no UoW) to guarantee log durability
/// even if the surrounding transaction rolls back.
/// </summary>
public sealed class SubmissionBatchAuditLogger : ISubmissionBatchAuditLogger
{
    private readonly MetadataDbContext _db;
    private readonly ILogger<SubmissionBatchAuditLogger> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SubmissionBatchAuditLogger(
        MetadataDbContext db,
        ILogger<SubmissionBatchAuditLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        long batchId,
        int institutionId,
        Guid correlationId,
        string action,
        object? detail,
        int? performedBy,
        CancellationToken ct = default)
    {
        try
        {
            var entry = new SubmissionBatchAuditLog
            {
                BatchId = batchId,
                InstitutionId = institutionId,
                CorrelationId = correlationId,
                Action = action,
                Detail = detail is null ? null : JsonSerializer.Serialize(detail, JsonOpts),
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow
            };

            _db.SubmissionBatchAuditLogs.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failures must never abort the submission pipeline
            _logger.LogError(ex,
                "Failed to write audit log for batch {BatchId}, action {Action}", batchId, action);
        }
    }
}
