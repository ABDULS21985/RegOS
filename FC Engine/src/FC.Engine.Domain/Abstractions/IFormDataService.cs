using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IFormDataService
{
    /// <summary>
    /// Upserts a JSON snapshot of the current form state for crash recovery and conflict detection.
    /// </summary>
    Task SaveDraftAsync(
        Guid tenantId,
        int institutionId,
        string returnCode,
        string period,
        List<Dictionary<string, string>> rows,
        string savedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent draft snapshot for a given return+period, or null if none exists.
    /// </summary>
    Task<ReturnDraft?> GetDraftAsync(
        Guid tenantId,
        int institutionId,
        string returnCode,
        string period,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the draft snapshot on successful final submission.
    /// </summary>
    Task DeleteDraftAsync(
        Guid tenantId,
        int institutionId,
        string returnCode,
        string period,
        CancellationToken ct = default);
}
