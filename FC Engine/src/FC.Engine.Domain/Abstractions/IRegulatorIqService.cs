using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IRegulatorIqService
{
    Task<RegulatorIqTurnResult> QueryAsync(
        RegulatorIqQueryRequest request,
        CancellationToken ct = default);

    Task<List<RegIqTurn>> GetConversationHistoryAsync(
        Guid conversationId,
        string regulatorId,
        CancellationToken ct = default);

    Task<Guid> StartExaminationSessionAsync(
        string regulatorId,
        Guid targetTenantId,
        CancellationToken ct = default);

    Task EndExaminationSessionAsync(
        Guid conversationId,
        string regulatorId,
        CancellationToken ct = default);

    Task<byte[]> ExportConversationPdfAsync(
        Guid conversationId,
        CancellationToken ct = default);

    Task<byte[]> GenerateExaminationBriefingPdfAsync(
        Guid targetTenantId,
        string regulatorCode,
        CancellationToken ct = default);

    Task SubmitFeedbackAsync(
        int turnId,
        int rating,
        string? feedbackText,
        string regulatorId,
        CancellationToken ct = default);
}
