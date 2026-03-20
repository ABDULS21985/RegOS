using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly MetadataDbContext _db;

    public EmailTemplateRepository(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<EmailTemplate?> GetActiveTemplate(string templateCode, Guid tenantId, CancellationToken ct = default)
    {
        var tenantTemplate = await _db.EmailTemplates
            .Where(t => t.TemplateCode == templateCode && t.TenantId == tenantId && t.IsActive)
            .FirstOrDefaultAsync(ct);

        if (tenantTemplate is not null)
        {
            return tenantTemplate;
        }

        return await _db.EmailTemplates
            .Where(t => t.TemplateCode == templateCode && t.TenantId == null && t.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<EmailTemplate>> GetTemplatesForTenant(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.EmailTemplates
            .Where(t => t.TenantId == tenantId || t.TenantId == null)
            .Where(t => t.IsActive)
            .OrderBy(t => t.TemplateCode)
            .ThenByDescending(t => t.TenantId.HasValue)
            .ToListAsync(ct);
    }
}
