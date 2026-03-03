using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IPortalUserRepository
{
    Task<PortalUser?> GetByUsername(string username, CancellationToken ct = default);
    Task<PortalUser?> GetByEmail(string email, CancellationToken ct = default);
    Task<PortalUser?> GetById(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PortalUser>> GetAll(CancellationToken ct = default);
    Task<PortalUser> Create(PortalUser user, CancellationToken ct = default);
    Task Update(PortalUser user, CancellationToken ct = default);
    Task<bool> UsernameExists(string username, CancellationToken ct = default);
}

public interface ILoginAttemptRepository
{
    Task Create(LoginAttempt attempt, CancellationToken ct = default);
    Task<int> CountRecentFailures(string username, TimeSpan window, CancellationToken ct = default);
}

public interface IPasswordResetTokenRepository
{
    Task Create(PasswordResetToken token, CancellationToken ct = default);
    Task<PasswordResetToken?> GetByToken(string token, CancellationToken ct = default);
    Task Update(PasswordResetToken token, CancellationToken ct = default);
    Task InvalidateAllForUser(int userId, CancellationToken ct = default);
}
