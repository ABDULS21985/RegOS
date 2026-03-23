using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class ModuleService : IModuleService
{
    private readonly IDbContextFactory<MetadataDbContext> _dbFactory;

    public ModuleService(IDbContextFactory<MetadataDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<ModuleSummaryDto>> GetModuleSummaries(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var modules = await db.Modules
            .AsNoTracking()
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.ModuleCode)
            .ToListAsync(ct);

        var allVersions = await db.ModuleVersions
            .AsNoTracking()
            .OrderByDescending(v => v.Id)
            .ToListAsync(ct);

        var latestVersions = allVersions
            .GroupBy(v => v.ModuleId)
            .Select(g => g.First())
            .ToDictionary(v => v.ModuleId);

        var rawTenantRows = await db.SubscriptionModules
            .AsNoTracking()
            .Where(sm => sm.IsActive)
            .Join(
                db.Subscriptions.AsNoTracking(),
                sm => sm.SubscriptionId,
                s => s.Id,
                (sm, s) => new { sm.ModuleId, s.TenantId })
            .ToListAsync(ct);

        var tenantCounts = rawTenantRows
            .GroupBy(x => x.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.TenantId).Distinct().Count());

        return modules.Select(m => new ModuleSummaryDto
        {
            Id = m.Id,
            ModuleCode = m.ModuleCode,
            ModuleName = m.ModuleName,
            RegulatorCode = m.RegulatorCode,
            SheetCount = m.SheetCount,
            IsActive = m.IsActive,
            CurrentVersion = latestVersions.TryGetValue(m.Id, out var version) ? version.VersionCode : null,
            ActiveTenants = tenantCounts.TryGetValue(m.Id, out var count) ? count : 0
        }).ToList();
    }

    public async Task<ModuleDetailDto?> GetModuleDetail(string moduleCode, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var module = await db.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleCode == moduleCode, ct);

        if (module is null)
            return null;

        var moduleVersions = await db.ModuleVersions
            .AsNoTracking()
            .Where(v => v.ModuleId == module.Id)
            .OrderByDescending(v => v.Id)
            .ToListAsync(ct);

        var templates = await db.ReturnTemplates
            .AsNoTracking()
            .Where(t => t.ModuleId == module.Id)
            .OrderBy(t => t.ReturnCode)
            .ToListAsync(ct);

        var templateIds = templates.Select(t => t.Id).ToList();
        var versions = await db.TemplateVersions
            .AsNoTracking()
            .Where(v => templateIds.Contains(v.TemplateId))
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

        var versionIds = versions.Select(v => v.Id).ToList();

        var fieldCounts = versionIds.Count == 0
            ? new Dictionary<int, int>()
            : await db.TemplateFields
                .AsNoTracking()
                .Where(f => versionIds.Contains(f.TemplateVersionId))
                .GroupBy(f => f.TemplateVersionId)
                .Select(g => new { VersionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VersionId, x => x.Count, ct);

        var formulaCounts = versionIds.Count == 0
            ? new Dictionary<int, int>()
            : await db.IntraSheetFormulas
                .AsNoTracking()
                .Where(f => versionIds.Contains(f.TemplateVersionId) && f.IsActive)
                .GroupBy(f => f.TemplateVersionId)
                .Select(g => new { VersionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VersionId, x => x.Count, ct);

        return new ModuleDetailDto
        {
            Id = module.Id,
            ModuleCode = module.ModuleCode,
            ModuleName = module.ModuleName,
            RegulatorCode = module.RegulatorCode,
            Description = module.Description,
            SheetCount = module.SheetCount,
            IsActive = module.IsActive,
            Versions = moduleVersions.Select(v => new ModuleVersionSummaryDto
            {
                VersionCode = v.VersionCode,
                Status = v.Status,
                PublishedAt = v.PublishedAt,
                DeprecatedAt = v.DeprecatedAt
            }).ToList(),
            Templates = templates.Select(t =>
            {
                var latest = versions.FirstOrDefault(v => v.TemplateId == t.Id);
                return new ModuleTemplateSummaryDto
                {
                    ReturnCode = t.ReturnCode,
                    Name = t.Name,
                    PhysicalTableName = t.PhysicalTableName,
                    VersionNumber = latest?.VersionNumber ?? 0,
                    Status = latest?.Status.ToString() ?? "N/A",
                    FieldCount = latest != null && fieldCounts.TryGetValue(latest.Id, out var fields) ? fields : 0,
                    FormulaCount = latest != null && formulaCounts.TryGetValue(latest.Id, out var formulas) ? formulas : 0
                };
            }).ToList()
        };
    }

    public async Task ToggleModuleActive(string moduleCode, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var module = await db.Modules.FirstOrDefaultAsync(m => m.ModuleCode == moduleCode, ct)
            ?? throw new InvalidOperationException($"Module '{moduleCode}' not found");
        module.IsActive = !module.IsActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateModuleDetails(
        string moduleCode, string moduleName, string? description,
        string defaultFrequency, int? deadlineOffsetDays, int displayOrder,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var module = await db.Modules.FirstOrDefaultAsync(m => m.ModuleCode == moduleCode, ct)
            ?? throw new InvalidOperationException($"Module '{moduleCode}' not found");

        module.ModuleName = moduleName;
        module.Description = description;
        module.DefaultFrequency = defaultFrequency;
        module.DeadlineOffsetDays = deadlineOffsetDays;
        module.DisplayOrder = displayOrder;
        await db.SaveChangesAsync(ct);
    }
}
