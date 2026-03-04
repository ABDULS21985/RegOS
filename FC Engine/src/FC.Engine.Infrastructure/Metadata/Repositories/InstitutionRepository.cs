using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class InstitutionRepository : IInstitutionRepository
{
    private readonly MetadataDbContext _db;

    public InstitutionRepository(MetadataDbContext db) => _db = db;

    public async Task<Institution?> GetById(int id, CancellationToken ct = default)
        => await _db.Set<Institution>()
            .Include(i => i.ChildInstitutions)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task Update(Institution institution, CancellationToken ct = default)
    {
        _db.Set<Institution>().Update(institution);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Institution>> GetChildren(int parentInstitutionId, CancellationToken ct = default)
    {
        return await _db.Set<Institution>()
            .Where(i => i.ParentInstitutionId == parentInstitutionId && i.IsActive)
            .OrderBy(i => i.InstitutionName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Institution>> GetHierarchy(int rootInstitutionId, CancellationToken ct = default)
    {
        // Load all institutions for the same tenant, then build tree in memory
        var root = await _db.Set<Institution>().FindAsync(new object[] { rootInstitutionId }, ct);
        if (root is null) return Array.Empty<Institution>();

        var allInTenant = await _db.Set<Institution>()
            .Where(i => i.TenantId == root.TenantId && i.IsActive)
            .ToListAsync(ct);

        // Walk the tree starting from root
        var result = new List<Institution>();
        CollectDescendants(rootInstitutionId, allInTenant, result);
        return result;
    }

    public async Task<IReadOnlyList<Institution>> GetByTenant(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Set<Institution>()
            .Where(i => i.TenantId == tenantId && i.IsActive)
            .Include(i => i.ChildInstitutions)
            .OrderBy(i => i.ParentInstitutionId.HasValue ? 1 : 0)
            .ThenBy(i => i.InstitutionName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetDescendantIds(int parentInstitutionId, CancellationToken ct = default)
    {
        var root = await _db.Set<Institution>().FindAsync(new object[] { parentInstitutionId }, ct);
        if (root is null) return Array.Empty<int>();

        var allInTenant = await _db.Set<Institution>()
            .Where(i => i.TenantId == root.TenantId && i.IsActive)
            .Select(i => new { i.Id, i.ParentInstitutionId })
            .ToListAsync(ct);

        var ids = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(parentInstitutionId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in allInTenant.Where(i => i.ParentInstitutionId == current))
            {
                ids.Add(child.Id);
                queue.Enqueue(child.Id);
            }
        }

        return ids;
    }

    private static void CollectDescendants(int parentId, List<Institution> all, List<Institution> result)
    {
        var children = all.Where(i => i.ParentInstitutionId == parentId).ToList();
        foreach (var child in children)
        {
            result.Add(child);
            CollectDescendants(child.Id, all, result);
        }
    }
}
