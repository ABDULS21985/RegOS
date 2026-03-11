using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class OpsResiliencePackCatalogService
{
    private readonly MetadataDbContext _db;

    public OpsResiliencePackCatalogService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<OpsResiliencePackCatalogState> MaterializeAsync(
        IReadOnlyList<OpsResiliencePackSheetInput> rows,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        await EnsureStoreAsync(ct);

        var materializedAt = DateTime.UtcNow;
        var records = rows
            .Select(x => new OpsResiliencePackSheetRecord
            {
                SheetCode = x.SheetCode,
                SheetName = x.SheetName,
                RowCount = x.RowCount,
                Signal = x.Signal,
                Coverage = x.Coverage,
                Commentary = x.Commentary,
                RecommendedAction = x.RecommendedAction,
                MaterializedAt = materializedAt,
                CreatedAt = materializedAt
            })
            .ToList();

        await ClearExistingCatalogAsync(ct);

        _db.OpsResiliencePackSheets.AddRange(records);
        await _db.SaveChangesAsync(ct);

        return new OpsResiliencePackCatalogState
        {
            MaterializedAt = materializedAt,
            Sheets = records
                .OrderBy(x => x.SheetCode, StringComparer.OrdinalIgnoreCase)
                .Select(MapState)
                .ToList()
        };
    }

    public async Task<OpsResiliencePackCatalogState> LoadAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var records = await _db.OpsResiliencePackSheets
            .AsNoTracking()
            .OrderBy(x => x.SheetCode)
            .ToListAsync(ct);

        return new OpsResiliencePackCatalogState
        {
            MaterializedAt = records
                .OrderByDescending(x => x.MaterializedAt)
                .Select(x => (DateTime?)x.MaterializedAt)
                .FirstOrDefault(),
            Sheets = records.Select(MapState).ToList()
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

            IF OBJECT_ID(N'[meta].[ops_resilience_pack_sheets]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[ops_resilience_pack_sheets]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [SheetCode] NVARCHAR(40) NOT NULL,
                    [SheetName] NVARCHAR(240) NOT NULL,
                    [RowCount] INT NOT NULL,
                    [Signal] NVARCHAR(30) NOT NULL,
                    [Coverage] NVARCHAR(600) NOT NULL,
                    [Commentary] NVARCHAR(1200) NOT NULL,
                    [RecommendedAction] NVARCHAR(1200) NOT NULL,
                    [MaterializedAt] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ops_resilience_pack_sheets_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_ops_resilience_pack_sheets_SheetCode]
                    ON [meta].[ops_resilience_pack_sheets]([SheetCode]);
                CREATE INDEX [IX_ops_resilience_pack_sheets_Signal]
                    ON [meta].[ops_resilience_pack_sheets]([Signal]);
                CREATE INDEX [IX_ops_resilience_pack_sheets_MaterializedAt]
                    ON [meta].[ops_resilience_pack_sheets]([MaterializedAt]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task ClearExistingCatalogAsync(CancellationToken ct)
    {
        if (_db.Database.IsSqlServer())
        {
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM [meta].[ops_resilience_pack_sheets];", ct);
            return;
        }

        var existing = await _db.OpsResiliencePackSheets.ToListAsync(ct);
        if (existing.Count == 0)
        {
            return;
        }

        _db.OpsResiliencePackSheets.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
    }

    private static OpsResiliencePackSheetState MapState(OpsResiliencePackSheetRecord record) =>
        new()
        {
            SheetCode = record.SheetCode,
            SheetName = record.SheetName,
            RowCount = record.RowCount,
            Signal = record.Signal,
            Coverage = record.Coverage,
            Commentary = record.Commentary,
            RecommendedAction = record.RecommendedAction,
            MaterializedAt = record.MaterializedAt
        };
}

public sealed class OpsResiliencePackCatalogState
{
    public DateTime? MaterializedAt { get; init; }
    public List<OpsResiliencePackSheetState> Sheets { get; init; } = [];
}

public sealed class OpsResiliencePackSheetInput
{
    public string SheetCode { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public string Signal { get; init; } = string.Empty;
    public string Coverage { get; init; } = string.Empty;
    public string Commentary { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
}

public sealed class OpsResiliencePackSheetState
{
    public string SheetCode { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public string Signal { get; init; } = string.Empty;
    public string Coverage { get; init; } = string.Empty;
    public string Commentary { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public DateTime MaterializedAt { get; init; }
}
