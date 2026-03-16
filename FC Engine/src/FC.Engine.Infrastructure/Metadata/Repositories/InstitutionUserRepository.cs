using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class InstitutionUserRepository : IInstitutionUserRepository
{
    private readonly IDbContextFactory<MetadataDbContext> _dbFactory;

    public InstitutionUserRepository(IDbContextFactory<MetadataDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<InstitutionUser?> GetById(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<InstitutionUser?> GetByUsername(string username, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Username == username, ct);
    }

    public async Task<InstitutionUser?> GetByEmail(string email, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .Include(u => u.Institution)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<IReadOnlyList<InstitutionUser>> GetByInstitution(int institutionId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .Where(u => u.InstitutionId == institutionId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountByInstitution(int institutionId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .CountAsync(u => u.InstitutionId == institutionId, ct);
    }

    public async Task<bool> UsernameExists(string username, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .AnyAsync(u => u.Username == username, ct);
    }

    public async Task<bool> EmailExists(string email, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.InstitutionUsers
            .AnyAsync(u => u.Email == email, ct);
    }

    public async Task Create(InstitutionUser user, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.InstitutionUsers.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task Update(InstitutionUser user, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.InstitutionUsers.Update(user);
        await db.SaveChangesAsync(ct);
    }
}
