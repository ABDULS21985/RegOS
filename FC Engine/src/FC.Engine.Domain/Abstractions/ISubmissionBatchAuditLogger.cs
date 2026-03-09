namespace FC.Engine.Domain.Abstractions;

public interface ISubmissionBatchAuditLogger
{
    Task LogAsync(
        long batchId,
        int institutionId,
        Guid correlationId,
        string action,
        object? detail,
        int? performedBy,
        CancellationToken ct = default);
}
