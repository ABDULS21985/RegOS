using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Abstractions;

public interface IDsarService
{
    Task<DataSubjectRequest> CreateRequest(
        Guid tenantId,
        DataSubjectRequestType requestType,
        int requestedBy,
        string requestedByUserType,
        string? description,
        CancellationToken ct = default);

    Task<IReadOnlyList<DataSubjectRequest>> GetRequests(Guid tenantId, CancellationToken ct = default);

    Task<string> GenerateAccessPackage(int dsarId, int processedByUserId, CancellationToken ct = default);

    Task ProcessErasure(int dsarId, int approvedByDpoId, CancellationToken ct = default);

    Task<DataSubjectRequest> UpdateStatus(
        int dsarId,
        DataSubjectRequestStatus status,
        int processedByUserId,
        string? responseNotes,
        CancellationToken ct = default);
}
