using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IInstitutionRepository
{
    Task<Institution?> GetById(int id, CancellationToken ct = default);
    Task Update(Institution institution, CancellationToken ct = default);
}
