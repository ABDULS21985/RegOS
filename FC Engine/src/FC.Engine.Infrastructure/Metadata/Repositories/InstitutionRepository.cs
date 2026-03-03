using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class InstitutionRepository : IInstitutionRepository
{
    private readonly MetadataDbContext _db;

    public InstitutionRepository(MetadataDbContext db) => _db = db;

    public async Task<Institution?> GetById(int id, CancellationToken ct = default)
        => await _db.Set<Institution>().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task Update(Institution institution, CancellationToken ct = default)
    {
        _db.Set<Institution>().Update(institution);
        await _db.SaveChangesAsync(ct);
    }
}
