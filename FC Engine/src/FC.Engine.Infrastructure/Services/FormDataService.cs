using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class FormDataService : IFormDataService
{
    private readonly MetadataDbContext _db;

    public FormDataService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task SaveDraftAsync(
        Guid tenantId,
        int institutionId,
        string returnCode,
        string period,
        List<Dictionary<string, string>> rows,
        string savedBy,
        CancellationToken ct = default)
    {
        var existing = await _db.ReturnDrafts
            .FirstOrDefaultAsync(d =>
                d.TenantId == tenantId &&
                d.InstitutionId == institutionId &&
                d.ReturnCode == returnCode &&
                d.Period == period, ct);

        var json = JsonSerializer.Serialize(rows);

        if (existing is null)
        {
            _db.ReturnDrafts.Add(new ReturnDraft
            {
                TenantId = tenantId,
                InstitutionId = institutionId,
                ReturnCode = returnCode,
                Period = period,
                DataJson = json,
                LastSavedAt = DateTime.UtcNow,
                SavedBy = savedBy
            });
        }
        else
        {
            existing.DataJson = json;
            existing.LastSavedAt = DateTime.UtcNow;
            existing.SavedBy = savedBy;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<ReturnDraft?> GetDraftAsync(
        Guid tenantId,
        int institutionId,
        string returnCode,
        string period,
        CancellationToken ct = default)
    {
        return _db.ReturnDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.TenantId == tenantId &&
                d.InstitutionId == institutionId &&
                d.ReturnCode == returnCode &&
                d.Period == period, ct);
    }

    public async Task DeleteDraftAsync(
        Guid tenantId,
        int institutionId,
        string returnCode,
        string period,
        CancellationToken ct = default)
    {
        var draft = await _db.ReturnDrafts
            .FirstOrDefaultAsync(d =>
                d.TenantId == tenantId &&
                d.InstitutionId == institutionId &&
                d.ReturnCode == returnCode &&
                d.Period == period, ct);

        if (draft is not null)
        {
            _db.ReturnDrafts.Remove(draft);
            await _db.SaveChangesAsync(ct);
        }
    }
}
