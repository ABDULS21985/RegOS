using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly MetadataDbContext _db;

    public TemplateRepository(MetadataDbContext db) => _db = db;

    public async Task<ReturnTemplate?> GetById(int id, CancellationToken ct = default)
    {
        return await _db.ReturnTemplates
            .Include(t => t.Versions)
                .ThenInclude(v => v.Fields)
            .Include(t => t.Versions)
                .ThenInclude(v => v.ItemCodes)
            .Include(t => t.Versions)
                .ThenInclude(v => v.IntraSheetFormulas)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<ReturnTemplate?> GetByReturnCode(string returnCode, CancellationToken ct = default)
    {
        return await _db.ReturnTemplates
            .Include(t => t.Versions)
                .ThenInclude(v => v.Fields)
            .Include(t => t.Versions)
                .ThenInclude(v => v.ItemCodes)
            .Include(t => t.Versions)
                .ThenInclude(v => v.IntraSheetFormulas)
            .FirstOrDefaultAsync(t => t.ReturnCode == returnCode, ct);
    }

    public async Task<ReturnTemplate?> GetPublishedByReturnCode(string returnCode, CancellationToken ct = default)
    {
        return await _db.ReturnTemplates
            .Include(t => t.Versions.Where(v => v.Status == TemplateStatus.Published))
                .ThenInclude(v => v.Fields)
            .Include(t => t.Versions.Where(v => v.Status == TemplateStatus.Published))
                .ThenInclude(v => v.ItemCodes)
            .Include(t => t.Versions.Where(v => v.Status == TemplateStatus.Published))
                .ThenInclude(v => v.IntraSheetFormulas)
            .FirstOrDefaultAsync(t => t.ReturnCode == returnCode, ct);
    }

    public async Task<IReadOnlyList<ReturnTemplate>> GetAll(CancellationToken ct = default)
    {
        return await _db.ReturnTemplates
            .Include(t => t.Versions)
            .OrderBy(t => t.ReturnCode)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReturnTemplate>> GetByFrequency(string frequency, CancellationToken ct = default)
    {
        return await _db.ReturnTemplates
            .Where(t => t.Frequency.ToString() == frequency)
            .OrderBy(t => t.ReturnCode)
            .ToListAsync(ct);
    }

    public async Task Add(ReturnTemplate template, CancellationToken ct = default)
    {
        _db.ReturnTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task Update(ReturnTemplate template, CancellationToken ct = default)
    {
        _db.ReturnTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsByReturnCode(string returnCode, CancellationToken ct = default)
    {
        return await _db.ReturnTemplates.AnyAsync(t => t.ReturnCode == returnCode, ct);
    }
}
