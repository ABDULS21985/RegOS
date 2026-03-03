using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly MetadataDbContext _db;

    public PasswordResetTokenRepository(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task Create(PasswordResetToken token, CancellationToken ct = default)
    {
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PasswordResetToken?> GetByToken(string token, CancellationToken ct = default)
    {
        return await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, ct);
    }

    public async Task Update(PasswordResetToken token, CancellationToken ct = default)
    {
        _db.PasswordResetTokens.Update(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task InvalidateAllForUser(int userId, CancellationToken ct = default)
    {
        var activeTokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.IsUsed = true;
            token.UsedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
