namespace FC.Engine.Domain.Abstractions;

public interface IDraftDataService
{
    Task<int> GetOrCreateDraftSubmission(
        Guid tenantId,
        string returnCode,
        int institutionId,
        int returnPeriodId,
        int? submittedByUserId = null,
        CancellationToken ct = default);

    Task SaveDraftRows(
        Guid tenantId,
        int submissionId,
        string returnCode,
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken ct = default);
}
