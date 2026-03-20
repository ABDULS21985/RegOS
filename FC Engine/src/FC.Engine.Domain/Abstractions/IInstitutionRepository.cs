using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IInstitutionRepository
{
    Task<Institution?> GetById(int id, CancellationToken ct = default);
    Task Update(Institution institution, CancellationToken ct = default);

    /// <summary>Get all child institutions for the given parent (direct children only).</summary>
    Task<IReadOnlyList<Institution>> GetChildren(int parentInstitutionId, CancellationToken ct = default);

    /// <summary>Get the full hierarchy tree (recursive) rooted at the given institution.</summary>
    Task<IReadOnlyList<Institution>> GetHierarchy(int rootInstitutionId, CancellationToken ct = default);

    /// <summary>Get all institutions belonging to a tenant, including hierarchy relationships.</summary>
    Task<IReadOnlyList<Institution>> GetByTenant(Guid tenantId, CancellationToken ct = default);

    /// <summary>Get all descendant institution IDs (recursive) for aggregation queries.</summary>
    Task<IReadOnlyList<int>> GetDescendantIds(int parentInstitutionId, CancellationToken ct = default);
}
