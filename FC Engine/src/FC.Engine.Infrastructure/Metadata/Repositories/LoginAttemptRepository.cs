using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class LoginAttemptRepository : ILoginAttemptRepository
{
    private readonly MetadataDbContext _db;

    public LoginAttemptRepository(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task Create(LoginAttempt attempt, CancellationToken ct = default)
    {
        _db.LoginAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CountRecentFailures(string username, TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        return await _db.LoginAttempts
            .CountAsync(a => a.Username == username && !a.Succeeded && a.AttemptedAt >= cutoff, ct);
    }
}
