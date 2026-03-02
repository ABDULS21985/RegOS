using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IPortalUserRepository
{
    Task<PortalUser?> GetByUsername(string username, CancellationToken ct = default);
    Task<PortalUser?> GetById(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PortalUser>> GetAll(CancellationToken ct = default);
    Task<PortalUser> Create(PortalUser user, CancellationToken ct = default);
    Task Update(PortalUser user, CancellationToken ct = default);
    Task<bool> UsernameExists(string username, CancellationToken ct = default);
}
