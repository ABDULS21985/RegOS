using FC.Engine.Domain.Models.BatchSubmission;

namespace FC.Engine.Domain.Abstractions;

public interface IRegulatorQueryService
{
    Task<IReadOnlyList<RegulatoryQuerySummary>> GetOpenQueriesAsync(
        int institutionId, string? regulatorCode,
        int page, int pageSize, CancellationToken ct = default);

    Task AssignQueryAsync(
        int institutionId, long queryId, int assignToUserId, CancellationToken ct = default);

    Task<long> SubmitResponseAsync(
        int institutionId, long queryId, string responseText,
        IReadOnlyList<AttachmentPayload> attachments,
        int respondedByUserId, CancellationToken ct = default);
}

public sealed record RegulatoryQuerySummary(
    long QueryId,
    long BatchId,
    string BatchReference,
    string RegulatorCode,
    string QueryReference,
    string QueryType,
    string QueryText,
    DateOnly? DueDate,
    string Priority,
    string Status,
    int? AssignedToUserId,
    DateTime ReceivedAt,
    DateTime? RespondedAt);
