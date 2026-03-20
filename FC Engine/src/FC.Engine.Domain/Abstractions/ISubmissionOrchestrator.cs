using FC.Engine.Domain.Models.BatchSubmission;

namespace FC.Engine.Domain.Abstractions;

/// <summary>
/// Orchestrates end-to-end regulatory submission: sign → dispatch → track → respond.
/// All methods enforce tenant isolation via institutionId.
/// </summary>
public interface ISubmissionOrchestrator
{
    Task<BatchSubmissionResult> SubmitBatchAsync(
        int institutionId,
        string regulatorCode,
        IReadOnlyList<int> submissionIds,
        int submittedByUserId,
        CancellationToken ct = default);

    Task<BatchSubmissionResult> RetryBatchAsync(
        int institutionId,
        long batchId,
        CancellationToken ct = default);

    Task<BatchStatusRefreshResult> RefreshStatusAsync(
        int institutionId,
        long batchId,
        CancellationToken ct = default);
}

public sealed record BatchSubmissionResult(
    bool Success,
    long BatchId,
    string BatchReference,
    string Status,
    BatchRegulatorReceipt? Receipt,
    string? ErrorMessage,
    Guid CorrelationId);

public sealed record BatchStatusRefreshResult(
    long BatchId,
    string PreviousStatus,
    string CurrentStatus,
    bool StatusChanged,
    BatchRegulatorReceipt? LatestReceipt);
