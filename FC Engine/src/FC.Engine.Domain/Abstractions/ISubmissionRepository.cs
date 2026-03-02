using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface ISubmissionRepository
{
    Task<Submission?> GetById(int id, CancellationToken ct = default);
    Task<Submission?> GetByIdWithReport(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetByInstitution(int institutionId, CancellationToken ct = default);
    Task Add(Submission submission, CancellationToken ct = default);
    Task Update(Submission submission, CancellationToken ct = default);
}
