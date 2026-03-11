using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class DashboardBriefingPackCatalogService
{
    private readonly MetadataDbContext _db;

    public DashboardBriefingPackCatalogService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardBriefingPackCatalogState> MaterializeAsync(
        string lens,
        int? institutionId,
        IReadOnlyList<DashboardBriefingPackSectionInput> sections,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lens);
        ArgumentNullException.ThrowIfNull(sections);

        await EnsureStoreAsync(ct);

        var normalizedLens = lens.Trim().ToLowerInvariant();
        var materializedAt = DateTime.UtcNow;
        var records = sections
            .Select(x => new DashboardBriefingPackSectionRecord
            {
                Lens = normalizedLens,
                InstitutionId = institutionId,
                SectionCode = x.SectionCode,
                SectionName = x.SectionName,
                Coverage = x.Coverage,
                Signal = x.Signal,
                Commentary = x.Commentary,
                RecommendedAction = x.RecommendedAction,
                MaterializedAt = materializedAt,
                CreatedAt = materializedAt
            })
            .ToList();

        await ClearExistingCatalogAsync(normalizedLens, institutionId, ct);

        if (records.Count > 0)
        {
            _db.DashboardBriefingPackSections.AddRange(records);
            await _db.SaveChangesAsync(ct);
        }

        return new DashboardBriefingPackCatalogState
        {
            Lens = normalizedLens,
            InstitutionId = institutionId,
            MaterializedAt = materializedAt,
            Sections = records
                .OrderBy(x => x.SectionCode, StringComparer.OrdinalIgnoreCase)
                .Select(MapState)
                .ToList()
        };
    }

    public async Task<DashboardBriefingPackCatalogState> LoadAsync(
        string lens,
        int? institutionId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lens);

        await EnsureStoreAsync(ct);

        var normalizedLens = lens.Trim().ToLowerInvariant();
        var query = _db.DashboardBriefingPackSections
            .AsNoTracking()
            .Where(x => x.Lens == normalizedLens && x.InstitutionId == institutionId)
            .OrderBy(x => x.SectionCode);

        var records = await query.ToListAsync(ct);

        return new DashboardBriefingPackCatalogState
        {
            Lens = normalizedLens,
            InstitutionId = institutionId,
            MaterializedAt = records
                .OrderByDescending(x => x.MaterializedAt)
                .Select(x => (DateTime?)x.MaterializedAt)
                .FirstOrDefault(),
            Sections = records.Select(MapState).ToList()
        };
    }

    private async Task EnsureStoreAsync(CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF SCHEMA_ID(N'meta') IS NULL
                EXEC(N'CREATE SCHEMA [meta]');

            IF OBJECT_ID(N'[meta].[dashboard_briefing_pack_sections]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[dashboard_briefing_pack_sections]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Lens] NVARCHAR(40) NOT NULL,
                    [InstitutionId] INT NULL,
                    [SectionCode] NVARCHAR(40) NOT NULL,
                    [SectionName] NVARCHAR(240) NOT NULL,
                    [Coverage] NVARCHAR(600) NOT NULL,
                    [Signal] NVARCHAR(30) NOT NULL,
                    [Commentary] NVARCHAR(1200) NOT NULL,
                    [RecommendedAction] NVARCHAR(1200) NOT NULL,
                    [MaterializedAt] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_dashboard_briefing_pack_sections_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_dashboard_briefing_pack_sections_Lens_InstitutionId_SectionCode]
                    ON [meta].[dashboard_briefing_pack_sections]([Lens], [InstitutionId], [SectionCode]);
                CREATE INDEX [IX_dashboard_briefing_pack_sections_Lens_InstitutionId]
                    ON [meta].[dashboard_briefing_pack_sections]([Lens], [InstitutionId]);
                CREATE INDEX [IX_dashboard_briefing_pack_sections_MaterializedAt]
                    ON [meta].[dashboard_briefing_pack_sections]([MaterializedAt]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task ClearExistingCatalogAsync(string lens, int? institutionId, CancellationToken ct)
    {
        if (_db.Database.IsSqlServer())
        {
            if (institutionId.HasValue)
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM [meta].[dashboard_briefing_pack_sections] WHERE [Lens] = {0} AND [InstitutionId] = {1};",
                    [lens, institutionId.Value],
                    ct);
            }
            else
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM [meta].[dashboard_briefing_pack_sections] WHERE [Lens] = {0} AND [InstitutionId] IS NULL;",
                    [lens],
                    ct);
            }

            return;
        }

        var existing = await _db.DashboardBriefingPackSections
            .Where(x => x.Lens == lens && x.InstitutionId == institutionId)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            return;
        }

        _db.DashboardBriefingPackSections.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
    }

    private static DashboardBriefingPackSectionState MapState(DashboardBriefingPackSectionRecord record) =>
        new()
        {
            Lens = record.Lens,
            InstitutionId = record.InstitutionId,
            SectionCode = record.SectionCode,
            SectionName = record.SectionName,
            Coverage = record.Coverage,
            Signal = record.Signal,
            Commentary = record.Commentary,
            RecommendedAction = record.RecommendedAction,
            MaterializedAt = record.MaterializedAt
        };
}

public sealed class DashboardBriefingPackCatalogState
{
    public string Lens { get; init; } = string.Empty;
    public int? InstitutionId { get; init; }
    public DateTime? MaterializedAt { get; init; }
    public List<DashboardBriefingPackSectionState> Sections { get; init; } = [];
}

public sealed class DashboardBriefingPackSectionInput
{
    public string SectionCode { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public string Coverage { get; init; } = string.Empty;
    public string Signal { get; init; } = string.Empty;
    public string Commentary { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
}

public sealed class DashboardBriefingPackSectionState
{
    public string Lens { get; init; } = string.Empty;
    public int? InstitutionId { get; init; }
    public string SectionCode { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public string Coverage { get; init; } = string.Empty;
    public string Signal { get; init; } = string.Empty;
    public string Commentary { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public DateTime MaterializedAt { get; init; }
}
